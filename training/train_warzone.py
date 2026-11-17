"""Train/export an experimental Warzone detector only after manually annotated data exists."""
import argparse
import json
import math
from pathlib import Path
from datetime import datetime, timezone

def validate(root, minimum_train, minimum_val):
    counts = {}
    image_hashes = {}
    import hashlib
    for split, minimum in (("train", minimum_train), ("val", minimum_val)):
        images = list((root / "images" / split).glob("*.png"))
        if len(images) < minimum:
            raise ValueError(f"{split}: {len(images)} immagini; ne servono almeno {minimum} per questo esperimento.")
        positives = 0
        for image in images:
            digest = hashlib.sha256(image.read_bytes()).hexdigest()
            if digest in image_hashes and image_hashes[digest] != split:
                raise ValueError("Immagine duplicata tra train e val. Separare le sessioni.")
            image_hashes[digest] = split
            label = root / "labels" / split / (image.stem + ".txt")
            if not label.is_file():
                raise ValueError(f"Etichetta mancante: {label.name}")
            lines = [line.split() for line in label.read_text(encoding="utf-8-sig").splitlines() if line.strip()]
            positives += bool(lines)
            for fields in lines:
                if len(fields) != 5 or fields[0] != "0":
                    raise ValueError(f"Formato YOLO non valido: {label.name}")
                x, y, width, height = map(float, fields[1:])
                if not all(math.isfinite(v) for v in (x, y, width, height)) or not (0 <= x <= 1 and 0 <= y <= 1 and 0 < width <= 1 and 0 < height <= 1):
                    raise ValueError(f"Coordinate non valide: {label.name}")
        if positives == 0:
            raise ValueError(f"{split}: nessuna immagine con persone annotate.")
        counts[split] = {"images": len(images), "positive_images": positives}
    return counts

def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--dataset", type=Path, required=True)
    p.add_argument("--output", type=Path, default=Path("warzone-training"))
    p.add_argument("--epochs", type=int, default=50)
    p.add_argument("--device", default="cpu")
    p.add_argument("--min-train", type=int, default=100)
    p.add_argument("--min-val", type=int, default=25)
    p.add_argument("--validate-only", action="store_true")
    args = p.parse_args()
    if args.min_train < 1 or args.min_val < 1 or args.epochs < 1:
        p.error("Conteggi ed epoche devono essere positivi.")
    root = args.dataset.resolve()
    try:
        counts = validate(root, args.min_train, args.min_val)
    except (OSError, ValueError) as e:
        p.error(str(e))
    print(json.dumps(counts, indent=2))
    if args.validate_only:
        return
    from ultralytics import YOLO
    yaml = root / "dataset.yaml"
    yaml.write_text("path: " + json.dumps(root.as_posix()) + "\ntrain: images/train\nval: images/val\nnames:\n  0: person\n", encoding="utf-8")
    model = YOLO("yolov8n.pt")
    model.train(data=str(yaml), epochs=args.epochs, imgsz=640, patience=15, device=args.device, project=str(args.output.resolve()), name="cyno-warzone", exist_ok=False)
    best = Path(model.trainer.best)
    trained = YOLO(str(best))
    metrics = trained.val(data=str(yaml), imgsz=640, device=args.device, split="val")
    onnx = trained.export(format="onnx", imgsz=640, batch=1, dynamic=False, half=False, nms=False, simplify=False, opset=17, device="cpu")
    manifest = {"name": "Cyno Warzone experimental", "status": "trained-experimental-not-game-validated", "trained_utc": datetime.now(timezone.utc).isoformat(), "dataset": counts, "mAP50": float(metrics.box.map50), "mAP50_95": float(metrics.box.map), "onnx": str(onnx), "base": "Ultralytics YOLOv8n", "note": "La validazione su immagini non equivale a verifica in gioco; separare scene e sessioni train/val."}
    Path(str(onnx) + ".manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    print("Modello esportato. Importalo nella console Cyno DS4.AI per cifrarlo localmente.")

if __name__ == "__main__":
    main()
