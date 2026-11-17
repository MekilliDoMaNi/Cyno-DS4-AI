# Cyno DS4.AI — componenti di terze parti

Il codice originale di questa ricostruzione indipendente è MIT. Il modello generico pubblico YOLOv8n è un asset separato, non un modello Warzone e non codice MIT.
Fonte della copia ONNX: https://huggingface.co/webml/yolov8n/resolve/main/onnx/yolov8n.onnx
SHA-256 del modello in chiaro incluso: 190BA5F1E61411A001683E349D6B2CDB0804C0DC67A5E34CD8FF6FD00EE54B4D.
Ultralytics: https://github.com/ultralytics/ultralytics e https://www.ultralytics.com/license . Testo della licenza upstream incluso in MODEL-LICENSE.txt. Consultare i termini del modello e dei pesi derivati prima della redistribuzione.

Dipendenze con condizioni proprie: .NET 8, HidSharp 2.1.0, Nefarius.ViGEm.Client 1.21.256, Microsoft.ML.OnnxRuntime 1.20.1, System.Security.Cryptography.ProtectedData 8.0.0. Strumenti di test xUnit/Microsoft.NET.Test.Sdk nei soli sorgenti.
Driver non inclusi. ViGEm upstream è ritirato: https://docs.nefarius.at/projects/ViGEm/End-of-Life/ .

Il vault usa AES-GCM e DPAPI dell'account Windows come protezione locale; non viene presentato come DRM inviolabile. Nessun codice, modello cifrato, chiave o licenza di DS4AI.exe è distribuito con Cyno DS4.AI. Il modello cifrato locale viene generato al primo avvio dalla risorsa pubblica inclusa.

Fonti tecniche:
https://learn.microsoft.com/dotnet/standard/security/how-to-use-data-protection
https://onnxruntime.ai/docs/get-started/with-csharp.html
https://docs.ultralytics.com/modes/train/
https://docs.ultralytics.com/modes/export/
