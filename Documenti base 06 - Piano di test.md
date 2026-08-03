# Piano di Test

Due livelli di test: automatici (xUnit, sulla logica deterministica) e manuali (scenari end-to-end su BricsCAD reale, dove entra in gioco il comportamento di Claude).

---

## 1. Test automatici (xUnit — `ClaudeBridge.Tests`)

Coprono `ClaudeBridge.Core`, usando un mock di `IBricscadBridge`. **Non richiedono BricsCAD aperto.**

### Orchestratore / ciclo di tool-use
- [ ] Il ciclo gestisce correttamente una risposta di Claude con una singola tool call.
- [ ] Il ciclo gestisce correttamente più tool call in sequenza nello stesso turno.
- [ ] Un tool con `mode: write` non viene mai eseguito senza flag di conferma esplicito.
- [ ] Parametri non conformi allo schema producono `SCHEMA_VALIDATION_FAILED` senza chiamare il bridge.
- [ ] Un tool sconosciuto richiesto da Claude produce `TOOL_NOT_FOUND` gestito, non un'eccezione non catturata.
- [ ] Il bridge irraggiungibile (`BRIDGE_UNAVAILABLE`) viene comunicato a Claude come risultato del tool, non come crash dell'app.

### Validazione schema (per ogni tool del Tool Contract)
- [ ] `find_blocks`: pattern vuoto → errore gestito; pattern valido → validazione passa.
- [ ] `get_block_attributes`: handle mancante nei parametri → errore di schema.
- [ ] `set_block_attribute`: chiamata senza `user_confirmed: true` → mai inoltrata al bridge.
- [ ] `run_lisp`: chiamata senza campo `reason` → errore di schema (il campo è obbligatorio per motivi di trasparenza UI).

### Client API Anthropic
- [ ] Gestione corretta di un errore HTTP (rate limit, chiave non valida) senza far cadere l'intera sessione di chat.
- [ ] Serializzazione corretta della cronologia conversazione su più turni.

### Motore geometrico (`ClaudeBridge.GeometryEngine`)
Testabile con dati sintetici (entità astratte, non richiede BricsCAD né disegni reali per i test unitari):
- [ ] Clustering: due gruppi di entità chiaramente separati nello spazio producono due cluster distinti.
- [ ] Clustering: entità di un singolo simbolo (vicine/connesse) producono un solo cluster, non frammentato.
- [ ] Firma geometrica: la stessa forma, traslata/ruotata/scalata, produce una firma con similarità alta rispetto all'originale (invarianza).
- [ ] Firma geometrica: due forme chiaramente diverse (es. un cerchio vs un rettangolo) producono una similarità bassa.
- [ ] `find_similar_geometry`: soglie di tolleranza applicate correttamente (match sopra soglia inclusi, sotto soglia esclusi).

---

## 2. Test manuali (scenari end-to-end su BricsCAD reale)

Da eseguire su un disegno di prova preparato ad hoc (vedi §3), non su disegni di produzione, finché il flusso non è consolidato.

### Scenario A — Controllo tag telecamere
1. Aprire il disegno di test con N telecamere, di cui alcune senza tag compilato.
2. In chat: "controlla se ho messo il tag a ogni telecamera".
3. Verificare che Claude chiami `find_blocks` con il pattern corretto.
4. Verificare che identifichi correttamente le telecamere senza tag (confrontare col numero atteso, noto a priori nel disegno di test).
5. Verificare che chiami `highlight_entities` sulle telecamere problematiche.
6. Verificare che la risposta in chat sia comprensibile e corretta (nessun falso positivo/negativo).

### Scenario B — Controllo cartiglio
1. Disegno di test con cartiglio a cui manca almeno un campo obbligatorio (es. `DATA` vuota).
2. In chat: "controlla se nel cartiglio ho inserito tutti i dati".
3. Verificare che Claude chiami `get_titleblock_data` e riporti esattamente i campi mancanti attesi.

### Scenario C — Azione con conferma
1. In chat: "sposta la telecamera con tag X di 2 metri a destra".
2. Verificare che appaia la card di conferma con l'azione descritta correttamente prima di qualunque modifica al disegno.
3. Annullare la conferma → verificare che il disegno non sia stato modificato.
4. Ripetere e confermare → verificare che la modifica sia effettivamente applicata correttamente.

### Scenario D — Nessun risultato / gestione errori
1. Chiedere di un blocco che non esiste nel disegno ("controlla i tag delle stampanti 3D").
2. Verificare che Claude comunichi chiaramente l'assenza di risultati, senza inventare dati.

### Scenario E — `run_lisp` come fallback
1. Chiedere un'azione non coperta da tool dedicati (da definire un esempio concreto una volta chiari i casi d'uso).
2. Verificare che Claude usi `run_lisp` solo dopo aver considerato i tool dedicati, mostrando `reason` comprensibile prima della conferma.

### Scenario F — Disegno con nomi di blocco mai visti (deduzione semantica)
1. Aprire un disegno di test con blocchi telecamera dal nome insolito/non ovvio (es. `IPCAM_01`, `DEV_SEC_CAM`), diverso da qualunque esempio già discusso in precedenza con Claude.
2. In chat: "controlla se ho messo il tag a ogni telecamera".
3. Verificare che Claude chiami `list_block_definitions` prima di `find_blocks` (non assuma un nome).
4. Se il nome è ragionevolmente chiaro, verificare che proceda direttamente dichiarando quale blocco ha usato (senza chiedere conferma inutile).
5. Preparare una seconda variante con un nome deliberatamente ambiguo (es. `CAM01` che potrebbe non essere una telecamera) e verificare che in quel caso Claude chieda conferma prima di procedere.
6. Preparare una terza variante senza alcun blocco riconducibile a "telecamera" e verificare che Claude comunichi l'assenza di corrispondenze senza inventare risultati.

---

### Scenario G — Fallback su testo grezzo e screenshot mirato
1. Aprire un disegno di test con un cartiglio compilato a **testo libero** (TEXT/MTEXT), non tramite attributi di blocco.
2. In chat: "controlla se nel cartiglio ho inserito tutti i dati".
3. Verificare che Claude, non trovando attributi utili, chiami `get_text_in_region` sull'area del cartiglio prima di ricorrere allo screenshot.
4. Preparare una seconda variante con un simbolo disegnato "a mano" (geometria semplice, non blocco) in un punto noto del disegno; chiedere a Claude di descriverlo o verificarne la presenza.
5. Verificare che Claude chiami `get_region_screenshot` solo su quella zona specifica (bounding box nota), non su tutto il disegno.
6. Verificare che, dopo l'interpretazione visiva, qualunque azione (es. evidenziazione) usi comunque l'handle noto e non una coordinata dedotta dai pixel.

---

### Scenario H — "Insegna per esempio" end-to-end
1. Aprire un disegno reale con blocchi/geometria nominati in modo casuale (es. `mncfasoijds`) o simboli non a blocco, con più istanze note dello stesso simbolo (es. 5 telecamere disegnate come semplice geometria).
2. In chat: "controlla se ho messo il tag a ogni telecamera" (o richiesta equivalente).
3. Verificare che Claude, non trovando nulla di utile al Livello 1, chieda di indicare un esempio.
4. Selezionare in BricsCAD un'istanza nota del simbolo.
5. Verificare che Claude chiami `get_geometry_signature` sulla selezione e poi `find_similar_geometry` sul disegno.
6. Confrontare i match trovati con il numero atteso (noto a priori nel disegno di test) — verificare falsi positivi/negativi.
7. Verificare che Claude dichiari il punteggio di similarità dei match, non li presenti come certezza assoluta.

### Scenario I — Match a confidenza media/bassa
1. Nello stesso disegno, includere una variante del simbolo leggermente diversa (dimensione diversa, orientamento diverso, o simbolo simile ma semanticamente diverso).
2. Ripetere lo Scenario H e verificare che Claude tratti questo caso diversamente: conferma visiva (`get_region_screenshot`) per punteggi intermedi, esclusione/segnalazione esplicita per punteggi bassi — non incluso silenziosamente tra i match certi.

### Scenario J — Libreria simboli personale (riuso tra sessioni)
1. Dopo lo Scenario H, salvare la firma come categoria personale (`save_signature_as_category`, scope `personal_library`).
2. Chiudere e riaprire una nuova sessione, su un disegno **diverso** (di un cliente diverso, se disponibile).
3. Ripetere una richiesta simile ("controlla le telecamere") e verificare che Claude proponga la firma salvata come ipotesi di partenza invece di richiedere subito un nuovo esempio — ma la tratti comunque come ipotesi da verificare, non come certezza, specialmente se il punteggio di similarità sul nuovo disegno è basso.

### Scenario K — Taratura del clustering spaziale
1. Preparare un disegno con un simbolo composto da più entità molto vicine a un elemento strutturale (es. una telecamera disegnata a ridosso di una parete o di una linea di quotatura).
2. Verificare che `list_entity_clusters` non unisca erroneamente il simbolo con l'elemento strutturale in un unico cluster (o, se lo fa, annotarlo come problema di taratura delle soglie da correggere prima di procedere oltre la Fase 0).

---

### Scenario L — Persistenza tag XDATA tra sessioni sullo stesso disegno
1. Eseguire lo Scenario H su un disegno di test, poi salvare il riconoscimento con `tag_entities_as_category`.
2. Chiudere del tutto BricsCAD e la sessione di chat.
3. Riaprire lo stesso file e avviare una nuova conversazione: "controlla se ho messo il tag a ogni telecamera".
4. Verificare che Claude chiami `find_tagged_entities` per primo e trovi immediatamente le entità già taggate, senza rifare clustering/matching geometrico.
5. Verificare che il tag sopravviva a un salvataggio/riapertura standard del file DWG (non solo a livello di sessione applicativa in memoria).

### Scenario M — Execution policy su `run_lisp`
1. In chat, chiedere esplicitamente (o indirettamente, tramite una richiesta che potrebbe indurre Claude a proporre codice pericoloso) un'azione che richiederebbe accesso al filesystem o l'esecuzione di comandi di sistema.
2. Verificare che l'espressione venga rifiutata con `BLOCKED_BY_EXECUTION_POLICY` **prima** di arrivare alla card di conferma UI, non dopo.
3. Verificare che un'espressione LISP legittima (es. manipolazione di un'entità) non venga invece bloccata dalla policy.

---

## 3. Disegno/i di test

Preparare almeno un disegno DWG dedicato ai test, con:
- un set noto di blocchi telecamera, alcuni con tag compilato e alcuni no (numero esatto annotato, es. "40 telecamere, 3 senza tag: handle noti");
- un cartiglio con almeno un campo obbligatorio mancante;
- una o più varianti di naming inconsuete/ambigue per testare lo Scenario F (deduzione semantica su nomi mai visti);
- una variante di cartiglio a testo libero (non attributi) e un simbolo disegnato "a mano" per lo Scenario G;
- più istanze note dello stesso simbolo disegnate senza nome di blocco utile, per gli Scenari H-K (numero esatto annotato a priori);
- almeno una variante "quasi simile ma diversa" dello stesso simbolo, per lo Scenario I;
- un simbolo posizionato molto vicino a un elemento strutturale (parete, quotatura), per lo Scenario K.

Tenere questo disegno versionato separatamente (non nel repo Git se troppo pesante, ma referenziato qui con percorso/posizione condivisa).

---

## 4. Checklist pre-rilascio (ogni fase)

- [ ] Tutti i test xUnit passano.
- [ ] Scenari manuali A-D eseguiti senza errori bloccanti.
- [ ] Nessuna chiave API o dato sensibile presente nei log committati o nel repository.
- [ ] Verificato comportamento con conferma UI su almeno un tool `write`.
