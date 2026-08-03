# bricscad-claude-bridge

Plugin .NET per BricsCAD che integra Claude in un pannello di chat: interroga il disegno (blocchi, attributi, layer, e — quando nomi e attributi non bastano — simboli riconosciuti per similarità geometrica), riceve consigli e può eseguire comandi CAD su richiesta, con conferma dell'utente.

## Cosa fa

- Chat integrata in BricsCAD per interrogare il disegno in linguaggio naturale (es. "controlla se ogni telecamera ha il tag", "quali campi del cartiglio sono vuoti").
- Esecuzione di comandi CAD su richiesta esplicita, con conferma prima di ogni modifica.
- Evidenziazione e zoom automatico sulle entità discusse in chat.

## Stato del progetto

🚧 In sviluppo — vedi la roadmap nel documento delle linee guida.

## Documentazione

| Documento | Contenuto |
|---|---|
| [`Documenti base 02 - Linee guida claude bricscad.md`](./Documenti%20base%2002%20-%20Linee%20guida%20claude%20bricscad.md) | Visione, architettura, UI, roadmap |
| [`Documenti base 03 - Tool contract claude bricscad.md`](./Documenti%20base%2003%20-%20Tool%20contract%20claude%20bricscad.md) | Schema esatto di ogni tool esposto a Claude |
| [`Documenti base 04 - Riconoscimento simboli in disegni eterogenei.md`](./Documenti%20base%2004%20-%20Riconoscimento%20simboli%20in%20disegni%20eterogenei.md) | I tre livelli di riconoscimento (nomi/attributi, matching geometrico, conferma visiva) per disegni senza standard condiviso |
| [`Documenti base 05 - Setup environment guide.md`](./Documenti%20base%2005%20-%20Setup%20environment%20guide.md) | Come configurare l'ambiente di sviluppo |
| [`Documenti base 06 - Piano di test.md`](./Documenti%20base%2006%20-%20Piano%20di%20test.md) | Checklist di test manuali e automatici |
| [`Documenti base 07 - Sicurezza privacy.md`](./Documenti%20base%2007%20-%20Sicurezza%20privacy.md) | Cosa viene inviato all'API, gestione chiavi, dati sensibili |
| [`Documenti base 08 - System prompt di riferimento.md`](./Documenti%20base%2008%20-%20System%20prompt%20di%20riferimento.md) | Bozza delle istruzioni operative reali passate a Claude dall'orchestratore |
| [`Documenti base 09 - Esempio end-to-end tracciato.md`](./Documenti%20base%2009%20-%20Esempio%20end-to-end%20tracciato.md) | Traccia completa di una conversazione reale con sequenza esatta di tool call e payload |

## Requisiti rapidi

- BricsCAD V24+ (Pro se si usano API verticali)
- Visual Studio 2022
- .NET 10 SDK
- Chiave API Anthropic (configurata come variabile d'ambiente, mai nel codice)

Per il setup completo vedi [`setup_environment_guide.md`](./setup_environment_guide.md).

## Struttura del repository

```
ClaudeForBricsCAD.sln
├─ ClaudeBridge.Core/          libreria agnostica da BricsCAD (orchestratore, tool schema, client API)
├─ ClaudeBridge.BricscadPlugin/ plugin .NET specifico BricsCAD (implementazione tool)
├─ ClaudeBridge.GeometryEngine/ clustering spaziale, firme geometriche, matching per similarità
├─ ClaudeBridge.UI/             pannello di chat WPF
└─ ClaudeBridge.Tests/          test xUnit
```

## Licenza

*(da definire)*
