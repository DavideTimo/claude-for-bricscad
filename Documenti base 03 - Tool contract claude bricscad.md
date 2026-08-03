# Tool Contract — Claude ↔ Orchestratore ↔ BricsCAD Bridge

Questo documento definisce lo schema esatto di ogni tool esposto a Claude: input, output, errori, e chi è responsabile di validare cosa. È il contratto tra tre parti:

- **Claude** — decide quale tool chiamare e con quali parametri, in base alla conversazione.
- **Orchestratore** (`ClaudeBridge.Core`) — valida i parametri contro lo schema, gestisce il ciclo di tool-use, non conosce BricsCAD.
- **Bridge** (`ClaudeBridge.BricscadPlugin`) — esegue realmente l'operazione su BricsCAD tramite `IBricscadBridge`, non conosce Claude.

## Principio di validazione

- **Orchestratore**: valida che i parametri rispettino lo schema JSON (tipi, campi obbligatori, enum validi). Se non validi, non passa la chiamata al bridge e restituisce a Claude un errore di schema (vedi §Errori comuni).
- **Bridge**: valida la validità *semantica* rispetto al disegno aperto (handle esistente, blocco trovato, drawing aperto). Restituisce errori applicativi ben definiti, mai eccezioni non gestite.
- **UI**: intercetta i tool marcati come `write` prima dell'esecuzione e richiede conferma esplicita all'utente. Il bridge non deve mai eseguire un tool `write` senza che la UI abbia già ottenuto conferma.

Ogni tool ha un campo `mode`: `read` (nessuna conferma richiesta) o `write` (conferma UI obbligatoria).

## Thread-marshaling (vincolo trasversale, tutti i tool)

L'API .NET di BricsCAD — come la gran parte delle API di applicazioni CAD/DCC single-document (es. l'API `bpy` di Blender) — non è pensata per essere chiamata da thread arbitrari: le operazioni sul database del disegno vanno eseguite sul thread principale dell'applicazione/documento.

Poiché l'orchestratore comunica con Claude in modo asincrono (chiamate HTTP verso l'API Anthropic) e potrebbe invocare i tool da un contesto thread diverso da quello di BricsCAD, **il bridge (`ClaudeBridge.BricscadPlugin`) deve marshalling esplicitamente ogni chiamata verso l'API BricsCAD sul thread corretto** (es. tramite il meccanismo di sincronizzazione con il thread principale offerto dall'SDK BricsCAD/.NET — da individuare in fase di Fase 0, verificando la documentazione sviluppatori della propria versione), invece di invocare l'API direttamente dal thread di ricezione della richiesta.

**Questo va implementato una sola volta, a livello di implementazione di `IBricscadBridge`** (un wrapper comune che marshalla e poi esegue), non ripetuto tool per tool. Va comunque tenuto presente come rischio tecnico esplicito da validare presto in Fase 0, perché un'implementazione naive (chiamare l'API da un thread qualsiasi) può produrre errori intermittenti difficili da diagnosticare (crash, dati corrotti, comportamento incoerente) piuttosto che un errore immediato e chiaro.

---

## 0. `list_block_definitions`

**Mode:** `read`
**Scopo:** restituisce l'inventario completo dei blocchi presenti nel disegno corrente, senza filtro per nome. È il tool di **scoperta** da chiamare quando la richiesta dell'utente riguarda una categoria di oggetti (es. "telecamere") senza specificare il nome esatto del blocco — i disegni gestiti sono eterogenei e non esiste uno standard di naming presupposto (vedi documento 04, "Riconoscimento simboli in disegni eterogenei", per il framework completo a tre livelli).

**Input**
```json
{
  "space": "string (opzionale, enum: \"model\" | \"paper\" | \"any\", default \"any\")"
}
```

**Output**
```json
{
  "block_definitions": [
    {
      "name": "string",
      "instance_count": 0,
      "attribute_tags": ["string"],
      "sample_layer": "string"
    }
  ]
}
```

**Errori applicativi**
- `NO_DRAWING_OPEN`

**Nota per Claude (istruzione di sistema):** dopo aver ottenuto l'elenco, interpretare quali nomi corrispondono al concetto richiesto dall'utente usando ragionamento semantico. Procedere direttamente se la corrispondenza è chiara (dichiarando comunque quali blocchi sono stati usati), chiedere conferma solo se il nome è ambiguo o generico. Non esiste un pattern o una lista di nomi noti a priori: ogni disegno va interpretato singolarmente.

---

## 1. `find_blocks`

**Mode:** `read`
**Scopo:** trova istanze di blocco nel disegno per nome (con pattern) ed eventualmente filtro layer. Il pattern viene tipicamente costruito da Claude *dopo* aver chiamato `list_block_definitions` per scoprire i nomi realmente presenti — non da una convenzione nota in anticipo.

**Input**
```json
{
  "name_pattern": "string (obbligatorio, es. \"TELECAMERA*\", supporta wildcard * e ?)",
  "layer": "string (opzionale, filtra per layer esatto)",
  "space": "string (opzionale, enum: \"model\" | \"paper\" | \"any\", default \"any\")"
}
```

**Output**
```json
{
  "matches": [
    {
      "handle": "string",
      "block_name": "string",
      "layer": "string",
      "insertion_point": { "x": 0.0, "y": 0.0, "z": 0.0 },
      "layout": "string (nome layout/model space)",
      "has_attributes": true
    }
  ],
  "count": 0
}
```

**Errori applicativi**
- `NO_DRAWING_OPEN` — nessun disegno attivo.
- `INVALID_PATTERN` — pattern non valido/vuoto.

**Note per Claude:** se `count` è 0, non assumere che il pattern sia sbagliato: chiamare `list_block_definitions` per verificare i nomi realmente presenti nel disegno prima di concludere che non esistono corrispondenze, e comunicarlo esplicitamente all'utente.

---

## 2. `get_block_attributes`

**Mode:** `read`
**Scopo:** legge tutti gli attributi (tag/valore) di una specifica istanza di blocco.

**Input**
```json
{
  "handle": "string (obbligatorio)"
}
```

**Output**
```json
{
  "handle": "string",
  "block_name": "string",
  "attributes": [
    { "tag": "string", "value": "string", "is_empty": false }
  ]
}
```

**Errori applicativi**
- `HANDLE_NOT_FOUND` — handle non esiste nel disegno corrente.
- `NOT_A_BLOCK_REFERENCE` — l'handle esiste ma non è un blocco.
- `NO_ATTRIBUTES` — il blocco esiste ma non ha attributi definiti (non è un errore bloccante, va segnalato come informazione).

---

## 3. `set_block_attribute`

**Mode:** `write` — richiede conferma UI esplicita.

**Input**
```json
{
  "handle": "string (obbligatorio)",
  "tag": "string (obbligatorio, nome esatto dell'attributo)",
  "value": "string (obbligatorio, nuovo valore)"
}
```

**Output**
```json
{
  "handle": "string",
  "tag": "string",
  "previous_value": "string",
  "new_value": "string",
  "success": true
}
```

**Errori applicativi**
- `HANDLE_NOT_FOUND`
- `TAG_NOT_FOUND` — l'attributo indicato non esiste su quel blocco.
- `WRITE_REJECTED_BY_USER` — l'utente ha annullato la conferma in UI (non è un errore di sistema, va gestito come esito normale del flusso).

**Nota:** l'orchestratore non deve mai inoltrare questo tool al bridge senza un flag `user_confirmed: true` proveniente dalla UI.

---

## 4. `get_titleblock_data`

**Mode:** `read`
**Scopo:** shortcut dedicato al cartiglio: individua il blocco cartiglio (secondo convenzione di progetto, vedi documento "Convenzioni naming disegno") e restituisce i suoi campi con validazione di completezza.

**Input**
```json
{
  "layout": "string (opzionale, default: layout corrente)"
}
```

**Output**
```json
{
  "handle": "string",
  "layout": "string",
  "fields": [
    { "tag": "string", "value": "string", "required": true, "is_empty": false }
  ],
  "missing_required_fields": ["string"]
}
```

**Errori applicativi**
- `TITLEBLOCK_NOT_FOUND` — nessun blocco cartiglio riconosciuto nel layout indicato.
- `LAYOUT_NOT_FOUND`

**Nota:** l'elenco dei campi "required" è definito in configurazione di progetto, non hardcoded nel bridge (si aggancia al documento "Convenzioni naming disegno").

---

## 5. `list_layers`

**Mode:** `read`

**Input:** nessun parametro richiesto (oggetto vuoto `{}`).

**Output**
```json
{
  "layers": [
    { "name": "string", "is_frozen": false, "is_locked": false, "color": "string" }
  ]
}
```

---

## 6. `get_entities_by_layer`

**Mode:** `read`

**Input**
```json
{
  "layer": "string (obbligatorio)",
  "entity_type": "string (opzionale, es. \"INSERT\", \"LINE\", \"TEXT\"; default: tutti)"
}
```

**Output**
```json
{
  "entities": [
    { "handle": "string", "type": "string", "layer": "string" }
  ],
  "count": 0
}
```

**Errori applicativi**
- `LAYER_NOT_FOUND`

---

## 6bis. `list_entity_clusters`

**Mode:** `read`
**Scopo:** segmenta il disegno (o un'area) in gruppi di entità spazialmente vicine/connesse, di dimensione compatibile con un "simbolo" (non intere pareti, quotature strutturali, o altri elementi di grande estensione). È il primo passo del matching geometrico: prima di poter cercare "qualcosa che assomiglia a X", serve un elenco di candidati su cui calcolare una firma.

**Input**
```json
{
  "bounding_box": {
    "min": { "x": 0.0, "y": 0.0 },
    "max": { "x": 0.0, "y": 0.0 }
  },
  "layout": "string (opzionale, default: layout corrente)",
  "max_cluster_extent": "number (opzionale, dimensione massima di un cluster in unità disegno; oltre questa soglia l'entità è considerata 'elemento strutturale' e non un candidato simbolo)"
}
```

**Output**
```json
{
  "clusters": [
    {
      "cluster_id": "string",
      "handles": ["string"],
      "bounding_box": { "min": { "x": 0.0, "y": 0.0 }, "max": { "x": 0.0, "y": 0.0 } },
      "entity_count": 0,
      "is_block_instance": false
    }
  ]
}
```

**Errori applicativi**
- `LAYOUT_NOT_FOUND`
- `REGION_TOO_LARGE` — se `bounding_box` copre l'intero disegno e questo è troppo denso per una segmentazione utile in tempi ragionevoli; in tal caso restringere l'area.

**Nota (rischio tecnico noto):** distinguere "un simbolo" da "un pezzo di un elemento più grande" con euristiche di prossimità/connettività è la parte più delicata del motore geometrico — soglie troppo larghe uniscono simboli distinti, soglie troppo strette spezzano un simbolo in più cluster. Va tarato e verificato sui disegni reali dell'utente (vedi Piano di Test, scenario dedicato), non assunto corretto a priori.

---

## 6ter. `get_geometry_signature`

**Mode:** `read`
**Scopo:** estrae una firma geometrica invariante a scala/rotazione da un cluster di entità o da un elenco di handle indicato esplicitamente (es. una selezione fatta dall'utente in UI, "questo è un esempio di telecamera").

**Input**
```json
{
  "handles": ["string"]
}
```

**Output**
```json
{
  "signature": {
    "primitive_counts": { "LINE": 0, "ARC": 0, "CIRCLE": 0, "POLYLINE": 0 },
    "relative_geometry": "string (rappresentazione interna opaca a Claude: descrittori di forma normalizzati — es. distanze/angoli relativi tra primitive, non esposti come dettaglio interpretabile)",
    "bounding_box_aspect_ratio": 0.0
  },
  "source_handles": ["string"]
}
```

**Errori applicativi**
- `HANDLE_NOT_FOUND` (uno o più handle non esistono)
- `EMPTY_SELECTION`

**Nota per Claude:** il campo `signature` è un dato tecnico da passare integralmente a `find_similar_geometry`/`save_signature_as_category`, non da interpretare o descrivere direttamente all'utente in termini di numeri — comunicare invece in linguaggio naturale cosa si sta facendo ("ho analizzato la forma di questo simbolo, ora cerco elementi simili nel disegno").

---

## 6quater. `find_similar_geometry`

**Mode:** `read`
**Scopo:** cerca nel disegno (o in un elenco di cluster candidati già noto da `list_entity_clusters`) i gruppi di entità con firma geometrica simile a quella data, entro una soglia di tolleranza.

**Input**
```json
{
  "signature": "object (ottenuto da get_geometry_signature o da save_signature_as_category)",
  "tolerance": "number (opzionale, 0.0-1.0, default 0.85; più alto = più permissivo)",
  "space": "string (opzionale, enum: \"model\" | \"paper\" | \"any\", default \"any\")",
  "bounding_box": "object (opzionale, per restringere la ricerca a un'area)"
}
```

**Output**
```json
{
  "matches": [
    {
      "handles": ["string"],
      "bounding_box": { "min": { "x": 0.0, "y": 0.0 }, "max": { "x": 0.0, "y": 0.0 } },
      "similarity_score": 0.0
    }
  ]
}
```

**Errori applicativi**
- `NO_DRAWING_OPEN`
- `INVALID_SIGNATURE` — la firma passata non è in un formato riconosciuto (es. proveniente da una versione diversa del motore, vedi §Versionamento).

**Nota per Claude (politica di confidenza):** un match con `similarity_score` alto (da tarare, indicativamente >0.9) può essere presentato come risultato diretto, dichiarando comunque il punteggio; un punteggio intermedio va presentato come "possibile corrispondenza, da verificare" e può giustificare una conferma visiva mirata (`get_region_screenshot`) prima di procedere; un punteggio basso non va scartato silenziosamente ma nemmeno proposto come match — va omesso o segnalato come incerto solo su richiesta esplicita dell'utente.

---

## 6quinquies. `save_signature_as_category` / `list_learned_categories`

**Mode:** `read` (operazioni locali, non modificano il disegno)

**`save_signature_as_category` — Input**
```json
{
  "signature": "object",
  "label": "string (es. \"telecamera\")",
  "scope": "string (enum: \"session\" | \"personal_library\")"
}
```

**Output**
```json
{
  "saved": true,
  "label": "string",
  "scope": "string"
}
```

**`list_learned_categories` — Input:** nessun parametro (oggetto vuoto `{}`).

**Output**
```json
{
  "categories": [
    { "label": "string", "scope": "string", "created_at": "string (ISO 8601)" }
  ]
}
```

**Nota per Claude:** una firma salvata con `scope: "personal_library"` viene riproposta come **ipotesi di partenza** in disegni futuri, mai come certezza — i disegni sono eterogenei, quindi un simbolo "telecamera" imparato su un disegno può non generalizzare bene a un disegno di un cliente diverso. Trattare sempre un match ottenuto da una firma di libreria con lo stesso criterio di confidenza descritto in `find_similar_geometry`, non assumerlo automaticamente corretto solo perché già "appreso".

---

## 6sextus. `tag_entities_as_category` / `find_tagged_entities`

**Scopo:** persistere il riconoscimento fatto una volta **direttamente nel file DWG**, tramite dati estesi (XDATA/extension dictionary) attaccati alle entità, non tramite una libreria esterna. A differenza di `save_signature_as_category` (che salva una *firma* per fare matching su disegni futuri), questo tool salva un'**etichetta diretta su entità già identificate in questo disegno**, così che riaprendo lo stesso file in futuro il riconoscimento sia immediato, senza rifare clustering/matching geometrico.

### `find_tagged_entities`

**Mode:** `read`
**Scopo:** cerca entità che hanno già un tag persistente salvato in una sessione precedente su questo stesso disegno. **Va controllato per primo**, prima ancora del Livello 1 (nomi/attributi): se il disegno è già stato "insegnato" in passato, è l'informazione più specifica e affidabile disponibile.

**Input**
```json
{
  "label": "string (opzionale; se omesso restituisce tutti i tag presenti nel disegno)"
}
```

**Output**
```json
{
  "tagged_entities": [
    { "handle": "string", "label": "string", "tagged_at": "string (ISO 8601)" }
  ]
}
```

**Errori applicativi**
- `NO_DRAWING_OPEN`

### `tag_entities_as_category`

**Mode:** `write` — modifica il file (scrive XDATA), ma non altera la geometria visibile; da trattare con lo stesso criterio di `highlight_entities` per quanto riguarda il livello di conferma richiesto (**da decidere in fase di UI**, vedi documento linee guida §4) — probabilmente una conferma leggera è sufficiente, dato il rischio molto contenuto rispetto a una scrittura geometrica.

**Input**
```json
{
  "handles": ["string"],
  "label": "string (es. \"telecamera\")"
}
```

**Output**
```json
{
  "tagged_count": 0,
  "label": "string"
}
```

**Errori applicativi**
- `HANDLE_NOT_FOUND`

**Nota tecnica:** implementare tramite un nome applicazione XDATA registrato (es. `CLAUDEBRIDGE`), per evitare collisioni con XDATA di altri plugin/software eventualmente già presenti sulle stesse entità.

**Nota privacy (vedi anche documento Sicurezza & Privacy):** i dati XDATA viaggiano **con il file DWG** — se il disegno viene ri-condiviso con il cliente o con terzi, i tag applicati restano nel file (per quanto invisibili nell'interfaccia standard, sono estraibili via LISP/API da chi sa cercarli). Non includere nell'etichetta alcuna informazione sensibile oltre alla categoria stessa (es. "telecamera"), mai note interne, commenti riservati, o dati di progetto non pertinenti alla categorizzazione.

---

## 7. `highlight_entities`

**Mode:** `write` — ma di sola visualizzazione (non modifica il DWG in modo persistente); in v1 richiede comunque conferma leggera o può essere eseguito senza conferma se il progetto lo considera "non distruttivo" — **da decidere in fase di UI** (vedi documento linee guida, §4).

**Input**
```json
{
  "handles": ["string"],
  "color": "string (opzionale, es. \"red\"; default: colore evidenziazione standard di progetto)",
  "clear_previous": true
}
```

**Output**
```json
{
  "highlighted_count": 0,
  "not_found_handles": ["string"]
}
```

**Nota:** l'evidenziazione è temporanea, non salvata nel DWG; va rimossa a fine sessione o su comando esplicito dell'utente/Claude (`clear_previous`).

---

## 8. `zoom_to`

**Mode:** `read` (non modifica il disegno, solo la vista)

**Input**
```json
{
  "handle": "string (obbligatorio)",
  "margin_factor": "number (opzionale, default 1.5)"
}
```

**Output**
```json
{
  "success": true
}
```

**Errori applicativi**
- `HANDLE_NOT_FOUND`

---

## 9. `run_lisp`

**Mode:** `write` — sempre conferma esplicita, mostrando all'utente l'espressione LISP esatta prima dell'esecuzione.

**Input**
```json
{
  "expression": "string (obbligatorio, espressione LISP valida)",
  "reason": "string (obbligatorio, spiegazione in linguaggio naturale di cosa fa, mostrata all'utente in UI)"
}
```

**Output**
```json
{
  "success": true,
  "result": "string (output testuale della valutazione LISP, se presente)",
  "error_message": "string (se success=false)",
  "blocked_by_policy": false
}
```

**Errori applicativi**
- `LISP_EVAL_ERROR` — errore di sintassi o esecuzione, con messaggio nativo di BricsCAD.
- `WRITE_REJECTED_BY_USER`
- `BLOCKED_BY_EXECUTION_POLICY` — l'espressione è stata rifiutata prima ancora di arrivare alla conferma utente, perché viola la execution policy (vedi sotto).

### Execution policy (whitelist/blacklist)

`run_lisp` è l'unico tool che esegue codice arbitrario, quindi è anche il punto di maggior rischio del contratto. Il bridge deve applicare una **policy di esecuzione** prima di proporre l'espressione all'utente per conferma, non affidarsi alla sola conferma UI come unica barriera:

- **Blacklist di funzioni pericolose**, da rifiutare sempre a prescindere dalla conferma utente: funzioni che accedono al filesystem al di fuori dell'ambito del disegno (`(open ...)`, scrittura/lettura file arbitraria), funzioni che eseguono comandi di sistema o shell, funzioni che modificano configurazioni globali di BricsCAD non legate al disegno corrente.
- **Whitelist implicita preferita**: se possibile, definire l'insieme di funzioni LISP effettivamente necessarie per i casi d'uso del progetto (manipolazione entità, query, comandi di disegno) ed essere restrittivi di default, ampliando solo se un caso reale lo richiede — più sicuro che partire da "tutto permesso tranne la blacklist".
- La policy va implementata nel bridge (non nell'orchestratore, che non conosce LISP), e il rifiuto per policy è un errore distinto (`BLOCKED_BY_EXECUTION_POLICY`) dal rifiuto per conferma utente negata (`WRITE_REJECTED_BY_USER`), perché comunicano cose diverse a Claude: nel primo caso l'azione non è mai proponibile, nel secondo è stata proposta e l'utente ha scelto di non procedere.

**Nota:** questo è il tool "di fuga" per operazioni non coperte da tool dedicati. Va usato da Claude solo quando nessun altro tool copre il caso, e sempre con `reason` chiaro e comprensibile a un non programmatore.

---

## 10bis. `get_text_in_region`

**Mode:** `read`
**Scopo:** legge tutto il testo grezzo (entità TEXT/MTEXT) presente in un'area del disegno, in coordinate del disegno. Utile per cartigli o annotazioni compilate come testo libero, non come attributi di blocco — caso frequente e non coperto da `get_block_attributes`.

**Input**
```json
{
  "bounding_box": {
    "min": { "x": 0.0, "y": 0.0 },
    "max": { "x": 0.0, "y": 0.0 }
  },
  "layout": "string (opzionale, default: layout corrente)"
}
```

**Output**
```json
{
  "text_entities": [
    { "handle": "string", "content": "string", "position": { "x": 0.0, "y": 0.0 } }
  ]
}
```

**Errori applicativi**
- `LAYOUT_NOT_FOUND`
- `EMPTY_REGION` — nessun testo trovato nell'area indicata (non bloccante, va segnalato come informazione).

**Nota per Claude:** usare questo tool quando `get_block_attributes`/`get_titleblock_data` non trovano attributi utili, prima di ricorrere allo screenshot — è comunque un dato esatto (testo reale), non un'interpretazione visiva.

---

## 10ter. `get_region_screenshot`

**Mode:** `read`
**Scopo:** esporta uno screenshot raster di una zona **specifica e delimitata** del disegno, individuata in coordinate del disegno (non il viewport corrente dell'utente a schermo). Da usare come ultima risorsa, quando né gli attributi né il testo grezzo bastano a interpretare un elemento, o per confermare visivamente un caso ambiguo.

**Input**
```json
{
  "bounding_box": {
    "min": { "x": 0.0, "y": 0.0 },
    "max": { "x": 0.0, "y": 0.0 }
  },
  "layout": "string (opzionale, default: layout corrente)",
  "zoom_margin_factor": "number (opzionale, default 1.3)",
  "max_resolution": "integer (opzionale, default 1568)"
}
```

**Output**
```json
{
  "image_base64": "string",
  "media_type": "image/png",
  "actual_bounding_box": { "min": { "x": 0.0, "y": 0.0 }, "max": { "x": 0.0, "y": 0.0 } }
}
```

**Errori applicativi**
- `LAYOUT_NOT_FOUND`
- `REGION_TOO_LARGE` — l'area richiesta è troppo estesa per una lettura visiva utile (es. l'intero foglio); il bridge dovrebbe rifiutare e chiedere a Claude di restringere la bounding box, per evitare screenshot illeggibili o eccessivamente costosi.

**Nota per Claude (limite noto):** questo tool converte la geometria vettoriale esatta in un'immagine raster — si perde la corrispondenza diretta pixel↔handle. Va usato solo per **interpretare visivamente** un'area già localizzata tramite dati strutturati (bounding box nota da `find_blocks`/`list_block_definitions`), mai per "cercare" qualcosa su un'area generica o sull'intero disegno. Dopo l'interpretazione visiva, per qualunque azione (evidenziare, modificare) tornare a usare l'handle noto dal dato strutturato, non tentare di dedurre coordinate dai pixel dell'immagine.

---

## 11. `export_current_view_image` *(vedi anche `get_region_screenshot`, più mirato)*

**Mode:** `read`

**Input**
```json
{
  "layout": "string (opzionale, default: layout corrente)",
  "max_resolution": "integer (opzionale, default 1568, in linea con i limiti pratici di elaborazione immagini)"
}
```

**Output**
```json
{
  "image_base64": "string",
  "media_type": "image/png"
}
```

**Nota:** da usare solo come complemento, non come sostituto dei tool sui dati strutturati (vedi discussione in linee guida principali, §Estensione futura preprocessing NN, per i casi in cui questo tool non basta).

---

## Errori comuni (livello orchestratore, validi per tutti i tool)

| Codice | Significato |
|---|---|
| `SCHEMA_VALIDATION_FAILED` | parametri non conformi allo schema del tool |
| `TOOL_NOT_FOUND` | Claude ha richiesto un tool non registrato |
| `BRIDGE_UNAVAILABLE` | il plugin non è raggiungibile (BricsCAD chiuso o plugin non caricato) |
| `INTERNAL_ERROR` | eccezione non gestita nel bridge — da loggare sempre, mai esporre stack trace grezzo a Claude |

Tutti gli errori vengono restituiti a Claude come risultato del tool (non come eccezione dell'orchestratore), in modo che Claude possa reagire in linguaggio naturale ("non ho trovato disegni aperti, puoi aprirne uno?") invece di interrompere la conversazione.

---

## Versionamento dello schema

Ogni tool ha un campo implicito `schema_version` (partire da `"1.0"`). Quando si modifica un contratto in modo non retrocompatibile (rinomina campo, cambio tipo), incrementare la versione e documentare il cambio in un changelog in fondo a questo file, per evitare che orchestratore e bridge si disallineino silenziosamente durante lo sviluppo.

### Changelog
- **1.0** — versione iniziale, tool 1-10 come sopra.
- **1.1** — aggiunto tool `list_block_definitions` (§0), necessario perché i disegni gestiti sono totalmente eterogenei e non esiste uno standard di naming presupposto (vedi documento 04).
- **1.2** — aggiunti `get_text_in_region` e `get_region_screenshot` (§10bis, §10ter): meccanismo "dati esatti per localizzare + immagine mirata per interpretare", per i casi in cui blocchi/attributi non bastano (cartigli a testo libero, simboli disegnati senza blocco, verifica visiva di casi ambigui).
- **2.0** — aggiunto il **Livello 2 di riconoscimento** (matching geometrico): `list_entity_clusters`, `get_geometry_signature`, `find_similar_geometry`, `save_signature_as_category`, `list_learned_categories` (§6bis-6quinquies). Cambio di scope importante: il riconoscimento per nome/attributo (Livello 1) non è più sufficiente da solo, dato l'uso reale su disegni di terzi con naming casuale o geometria non a blocco — vedi documento "Riconoscimento simboli in disegni eterogenei" per il razionale completo.
- **2.1** — tre aggiunte ispirate a un'analisi di architetture MCP simili in altri ambiti (integrazioni AI-Blender): (a) sezione trasversale su thread-marshaling, requisito implementativo per `IBricscadBridge`; (b) execution policy (whitelist/blacklist) per `run_lisp` (§9), con nuovo errore `BLOCKED_BY_EXECUTION_POLICY`; (c) nuovi tool `tag_entities_as_category`/`find_tagged_entities` (§6sextus) per persistere il riconoscimento direttamente nel file DWG via XDATA, controllato per primo prima ancora del Livello 1.
