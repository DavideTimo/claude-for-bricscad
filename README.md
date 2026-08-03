# Linee Guida di Progetto — Assistente Claude per BricsCAD

## 1. Visione e obiettivi

Integrare Claude direttamente in BricsCAD tramite un pannello di chat, per permettere all'utente di:

1. **Interrogare il disegno** in linguaggio naturale ("controlla se ogni telecamera ha il tag", "quali campi del cartiglio sono vuoti").
2. **Ricevere consigli** su modellazione, standard di disegno, coerenza dei dati.
3. **Delegare l'esecuzione di comandi** direttamente a Claude, su richiesta esplicita, con conferma dell'utente prima di modifiche irreversibili.
4. **Ottenere feedback visivo** nel disegno stesso (evidenziazione, zoom su entità, annotazioni temporanee) in risposta all'analisi di Claude.

### Obiettivi non-goal (per ora)
- Non puntiamo a un "autopilota" che disegna interi progetti senza supervisione.
- Non puntiamo, in questa fase, al riconoscimento visivo puro di simboli via immagine: ci appoggiamo ai dati strutturati del DWG (blocchi/attributi), più affidabili e verificabili.
- Non gestiamo (in v1) collaborazione multi-utente o versioning del disegno.

---

## 2. Principio architetturale di fondo

Claude non "vede" il disegno come farebbe un umano: lo **interroga** come una base dati tramite un set di *tool* (function-calling) che il plugin espone. Le operazioni visive (evidenziare, fare zoom, mostrare uno screenshot) sono un canale di supporto, non il meccanismo primario di comprensione.

```
BricsCAD (processo host)
│
├─ Pannello Chat (WPF, docked panel)
│     • UI conversazionale
│     • mostra proposte di Claude, richiede conferma per azioni
│
├─ Plugin .NET (BRX-based, C#)
│     • Espone "tool" che leggono/scrivono il database del disegno
│     • Esegue le azioni confermate dall'utente
│     • Gestisce evidenziazioni, zoom, overlay temporanei
│
├─ Orchestratore (libreria C# separata, testabile)
│     • Ciclo di tool-use con l'API Anthropic (Messages API)
│     • Mantiene lo storico conversazione
│     • Serializza i risultati dei tool in JSON per Claude
│
└─ API Anthropic (Claude Sonnet/Opus via Messages API)
```

Il plugin e l'orchestratore sono **disaccoppiati**: l'orchestratore non sa nulla di BricsCAD, riceve solo definizioni di tool astratte e le esegue tramite un'interfaccia (`IBricscadBridge`). Questo permette di testare la logica di conversazione senza aprire BricsCAD, e in futuro di riusarla su un altro CAD se necessario.

---

## 3. Linguaggio e ambiente di sviluppo

Confermato l'orientamento **.NET / Microsoft stack**, che è anche la scelta più naturale perché BricsCAD espone un'API .NET nativa (oltre a BRX C++, LISP, COM).

| Componente | Tecnologia |
|---|---|
| Linguaggio | C# (.NET 8) |
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
├─ ClaudeBridge.UI/             → pannello WPF
│   ├─ ChatPanel.xaml
│   ├─ ChatPanelViewModel.cs
│   └─ MessageBubbleControl.xaml
│
└─ ClaudeBridge.Tests/          → xUnit, mock di IBricscadBridge
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
- **Feedback nel disegno**:
  - Evidenziazione temporanea (colore/overlay) delle entità menzionate nella risposta.
  - Zoom automatico opzionale (con conferma) sull'entità discussa.
  - Le evidenziazioni si puliscono a fine sessione o su comando esplicito.

---

## 5. Tool iniziali da esporre a Claude (v1)

| Tool | Funzione |
|---|---|
| `find_blocks(name_pattern, layer?)` | Trova istanze di blocco per nome/layer |
| `get_block_attributes(handle)` | Legge attributi di un blocco |
| `set_block_attribute(handle, tag, value)` | Scrive un attributo (richiede conferma UI) |
| `get_titleblock_data()` | Shortcut dedicato al cartiglio |
| `list_layers()` | Elenco layer del disegno |
| `get_entities_by_layer(layer)` | Entità per layer |
| `highlight_entities(handles[], color?)` | Evidenzia entità nel disegno |
| `zoom_to(handle)` | Centra la vista su un'entità |
| `run_lisp(expression)` | Esecuzione comando LISP generico (solo dopo conferma esplicita, per azioni non coperte da tool dedicati) |
| `export_current_view_image()` | Screenshot per casi che richiedono analisi visiva |

I tool "di sola lettura" (find_blocks, get_*, list_*) possono essere eseguiti senza conferma. I tool "di scrittura" (set_*, run_lisp, e in generale ogni comando che modifica il DWG) **richiedono sempre conferma esplicita** nella UI, almeno in v1.

---

## 6. Sicurezza e affidabilità

- **Nessuna azione distruttiva automatica**: ogni scrittura sul disegno passa da una conferma utente esplicita in v1; si potrà introdurre una modalità "autopilota" più avanti, per singoli utenti esperti, come opzione disattivabile.
- **API key**: mai in chiaro nel codice; salvata localmente in configurazione utente, con possibilità di usarla da variabile d'ambiente.
- **Validazione dei parametri dei tool**: l'orchestratore valida lo schema prima di eseguire (es. handle esistente, valori plausibili) prima di passare al bridge.
- **Logging**: tenere traccia locale (file di log) di tool chiamati e azioni eseguite, per audit e per capire eventuali comportamenti indesiderati.
- **Convenzioni sui disegni**: il progetto assume nomi di blocco/attributo coerenti (es. `TELECAMERA`, `TAG`). Va previsto un file di configurazione/mapping per adattare i pattern ai propri standard di disegno, così da non hardcodare nomi specifici nel codice.

---

## 7. Roadmap proposta (fasi)

1. **Fase 0 — Prototipo bridge**: libreria `ClaudeBridge.Core` + implementazione minima del bridge per `find_blocks`/`get_block_attributes`, testata su un disegno reale, senza UI (solo console/log).
2. **Fase 1 — Ciclo di conversazione**: integrazione con Messages API, tool-use funzionante da riga di comando o UI minimale.
3. **Fase 2 — Pannello WPF**: chat UI dockata in BricsCAD, senza ancora azioni di scrittura.
4. **Fase 3 — Azioni con conferma**: `set_block_attribute`, `run_lisp`, evidenziazione/zoom.
5. **Fase 4 — Casi d'uso verticali**: controllo tag telecamere, controllo cartiglio, altri controlli specifici del tuo dominio.
6. **Fase 5 — Rifiniture**: gestione errori, logging, configurabilità pattern/nomi, eventuale modalità "azione diretta" senza conferma per utenti esperti.

---

## 8. Estensione futura — preprocessing con reti neurali specializzate

**Non è parte della v1.** Il flusso base (Fasi 0-5) si basa su estrazione deterministica da blocchi/attributi, che copre la maggior parte dei casi d'uso con affidabilità del 100% e senza training. Questa sezione va rivalutata solo dopo aver verificato sui disegni reali dove il dato strutturato non basta.

### Quando avrebbe senso introdurla

- **Disegni legacy o non standardizzati**: simboli disegnati con geometria semplice (linee/cerchi/testo sciolto) invece che come blocchi veri, senza attributi collegati.
- **Cartigli o disegni non-nativi** (PDF scansionati, disegni di terzi non in DWG): serve OCR/estrazione tabellare, non query sul database.
- **Controlli geometrici/topologici** non catturabili dagli attributi: sovrapposizioni, simboli fuori standard, clash tra elementi.
- **Ricerca per similarità visiva**: individuare simboli "orfani" disegnati manualmente, non riconoscibili come blocco.

### Quando NON ha senso

Se i blocchi/attributi sono già coerenti e presenti, aggiungere un modello neurale sopra un dato già esatto introduce solo un rischio di falsi positivi/negativi senza alcun beneficio.

### Architetture candidate (alcune di ricerca, non "pronte all'uso")

| Esigenza | Approccio |
|---|---|
| Symbol spotting su geometria vettoriale (DWG nativo) | Graph Neural Network: entità → grafo (nodi = primitive, archi = relazioni spaziali/topologiche); più precisa della CV su raster perché sfrutta la geometria esatta |
| Estrazione cartiglio da PDF/scansioni | Modelli layout-aware per documenti/tabelle (es. Table Transformer, LayoutLM-style, Donut) |
| Rilevazione anomalie senza ground truth chiaro | Autoencoder / anomaly detection addestrati su configurazioni "normali" |
| Ricerca per similarità di simboli disegnati a mano | Embedding visivi tipo CLIP/SigLIP |

### Come si inserirebbe nell'architettura

Un **microservizio di preprocessing separato** (verosimilmente Python, per l'ecosistema ML), che:
1. riceve il DWG grezzo (o l'export raster/PDF),
2. applica il modello appropriato,
3. restituisce output strutturato in JSON,
4. viene consumato dagli stessi tool già esposti a Claude (`find_blocks`, `get_titleblock_data`, ecc.), mantenendo l'orchestratore e Claude agnostici rispetto alla fonte del dato (blocco nativo vs. modello ML).

In questo modo Claude continua a lavorare sempre su dati "puliti" e strutturati, indipendentemente dal fatto che il disegno originale fosse ben strutturato o meno.

### Prerequisito prima di avviare questa fase

Un audit su un campione rappresentativo dei propri disegni per quantificare il problema reale (es. "il X% dei disegni più vecchi ha simboli non a blocco"), invece di costruire un modello generico "per sicurezza" senza un caso d'uso misurato.

---

## 9. Domande aperte da chiarire prima di iniziare lo sviluppo

- Versione di BricsCAD target (V24/V25/V26?) e licenza (Pro necessaria per alcune API verticali).
- I disegni esistenti seguono già una nomenclatura coerente di blocchi/attributi, o serve prima un lavoro di normalizzazione?
- Preferenza per l'SDK ufficiale Anthropic (se disponibile in C#) o client HTTP diretto verso le Messages API.
- Modalità di distribuzione del plugin (uso singolo, o distribuzione a più postazioni/utenti in azienda)?
