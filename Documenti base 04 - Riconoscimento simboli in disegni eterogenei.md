# Riconoscimento Simboli in Disegni Eterogenei

## 0. Perché questo documento esiste (e ha sostituito un approccio più semplice)

La versione iniziale del progetto assumeva che i disegni avessero blocchi e attributi ragionevolmente nominati, e che uno "scopri i nomi a runtime" bastasse. **L'uso reale ha smentito questa ipotesi**: i disegni gestiti provengono spesso da clienti o terze parti, con blocchi nominati in modo casuale (es. `mncfasoijds`) o del tutto assenti — dettagli disegnati come semplice geometria (linee, polilinee, oggetti 3D), mai inseriti come blocco. In questo scenario, il riconoscimento basato solo su nomi/attributi copre una frazione piccola dei casi reali, non la maggioranza.

Il principio guida diventa quindi: **il riconoscimento avviene su quattro livelli (0-3), in cascata**, dal più affidabile/economico al più euristico/costoso.

---

## 1. I livelli di riconoscimento

### Livello 0 — Tag persistenti già presenti nel disegno (da controllare per primo)
Se questo stesso disegno è già stato "insegnato" in una sessione precedente, l'informazione più specifica e affidabile disponibile è un tag salvato direttamente nel file (via XDATA, dati estesi invisibili attaccati alle entità — vedi Tool Contract, §6sextus). Va controllato **prima** di qualunque altro livello: se presente, evita di rifare da capo clustering e matching geometrico. Tool coinvolti: `find_tagged_entities`, `tag_entities_as_category`.

### Livello 1 — Nomi e attributi (quando esistono ed sono informativi)
Se il blocco ha un nome descrittivo e attributi compilati, è il dato più esatto ed economico: nessuna ambiguità, nessun calcolo geometrico necessario. Tool coinvolti: `list_block_definitions`, `find_blocks`, `get_block_attributes`, `get_titleblock_data`.

### Livello 2 — Matching geometrico per similarità di forma ("insegna per esempio")
Quando nomi/attributi non bastano o non esistono — **il caso più comune nei tuoi disegni reali** — l'unica informazione affidabile è la forma stessa del simbolo: quante linee/archi/cerchi lo compongono, che proporzioni e disposizione relativa hanno. Questo funziona indipendentemente dal fatto che l'oggetto sia un blocco o geometria sciolta, e non richiede nessuno standard condiviso tra disegni diversi. Tool coinvolti: `list_entity_clusters`, `get_geometry_signature`, `find_similar_geometry`, `save_signature_as_category`, `list_learned_categories`.

### Livello 3 — Conferma visiva mirata
Uno screenshot ritagliato su un'area già nota (dal livello 1 o 2), usato per confermare un match incerto o interpretare un dettaglio che la geometria da sola non chiarisce. Mai usato per "cercare" alla cieca su un'area generica. Tool coinvolti: `get_text_in_region`, `get_region_screenshot`.

---

## 2. Il flusso "insegna per esempio" (cuore del Livello 2)

Questo è il meccanismo pensato per il tuo caso d'uso reale: disegni sempre diversi, mai uno standard che si ripete.

1. L'utente chiede, ad esempio: *"controlla se ho messo il tag a ogni telecamera"*.
2. Claude controlla prima il **Livello 0** (`find_tagged_entities("telecamera")`): se questo disegno ha già tag persistenti da una sessione precedente, li usa direttamente, senza rifare nulla.
3. Se il Livello 0 non dà risultati, prova il Livello 1 (`list_block_definitions`): se trova un nome chiaramente riconducibile a "telecamera", procede direttamente (vedi documento Tool Contract per la politica di confidenza sui nomi).
4. Se il Livello 1 non dà nulla di utile (nomi casuali tipo `mncfasoijds`, o nessun blocco pertinente), Claude verifica se esiste già una firma salvata per "telecamera" nella libreria personale (`list_learned_categories`) e la propone come ipotesi di partenza.
5. Se non esiste, Claude chiede all'utente: *"Puoi indicarmi un esempio di telecamera nel disegno?"* — l'utente seleziona un'istanza direttamente in BricsCAD (selezione nativa, non descrizione a parole).
6. Claude chiama `get_geometry_signature` sulla selezione, ottenendo una firma invariante a scala/rotazione.
7. Claude chiama `find_similar_geometry` con quella firma su tutto il disegno (o un'area indicata), ottenendo i match con relativo punteggio di similarità.
8. In base al punteggio (vedi Tool Contract, §6quater, per le soglie): procede direttamente sui match ad alta confidenza, propone conferma visiva (Livello 3) sui match incerti, ignora/segnala quelli a bassa confidenza.
9. Facoltativamente, propone di salvare il risultato in due modi complementari: `tag_entities_as_category` per persistere il riconoscimento **in questo disegno** (così non serve rifare nulla se lo si riapre), e/o `save_signature_as_category` con scope `personal_library` per riusare la firma su disegni diversi — chiarendo in entrambi i casi che restano ipotesi, non certezze, se riproposte in futuro.

---

## 3. Rischi tecnici noti (onestà sul livello di R&D coinvolto)

Questo non è un meccanismo "pronto e infallibile": ci sono punti genuinamente difficili da tarare, ed è importante saperlo prima di aspettarsi risultati perfetti fin dal primo prototipo.

- **Clustering spaziale (`list_entity_clusters`)**: distinguere "un simbolo" da "un pezzo di un elemento più grande" (una parete, una quotatura) con euristiche di prossimità è delicato. Soglie troppo larghe uniscono simboli distinti in un unico cluster; soglie troppo strette spezzano un simbolo in più pezzi. Va tarato empiricamente sui disegni reali, non assunto corretto a tavolino.
- **Invarianza scala/rotazione della firma geometrica**: un simbolo disegnato più grande, ruotato, o leggermente diverso da un disegnatore all'altro deve produrre una firma comunque riconoscibile come simile. La qualità del descrittore di forma scelto (rapporti tra lunghezze, angoli relativi, conteggio primitive) determina quanto bene questo funziona; è un'area dove serve iterazione pratica, non solo teoria.
- **Simboli simili tra loro ma semanticamente diversi**: due tipi di sensori diversi potrebbero avere geometria quasi identica (es. entrambi un cerchio con una croce). Il matching geometrico da solo non distingue significati, solo forme — qui la conferma visiva (Livello 3) o la conferma dell'utente diventano necessarie, non opzionali.
- **Generalizzazione della libreria personale tra disegni di clienti diversi**: una firma imparata su un disegno di un cliente potrebbe non generalizzare bene a un disegno di un altro cliente con uno stile di disegno diverso, anche per lo stesso concetto ("telecamera"). Va trattata sempre come ipotesi, mai come certezza definitiva (vedi Tool Contract, nota su `save_signature_as_category`).
- **Persistenza dei tag XDATA e privacy**: i tag applicati con `tag_entities_as_category` viaggiano con il file DWG. Se il disegno viene ri-condiviso con il cliente o terzi, restano nel file (invisibili nell'uso normale, ma estraibili via LISP/API). Non vanno mai usati per annotare informazioni sensibili, solo la categoria riconosciuta (vedi Tool Contract, §6sextus, e documento Sicurezza & Privacy).

**Per questo la Fase 0 della roadmap (vedi documento "Linee guida di progetto", §7) prevede un prototipo isolato del motore geometrico, validato su 2-3 disegni reali eterogenei, prima di formalizzare ulteriormente lo schema o costruire la UI attorno.**

---

## 4. Perché non un modello neurale addestrato fin da subito

Un Graph Neural Network o un modello di visione addestrato su misura sarebbe, in teoria, più robusto del matching euristico — ma richiede un dataset etichettato che oggi non esiste. Costruirlo "fin da subito" significherebbe investire settimane in raccolta ed etichettatura dati prima di avere qualunque cosa funzionante, mentre il matching geometrico euristico è implementabile ed testabile su disegni reali da subito, con la libreria .NET del progetto.

Il percorso naturale è: matching euristico in uso reale → genera esempi/conferme dall'utente → questi diventano potenziale materiale di addestramento futuro (vedi documento "Linee guida di progetto", §8, per questo step successivo).

---

## 5. Impatto sugli altri documenti

- **Tool Contract (03)**: aggiunti `list_entity_clusters`, `get_geometry_signature`, `find_similar_geometry`, `save_signature_as_category`, `list_learned_categories` (§6bis-6quinquies, versione 2.0); aggiunti `tag_entities_as_category`/`find_tagged_entities` (§6sextus) e execution policy per `run_lisp` (§9), versione 2.1.
- **Linee guida (02)**: nuovo componente architetturale `ClaudeBridge.GeometryEngine`; roadmap con Fase 0 a doppio binario (bridge dati + prototipo geometrico) e nuova Fase 5 per la libreria simboli personale.
- **Piano di Test (06)**: scenari dedicati al flusso "insegna per esempio", al tuning del clustering, e ai casi di match a bassa/media confidenza.
- **Setup Environment Guide (05)**: punto critico aggiunto su thread-marshaling verso il thread principale di BricsCAD.
- **Sicurezza & Privacy (07)**: sezione dedicata all'execution policy di `run_lisp` e alla privacy dei tag XDATA persistenti.

**Nota sulla provenienza di alcune di queste scelte:** i tool `tag_entities_as_category`/`find_tagged_entities`, il requisito di thread-marshaling e l'execution policy per `run_lisp` sono ispirati da un'analisi di architetture MCP già mature in un ambito simile (integrazioni AI-3D come BlenderMCP), dove pattern equivalenti (memoria semantica basata su tag, marshalling verso il thread principale dell'host, policy di esecuzione attorno al tool di codice arbitrario) si sono dimostrati necessari in pratica.
