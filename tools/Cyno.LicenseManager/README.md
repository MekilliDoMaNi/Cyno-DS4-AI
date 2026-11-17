# Come creare le licenze — SOLO PROPRIETARIO

Questo pacchetto è per te. Non inserirlo nello ZIP da inviare ai clienti. La chiave privata è già stata creata e protetta nel tuo attuale profilo Windows, fuori da questi archivi.

## Autorizzare un altro PC

1. Invia al destinatario soltanto Cyno-DS4.AI-DA-CONDIVIDERE.zip.
2. Il destinatario estrae l'archivio, avvia Cyno DS4.AI.exe e preme ESPORTA RICHIESTA.
3. Ricevi da lui il file .cynorequest. Il codice PC da solo non sostituisce questo file firmato.
4. Apri Cyno License Manager.exe sul tuo PC.
5. Premi APRI RICHIESTA e seleziona il file ricevuto.
6. Scrivi il nome del cliente o del PC; scegli Senza scadenza oppure una data.
7. Premi GENERA LICENZA FIRMATA e salva il file .cynolicense.
8. Invia soltanto quel file al destinatario. Lui lo importa nell'app e il PC viene autorizzato.

Lo stesso file licenza non attiva un PC/profilo diverso. Per un altro computer serve una nuova richiesta e una nuova licenza. Per rinnovi con scadenza genera un nuovo file sullo stesso codice PC. Non è implementata una revoca remota delle licenze permanenti.

## Fai un backup della chiave privata

Nel gestore, inserisci una password di almeno 12 caratteri e premi ESPORTA BACKUP CIFRATO. Salva il .pem cifrato in un posto riservato, separato dalla password. Non inviare mai questo backup o la password ai clienti.

Se cambi PC/profilo del proprietario, apri il gestore, inserisci la password e usa RIPRISTINA BACKUP. Il gestore accetta solo il backup corrispondente alla chiave pubblica di questa build. Senza la chiave originale o il backup non puoi generare nuove licenze compatibili con questa versione distribuita.

La chiave attuale è in %LOCALAPPDATA%/Cyno DS4.AI/LicenseAuthority/issuer.dpapi, protetta con DPAPI CurrentUser. Quel file non è un backup portabile: usa il comando di esportazione cifrata del gestore. Un registro delle emissioni è salvato nella stessa cartella. I file già emessi non sono salvati automaticamente nel pacchetto.

## Limiti della protezione

L'app contiene solo la chiave pubblica di verifica. La chiave privata non è stata inserita nel pacchetto clienti né nel pacchetto gestore. Il software rimane una ricostruzione con sorgenti disponibili: chi modifica/ricompila il codice può rimuovere un controllo. Le vecchie versioni 0.5 e precedenti senza attivazione non diventano protette; non distribuirle se vuoi usare il nuovo sistema.
