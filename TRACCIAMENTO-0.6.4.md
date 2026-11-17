# Cyno DS4.AI 0.6.4 - tracciamento e area di analisi

## Implementato

- Corretta l'attenuazione dell'assistenza con input manuale: il peso dello stick viene applicato all'uscita, senza moltiplicare ripetutamente lo stato a ogni report. Prima l'assistenza poteva quasi annullarsi a 1000 Hz con movimenti moderati dello stick.
- Opzione Mantieni bersaglio mentre premi il tasto: associazione ai rettangoli osservati e nessun passaggio volontario al vicino più centrale finché resta una corrispondenza plausibile. Perdita di corrispondenza: nessuna correzione verso posizioni invisibili; una successiva rilevazione può riacquisire un bersaglio.
- Area di analisi più ampia del FOV: con FOV 150, analisi 600x600 anziché 300x300, per conservare più contesto del corpo. Il FOV di acquisizione resta 150 e la conversione delle coordinate conserva lo stesso errore fisico del mirino. L'area è limitata alla dimensione del monitor.
- Compensazione sperimentale del ritardo: estrapolazione limitata dei dati osservati, senza invertire la correzione oltre il bersaglio visibile. Frame oltre 250 ms, rilascio del tasto e perdita del target interrompono la correzione.
- Opzione Zona testa stimata: punto al 12% dall'alto del rettangolo persona, relativo alle dimensioni del soggetto. I rettangoli tagliati superiormente vengono scartati. Non è un riconoscimento anatomico della testa.
- Pulsante APPLICA PRESET ZONA TESTA STIMATA nella scheda VISIONE: attiva e salva le opzioni nuove. Conserva il tasto L1/L2 scelto e lo schema mira/fuoco del profilo.
- Cerchio FOV visibile nell'anteprima anche quando l'analisi comprende un'area più ampia. Profili precedenti compatibili: le opzioni nuove restano disattivate finché selezionate o importate.

## Verifiche

72 test .NET superati: mantenimento del bersaglio, perdita di corrispondenza, punto stimato, geometria del contesto, limitazione della previsione, arresto su perdita/ritardo e consistenza fra 120 e 1000 Hz. I sei test del notificatore Discord restano separati.

Analisi locale del filmato fornito: 2560x1440, AV1, 60 fps, durata 34.475 secondi. Campionati 15 fotogrammi, uno ogni 2 secondi, fra circa 1 e 29 secondi. Con lo stesso modello persona incluso e soglia candidati 0.29, l'area 300x300 produce almeno un candidato in 4 campioni; 600x600 in 12 campioni. I candidati possono essere altri manichini o rilevazioni sbagliate: NON è una misura di precisione, di recall del bersaglio desiderato o di headshot. Non sono disponibili input del controller sincronizzati né annotazioni di riferimento. Video e fotogrammi privati non sono distribuiti.

## Ancora mancante

Non è implementata una garanzia di mira sempre sulla testa o di aggancio permanente identico al filmato. Il modello incluso è generico COCO e può perdere o confondere i manichini, soprattutto dentro l'ottica, con occlusioni o lampi di sparo. Non ci sono nuovi pesi Warzone addestrati, modello anatomico testa, calibrazione specifica sensibilità/FOV/ottica o validazione in partita di queste modifiche.

La stima della testa può essere errata quando il rettangolo descrive solo una parte del corpo. Il tracciamento non distingue amici e nemici e può confondere soggetti che si incrociano. Il preset non attiva automaticamente fuoco, recoil o altre macro. Servono un modello appropriato e prove con riferimento annotato per raggiungere il comportamento richiesto; aumentare la forza non sostituisce il riconoscimento.

## Uso

Dopo l'aggiornamento, apri VISIONE e premi APPLICA PRESET ZONA TESTA STIMATA, oppure importa Cyno-Warzone-zona-testa-stimata-0.6.4.json. Tieni il tasto di attivazione configurato. Se la compensazione sperimentale aumenta le oscillazioni con la tua sensibilità, disattivala e salva il profilo. La licenza resta quella del PC autorizzato.