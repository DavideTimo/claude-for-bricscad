# Sicurezza & Privacy

Documento che definisce cosa viene inviato all'API Anthropic, come vengono gestite le credenziali, e quali cautele osservare se i disegni contengono dati riservati (clienti, progetti aziendali).

---

## 1. Cosa viene inviato all'API Anthropic

| Tipo di dato | Inviato? | Note |
|---|---|---|
| Testo della conversazione (richieste utente, risposte Claude) | Sì | Normale funzionamento della chat |
| Risultati dei tool (es. attributi blocchi, nomi layer) | Sì | Solo i dati restituiti dai tool, non l'intero DWG |
| Screenshot/immagine del disegno (`export_current_view_image`) | Solo se il tool viene chiamato | Da usare con parsimonia: un'immagine può rivelare più contesto del previsto (es. altri elementi del disegno non pertinenti alla domanda) |
| Intero file DWG | **Mai**, in nessun tool definito nel Tool Contract | Se in futuro si aggiungono tool che leggono l'intero file, va rivalutato questo documento |

**Principio guida:** i tool restituiscono solo dati puntuali e pertinenti alla richiesta (un blocco, un layer, un attributo), non l'intero disegno. Questo limita naturalmente l'esposizione di dati non necessari, oltre a essere più efficiente.

---

## 2. Gestione della chiave API

- La chiave Anthropic **non è mai** committata nel repository Git (vedi `.gitignore` nel documento Setup/Environment).
- In sviluppo: variabile d'ambiente locale.
- In eventuale distribuzione a più postazioni: da valutare un sistema di gestione centralizzata (es. vault aziendale, o distribuzione manuale sicura) — **da definire quando si arriva a quella fase**, non necessario per uso singolo-utente.
- Rotazione della chiave: procedura da documentare qui una volta scelto il meccanismo di distribuzione.

---

## 3. Dati sensibili nei disegni

Se i disegni riguardano clienti/progetti con clausole di riservatezza (NDA):

- Verificare se il contratto col cliente pone vincoli sull'invio di dati/metadati del progetto a servizi terzi (incluse le API di Claude/Anthropic).
- In caso di dubbio, preferire l'uso su disegni interni/di test finché non si è verificato il vincolo contrattuale.
- Evitare di includere nei nomi di blocco/attributo dati personali diretti (nomi di persone, codici fiscali) se non strettamente necessario — questi finirebbero nei risultati dei tool e quindi nella conversazione inviata all'API.

**Nota:** Anthropic ha proprie policy sulla gestione dei dati inviati via API (retention, training); consultare la documentazione ufficiale Anthropic (`docs.claude.com`) per i dettagli aggiornati prima di un uso su dati aziendali riservati, poiché queste policy possono evolvere nel tempo.

---

## 4. Logging

- I log locali del plugin (per debug/audit) devono registrare **quali tool sono stati chiamati e con quali parametri**, ma vanno valutati con attenzione se includere anche i valori di attributi potenzialmente sensibili.
- I log non vanno mai committati nel repository.
- Definire una retention/rotazione dei log locali (es. cancellazione automatica oltre N giorni) — da implementare in Fase 5 (rifiniture).

---

## 5. Azioni distruttive

- Ogni tool `write` (vedi Tool Contract) richiede conferma esplicita dell'utente in UI, per costruzione.
- Non prevedere, almeno in v1, una modalità "silenziosa" che scriva sul disegno senza interazione umana.
- Valutare un log delle modifiche effettivamente applicate (chi/cosa/quando) separato dal log tecnico, utile in caso di necessità di rollback manuale (BricsCAD stesso mantiene undo, ma un log applicativo aiuta a capire cosa ha fatto l'assistente in una sessione).

---

## 5bis. Execution policy per `run_lisp`

`run_lisp` esegue codice arbitrario e va trattato come il punto di maggior rischio del sistema, con una barriera **in più** rispetto alla sola conferma UI:

- Il bridge applica una policy (whitelist preferibile a blacklist) che rifiuta a priori espressioni che accedono al filesystem fuori dall'ambito del disegno, eseguono comandi di sistema/shell, o modificano configurazioni globali non legate al disegno corrente — vedi Tool Contract, §9, per il dettaglio.
- Questo rifiuto avviene **prima** che l'espressione arrivi alla card di conferma utente: la conferma UI non deve essere l'unica difesa contro codice pericoloso, perché un utente (o una risposta di Claude non controllata) potrebbe confermare senza cogliere appieno le implicazioni di un'espressione LISP complessa.
- Tenere la policy aggiornata mano a mano che emergono nuovi casi d'uso legittimi, preferendo ampliare una whitelist ristretta piuttosto che partire permissivi e restringere dopo un incidente.

---

## 6. Checklist prima di un uso su disegni reali/aziendali

- [ ] Verificato che nessun vincolo contrattuale/NDA impedisca l'invio di metadati di progetto a servizi API esterni.
- [ ] Chiave API gestita correttamente (non in chiaro, non committata).
- [ ] Rivisto quali tool `read` vengono usati abitualmente e se espongono dati più sensibili del necessario (es. `export_current_view_image` su disegni con informazioni non pertinenti visibili).
- [ ] Log locali configurati per non conservare indefinitamente dati potenzialmente sensibili.
- [ ] Execution policy di `run_lisp` verificata e testata contro almeno un tentativo di espressione pericolosa (accesso filesystem, comando di sistema), per confermare che venga bloccata prima della conferma UI.
