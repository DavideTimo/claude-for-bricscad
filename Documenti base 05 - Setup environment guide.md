# Setup & Environment Guide

Guida per configurare da zero l'ambiente di sviluppo del progetto.

---

## 1. Requisiti software

| Componente | Versione minima consigliata | Note |
|---|---|---|
| BricsCAD | V24 o superiore (Pro se si usano API verticali) | Verificare quale versione installata sulla propria macchina/azienda |
| Visual Studio | 2022, aggiornato a una versione recente (Community va bene) | Con workload ".NET desktop development"; verificare che la versione installata supporti il tooling .NET 10, altrimenti aggiornare Visual Studio |
| .NET SDK | .NET 10 | Per la libreria `ClaudeBridge.Core` e i test |
| .NET Framework | versione richiesta dall'API .NET di BricsCAD installata (verificare in `BricscadApp` / SDK BricsCAD) | Il plugin BricsCAD potrebbe richiedere .NET Framework classico invece di .NET moderno, a seconda della versione BricsCAD — **da verificare all'apertura della Fase 0**, perché condiziona il targeting del progetto plugin |
| Git | qualsiasi versione recente | |
| BricsCAD Developer Reference / SDK | corrispondente alla propria versione BricsCAD | Scaricabile dal portale sviluppatori Bricsys |

> ⚠️ Punto critico da chiarire subito in Fase 0: la libreria `ClaudeBridge.Core` (agnostica da BricsCAD) può restare su .NET 10, ma `ClaudeBridge.BricscadPlugin` deve targetizzare il framework richiesto dall'SDK BricsCAD installato, che potrebbe non coincidere con .NET 10 (le API .NET di BricsCAD potrebbero essere ancora ferme a .NET Framework classico o a una versione .NET precedente). Verificare nella documentazione sviluppatori della propria versione BricsCAD prima di impostare i progetti, e valutare se serva un layer di comunicazione (es. named pipe/IPC) tra un plugin su framework più datato e una libreria core su .NET 10.

> ⚠️ Secondo punto critico da verificare in Fase 0: individuare il meccanismo corretto per eseguire operazioni sul documento BricsCAD dal thread giusto (thread-marshaling). Le API single-document di applicazioni CAD/DCC tipicamente richiedono che le operazioni sul database del disegno avvengano sul thread principale dell'applicazione — l'implementazione di `IBricscadBridge` deve marshalling le chiamate di conseguenza (vedi Tool Contract, sezione "Thread-marshaling", per il razionale). Verificare nella documentazione BricsCAD quale API di sincronizzazione col thread principale è disponibile (es. equivalente di un `Application.Idle`/dispatcher) prima di scrivere il bridge.

---

## 2. Setup iniziale

1. Clonare il repository:
   ```bash
   git clone <url-repo>
   cd bricscad-claude-bridge
   ```
2. Aprire `ClaudeForBricsCAD.sln` in Visual Studio.
3. Ripristinare i pacchetti NuGet (Visual Studio lo fa automaticamente all'apertura, o `dotnet restore` da riga di comando per i progetti .NET 10).
4. Configurare i riferimenti all'SDK BricsCAD nel progetto `ClaudeBridge.BricscadPlugin` (percorso DLL di riferimento, tipicamente nella cartella di installazione di BricsCAD — da annotare qui una volta individuato: `<percorso da compilare>`).

---

## 3. Configurazione API key Anthropic

**Mai** salvare la chiave API nel codice sorgente o nel repository Git.

Opzioni consigliate, in ordine di preferenza per lo sviluppo locale:

1. **Variabile d'ambiente** (consigliata):
   ```bash
   setx ANTHROPIC_API_KEY "sk-ant-..."
   ```
   e lettura in codice tramite `Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")`.

2. **File di configurazione locale non versionato** (`appsettings.local.json`, aggiunto a `.gitignore`), per chi preferisce non toccare variabili d'ambiente di sistema.

Aggiungere sempre al `.gitignore`:
```
appsettings.local.json
*.local.json
.env
```

---

## 4. Debug del plugin in BricsCAD

1. Nel progetto `ClaudeBridge.BricscadPlugin`, impostare come "avvio esterno" (external program) l'eseguibile di BricsCAD (`bricscad.exe`), nelle proprietà di debug del progetto.
2. Impostare un breakpoint nel codice del plugin.
3. Avviare il debug (F5): Visual Studio lancia BricsCAD e si aggancia al processo.
4. In BricsCAD, caricare il plugin manualmente la prima volta con `NETLOAD` (comando standard per plugin .NET stile AutoCAD/BricsCAD), puntando alla DLL compilata in `bin/Debug`.
5. Da quel momento i comandi registrati dal plugin sono disponibili nella command line di BricsCAD; i breakpoint in Visual Studio si attivano normalmente.

---

## 5. Esecuzione dei test

I test dell'orchestratore (`ClaudeBridge.Tests`) **non richiedono BricsCAD aperto**, perché usano un mock di `IBricscadBridge`:

```bash
cd ClaudeBridge.Tests
dotnet test
```

I test "manuali" che richiedono BricsCAD reale seguono invece il documento **Piano di Test** (checklist scenari), non xUnit.

---

## 6. Struttura repository (riferimento rapido)

Vedi documento principale "Linee guida di progetto", §3, per la struttura completa delle cartelle.

---

## 7. Checklist di verifica ambiente pronto

- [ ] BricsCAD si avvia correttamente in debug da Visual Studio.
- [ ] `NETLOAD` carica il plugin senza errori.
- [ ] Variabile d'ambiente API key impostata e leggibile dal codice.
- [ ] `dotnet test` su `ClaudeBridge.Tests` passa senza BricsCAD aperto.
- [ ] Verificato il targeting framework corretto per `ClaudeBridge.BricscadPlugin` (vedi punto critico §1).
- [ ] Individuato e testato il meccanismo di thread-marshaling verso il thread principale di BricsCAD (vedi secondo punto critico §1).
