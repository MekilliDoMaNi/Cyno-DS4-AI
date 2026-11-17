# Addestramento Warzone sperimentale

Il pacchetto app non contiene pesi addestrati su Warzone. Lo script non è stato usato per addestrare un modello durante questa sessione: manca un dataset Warzone annotato e verificato.

La scheda Dataset congela il crop locale e permette rettangoli manuali per ogni persona. Salva campioni positivi o negativi espliciti in `%LOCALAPPDATA%/Cyno DS4.AI/WarzoneDataset`. Usa sessioni e scene diverse per train e val, selezionando la casella validazione prima di salvare i campioni dedicati. Il validatore rifiuta duplicati esatti tra le due divisioni; non riconosce frame quasi identici o leakage semantico.

Su un ambiente Python 3.11/3.12 dedicato, installa requirements.txt. Prima esegui:

    python train_warzone.py --dataset "PERCORSO_DATASET" --validate-only

Dopo revisione manuale delle etichette, per addestrare:

    python train_warzone.py --dataset "PERCORSO_DATASET" --device cpu --epochs 50

CPU può essere lenta. Un ambiente PyTorch/CUDA compatibile può usare --device 0; la preparazione GPU non è automatica. Le soglie iniziali di 100 immagini train e 25 val sono minimi per un esperimento, non garanzie di qualità. Il file ONNX esportato usa input statico FP32 640x640 e nessun NMS integrato, compatibile con la console. Importalo con IMPORTA MODELLO: la console ne crea una copia cifrata per il tuo account Windows, mantenendo il file sorgente.

Ultralytics e i pesi derivati hanno condizioni proprie: https://www.ultralytics.com/license . Documentazione: https://docs.ultralytics.com/modes/train/ e https://docs.ultralytics.com/modes/export/ . Nessun dato viene caricato in rete dallo script; il primo avvio di Ultralytics può scaricare il modello base e le dipendenze. Non viene aggirata alcuna licenza del programma originale.
