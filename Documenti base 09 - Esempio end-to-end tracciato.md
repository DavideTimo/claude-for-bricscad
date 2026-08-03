# Esempio End-to-End Tracciato

Traccia completa di una conversazione reale, con la sequenza esatta di chiamate tool e risposte JSON, per rendere concreto quanto descritto in prosa nei documenti 03 e 04. Corrisponde allo Scenario H del Piano di Test, con l'aggiunta del controllo Livello 0 e del salvataggio finale.

Contesto: un disegno di un cliente terzo, mai visto prima, con 5 telecamere disegnate come semplice geometria (non blocchi), nomi di layer/blocco non informativi.

---

## Turno 1

**Utente:** "controlla se ho messo il tag a ogni telecamera"

**Claude → tool call 1**
```json
{ "tool": "find_tagged_entities", "input": { "label": "telecamera" } }
```
**Risultato**
```json
{ "tagged_entities": [] }
```
*(Livello 0: nessun tag persistente trovato — è la prima volta che questo disegno viene analizzato.)*

**Claude → tool call 2**
```json
{ "tool": "list_block_definitions", "input": { "space": "any" } }
```
**Risultato**
```json
{
  "block_definitions": [
    { "name": "mncfasoijds", "instance_count": 12, "attribute_tags": [], "sample_layer": "0" },
    { "name": "A$C4E2F1B0", "instance_count": 3, "attribute_tags": [], "sample_layer": "IMPIANTI" }
  ]
}
```
*(Livello 1: nessun nome plausibile — entrambi sono nomi generati automaticamente, non riconducibili a "telecamera". Le telecamere in questo disegno, come da contesto, non sono nemmeno blocchi.)*

**Claude → tool call 3**
```json
{ "tool": "list_learned_categories", "input": {} }
```
**Risultato**
```json
{ "categories": [] }
```
*(Nessuna firma salvata in precedenza, né in questa sessione né in libreria personale.)*

**Claude → risposta all'utente (nessuna azione sul disegno, solo testo):**
> "Non trovo blocchi con un nome riconducibile a 'telecamera', e i blocchi presenti (`mncfasoijds`, `A$C4E2F1B0`) hanno nomi generati automaticamente. Puoi indicarmi un esempio di telecamera selezionandolo direttamente nel disegno?"

---

## Turno 2

**Utente:** *(seleziona un'istanza nel disegno tramite l'interfaccia BricsCAD — la UI cattura la selezione)*
"eccola" *(il messaggio include gli handle selezionati, passati dalla UI, es. `["2A4F", "2A50", "2A51"]`)*

**Claude → tool call 4**
```json
{ "tool": "get_geometry_signature", "input": { "handles": ["2A4F", "2A50", "2A51"] } }
```
**Risultato**
```json
{
  "signature": {
    "primitive_counts": { "LINE": 4, "ARC": 1, "CIRCLE": 1, "POLYLINE": 0 },
    "relative_geometry": "<descrittore interno opaco>",
    "bounding_box_aspect_ratio": 1.15
  },
  "source_handles": ["2A4F", "2A50", "2A51"]
}
```

**Claude → tool call 5**
```json
{
  "tool": "find_similar_geometry",
  "input": { "signature": "<oggetto signature sopra>", "tolerance": 0.85, "space": "any" }
}
```
**Risultato**
```json
{
  "matches": [
    { "handles": ["2A4F", "2A50", "2A51"], "bounding_box": {"min": {"x": 10.2, "y": 5.1}, "max": {"x": 11.4, "y": 6.3}}, "similarity_score": 1.0 },
    { "handles": ["3B10", "3B11", "3B12"], "bounding_box": {"min": {"x": 25.0, "y": 8.0}, "max": {"x": 26.2, "y": 9.2}}, "similarity_score": 0.94 },
    { "handles": ["4C20", "4C21", "4C22"], "bounding_box": {"min": {"x": 40.5, "y": 12.0}, "max": {"x": 41.7, "y": 13.2}}, "similarity_score": 0.91 },
    { "handles": ["5D30", "5D31"], "bounding_box": {"min": {"x": 55.0, "y": 20.0}, "max": {"x": 55.9, "y": 20.8}}, "similarity_score": 0.68 },
    { "handles": ["6E40", "6E41", "6E42", "6E43"], "bounding_box": {"min": {"x": 70.0, "y": 30.0}, "max": {"x": 72.0, "y": 31.5}}, "similarity_score": 0.35 }
  ]
}
```

*(Politica di confidenza, vedi documento 08: i primi tre — incluso l'esempio originale — sono >0.90, alta confidenza. Il quarto è 0.68, confidenza media: giustifica una conferma visiva. Il quinto è 0.35, sotto soglia: va escluso.)*

**Claude → tool call 6** *(conferma visiva sul match a confidenza media)*
```json
{
  "tool": "get_region_screenshot",
  "input": { "bounding_box": {"min": {"x": 55.0, "y": 20.0}, "max": {"x": 55.9, "y": 20.8}}, "zoom_margin_factor": 1.3 }
}
```
**Risultato:** *(immagine base64, non riportata qui — Claude la osserva e determina che si tratta effettivamente di una telecamera, disegnata in modo leggermente diverso dalle altre)*

**Claude → risposta all'utente:**
> "Ho trovato 4 telecamere nel disegno con forma simile all'esempio che mi hai indicato: 3 con corrispondenza molto alta, e una quarta (in basso a destra, verso le coordinate 55,20) disegnata in modo leggermente diverso — ho verificato visivamente ed è comunque una telecamera. Ho scartato un quinto elemento con forma solo vagamente simile, che non sembra esserlo. Vuoi che le evidenzi tutte nel disegno?"

---

## Turno 3

**Utente:** "sì, evidenziale, e salva che hai capito cosa sono le telecamere in questo disegno"

**Claude → tool call 7**
```json
{
  "tool": "highlight_entities",
  "input": { "handles": ["2A4F","2A50","2A51","3B10","3B11","3B12","4C20","4C21","4C22","5D30","5D31"], "color": "yellow", "clear_previous": true }
}
```
**Risultato**
```json
{ "highlighted_count": 11, "not_found_handles": [] }
```

**Claude → card di conferma in UI** *(tool `write`, serve conferma esplicita)*
```json
{
  "tool": "tag_entities_as_category",
  "input": { "handles": ["2A4F","2A50","2A51","3B10","3B11","3B12","4C20","4C21","4C22","5D30","5D31"], "label": "telecamera" },
  "confirmation_required": true,
  "proposed_action_description": "Salvare un tag persistente 'telecamera' su queste 4 telecamere in questo disegno, così da riconoscerle immediatamente se riapri questo file in futuro."
}
```

**Utente conferma in UI → esecuzione effettiva**
**Risultato**
```json
{ "tagged_count": 11, "label": "telecamera" }
```

**Claude → risposta finale:**
> "Fatto — le 4 telecamere sono evidenziate in giallo nel disegno, e ho salvato il riconoscimento in questo file: se lo riapri in futuro le troverò immediatamente senza dover rifare l'analisi. Vuoi che salvi anche questa forma nella tua libreria personale, per riconoscere telecamere simili anche su altri disegni? Attenzione: essendo disegni di clienti diversi, potrebbe non generalizzare perfettamente."

---

## Cosa illustra questa traccia

- **La cascata a 4 livelli applicata realmente**: Livello 0 e 1 falliscono rapidamente (2 chiamate economiche), prima di passare al matching geometrico più costoso.
- **La politica di confidenza in azione concreta**: tre fasce (alta/media/bassa) con comportamento diverso per ciascuna, non un'unica soglia binaria.
- **La conferma visiva usata correttamente**: solo su un'area già nota (il match a confidenza media), mai come ricerca generica.
- **La trasparenza richiesta dal system prompt**: Claude dichiara sempre cosa ha controllato e perché, non presenta i risultati come dati di fatto senza contesto.
- **La doppia via di persistenza**: tag nel disegno corrente (immediato, sempre proposto) vs. libreria personale tra disegni (esplicitamente presentato come ipotesi più debole, dato che i disegni sono di clienti diversi).
- **Il flusso di conferma per i tool `write`**: `tag_entities_as_category` passa dalla UI prima di essere eseguito, `highlight_entities` no (trattato come non distruttivo, coerente con quanto lasciato aperto nel documento 02, §4).

Questa traccia va usata come riferimento per i test manuali (Scenario H) e come specifica di comportamento per chi implementa l'orchestratore: il ciclo di tool-use reale dovrebbe produrre una sequenza equivalente a questa, a meno di differenze nei dati reali del disegno di test.
