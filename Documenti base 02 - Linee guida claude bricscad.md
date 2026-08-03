# Linee Guida di Progetto — Assistente Claude per BricsCAD

## 1. Visione e obiettivi

Integrare Claude direttamente in BricsCAD tramite un pannello di chat, per permettere all'utente di:

1. **Interrogare il disegno** in linguaggio naturale ("controlla se ogni telecamera ha il tag", "quali campi del cartiglio sono vuoti").
2. **Ricevere consigli** su modellazione, standard di disegno, coerenza dei dati.
3. **Delegare l'esecuzione di comandi** direttamente a Claude, su richiesta esplicita, con conferma dell'utente prima di modifiche irreversibili.
4. **Ottenere feedback visivo** nel disegno stesso (evidenziazione, zoom su entità, annotazioni temporanee) in risposta all'analisi di Claude.

### Obiettivi non-goal (per ora)
- Non puntiamo a un "autopilota" che disegna interi progetti senza supervisione.
- Non puntiamo, in v1, a un modello neurale addestrato su misura (serve un dataset che oggi non esiste) — il riconoscimento di simboli senza nome/blocco si basa su matching geometrico euristico (vedi §5) e conferma visiva mirata, non su una rete neurale addestrata da zero.
- Non gestiamo (in v1) collaborazione multi-utente o versioning del disegno.

> **Nota importante**: la premessa iniziale del progetto ipotizzava disegni con blocchi e attributi ragionevolmente nominati. L'uso reale ha smentito questa ipotesi: i disegni gestiti provengono spesso da terzi, con blocchi nominati in modo casuale o del tutto assenti (geometria sciolta: linee, polilinee, oggetti 3D). Il riconoscimento per nome/attributo resta il primo tentativo quando disponibile, ma **non può essere l'unico pilastro**: il matching geometrico (vedi §5) è ora parte integrante della v1, non un'estensione futura.

---

## 2. Principio architetturale di fondo

Claude non "vede" il disegno come farebbe un umano di default: lo **interroga** come una base dati tramite un set di *tool* (function-calling). Ma dato che i disegni reali non hanno uno standard di naming, il sistema include ora **quattro livelli di riconoscimento**, usati in cascata (vedi documento "Riconoscimento simboli in disegni eterogenei" per i dettagli):

0. **Tag persistenti già presenti nel disegno** (salvati in una sessione precedente via XDATA) — controllato per primo, se disponibile evita di rifare tutto il resto.
1. **Nomi/attributi** (quando esistono e sono informativi) — dato esatto.
2. **Matching geometrico per similarità di forma** ("insegna per esempio") — quando nomi/attributi non bastano o non esistono.
3. **Conferma visiva mirata** (screenshot di una zona nota) — per confermare un match incerto, mai per "cercare" alla cieca.

```
BricsCAD (processo host)
│
├─ Pannello Chat (WPF, docked panel)
│     • UI conversazionale
│     • mostra proposte di Claude, richiede conferma per azioni
│     • permette all'utente di indicare un esempio ("questa è una telecamera")
│
├─ Plugin .NET (BRX-based, C#)
│     • Espone "tool" che leggono/scrivono il database del disegno
│     • Esegue le azioni confermate dall'utente
│     • Gestisce evidenziazioni, zoom, overlay temporanei
│
├─ Motore di Riconoscimento Geometrico (libreria C# separata, ClaudeBridge.GeometryEngine)
│     • Clustering spaziale di entità (raggruppa geometria sciolta in candidati "simbolo")
│     • Estrazione di firme geometriche invarianti a scala/rotazione
│     • Calcolo di similarità tra firme, ricerca nel disegno
│     • Libreria personale di simboli appresi (opzionale, persistita)
│
├─ Orchestratore (libreria C# separata, testabile)
│     • Ciclo di tool-use con l'API Anthropic (Messages API)
│     • Mantiene lo storico conversazione
│     • Serializza i risultati dei tool in JSON per Claude
│
└─ API Anthropic (Claude Sonnet/Opus via Messages API)
```

Il plugin, il motore geometrico e l'orchestratore restano **disaccoppiati**: l'orchestratore non sa nulla di BricsCAD né di come viene calcolata una firma geometrica, riceve solo definizioni di tool astratte e i loro risultati. Questo permette di testare/evolvere il motore di riconoscimento (che è la parte più "di ricerca" del progetto) senza toccare il resto.

---

## 3. Linguaggio e ambiente di sviluppo

Confermato l'orientamento **.NET / Microsoft stack**, che è anche la scelta più naturale perché BricsCAD espone un'API .NET nativa (oltre a BRX C++, LISP, COM).

| Componente | Tecnologia |
|---|---|
| Linguaggio | C# (.NET 10) |
| Plugin BricsCAD | BricsCAD .NET API (namespace `Teigha`/`Bricscad.ApplicationServices`, `Bricscad.DatabaseServices`) |
| UI pannello | WPF (docked tool palette in BricsCAD) |
| Comunicazione con Claude | HttpClient verso Anthropic Messages API (SDK ufficiale C# se disponibile, altrimenti client HTTP diretto) |
| Persistenza configurazione | file JSON locale (API key, preferenze) — **mai** hardcoded |
| Test | xUnit per l'orchestratore, isolato da BricsCAD |
| IDE | Visual Studio 2022 (richiesto per debug plugin nativi BRX/.NET in-process) |
| Versionamento | Git, repository singolo con soluzione multi-progetto |

### Struttura soluzione proposta

```
ClaudeForBricsCAD.sln
│
├─ ClaudeBridge.Core/          → libreria .NET standard, no dipendenze BricsCAD
│   ├─ IBricscadBridge.cs      → interfaccia astratta dei tool
│   ├─ ToolDefinitions.cs      → schema JSON dei tool per Claude
│   ├─ ClaudeOrchestrator.cs   → ciclo di conversazione + tool-use
│   └─ AnthropicClient.cs      → wrapper API
│
├─ ClaudeBridge.BricscadPlugin/ → progetto .NET Framework/Core specifico BricsCAD
│   ├─ BricscadBridgeImpl.cs    → implementazione IBricscadBridge (entget, blocchi, attributi...)
│   ├─ HighlightService.cs      → evidenziazione/zoom entità
│   └─ PluginEntryPoint.cs      → registrazione comandi BricsCAD
│
├─ ClaudeBridge.GeometryEngine/ → libreria .NET standard, no dipendenze BricsCAD dirette
│   ├─ EntityClustering.cs      → raggruppamento spaziale di entità in candidati "simbolo"
│   ├─ ShapeSignature.cs        → estrazione firma geometrica invariante a scala/rotazione
│   ├─ SimilarityMatcher.cs     → confronto firme, ricerca per soglia di similarità
│   └─ SymbolLibrary.cs         → libreria personale di simboli appresi (persistenza locale)
│
├─ ClaudeBridge.UI/             → pannello WPF
│   ├─ ChatPanel.xaml
│   ├─ ChatPanelViewModel.cs
│   └─ MessageBubbleControl.xaml
│
└─ ClaudeBridge.Tests/          → xUnit, mock di IBricscadBridge, test unitari su GeometryEngine
```

---

## 4. UI — pannello di chat

- **Posizione**: docked panel laterale (come le palette esistenti di BricsCAD), sempre accessibile durante il lavoro sul disegno.
- **Elementi principali**:
  - Cronologia messaggi (utente / Claude), stile chat.
  - Area di input testo in basso.
  - Quando Claude propone un'azione sul disegno (es. "vuoi che sposti questa entità?"), mostrare una **card di conferma** con: descrizione azione, entità coinvolte (con pulsante "mostra nel disegno"), pulsanti Conferma/Annulla.
  - Indicatore di "sta pensando/eseguendo tool..." durante le chiamate.
  - Log espandibile facoltativo: quali tool sono stati chiamati e con quali parametri (utile in fase di debug e per fiducia dell'utente).
  - **Selezione di un esempio ("insegna per esempio")**: quando Claude chiede "puoi indicarmi un esempio di telecamera nel disegno?", l'utente deve poter selezionare un'entità/gruppo direttamente nel disegno (selezione BricsCAD nativa) invece di descriverla a parole; il plugin cattura la selezione e la passa a Claude come riferimento.
- **Feedback nel disegno**:
  - Evidenziazione temporanea (colore/overlay) delle entità menzionate nella risposta.
  - Zoom automatico opzionale (con conferma) sull'entità discussa.
  - Le evidenziazioni si puliscono a fine sessione o su comando esplicito.

---

## 5. Tool da esporre a Claude (v1)

### Livello 0 — tag persistenti già presenti nel disegno (controllato per primo)

| Tool | Funzione |
|---|---|
| `find_tagged_entities(label?)` | Cerca entità già taggate in una sessione precedente su questo disegno (via XDATA) |
| `tag_entities_as_category(handles[], label)` | Persiste un'etichetta direttamente nel file, per riconoscimento immediato in sessioni future |

### Livello 1 — nomi/attributi (quando disponibili)

| Tool | Funzione |
|---|---|
| `list_block_definitions()` | Scopre tutti i blocchi presenti nel disegno corrente |
| `find_blocks(name_pattern, layer?)` | Trova istanze di blocco per nome/layer, tipicamente dopo la scoperta |
| `get_block_attributes(handle)` | Legge attributi di un blocco |
| `get_titleblock_data()` | Shortcut dedicato al cartiglio |
| `list_layers()` | Elenco layer del disegno |
| `get_entities_by_layer(layer)` | Entità per layer |

### Livello 2 — matching geometrico ("insegna per esempio")

| Tool | Funzione |
|---|---|
| `list_entity_clusters(bounding_box?, layout?, max_cluster_extent?)` | Segmenta il disegno (o un'area) in gruppi di entità vicine/connesse di dimensione "a simbolo", indipendentemente dal fatto che siano blocchi o geometria sciolta |
| `get_geometry_signature(handles[])` | Estrae la firma geometrica (invariante a scala/rotazione) di un cluster o di un gruppo di entità indicato |
| `find_similar_geometry(signature, tolerance?, space?)` | Cerca nel disegno cluster con firma simile a quella data, restituendo match con punteggio di similarità |
| `save_signature_as_category(signature, label, scope)` | Salva una firma come categoria riconosciuta (`scope`: `session` o `personal_library`), per riuso nella stessa sessione o tra disegni diversi |
| `list_learned_categories()` | Elenca le categorie già salvate nella libreria personale |

### Livello 3 — conferma visiva mirata

| Tool | Funzione |
|---|---|
| `get_text_in_region(bounding_box)` | Legge testo grezzo (cartigli/annotazioni non a blocco) in un'area nota |
| `get_region_screenshot(bounding_box)` | Screenshot mirato su un'area nota, per confermare visivamente un match incerto — mai per cercare alla cieca (vedi documento "Riconoscimento simboli", §7, per il meccanismo e i suoi limiti) |
| `export_current_view_image()` | Screenshot per casi che richiedono analisi visiva generale |

### Azioni ed esecuzione

| Tool | Funzione |
|---|---|
| `set_block_attribute(handle, tag, value)` | Scrive un attributo (richiede conferma UI) |
| `highlight_entities(handles[], color?)` | Evidenzia entità nel disegno |
| `zoom_to(handle)` | Centra la vista su un'entità |
| `run_lisp(expression)` | Esecuzione comando LISP generico (solo dopo conferma esplicita, per azioni non coperte da tool dedicati) |

I tool "di sola lettura" (livelli 1-3, incluse le funzioni di libreria simboli) possono essere eseguiti senza conferma. I tool "di scrittura" sul disegno (`set_block_attribute`, `run_lisp`) **richiedono sempre conferma esplicita** nella UI, almeno in v1.

---

## 6. Sicurezza e affidabilità

- **Nessuna azione distruttiva automatica**: ogni scrittura sul disegno passa da una conferma utente esplicita in v1; si potrà introdurre una modalità "autopilota" più avanti, per singoli utenti esperti, come opzione disattivabile.
- **API key**: mai in chiaro nel codice; salvata localmente in configurazione utente, con possibilità di usarla da variabile d'ambiente.
- **Validazione dei parametri dei tool**: l'orchestratore valida lo schema prima di eseguire (es. handle esistente, valori plausibili) prima di passare al bridge.
- **Logging**: tenere traccia locale (file di log) di tool chiamati e azioni eseguite, per audit e per capire eventuali comportamenti indesiderati.
- **Nessuno standard di naming presupposto**: i disegni gestiti sono totalmente eterogenei tra loro (provenienza, software, autori diversi, blocchi nominati casualmente o assenti). Claude scopre la struttura di ogni disegno a runtime e, quando nomi/attributi non bastano, usa il matching geometrico per similarità di forma (vedi documento "Riconoscimento simboli in disegni eterogenei").
- **Il matching geometrico è euristico, non esatto**: si basa su soglie di similarità, quindi può produrre falsi positivi/negativi. Claude deve dichiarare sempre il livello di confidenza di un match e chiedere conferma nei casi borderline, mai presentare un match geometrico come certezza assoluta.
- **Thread-marshaling**: l'implementazione del bridge deve marshalling esplicitamente le chiamate all'API BricsCAD sul thread principale, non chiamarle direttamente dal thread di gestione della richiesta (vedi Tool Contract, sezione dedicata, e Setup Environment Guide per il punto critico da verificare in Fase 0).
- **Execution policy su `run_lisp`**: il bridge applica una whitelist/blacklist che rifiuta espressioni pericolose (accesso filesystem, comandi di sistema) prima ancora della conferma UI, non si affida alla sola conferma utente come barriera (vedi Tool Contract §9 e documento Sicurezza & Privacy §5bis).

---

## 7. Roadmap proposta (fasi)

1. **Fase 0 — Prototipo doppio binario**: 
   - (a) libreria `ClaudeBridge.Core` + implementazione minima del bridge per `list_block_definitions`/`get_block_attributes`;
   - (b) prototipo isolato di `ClaudeBridge.GeometryEngine` — clustering spaziale + estrazione firma geometrica, testato su 2-3 disegni reali eterogenei per validare che il matching per forma funzioni prima di formalizzare l'intero schema.
   - Nessuna UI in questa fase, solo console/log.
2. **Fase 1 — Ciclo di conversazione**: integrazione con Messages API, tool-use funzionante da riga di comando o UI minimale, includendo sia i tool di livello 1 che quelli di livello 2 (geometria).
3. **Fase 2 — Pannello WPF**: chat UI dockata in BricsCAD, con supporto alla selezione di un esempio nel disegno ("insegna per esempio"), senza ancora azioni di scrittura.
4. **Fase 3 — Azioni con conferma**: `set_block_attribute`, `run_lisp`, evidenziazione/zoom.
5. **Fase 4 — Casi d'uso verticali**: controllo tag telecamere, controllo cartiglio, altri controlli specifici del tuo dominio, su disegni reali eterogenei.
6. **Fase 5 — Libreria simboli personale**: persistenza delle firme apprese tra sessioni/disegni diversi (`save_signature_as_category` con scope `personal_library`), per ridurre quante volte serve "insegnare" lo stesso simbolo.
7. **Fase 6 — Rifiniture**: gestione errori, logging, tuning delle soglie di similarità, eventuale modalità "azione diretta" senza conferma per utenti esperti.

---

## 8. Estensione futura ulteriore — modello addestrato su misura

Il matching geometrico euristico (§5, livello 2, dettagliato nel documento "Riconoscimento simboli in disegni eterogenei") è il pilastro della v1 per i casi senza nome/attributo utile. Un **modello addestrato su misura** (es. Graph Neural Network su rappresentazione vettoriale) resta comunque uno step successivo plausibile, ma non è realistico "da subito": richiede un dataset etichettato che oggi non esiste.

**Prerequisito prima di avviare questo step**: un uso reale prolungato del matching euristico genera naturalmente esempi (simboli insegnati e confermati dall'utente) che possono diventare i primi dati di addestramento — a quel punto la libreria simboli personale (Fase 5 della roadmap) diventa anche la base per valutare se un modello addestrato supererebbe l'euristica attuale, invece di costruirlo "per sicurezza" senza dati reali a supporto.

---

## 9. Domande aperte da chiarire prima di iniziare lo sviluppo

- Versione di BricsCAD target (V24/V25/V26?) e licenza (Pro necessaria per alcune API verticali).
- Preferenza per l'SDK ufficiale Anthropic (se disponibile in C#) o client HTTP diretto verso le Messages API.
- Modalità di distribuzione del plugin (uso singolo, o distribuzione a più postazioni/utenti in azienda)?
- Quali categorie di simboli sono prioritarie da riconoscere per prime (telecamere? altri impianti?), per scegliere i disegni di validazione del prototipo geometrico in Fase 0.
- La libreria simboli personale (Fase 5) va pensata per uso singolo-utente o condivisa tra più persone dello stesso studio/azienda?
