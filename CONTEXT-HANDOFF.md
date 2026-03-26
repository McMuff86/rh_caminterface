# CONTEXT-HANDOFF — RH_caminterface / RhinoCNCExporter

Dieses Dokument dient dem schnellen Einstieg bei Sitzungswechsel oder Übergabe an einen neuen Agenten/Entwickler.

---

## Was ist das Projekt?

Ein **Rhino 8 C#-Plugin** (Yak Package), das aus 2D-Geometrien + Layer-Konventionen CNC-Fräsprogramme generiert für:
- **SCM** (Maestro/CAD+T) → `.xcs` (Xilog-Format)
- **Biesse** (bSolid/BiesseWorks) → `.cix` (BEGIN/END Blöcke) oder `.bpp` (INI-Style)
- **Homag** (woodWOP) → `.mpr` (ASCII-Sektionen) oder `.mprx` (XML)

Einsatzgebiet: Holzbearbeitung / Möbelindustrie — Platten fräsen, bohren, Nuten schneiden.

## Aktueller Stand (zuletzt aktualisiert: 2026-03-24, Sprint 5 Validation + Sprint 6-8 Foundation In Progress)

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
  - UI: ExportPanel + ExportDialog als Rhino-Basis, separates Settings-Panel entfernt ✅
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

### Sprint 3 — Plate Detection + Coordinate Transform + CLAMEX (KOMPLETT ✅, 23.03.2026)
3D plate detection, coordinate transformation, CLAMEX macro generation, multi-plate export:
- **ClamexMacroBuilder**: Template-based SawCut_Lamello macro generation ✅
  - Vertical CLAMEX (E015, E004, E019, E032) — 48 parameters ✅
  - Horizontal CLAMEX (E015, E005, E022, E021) — 49 params + DZ-9.5 ✅
  - Validated against production XCS files (exact string match!) ✅
  - BuildFromBlock() for automatic orientation detection ✅
- **CoordinateTransformer** (Core — no RhinoCommon): ✅
  - WorldToPlateLocal: dot-product projection onto plate axes ✅
  - Flat plates (Z-up), upright XZ (side panels), upright YZ (back panels) ✅
  - DetermineSide / DetermineEdgeSide for machining side detection ✅
  - Factory methods: CreateFlatOrigin, CreateUprightXZOrigin, CreateUprightYZOrigin ✅
- **PlateDetector** (Plugin — needs RhinoCommon): ✅
  - Scans RhinoDoc for Solids/Extrusions → Plate DTOs ✅
  - BBox analysis: thinnest dimension = thickness, auto LPX/LPY ✅
  - Auto orientation: flat, upright XZ, upright YZ ✅
  - WK_PIECE fallback for legacy compatibility ✅
- **AssignmentResolver**: Extended with proximity-based assignment ✅
  - Layer match (Phase 2) + proximity check (Phase 3) + explicit CNC_Plate ✅
- **BlockAwareExportService**: Multi-plate export pipeline ✅
  - PlateDetector → BlockScanner → AssignmentResolver → CoordinateTransformer → MachiningFactory → EmitterRouter ✅
  - Per plate → separate .xcs file in output directory ✅
- **EmitterRouter**: Full SawCut_Lamello CreateMacro emission (no longer comment placeholder) ✅
- 133 new tests (316 total), all passing, 0 regressions ✅

### Sprint 4 — Multi-Platte Export + UI Erweiterung (CODE COMPLETE ✅, 23.03.2026)
Multi-plate service layer, export mode resolution, tree-based UI preview, export report:
- **ExportService3D**: neuer Service für Dokumentanalyse + Exportmodus-Auflösung + Batch-Export ✅
  - `AnalyzeDocument()` erkennt Legacy/3D/Block-Capabilities ✅
  - `ExportDocument()` routed Auto/Legacy/3D konsistent ✅
  - `ExportMultiPlate()` erzeugt pro Platte eine separate `.xcs`/`.cix` Datei ✅
- **Core Sprint-4 Modelle/Helper**: ✅
  - `ExportMode` Enum (`Automatic`, `LegacyOnly`, `MultiPlate3D`) ✅
  - `DocumentExportAnalysis`, `PlatePreview`, `ExportBatchPlan`, `ExportSummaryReport` ✅
  - `ExportModeResolver` für Auto-Detection ✅
  - `BatchExportPlanner` für Dateinamen/Selektionsplanung ✅
  - `ConfigurableMachineProfile` für UI-Offsets auf XCS/CIX ✅
- **ExportPanel UI**: ✅
  - Export-Modus Selector (Auto / 2D Legacy / 3D Multi-Platte) ✅
  - Maschinenwahl SCM/Biesse, Homag als Platzhalter ✅
  - **Baumansicht** Platte → zugeordnete Blöcke mit Checkboxen auf Root-Ebene ✅
  - Ordner-Export für Multi-Platte, Dateiexport für Legacy ✅
  - Export-Report ("N Platten, M Bearbeitungen exportiert") ✅
- **Build/Test-Status**:
  - `dotnet build RhinoCNCExporter/RhinoCNCExporter.csproj` grün ✅
  - Neue Sprint-4 Tests + gezielte Regressions-Tests grün ✅
  - Voller `dotnet test` Lauf führt alle 324 Tests aus, beendet sich in dieser CLI-Umgebung aktuell aber nicht sauber (Host/Runner-Hänger nach Testausführung) ⚠

### Sprint 5 — Produktionsvalidierung (IN ARBEIT 🟡, 23.03.2026)
Erster automatisierter Validierungsblock aus Produktionsbefunden umgesetzt:
- **Duplicate-safe BatchExportPlanner**: Gleichnamige Produktionsplatten wie `Schubladen_Doppel` oder `Revisionsture` erzeugen eindeutige Dateinamen (`_2`, `_3`, ...) statt sich gegenseitig zu überschreiben ✅
- **Eindeutige Platten-Selektion im 3D-Export**: UI + Service verwenden für Multi-Platte bevorzugt `LayerPath` als Auswahl-Key statt nur den Anzeigenamen ✅
- **Neue Sprint-5 Tests**: Produktionsnamen-Kollisionen, Sanitizing-Kollisionen und 24-Platten-Batch-Regression ergänzt ✅
- **AssignmentResolver validiert gegen echten Codepfad**: Tests binden jetzt die echte Plugin-Klasse ein statt eine lokale Nachbildung ✅
- **Edge Case gelöst**: Blöcke zwischen zwei Platten werden im Proximity-Pfad der nächstgelegenen Plattenfläche zugewiesen statt input-order-abhängig ✅
- **Altbestand bereinigt**: Veraltete `ExportMode`/`ExportReport`/`ExportModeDetector` Artefakte aus dem Compile-Graph entfernt ✅
- **DWG-abgeleitete Produktionsfixtures ergänzt**: `Putz-Schrank.dwg` → `Staub_SockelMont.xcs` und `Pult_und_Korpus_Novotny.dwg` → `NEW_Fertigauszug_Legrabox.xcs` sind jetzt als reproduzierbare Tests im Repo hinterlegt ✅
- **Normalisierte Produktionsvergleiche aktiv**: 3D-/Plate-basierter XCS-Output wird für heute unterstützte Referenzteile nach Normalisierung nicht-semantischer Unterschiede direkt gegen Produktions-XCS verglichen ✅
- **Feature-Gap formalisiert**: `NEW_Schubladen_Doppel_1.xcs` ist jetzt als BladeCut-/Sectioning-Referenz abgesichert, damit der offene MSL-Block nicht nur in Doku, sondern auch in Tests sichtbar bleibt ✅
- **Komplexere Putz-Schrank-Platte**: `Staub_Seite_links.xcs` (Aussenkontur E010, RNT 066, Einzelbohrungen, Lochreihen, System-32) ist als DWG-verknüpfter Produktionsvergleich mit handgebautem `Plate` + `PreserveMachiningOrder` abgedeckt ✅
- **Horizontale Produktionsvalidierung ergänzt**: `Staub_Boden.xcs` validiert jetzt `CreateWorkplane()`-basierte Horizontalbohrungen + RNT + Top-Bohrungen gegen eine DWG-abgeleitete Fixture ✅
- **XilogEmitter Lochreihen-Reihenfolge**: `EmitDrillPattern` emittiert jetzt wie CAD+T-Staub/Mittelseite — `CreatePattern` vor `CreateDrill` (vorher war die Reihenfolge invertiert) ✅
- **HorizontalDrill Routing korrigiert**: `EmitterRouter` nutzt jetzt den echten Horizontalbohrungs-Emitterpfad; freie Ebenen verwenden produktionskonforme L/R-Rotationen und kein doppeltes `SelectWorkplane` mehr ✅
- **NameService Hänger beseitigt**: truncierte Namenskollisionen (31-Zeichen-Limit + Suffix) führen nicht mehr in eine Endlosschleife; Regressionstest für freie Ebenen ergänzt ✅
- **`Plate.PreserveMachiningOrder`**: Optional, damit die Router-Ausgabe die Listenreihenfolge beibehält (nötig wenn Bohrungen und Lochreihen gemischt sind wie in Produktions-XCS) ✅
- **Normalisierung in Produktionsvergleichen**: Erstes Argument von `SetMachiningParameters` (`IJ`/`IL`/…) wird für den Diff neutralisiert ✅
- **Build/Test-Status**:
  - `dotnet test RhinoCNCExporter.Tests/RhinoCNCExporter.Tests.csproj --filter BatchExportPlannerTests` grün ✅
  - `dotnet test RhinoCNCExporter.Tests/RhinoCNCExporter.Tests.csproj --filter AssignmentResolverTests` grün ✅
  - `dotnet test RhinoCNCExporter.Tests/RhinoCNCExporter.Tests.csproj --filter ProductionReferenceValidationTests` grün ✅
  - `dotnet test RhinoCNCExporter.Tests/RhinoCNCExporter.Tests.csproj --filter NameServiceTests` grün ✅
  - `dotnet test RhinoCNCExporter.Tests/RhinoCNCExporter.Tests.csproj --filter "EmitterTests|EmitterRouterTests"` grün ✅
  - `dotnet build RhinoCNCExporter/RhinoCNCExporter.csproj` grün ✅
  - Rhino-Smoke-Tests und DWG-basierte Referenzvergleiche noch offen ⚠

### Sprint 6-8 Foundation — Werkzeug-DB + Strategie + Rhino-Preview (IN ARBEIT 🟡, 24.03.2026)
- **Tooling Core erweitert**: `RhinoCNCExporter.Core/Models/Tooling.cs` enthält jetzt `ToolHolderDefinition`, zusätzliche Werkzeugparameter (Halter, Material, Schneiden, StepOver, PlungeFeed) sowie `ToolLibrary`, `MachiningStrategy`, `ToolpathPlan`, `ToolpathPrimitive` ✅
- **Per-Machine Tool Libraries**: Default-Werkzeuge und Default-Halter für SCM/Xilog und Biesse, JSON Import/Export + Persistenz via `ToolLibraryStore` unter `%AppData%\\RhinoCNCExporter\\ToolLibraries` ✅
- **Profile erweitert**: `IMachineProfile` hat jetzt `MachineKey` für stabile Tool-Library-Zuordnung ✅
- **ToolpathPlanner**: Preview-Planung aus `Plate.Machinings` inklusive Rapid-, Drill-, Feed-, Roughing- und Finishing-Pässen ✅
- **Rhino Preview Service**: `ToolpathPreviewService` erzeugt farbkodierte Preview-Curves auf `RhinoCNC Preview::...` Layern; Bohrungen als Kreise, CLAMEX/RNT als vereinfachte Pfade ✅
- **ExportPanel erweitert**:
  - Tool-Library Import / Export / Defaults ✅
  - `Werkzeugmanager`-Dialog für CRUD von Werkzeugen und Haltern mit Parameterformularen ✅
  - `Werkzeugzuordnung`-Dialog für per-Operation Rough/Finish-/Holder-Overrides auf Basis der aktuell gewählten Platten ✅
  - Resizable Split-Views in beiden Tabs; Listenbereich, Editor und Preview können separat skaliert werden ✅
  - Listenbereiche in beiden Tabs bleiben bei engem Splitter horizontal/vertikal scrollbar, statt Spalten einfach abzuschneiden ✅
  - Live-Preview für Werkzeug-/Halter-Assembly als schematische CAD/CAM-Ansicht im Dialog; `CornerRadius` wird in Kontur und Preview-Text dargestellt ✅
  - `RNT066` ist jetzt in der Tool-Library als Rueckwandnuter-Scheibe modelliert; fixer Bohr-/Saegeaggregat-Einsatz und nur lineare X/Y-Bewegung werden in Default-Daten und Preview-Summary berücksichtigt ✅
  - Bohrer werden in der Tool-Library als fixe Werkzeuge im Bohraggregat geführt; die Vorschau zeigt sie als Zylinder mit Schaft statt als Fraeserprofil ✅
  - Migration älterer Tool-Libraries ohne Halterdaten beim Laden/Import via `ToolLibraryStore.MergeDefaults(...)` ✅
  - Rough/Finish Preview Toggle + Aufmass-Feld ✅
  - Speichern der Werkzeugzuordnung triggert direkt ein Replan der Rhino-Vorschau mit den neuen session-basierten Overrides ✅
  - Vorschau generieren / Vorschau löschen ✅
  - 2-Spalten-Dashboard statt reiner Vertikal-Stack; `Modus`, `Dokumentanalyse`, `Legacy-Layer`, `Einstellungen`, `Aktionen` und `Status` sind als einklappbare Bereiche organisiert ✅
  - Export-Report + Log in gemeinsamer Status-Ansicht mit Tabs; Tool-/Preview-/Export-Aktionen im rechten Sidebar-Block gebündelt ✅
- **Tests**:
  - Neue `ToolLibraryTests` + `ToolpathPlannerTests` grün ✅
  - Regressionsläufe `ProductionReferenceValidationTests`, `PipelineTests`, `EmitterTests` weiter grün ✅
- **Wichtig**:
  - `ToolLibrary.SuggestTool()` und `MachiningStrategy.CreateDefault()` erzwingen jetzt Kompatibilität nach `ToolKind` + `ToolMotionProfile` + Aggregatbindung; `GrooveRntMachining` nimmt dadurch die Rueckwandnuter-Säge (`RNT066`) statt eines Routers ✅
  - RNT-Grooves werden in der Preview-Planung nicht mehr als Rough/Finish-Routing behandelt; sie laufen als einzelner kompatibler Feed-Pass mit Sägewerkzeug ✅
  - `ToolpathPlanner` vergibt jetzt stabile `OperationKey`s pro Platte/Bearbeitung; `MachiningToolOverride` kann dadurch exakt auf einzelne Bearbeitungen angewendet werden ✅
  - Per-Operation Rough/Finish-/Holder-Overrides sind jetzt im Preview/UI verfügbar; sie wirken aktuell auf Toolpath-Planung und Rhino-Vorschau, noch nicht auf die finale CNC-Ausgabe ✅/⚠
  - Rough/Finish ist aktuell **Preview-/Planungslogik**, noch keine echte CNC-Multi-Pass-Ausgabe ✅/⚠
  - Keine echte Offset-Geometrie für Schruppbahnen; aktuelle Roughing-Pässe nutzen gleiche Grundgeometrie mit separater Pass-/Werkzeugsemantik ⚠
  - Preview im Werkzeugmanager ist aktuell schematisch 2D, noch kein echtes Rhino-3D-Tool-Assembly-Rendering ⚠

### Architektur-Klärung (23.03.2026)
- `docs/ARCHITECTURE-3D-TO-CNC.md` legt das **künftige** Face-Tagging-/Plugin-Command-Konzept fest
- **Noch nicht implementiert im Code**: `AddDrill`, `AddPocket`, `AddGroove`, `AddClamex`, Face-Tags und Feature-Erkennung sind aktuell ADR/Future Work
- **Aktueller produktiver Pfad bleibt**: Layer-/Block-basierte Pipeline mit PlateDetector, BlockScanner, AssignmentResolver, MachiningFactory, EmitterRouter

**Sprint 3 Dateien:**
```
RhinoCNCExporter.Core/
├── Blocks/
│   └── ClamexMacroBuilder.cs          (CLAMEX vertical/horizontal macro generation)
└── PlateDetection/
    └── CoordinateTransformer.cs       (World→plate-local coordinate math)

RhinoCNCExporter/
├── PlateDetection/
│   ├── PlateDetector.cs               (Solid→Plate detection with RhinoCommon)
│   └── CoordinateTransformer.cs       (Re-export from Core)
├── BlockScanning/
│   └── AssignmentResolver.cs          (MODIFIED: proximity-based assignment)
└── Services/
    └── BlockAwareExportService.cs     (MODIFIED: multi-plate export pipeline)

RhinoCNCExporter.Tests/
├── ClamexMacroBuilderTests.cs         (30 tests: production reference comparison)
├── CoordinateTransformerTests.cs      (26 tests: flat, upright, roundtrip)
└── MultiPlatePipelineTests.cs         (8 tests: full pipeline integration)
```

### Was fehlt / nächste Schritte (Sprint 5+)
1. **Sprint 5: Validation** — IN ARBEIT:
   - DWG-basierte Fixtures von einfachen Referenzteilen auf komplexe Platten (z.B. `Seite_links`, `Schubladen_Doppel`) erweitern
   - Vergleich 3D-Output vs. Produktions-XCS auf BladeCut-/Sectioning-/Helic-Fälle ausdehnen
   - Rhino Smoke-Test des neuen ExportPanels mit echten 3D-Modellen

2. **Neue MSL-Befehle** (aus 55-XCS-Analyse):
   - CreateBladeCut: Geneigte Schnitte/Fasen (36 Vorkommen)
   - CreateSectioningMillingStrategy + CreateSegment: Schneidstrategien (68 Vorkommen)
   - CreateHelicMillingStrategy: Spiralbearbeitung für Ausschnitte
   - Erweiterte SetMachiningParameters: "EF", "IL", "EH" zusätzlich zu "IJ"

3. **GeometryUtils Arc Detection** — `ToPolySegments()` für RhinoCommon ArcCurve-Erkennung
4. **BppLib Integration** — BppLib NuGet Package als Biesse-Abhängigkeit
5. **Homag-Emitter** (.mpr) — Noch nicht begonnen, aber Research vorhanden
6. **Nächster sauberer Ausbau in Sprint 6/7** — Preview-Overrides in echte Export-/CNC-Strategien überführen und Roughing-Geometrie mit realem Offset statt nur Pass-Semantik erzeugen
7. **UI Improvements** — Maschinenformat-Auswahl, Profile-Konfiguration
8. **Yak Package Build** — Finaler Package-Build und Test-Installation

## Schlüsseldateien

| Datei | Zweck |
|-------|-------|
| `RH_caminterface_v007.py` | Python-Referenz — Legacy-/Fallback-Referenz für XCS, **nicht mehr** Quelle der Wahrheit |
| `maestro_editor_text.txt` | Durchsuchbarer Maestro-Handbuch-Text (Page-Marker) |
| `docs/RESEARCH-CAM-FORMATS.md` | Umfassendes Research zu SCM, Biesse, Homag Formaten |
| `docs/XCS-REFERENCE-ANALYSIS.md` | Primäre fachliche Referenz für aktuelles XCS-Verhalten (55 Produktionsdateien) |
| `manifest.yml` | Yak Package Manifest für Rhino Package Manager |
| `RhinoCNCExporter/RhinoCNCExporter.csproj` | Plugin-Projekt (net7.0-windows, Rhino 8) |
| `RhinoCNCExporter/Core/LayerParser/LayerRegex.cs` | Alle Regex-Patterns + Parsing |
| `RhinoCNCExporter/Core/LayerParser/Specs.cs` | DTOs (CutSpec, PocketSpec, DrillSpec, ...) |
| `RhinoCNCExporter/Core/Emitters/IEmitter.cs` | Interface für alle CNC-Format-Emitter |
| `RhinoCNCExporter/Core/Emitters/XilogEmitter.cs` | SCM XCS-Ausgabe (vollständig) |
| `RhinoCNCExporter/Core/Emitters/BiesseEmitter.cs` | Biesse CIX-Ausgabe (Grundoperationen) |
| `RhinoCNCExporter/Core/Emitters/Emit*.cs` | Operationen-Emitter (CUT, POCKET, DRILL, ROW, GrooveCH, GrooveRNT, DrillPattern, HorizontalDrill) |
| `docs/CLAMEX-CONCEPT.md` | 3D-Block-basiertes CLAMEX-Konzept + 3D-Pipeline Vision |
| `tests/references/NEW_*.xcs` | 19 neue Produktions-XCS-Dateien (Schubladen, Revisionstüren, etc.) |
| `RhinoCNCExporter/Core/Profiles/IMachineProfile.cs` | Interface für Maschinenprofile |
| `RhinoCNCExporter/Core/Profiles/MachineProfile.cs` | Maschinenprofil-Basisklasse |
| `RhinoCNCExporter/Core/Profiles/BiesseProfile.cs` | Biesse-spezifische Konfiguration |
| `RhinoCNCExporter/Core/Profiles/ConfigurableMachineProfile.cs` | Laufzeit-Overrides für Setup-Offsets aus der UI |
| `RhinoCNCExporter/Services/ExportService.cs` | Multi-Format Export-Orchestrierung |
| `RhinoCNCExporter/Services/ExportService3D.cs` | Sprint-4 Service: Auto-Detection, Multi-Platte Export, Report |
| `RhinoCNCExporter/Core/Pipeline/ExportModeResolver.cs` | Auto/Legacy/3D Modus-Auflösung |
| `RhinoCNCExporter/Core/Pipeline/BatchExportPlanner.cs` | Dateinamen-/Selektionsplanung für Multi-Platte, inkl. Dubletten-Schutz |
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
- **XCS Source of Truth**: Produktions-XCS + `docs/XCS-REFERENCE-ANALYSIS.md`, nicht die Python-Datei
- **CIX ist kein XML**: Trotz "X" im Namen — es sind BEGIN/END Textblöcke
- **MPR Konturen**: Werden als separate `]n` Blöcke definiert und von Operationen referenziert
- **Biesse Seiten vs Homag**: Biesse nutzt SIDE=0-5, Homag nutzt Koordinatensysteme (KO)
- **System.Drawing.Common**: Wird als NuGet-Paket (v7.0.0) benötigt wegen `Icon`-Typ in `Panels.RegisterPanel`
- **Yak Package**: manifest.yml muss im Root des dist-Ordners liegen, `.rhp` daneben
- **Workplane**: Immer "Top", Eingaben immer in mm
- **`dotnet test` CLI-Hänger**: Der komplette Testlauf führt aktuell alle Tests aus, terminiert in dieser Umgebung aber nicht sauber; gezielte Testläufe funktionieren
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

## ✅ Sprint 9: Interactive CAM Commands (KOMPLETT ✅, 26.03.2026)

**Interactive CAM Command System implementiert:** Rhino-Commands für direktes Zuweisen von CNC-Bearbeitungen zu Geometrie via UserText + visuelle Rückmeldung.

### Was implementiert wurde
- **CncOperationSchema** (Core): Schema für UserText-basierte CNC-Operationen ohne RhinoCommon-Abhängigkeiten ✅
- **CncOperationService** (Plugin): Rhino-spezifische Wrapper für UserText-Operationen mit visueller Rückmeldung ✅
- **CamOperationDialogBase**: Basis-Dialog-Klasse mit Tool-Auswahl und gemeinsamen UI-Patterns ✅
- **Spezifische Dialogs**: ContourOperationDialog, PocketOperationDialog, DrillOperationDialog, GrooveOperationDialog ✅
- **Interactive Commands** (6 neue Rhino-Commands):
  - `CNCAddContour`: Kurven/Kanten auswählen → Konturfräsen-Dialog → UserText + Farbkodierung ✅
  - `CNCAddPocket`: Geschlossene Kurven → Taschen-Dialog → UserText + Farbkodierung ✅
  - `CNCAddDrill`: Punkte klicken oder auswählen → Bohr-Dialog → Kreise mit UserText ✅
  - `CNCAddGroove`: Linien/Kurven → Nut-Dialog → UserText + Farbkodierung ✅
  - `CNCRemoveOperation`: Auswahl → CNC-UserText entfernen + Standardfarbe wiederherstellen ✅
  - `CNCListOperations`: Alle CNC-Operationen im Dokument auflisten mit Zusammenfassung ✅
- **Pipeline-Integration**: UserTextMachiningReader konvertiert UserText zu Machining-Objekten ✅
- **MachiningBuilder erweitert**: `MergeAllSources()` mit Priorität UserText > Blocks > Legacy Layers ✅
- **ExportService3D Integration**: UserText-Operationen werden in Multi-Platte Export einbezogen ✅
- **Visuelle Rückmeldung**:
  - Farbkodierung nach Operation (Rot=Kontur, Blau=Tasche, Gelb=Bohrung, Grün=Nut) ✅
  - Text-Dots mit Bearbeitungs-Zusammenfassung (Werkzeug, Tiefe, Strategie) ✅
  - Tool-Auswahl aus bestehender ToolLibrary ✅
- **Tests**: Umfassende Unit-Tests für Schema, Validation, Pipeline-Integration ✅

### UserText Schema
```
CNC_Type: Contour|Pocket|Drill|Groove
CNC_Tool: Werkzeugname aus ToolLibrary
CNC_Depth: Bearbeitungstiefe (mm)
CNC_Diameter: Bohrdurchmesser (mm, nur Drill)
CNC_Width: Nutbreite (mm, nur Groove)  
CNC_Strategy: Rough|Finish|Both
CNC_Feedrate: Vorschub (mm/min, optional)
CNC_Stepover: Zustellung in % (nur Pocket)
CNC_Peck: true|false (Tieflochbohren)
CNC_PeckDepth: Zustell-Tiefe (mm)
CNC_RampEntry: Straight|Spiral|Profile (Pocket)
```

### Workflow
1. Geometrie zeichnen (Kurven, Punkte)
2. Command ausführen (z.B. `CNCAddContour`)
3. Geometrie auswählen
4. Dialog mit Tool-Auswahl und Parametern
5. OK → UserText wird gesetzt, Farbe geändert, Text-Dot erstellt
6. Export über ExportPanel → UserText-Operationen haben höchste Priorität

### Integration
- UserText-Operationen werden in ExportService3D automatisch gelesen
- Priorität: **UserText > Blocks > Legacy Layers**  
- Bestehende Pipeline bleibt unverändert (rückwärtskompatibel)
- Tool-Auswahl nutzt bestehende ToolLibrary-Infrastruktur

### Rhino-Kommandos zum Testen
- `RhinoCNCExporter` — Dockbares ExportPanel öffnen
- `ExportXilog` — Export-Dialog öffnen
- **Neue Interactive CAM Commands:**
  - `CNCAddContour` — Konturfräsen zu Kurven/Kanten hinzufügen
  - `CNCAddPocket` — Taschenbearbeitung zu geschlossenen Kurven hinzufügen
  - `CNCAddDrill` — Bohrungen durch Punktklicks oder Punktauswahl erstellen
  - `CNCAddGroove` — Nuten zu Linien/Kurven hinzufügen
  - `CNCRemoveOperation` — CNC-Bearbeitungen von Objekten entfernen
  - `CNCListOperations` — Alle CNC-Operationen im Dokument auflisten
