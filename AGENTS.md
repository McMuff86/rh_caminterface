# AGENTS.md — RH_caminterface / RhinoCNCExporter

## Projektübersicht

**RH_caminterface** ist ein Rhino-Plugin (C# / .NET 7 / Rhino 8), das aus 2D-Geometrien und Layer-Konventionen maschinenfähige CNC-Programme erzeugt.

### Unterstützte Maschinenformate

| Hersteller | Format | Dateiendung | Emitter-Klasse |
|------------|--------|-------------|-----------------|
| SCM (Maestro/CAD+T) | Xilog | `.xcs` | `XilogEmitter` |
| Biesse (bSolid) | CIX | `.cix` | `BiesseEmitter` (geplant) |
| Biesse (BiesseWorks) | BPP | `.bpp` | `BiesseEmitter` (geplant, optional) |
| Homag (woodWOP) | MPR | `.mpr` | `HomagEmitter` (geplant) |
| Homag (woodWOP 6+) | MPRX | `.mprx` | `HomagEmitter` (geplant, optional) |

### Quellen (nach Priorität)

- **Research-Dokument**: `docs/RESEARCH-CAM-FORMATS.md` — umfassende Format-Spezifikationen, CAM-Analyse, Praxis-Workflows
- **Python-Referenz** (Entwurf): `RH_caminterface_v007.py` — funktionaler Entwurf für XCS-Emitter, NICHT mehr "Quelle der Wahrheit" für alle Formate
- **Maestro-Handbuch**: `maestro_editor_text.txt` + `maestro_editor_outline.json` (extrahiert aus `Maestro Editor.pdf`)
- **BppLib (NuGet)**: Externe C#-Library für Biesse CIX/BPP-Erzeugung — als Dependency evaluieren
- **woodWOP Formatbeschreibung**: Dok-Nr. 9-080-42-7190 (über Research-Dokument referenziert)
- **C#-Plugin**: `RhinoCNCExporter/` — produktive Implementierung

## Architektur

```
RhinoCNCExporter.Core/                 # Core-Logik OHNE RhinoCommon
├── Models/                            # 🆕 Sprint 1: Zentrale Datenmodelle (DTOs)
│   ├── Enums.cs                       # MachiningType, MachiningSide, MachineFormat, etc.
│   ├── Plate.cs                       # Plate record (Name, Dimensions, Machinings)
│   ├── PlateOrigin.cs                 # Coordinate system origin for plate
│   ├── Machining.cs                   # Base + 8 subtypes (Drill, DrillPattern, etc.)
│   ├── FittingBlock.cs                # Parsed block with CNC_* attributes
│   └── ExportJob.cs                   # Export orchestration record
├── Blocks/                            # 🆕 Sprint 1: Block-Logik (ohne Rhino)
│   ├── BlockUserTextSchema.cs         # CNC_* key constants + validation
│   ├── CncUserTextParser.cs           # UserText dict → FittingBlock
│   └── MachiningFactory.cs            # FittingBlock → Machining objects
└── Pipeline/                          # 🆕 Sprint 1: Export-Orchestrierung
    ├── IMachiningBuilder.cs           # Interface: merge machinings
    ├── IEmitterRouter.cs              # Interface: route to emitter
    ├── IPlateExporter.cs              # Interface: export single plate
    ├── MachiningBuilder.cs            # Merge legacy + block machinings
    └── EmitterRouter.cs               # Route Machining → IEmitter calls

RhinoCNCExporter/                      # Plugin MIT RhinoCommon
├── PlugIn.cs                          # Rhino Plugin Entry
├── Commands/                          # User-facing Rhino Commands
│   ├── ExportCommand.cs               # Export-Dialog starten
│   └── SettingsCommand.cs             # Einstellungen öffnen
├── UI/                                # Eto.Forms UI
│   ├── SettingsPanel.cs
│   └── ExportDialog.cs
├── Services/
│   └── ExportService.cs               # Orchestrierung: Geometrie → Emitter → Datei
├── Core/
│   ├── LayerParser/                   # Layer-Namen → DTOs (CutSpec, PocketSpec, ...)
│   │   ├── Specs.cs
│   │   └── LayerRegex.cs
│   ├── Geometry/                      # Polyline, Offsets, Groove-Berechnungen
│   │   └── GeometryUtils.cs
│   ├── Naming/                        # Eindeutige Namen (max 31 Zeichen)
│   │   └── NameService.cs
│   ├── Emitters/                      # Maschinenspezifische Ausgabe
│   │   ├── IEmitter.cs               # Gemeinsames Interface
│   │   ├── XilogEmitter.cs           # SCM Maestro (.xcs)
│   │   ├── BiesseEmitter.cs          # Biesse (.cix)
│   │   ├── HomagEmitter.cs           # (geplant) Homag (.mpr)
│   │   └── Emit*.cs                  # Operation-spezifische Emitter
│   └── Profiles/                      # Maschinenprofile (Defaults, Technologien)
│       ├── MachineProfile.cs
│       └── MaestroCadTProfile.cs

RhinoCNCExporter.Tests/                # xUnit Tests (OHNE RhinoCommon)
├── ModelTests.cs                      # 🆕 Plate, Machining, FittingBlock, ExportJob
├── BlockUserTextSchemaTests.cs        # 🆕 Validation rules
├── CncUserTextParserTests.cs          # 🆕 Parsing + error cases
├── MachiningFactoryTests.cs           # 🆕 CNC_Type mapping + template expansion
├── PipelineTests.cs                   # 🆕 MachiningBuilder + EmitterRouter
├── EmitterTests.cs                    # XilogEmitter + BiesseEmitter
├── E2ETests.cs                        # End-to-End gegen Referenzdateien
├── LayerRegexTests.cs                 # Layer pattern parsing
├── NameServiceTests.cs                # Name generation + sanitization
└── SpecsTests.cs                      # Spec defaults + validation
```

## Layer-Konventionen (universell für alle Maschinen)

Alle Angaben in mm. Workplane Top. Maximale Namenslänge: 31 Zeichen.

| Typ | Layer-Muster | Beispiel |
|-----|-------------|----------|
| Werkstück | `WK_PIECE` | `WK_PIECE` |
| Kontur | `CUT_E<nnn>[_Z<t>][_S<s>][_D<Ø>]` | `CUT_E010_Z16_S4_D9.5` |
| Tasche | `POCKET_E<nnn>[_Z<t>][_S<s>][_D<Ø>][_O<step>]` | `POCKET_E015_Z8_O6` |
| Bohrung | `DRILL_D<Ø>[_Z<t>][_C P\|L]` | `DRILL_D5_Z17_CP` |
| Lochreihe | `DRILLROW_D<Ø>_Z<t>_P<pitch>[_N<n>]` | `DRILLROW_D5_Z17_P32` |
| Rückwandnut (Fräsen) | `RBNUT_CH_{X\|Y}_W<w>[_Z<t>][_S<s>][_E<nnn>]_{M\|P}` | `RBNUT_CH_X_W6_Z8_P` |
| Rückwandnut (Makro) | `RBNUT_RNT_{X\|Y}_W<w>[_Z<t>]_C<code>_{M\|P}` | `RBNUT_RNT_X_W5.5_Z8.3_C066_P` |
| **Pattern-Bohrung (NEU!)** | `DRILLPAT_D<Ø>_X<xCnt>_Y<yCnt>_P<pitch>[_Z<t>]` | `DRILLPAT_D5_X3_Y4_P32_Z13` |
| **Horizontal-Bohrung (NEU!)** | `HDRILL_D<Ø>[_Z<tiefe>][_SIDE L\|R]` | `HDRILL_D8_Z30_SIDE_L` |
| **CLAMEX-Block (Vision!)** | 3D-Block: `CLAMEX_P14`, `CLAMEX_P15` | Block-Platzierung in 3D |

## Konventionen für Agents / KI-Assistenten

### Coding-Standards
- C# 10/11, `nullable` enabled
- Pure Core-Klassen (kein Rhino-UI in Core/)
- Keine Magic Numbers — alles über Profile/Settings
- Fehler klar kommunizieren (Dialog + Log)
- SemVer (MAJOR.MINOR.PATCH)

### Testphilosophie — PFLICHT!
- **Unit Tests sind PFLICHT bei JEDEM Commit** — kein Code ohne Tests
- Pure Logik testbar ohne Rhino-UI
- xUnit für alle Tests in `RhinoCNCExporter.Tests/`
- Kategorien: Parser, Naming, Geometry, Emitter, Profile
- `dotnet test` zum Ausführen
- **Emitter-Tests:** Ausgabe gegen Referenz-Dateien (`tests/test_01.xcs`, `test_02.xcs`) vergleichen
- **Parser-Tests:** Jeden Layer-Pattern testen (CUT, POCKET, DRILL, DRILLROW, RBNUT_CH, RBNUT_RNT)
- **GeometryUtils-Tests:** Polyline-Sampling, Offset, Groove-Konstruktion
- **NameService-Tests:** 31-Zeichen-Limit, Duplikate, Sonderzeichen
- **Ziel:** 80%+ Code Coverage auf Core/

### Wichtige Regeln (März 2026)
1. **55 Produktions-XCS-Dateien** sind die neue Referenz — nicht mehr Python!
2. `docs/XCS-REFERENCE-ANALYSIS.md` enthält vollständige Produktions-Spezifikation
3. Neue MSL-Befehle implementieren: CreateBladeCut, CreateSectioningMillingStrategy, CreateHelicMillingStrategy
4. **CLAMEX-Vision:** 3D-Block-basierter Workflow — siehe `docs/CLAMEX-CONCEPT.md`
5. Emitter sind austauschbar — neue Maschinenformate als eigene Klassen
6. Layer-Parsing ist maschinenunabhängig
7. Geometrie-Berechnung ist maschinenunabhängig  
8. Nur die Ausgabe (Emitter + Profile) ist maschinenspezifisch
9. **3D-Pipeline Zukunft:** Aus 3D-Korpus pro Platte CNC-Programme ableiten

### Build & Run
```bash
dotnet build
dotnet test
```
Benötigt: RhinoCommon v8 SDK (Windows), `System\netcore\` Pfad für net7.0

### Yak Package bauen
```bash
# 1. Plugin bauen
dotnet build -c Release

# 2. dist-Ordner vorbereiten (manifest.yml + .rhp + icon)
# 3. Paket erstellen
"C:\Program Files\Rhino 8\System\Yak.exe" build --platform win

# 4. Testen
"C:\Program Files\Rhino 8\System\Yak.exe" push --source https://test.yak.rhino3d.com <package>.yak

# 5. Veröffentlichen
"C:\Program Files\Rhino 8\System\Yak.exe" push <package>.yak
```

### Maschinenformat-Kurzreferenz

**SCM/Xilog (.xcs)**: Zeilenbasierter Text. Header → Operationen → Footer.

**Biesse/CIX (.cix)**: `BEGIN/END` Blöcke. `BEGIN MAINDATA` (LPX/LPY/LPZ), dann `BEGIN MACRO NAME=<OP>` für jede Operation. Geometrie über GEO+START_POINT/LINE_EP/ARC_EPCE/ENDPATH. Referenz-Lib: [BppLib](https://github.com/viachpaliy/BppLib)

**Homag/MPR (.mpr)**: ASCII mit 5 Blöcken: `[H` (Header), `[001` (Variablen L/W/T), `[K` (Koordinatensysteme), `]n` (Konturen: KP/KL/KA), `<ID \Name\` (Operationen). IDs: 100=Werkstück, 102=BohrVert, 105=Konturfräsen, 109=Nuten, 112=Tasche.
