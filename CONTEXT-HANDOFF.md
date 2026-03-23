# CONTEXT-HANDOFF — RH_caminterface / RhinoCNCExporter

Dieses Dokument dient dem schnellen Einstieg bei Sitzungswechsel oder Übergabe an einen neuen Agenten/Entwickler.

---

## Was ist das Projekt?

Ein **Rhino 8 C#-Plugin** (Yak Package), das aus 2D-Geometrien + Layer-Konventionen CNC-Fräsprogramme generiert für:
- **SCM** (Maestro/CAD+T) → `.xcs` (Xilog-Format)
- **Biesse** (bSolid/BiesseWorks) → `.cix` (BEGIN/END Blöcke) oder `.bpp` (INI-Style)
- **Homag** (woodWOP) → `.mpr` (ASCII-Sektionen) oder `.mprx` (XML)

Einsatzgebiet: Holzbearbeitung / Möbelindustrie — Platten fräsen, bohren, Nuten schneiden.

## Aktueller Stand (zuletzt aktualisiert: 2026-03-23, Sprint 2 Complete)

### Deep Research + 55-XCS-Analyse abgeschlossen
- **`docs/RESEARCH-CAM-FORMATS.md`** — 33KB umfassendes Research-Dokument zu:
- **`docs/XCS-REFERENCE-ANALYSIS.md`** — Vollständige Analyse von 55 Produktions-XCS-Dateien:
  - 36 bestehende + 19 neue Dateien (März 2026)
  - Neue MSL-Befehle: CreateBladeCut, CreateSectioningMillingStrategy, CreateSegment, CreateHelicMillingStrategy
  - CLAMEX SawCut_Lamello-Makros mit ~48 Parametern
  - Production-Quality Header/Footer-Format analysiert
  - Detaillierte Befehlshäufigkeiten und Feature-Gap-Analyse
- **`docs/CLAMEX-CONCEPT.md`** — Vollständiges Konzept für 3D-Block-basierten CLAMEX-Workflow:
  - Block-Detection statt Layer-Konventionen (Adis Vision!)
  - 3D-to-CNC Pipeline Vision: Aus 3D-Korpus pro Platte CNC-Programme ableiten
  - CAD+T-ähnlicher Workflow: 3D zeichnen → CNC automatisch ableiten
  - Implementation Roadmap: Phase 1-3 (Blocks) → Phase 9+ (vollständige 3D-Pipeline)
  - SCM XCS/MSL-Format: Vollständige Spezifikation, Beispiele aus Python-Referenz
  - Biesse CIX/BPP-Format: Detaillierte Spezifikation, BppLib (C# NuGet!) analysiert
  - Homag MPR-Format: Offizielle Formatbeschreibung (75 Seiten) ausgewertet
  - CAM-Software-Vergleich: woodWOP, bSolid, Maestro, RhinoCAM, Mastercam
  - Open-Source Libraries: BppLib als direkt nutzbare NuGet-Dependency identifiziert
  - Praxis-Workflows: Typische Operationen, Werkzeuge, Nesting
- **Wichtigste Erkenntnis:** BppLib (NuGet) kann direkt für den Biesse-Emitter genutzt werden
- **Marktlücke bestätigt:** Kein existierendes Rhino-Plugin erzeugt CIX/MPR/XCS

### Was existiert und funktioniert
- **Python-Referenz** (`RH_caminterface_v007.py`): Vollständig funktional, kann .xcs-Dateien erzeugen
- **Phase 1 (SCM/XCS)** — KOMPLETT:
  - LayerParser (Regex + DTOs): implementiert ✅
  - NameService: implementiert mit Tests ✅
  - XilogEmitter: Vollständig implementiert ✅
  - Alle Operationen (CUT, POCKET, DRILL, DRILLROW, RBNUT_CH, RBNUT_RNT) ✅
  - Unit Tests vorhanden und grün ✅
  - GeometryUtils mit Polyline-Sampling, Offsets, Groove-Konstruktion ✅
  - ExportService End-to-End funktional ✅
  - UI: Settings-Panel + Export-Dialog als Grundgerüst
- **Phase 2 (IEmitter Interface + Biesse)** — KOMPLETT:
  - IEmitter Interface für Multi-Maschinen-Support ✅
  - IMachineProfile Interface für maschinenspezifische Konfiguration ✅
  - XilogEmitter refactored to implement IEmitter ✅
  - BiesseProfile mit Biesse-spezifischen Defaults ✅
  - BiesseEmitter mit CIX-Format Grundstruktur ✅
  - Header (MAINDATA), Drill (BG), Cut (ROUTG+GEO) implementiert ✅
  - E2E Tests gegen Referenz-XCS-Dateien ✅
  - ExportService unterstützt beide Formate ✅
- **Yak-Vorbereitung**: manifest.yml erstellt, .csproj für Rhino 8 netcore konfiguriert

### Phase 2.5 — Production-Quality XCS (KOMPLETT ✅, 23.03.2026)
Based on analysis of 36 real production XCS files:
- Production header/footer format (comment blocks, compact numbers) ✅
- CreatePattern() for drill grid arrays (122× in production) ✅
- AddArc2PointCenterToPolyline() for arc segments ✅
- CreateWorkplane() for horizontal drilling ✅
- Configurable setup offsets (Zugabe X/Y) via IMachineProfile + UI ✅
- New layer patterns: DRILLPAT, HDRILL ✅
- New emit classes: EmitDrillPattern, EmitHorizontalDrill ✅
- All 80+ tests green ✅

### Sprint 1 — Core Data Models + Pipeline Skeleton (KOMPLETT ✅, 23.03.2026)
Foundation for 3D-to-CNC block-based pipeline:
- **Core/Models/**: Plate, Machining (8 subtypes), FittingBlock, ExportJob, PlateOrigin, Enums ✅
- **Core/Blocks/**: BlockUserTextSchema (validation + constants), CncUserTextParser, MachiningFactory ✅
- **Core/Pipeline/**: IMachiningBuilder, IEmitterRouter, IPlateExporter interfaces ✅
- **Core/Pipeline/**: MachiningBuilder (merge + deduplicate), EmitterRouter (bridge to IEmitter) ✅
- MachiningFactory dispatch: DRILL, DRILLPATTERN, MACRO, HDRILL implemented; CUT/POCKET/GROOVE stubs ✅
- Template expansion: {DZ}, {X}, {Y} placeholders with arithmetic ({DZ}-9.5 etc.) ✅
- 90+ new unit tests covering models, schema validation, factory, parser, pipeline ✅
- All 95+ total tests green, 0 warnings ✅

**Sprint 1 Dateien:**
```
RhinoCNCExporter.Core/
├── Models/
│   ├── Enums.cs           (MachiningType, MachiningSide, MachineFormat, etc.)
│   ├── Plate.cs           (Plate record with dimensions, origin, machinings)
│   ├── PlateOrigin.cs     (Coordinate system for plate in world space)
│   ├── Machining.cs       (Base + 8 subtypes: Drill, DrillPattern, Routing, etc.)
│   ├── FittingBlock.cs    (Parsed block with CNC_* attributes)
│   └── ExportJob.cs       (Export orchestration record)
├── Blocks/
│   ├── BlockUserTextSchema.cs  (CNC_* key constants, validation)
│   ├── CncUserTextParser.cs    (UserText dict → FittingBlock)
│   └── MachiningFactory.cs     (FittingBlock → Machining objects)
└── Pipeline/
    ├── IMachiningBuilder.cs    (Interface)
    ├── IEmitterRouter.cs       (Interface)
    ├── IPlateExporter.cs       (Interface)
    ├── MachiningBuilder.cs     (Merge legacy + block machinings)
    └── EmitterRouter.cs        (Route Machining → IEmitter calls)

RhinoCNCExporter.Tests/
├── ModelTests.cs               (25 tests)
├── BlockUserTextSchemaTests.cs (20 tests)
├── CncUserTextParserTests.cs   (10 tests)
├── MachiningFactoryTests.cs    (20 tests)
└── PipelineTests.cs            (15 tests)
```

### Sprint 2 — Block Scanning + Starter Blocks (KOMPLETT ✅, 23.03.2026)
Block detection pipeline, starter blocks, assignment resolver, UI integration:
- **StarterBlockDefinitions**: 5 starter blocks as code-defined CNC_* dictionaries ✅
- **BlockScanner**: Scans RhinoDoc for block inserts with CNC_* UserText ✅
- **AssignmentResolver**: Layer-based block-to-plate assignment ✅
- **BlockAwareExportService**: Bridge to ExportService with feature flag + fallback ✅
- **ExportPanel UI**: Block detection checkbox, blocks list, scan button ✅
- 36 new tests, all passing. Total: 183 tests green, 0 regressions ✅

**Sprint 2 Dateien:**
```
RhinoCNCExporter.Core/
└── Blocks/
    └── StarterBlocks/
        └── StarterBlockDefinitions.cs  (5 starter block definitions)

RhinoCNCExporter/
├── BlockScanning/
│   ├── BlockScanner.cs           (RhinoDoc → List<FittingBlock>)
│   └── AssignmentResolver.cs     (Layer-based block-to-plate assignment)
├── Services/
│   └── BlockAwareExportService.cs (Block-aware export with feature flag)
└── UI/
    └── ExportPanel.cs            (MODIFIED: block detection UI added)

RhinoCNCExporter.Tests/
├── StarterBlockDefinitionsTests.cs (19 tests: schema, parse, factory, emitter)
├── AssignmentResolverTests.cs      (7 tests: grouping, matching, edge cases)
└── BlockIntegrationTests.cs        (10 tests: full pipeline integration)
```

### Was fehlt / nächste Schritte (Sprint 3+)
1. **CLAMEX-System** — 3D-Block-basierter Workflow für Lamello-Verbinder:
   - Block-Detection: CLAMEX-Blöcke in 3D-Modell erkennen
   - SawCut_Lamello-Makro: ~48-Parameter Makro für CLAMEX-Verbinder
   - 3D-Pipeline Vision: Aus 3D-Modell pro Platte CNC-Programme ableiten
   - **Referenz:** docs/CLAMEX-CONCEPT.md (vollständiges Konzept)

2. **Neue MSL-Befehle** (aus 55-XCS-Analyse):
   - CreateBladeCut: Geneigte Schnitte/Fasen (36 Vorkommen)
   - CreateSectioningMillingStrategy + CreateSegment: Schneidstrategien (68 Vorkommen)
   - CreateHelicMillingStrategy: Spiralbearbeitung für Ausschnitte
   - Erweiterte SetMachiningParameters: "EF", "IL", "EH" zusätzlich zu "IJ"

3. **GeometryUtils Arc Detection** — `ToPolySegments()` für RhinoCommon ArcCurve-Erkennung
4. **BppLib Integration** — BppLib NuGet Package als Biesse-Abhängigkeit
5. **Homag-Emitter** (.mpr) — Noch nicht begonnen, aber Research vorhanden
6. **UI Improvements** — Maschinenformat-Auswahl, Profile-Konfiguration
7. **Yak Package Build** — Finaler Package-Build und Test-Installation

## Schlüsseldateien

| Datei | Zweck |
|-------|-------|
| `RH_caminterface_v007.py` | Python-Referenz — Quelle der Wahrheit für XCS-Format |
| `maestro_editor_text.txt` | Durchsuchbarer Maestro-Handbuch-Text (Page-Marker) |
| `docs/RESEARCH-CAM-FORMATS.md` | Umfassendes Research zu SCM, Biesse, Homag Formaten |
| `manifest.yml` | Yak Package Manifest für Rhino Package Manager |
| `RhinoCNCExporter/RhinoCNCExporter.csproj` | Plugin-Projekt (net7.0-windows, Rhino 8) |
| `RhinoCNCExporter/Core/LayerParser/LayerRegex.cs` | Alle Regex-Patterns + Parsing |
| `RhinoCNCExporter/Core/LayerParser/Specs.cs` | DTOs (CutSpec, PocketSpec, DrillSpec, ...) |
| `RhinoCNCExporter/Core/Emitters/IEmitter.cs` | Interface für alle CNC-Format-Emitter |
| `RhinoCNCExporter/Core/Emitters/XilogEmitter.cs` | SCM XCS-Ausgabe (vollständig) |
| `RhinoCNCExporter/Core/Emitters/BiesseEmitter.cs` | Biesse CIX-Ausgabe (Grundoperationen) |
| `RhinoCNCExporter/Core/Emitters/Emit*.cs` | Operationen-Emitter (CUT, POCKET, DRILL, ROW, GrooveCH, GrooveRNT, DrillPattern, HorizontalDrill) |
| `docs/XCS-REFERENCE-ANALYSIS.md` | Vollständige Analyse von 55 Produktions-XCS-Dateien |
| `docs/CLAMEX-CONCEPT.md` | 3D-Block-basiertes CLAMEX-Konzept + 3D-Pipeline Vision |
| `tests/references/NEW_*.xcs` | 19 neue Produktions-XCS-Dateien (Schubladen, Revisionstüren, etc.) |
| `RhinoCNCExporter/Core/Profiles/IMachineProfile.cs` | Interface für Maschinenprofile |
| `RhinoCNCExporter/Core/Profiles/MachineProfile.cs` | Maschinenprofil-Basisklasse |
| `RhinoCNCExporter/Core/Profiles/BiesseProfile.cs` | Biesse-spezifische Konfiguration |
| `RhinoCNCExporter/Services/ExportService.cs` | Multi-Format Export-Orchestrierung |
| `tests/test_01.xcs`, `test_02.xcs` | XCS-Referenz-Ausgaben der Python-Implementierung |
| `tests/test_biesse_01.cix` | CIX-Referenz für Biesse-Format |
| `RhinoCNCExporter.Tests/EmitterTests.cs` | Unit Tests für alle Emitter |
| `RhinoCNCExporter.Tests/E2ETests.cs` | End-to-End Tests gegen Referenz-Dateien |

## Architektur-Entscheidungen

### Warum C# und nicht Python?
- Rhino-Plugins müssen als .rhp (kompilierte DLL) vorliegen für produktiven Einsatz
- RhinoPython-Scripts haben keinen Zugang zu Eto.Forms UI, Plugin-Settings, Yak-Packaging
- Performance und Typsicherheit für produktive Nutzung

### Warum Yak Package?
- Offizieller Rhino Package Manager — einfache Installation für Endbenutzer
- Automatische Updates via `_PackageManager` in Rhino
- Build: `yak build --platform win` → `rhinocncexporter-0.1.0-rh8_0-win.yak`
- Publish: `yak push <package>.yak`

### Multi-Maschinen-Strategie
```
Rhino-Geometrie → LayerParser → Specs (maschinenunabhängig)
                                  ↓
                    ExportService + MachineProfile
                                  ↓
                    ┌─────────────┼─────────────┐
                    ↓             ↓             ↓
              XilogEmitter  BiesseEmitter  HomagEmitter
                (.xcs)     (.cix/.bpp)    (.mpr/.mprx)
```

## Maschinenformat-Übersicht

### SCM — Xilog/Maestro (.xcs)
- Text-Format, Zeilenbasiert
- Maestro-Handbuch als Referenz: `maestro_editor_text.txt`
- Python-Referenz implementiert vollständig

### Biesse — CIX (.cix)
- Text-Format mit `BEGIN MACRO ... END MACRO` Blöcken
- Werkstück: `BEGIN MAINDATA` → LPX, LPY, LPZ
- Bohren: `NAME=BG` (X, Y, Dp, Dia, Thr)
- Fräsen: `NAME=ROUTG` + `NAME=GEO` (Geometrie separat definiert)
- Tasche: `NAME=POCK` (GID, Dia, Dp, TYP)
- Nut: `NAME=CUT_G` (X, Y, Xe, Ye, Dp)
- Geometrie: START_POINT, LINE_EP, ARC_EPCE, ENDPATH
- Seiten: 0=top, 1=bottom, 2=left, 3=right, 4=front, 5=back
- Open-Source Referenz: [BppLib](https://github.com/viachpaliy/BppLib) (C#)

### Homag — woodWOP MPR (.mpr)
- ASCII-Text, 5 feste Blöcke: `[H`, `[001`, `[K`, `]n`, `<ID \Name\`
- Werkstück: `<100 \WerkStck\` → LA (Länge), BR (Breite), DI (Dicke)
- Variablen: L, W, T mit Formeln (SIN, COS, IF/THEN/ELSE)
- Bohren: `<102 \BohrVert\` (XA, YA, DU, TI, AN)
- Kontur: `<105 \Konturfraesen\` + Kontur `]n` (KP, KL, KA)
- Nut: `<109 \Nuten\` (XA, YA, LA, TI, NB)
- Tasche: `<112 \Tasche\` (XA, YA, LA, BR, TI)
- Offizielle Spec: "woodWOP Formatbeschreibung" Dok-Nr. 9-080-42-7190
- Open-Source Referenz: [prgToMPR](https://github.com/mustafayildizmuh/prgToMPR) (C#)

## Build-Umgebung

```bash
# Build
dotnet build RhinoCNCExporter/RhinoCNCExporter.csproj -c Release

# Tests
dotnet test RhinoCNCExporter.Tests/RhinoCNCExporter.Tests.csproj

# Output
# RhinoCNCExporter/bin/Release/net7.0-windows/RhinoCNCExporter.rhp

# In Rhino laden
# _-PlugIn _Install "C:\Users\Adi.Muff\repos\RH_caminterface\RhinoCNCExporter\bin\Release\net7.0-windows\RhinoCNCExporter.rhp"
```

### Rhino 8 SDK DLL-Pfade (auf diesem Rechner verifiziert)
- `C:\Program Files\Rhino 8\System\netcore\RhinoCommon.dll` — Core API
- `C:\Program Files\Rhino 8\System\netcore\Rhino.UI.dll` — UI/Panels
- `C:\Program Files\Rhino 8\System\Eto.dll` — Eto.Forms (**nicht** in netcore/, sondern im Hauptverzeichnis!)

### Company / Branding
- Organization: **Solid-ai.ai** (in AssemblyInfo.cs)
- Plugin GUID: `2e8c8a7c-1bcb-4b0d-8a56-4b2b6f0d7f6e`

## Bekannte Fallen / Gotchas

- **Eto.dll Pfad**: Liegt in `System\`, NICHT in `System\netcore\` — häufige Build-Fehlerquelle
- **Maximale Namenslänge**: 31 Zeichen (Maestro-Limit) — NameService kürzt automatisch
- **RNT-Makro-Signatur**: Muss exakt dem Maestro-Format entsprechen (siehe Python-Referenz)
- **CIX ist kein XML**: Trotz "X" im Namen — es sind BEGIN/END Textblöcke
- **MPR Konturen**: Werden als separate `]n` Blöcke definiert und von Operationen referenziert
- **Biesse Seiten vs Homag**: Biesse nutzt SIDE=0-5, Homag nutzt Koordinatensysteme (KO)
- **System.Drawing.Common**: Wird als NuGet-Paket (v7.0.0) benötigt wegen `Icon`-Typ in `Panels.RegisterPanel`
- **Yak Package**: manifest.yml muss im Root des dist-Ordners liegen, `.rhp` daneben
- **Workplane**: Immer "Top", Eingaben immer in mm
- **.gitignore**: Ist vorhanden — `bin/`, `obj/`, `*.rhp`, `*.yak` werden ignoriert

## Wie weiterarbeiten

### ✅ Phase 1 — SCM/Maestro Emitter (KOMPLETT)
1. Python-Referenz analysiert und portiert ✅
2. Emit*.cs Stubs mit echtem XCS-Code implementiert ✅
3. GeometryUtils implementiert (Polyline-Sampling, Offsets, Groove-Konstruktion) ✅
4. Tests gegen Referenz-Ausgaben (`tests/test_01.xcs`, `test_02.xcs`) ✅
5. ExportService End-to-End funktional ✅

### ✅ Phase 2 — Multi-Maschinen-Abstraktion (KOMPLETT)
6. IEmitter-Interface extrahiert ✅
7. XilogEmitter refactored to IEmitter ✅
8. IMachineProfile-Interface implementiert ✅
9. BiesseEmitter mit CIX-Grundstruktur ✅
10. BiesseProfile mit Biesse-Defaults ✅
11. E2E Tests erweitert ✅

### Phase 3+ — Erweiterte Biesse/Homag-Unterstützung
9. **BppLib** als Referenz für CIX-Format nutzen (https://github.com/viachpaliy/BppLib)
10. **woodWOP Formatbeschreibung** für MPR-Format konsultieren (Dok-Nr. 9-080-42-7190)
11. **Maestro-Handbuch** bei Detailfragen: `maestro_editor_text.txt`

### Rhino-Kommandos zum Testen
- `ExportXilog` — Export-Dialog öffnen
- `RhinoCNCExporterSettings` — Settings-Panel öffnen
