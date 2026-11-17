# Cyno DS4.AI 0.6 — pacchetto da condividere

Estrai tutto lo ZIP e avvia Cyno DS4.AI.exe. Il runtime .NET è incluso. Il controller virtuale richiede ViGEmBus già presente sul PC; i driver non vengono installati automaticamente. Se HidHide nasconde il controller, autorizza il nuovo percorso con il file Autorizza-HidHide.cmd incluso o con il suo client.

## Attivazione obbligatoria

1. Sul PC destinatario avvia l'app: comparirà la schermata di attivazione.
2. Premi ESPORTA RICHIESTA e invia il file .cynorequest al proprietario di Cyno DS4.AI.
3. Il proprietario genera una licenza usando il suo gestore separato e restituisce un file .cynolicense.
4. Premi IMPORTA LICENZA sullo stesso PC/profilo Windows che ha creato la richiesta.

Una licenza di un altro PC o profilo non attiva l'app. Senza licenza valida non vengono aperti i motori controller e visione. La verifica viene ripetuta ogni 15 secondi durante l'uso; se fallisce il controller viene neutralizzato e la console torna alla schermata di attivazione.

## Come è legata al dispositivo

La licenza contiene l'identità firmata del dispositivo e la relativa chiave pubblica. L'app usa una chiave locale non esportabile CNG: TPM quando disponibile, altrimenti il provider software Windows. L'identificativo deriva anche da un hash dell'installazione Windows. Non è sufficiente copiare la cartella o il file licenza su un altro PC.

La verifica usa una chiave pubblica incorporata nella build. Nessuna chiave privata del proprietario, gestore licenze o licenza già attivata è inclusa in questo ZIP. I dati di licenza restano sotto %LOCALAPPDATA%/Cyno DS4.AI/Licensing. La nuova installazione o il ripristino di Windows, del profilo o delle chiavi TPM può richiedere una nuova autorizzazione.

Il sistema è offline: non implementa revoca remota né un orologio certificato per le scadenze. Le licenze permanenti non scadono. Questo controllo blocca l'uso ordinario di copie non autorizzate, ma non è un DRM inviolabile contro patch, ricompilazione dei sorgenti o clonazione completa dell'ambiente Windows. Le build precedenti senza licenza non vengono protette retroattivamente.

## Funzioni e modelli

Sono mantenuti controller USB, Xbox virtuale, profili, macro, visione e motore Warzone indipendente. I pesi inclusi restano generici COCO, non addestrati o validati su Warzone. Dettagli e limiti in FUNZIONI-E-LIMITI.md. Modelli e dipendenze hanno licenze proprie, separate dal codice MIT.

## Verifiche

Build Release senza errori/avvisi. 40 test superati: firma valida, dispositivo/chiave errati, firma alterata, altro firmatario, licenza scaduta/futura, prodotto/schema errati e file danneggiati, oltre ai test dei motori precedenti. Sul PC di sviluppo è stato osservato il blocco iniziale, poi l'attivazione reale tramite licenza firmata e il ritorno alla console con Edge collegato. Un secondo PC fisico non è stato usato; il rifiuto di altri identificativi è verificato nei test.
