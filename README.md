# Cyno DS4.AI 0.6.3

[Entra su Discord](https://discord.gg/nWbTcaxQw) · [Scarica l’ultima versione](https://github.com/MekilliDoMaNi/Cyno-DS4-AI/releases/latest)

Applicazione Windows x64 in C# .NET 8, con interfaccia cyberpunk, supporto DualSense/DualSense Edge e controller Xbox 360 virtuale. Ricostruzione indipendente; non affiliata a Sony o al programma DS4AI originale.

## Download

Scarica lo ZIP dalla [Release più recente](https://github.com/MekilliDoMaNi/Cyno-DS4-AI/releases/latest), estrailo e avvia `Cyno DS4.AI.exe`. Il runtime .NET è incluso. L'uscita virtuale richiede ViGEmBus già installato; HidHide richiede di autorizzare il percorso dell'app. Non sono installati driver automaticamente.

La build distribuita richiede l'attivazione del singolo PC da parte del proprietario. Nessuna licenza personale o chiave privata è inclusa nel repository o nelle Releases.

## Funzioni implementate

- DualSense e DualSense Edge USB; uscita Xbox 360 virtuale; monitor input, riconnessione e anteprima.
- Dead zone, inversione Y, rimappatura, profili JSON e libreria locale.
- Attivazione assistenza tramite tasto fisico L2 oppure L1.
- Schemi L2 mira/R2 fuoco, L1 mira/R1 fuoco oppure R1 mira/L1 fuoco, con scambio completo dei dorsali e grilletti. Nel gioco conserva L2 mira/R2 fuoco.
- Visione locale ONNX e colore; motore di associazione temporale, limite correzione e frenatura; selezione monitor e overlay.
- Aggiornamenti: controllo all'avvio e ogni sei ore, verifica firma ECDSA e SHA-256, download, sostituzione dell'eseguibile e riavvio dopo conferma. Backup dell'eseguibile precedente; ripristino se l'avvio del nuovo processo fallisce immediatamente.
- Licenze firmate legate al PC e protezione locale dei modelli con DPAPI/AES-GCM.

## Limiti

Il modello incluso è generico COCO: non è un modello Warzone addestrato. Nessuna garanzia di aggancio al bersaglio o parità con l'originale. Bluetooth sperimentale; feedback aptico, grilletti adattivi, GPU e addestramento integrato non completati. I nuovi schemi dei tasti non sono ancora verificati in partita.

Gli aggiornamenti richiedono una Release pubblica contenente `update.json` e `Cyno-DS4.AI.exe`. La 0.6.2 e le precedenti devono ricevere una prima sostituzione manuale per installare il sistema automatico. Le installazioni in cartelle non scrivibili richiedono un aggiornamento manuale con i permessi corretti; non viene richiesta elevazione automatica. Il backup copre errori immediati di sostituzione/avvio, non crash successivi del nuovo processo.

## Compilazione e test

Con .NET SDK 8 su Windows:

```powershell
dotnet build CynoDS4AI.sln -c Release
dotnet test CynoDS4AI.sln -c Release
dotnet publish src/OpenDS4AI.App/OpenDS4AI.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
```

Verifica locale: 65 test superati. Verificato il passaggio 0.6.2 -> 0.6.3 tramite helper firmato con sostituzione, riavvio, licenza invariata e input DualSense inoltrato al controller virtuale. La verifica online dipende dalla pubblicazione della Release.

## Pubblicare aggiornamenti

Vedi [AGGIORNAMENTI.md](AGGIORNAMENTI.md). Lo strumento `tools/Cyno.UpdatePublisher` usa una chiave privata protetta nel solo account Windows del proprietario, separata dalla firma delle licenze. Nel codice è presente soltanto la chiave pubblica.

Codice originale MIT; pesi e dipendenze hanno licenze separate: vedi LICENSE e THIRD-PARTY-NOTICES.md.

## Notifiche Discord

Le nuove Release stabili possono essere annunciate automaticamente in **📌・𝗜nfo-ds4ai**. È necessario configurare il segreto privato del webhook: vedi [DISCORD.md](DISCORD.md). Il link di invito è disponibile in cima alla pagina.
