# Discord: notifiche nuove versioni

Community: https://discord.gg/nWbTcaxQw
Canale scelto dal proprietario: 📌・𝗜nfo-ds4ai

Il workflow `.github/workflows/discord-release.yml` invia un solo messaggio quando pubblichi una Release stabile. Ignora bozze e prerelease; non notifica commit o modifiche al README. Non menziona @everyone né ruoli. Il contenuto contiene versione e link alla pagina di download GitHub.

## Attivazione una volta sola

1. In Discord, apri Modifica canale per 📌・𝗜nfo-ds4ai, poi Integrazioni > Webhook > Nuovo webhook. Assegna il nome Cyno DS4.AI - Aggiornamenti e controlla che il canale sia quello scelto. Serve il permesso Gestisci webhook.
2. Copia l'URL del webhook. Non inserirlo in README, messaggi pubblici, commit o file condivisi: contiene una credenziale per scrivere nel canale.
3. Nel repository GitHub apri Settings > Secrets and variables > Actions > New repository secret. Nome esatto: DISCORD_WEBHOOK_URL. Nel valore incolla l'URL base copiato da Discord, senza aggiungere /github, poi salva.
4. In Actions apri Discord - nuova versione e premi Run workflow per inviare una notifica della versione più recente. Da quel momento le nuove Release stabili generano automaticamente le notifiche.

Impostazione privata GitHub: https://github.com/MekilliDoMaNi/Cyno-DS4-AI/settings/secrets/actions
Workflow: https://github.com/MekilliDoMaNi/Cyno-DS4-AI/actions/workflows/discord-release.yml

Il workflow fallisce con un messaggio chiaro finché il segreto non è impostato: il link di invito Discord non basta ad autorizzare le notifiche. L'invio manuale può produrre duplicati se eseguito più volte. La Release v0.6.3 pubblicata prima dell'aggiunta del workflow non viene notificata retroattivamente senza esecuzione manuale.

La configurazione usa GitHub Actions e un webhook Discord standard; non richiede token dell'account personale né self-bot. I messaggi partono da GitHub, anche quando il PC del proprietario è spento. Non modifica l'app Windows o il canale aggiornamenti firmati.

Documentazione ufficiale:
- https://support.discord.com/hc/en-us/articles/228383668-Intro-to-Webhooks
- https://docs.github.com/en/actions/how-tos/write-workflows/choose-what-workflows-do/use-secrets