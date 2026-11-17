# Aggiornamenti automatici

## Clienti

Installa una volta la versione 0.6.3 sostituendo la precedente nella stessa cartella. Mantieni il file `updates.json` incluso accanto all'eseguibile. Da quel momento il controllo avviene all'avvio e ogni sei ore; in SISTEMA puoi anche premere CERCA AGGIORNAMENTI. Quando trova una versione nuova, l'app propone download e riavvio. Licenza, modelli e profili restano nell'account Windows.

Canale: https://github.com/MekilliDoMaNi/Cyno-DS4-AI/releases/latest/download/update.json

## Proprietario: una nuova versione

1. Incrementa la versione del progetto App, aggiorna il titolo, compila, esegui i test e pubblica l'eseguibile Windows x64 self-contained come indicato nel README.
2. Dal PC e dall'account proprietario, prepara la firma con:

```powershell
Cyno.UpdatePublisher.exe "C:\percorso\Cyno DS4.AI.exe" 0.6.4 https://github.com/MekilliDoMaNi/Cyno-DS4-AI "C:\percorso\release-0.6.4"
```

3. Crea una Release pubblica stabile con tag esatto `v0.6.4` e allega i file prodotti `Cyno-DS4.AI.exe` e `update.json`. Impostala come Latest. Per nuovi utenti allega anche lo ZIP completo.
4. Non rinominare gli asset firmati né sostituire il file exe dopo la firma: hash e dimensione devono coincidere. Il manifest punta al tag specifico per evitare file diversi fra controllo e download.

L'esempio 0.6.4 è per la prossima versione; la versione corrente è 0.6.3. Il comando controlla che il numero corrisponda al prodotto compilato.

## Firma e limiti

La chiave privata di aggiornamento è in `%LOCALAPPDATA%\Cyno DS4.AI\UpdateAuthority\update-signing.dpapi`, protetta con DPAPI CurrentUser. Non pubblicarla e non inviarla ai clienti. È separata dalla chiave licenze. Mantieni il PC/account proprietario: senza una procedura di backup sicura della chiave non potrai firmare aggiornamenti dopo la perdita del relativo profilo Windows. Non rigenerare la chiave per un'app già distribuita: le vecchie copie rifiuterebbero la nuova firma.

`init <percorso-chiave-pubblica>` serve solo alla prima configurazione del progetto. Lo strumento di firma non è incluso nello ZIP clienti. Non caricare cartelle AppData, licenze, richieste di attivazione o file privati su GitHub.

HTTPS, firma ECDSA e hash SHA-256 vengono verificati prima della sostituzione. Nessun comando remoto o script proveniente dal manifest viene eseguito. Il programma originale viene copiato come helper locale; la cartella di installazione resta la stessa per HidHide. Se non può scrivere nella cartella, l'aggiornamento fallisce mostrando l'errore e rimane disponibile l'installazione manuale.

Il controllo automatico non interrompe silenziosamente il gioco: l'utente conferma l'installazione. Non c'è un servizio di aggiornamento in background quando Cyno è chiuso. Non viene inviato alcun identificativo licenza al canale GitHub.