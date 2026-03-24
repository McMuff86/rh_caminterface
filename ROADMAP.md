# ROADMAP — RH_caminterface / RhinoCNCExporter

## Vision

Ein Rhino 8 Plugin (Yak Package), das aus 2D-Geometrien und Layer-Konventionen CNC-Programme für **SCM**, **Biesse** und **Homag** Maschinen erzeugt — konsistent, validierbar, erweiterbar.

---

## ✅ Phase 1: SCM/Maestro — Core fertigstellen

**Status**: KOMPLETT ✅

- [x] LayerParser vollständig implementiert + Tests ✅
- [x] GeometryUtils (Polyline, Offsets, Groove-Berechnungen) + Tests ✅
- [x] Emitter komplett implementiert (CUT, POCKET, DRILL, ROW, GrooveCH, GrooveRNT) ✅
- [x] NameService finalisiert + Tests ✅
- [x] ExportService Orchestrierung (Geometrie sammeln → parsen → emittieren → Datei schreiben) ✅
- [x] Maschinenprofile (MaestroCadTProfile als Default) ✅
- [x] .xcs-Ausgabe gegen Python-Referenz validiert ✅
- [x] ExportPanel / ExportDialog als Rhino UI-Basis vorhanden; altes SettingsPanel entfernt ✅
- [ ] Validierung & Warnings (Layer-Mismatches, Geometrie-Probleme) — planned

## ✅ Phase 2: Emitter-Abstraktion — Multi-Maschinen-Architektur

**Status**: KOMPLETT ✅

- [x] `IEmitter`-Interface definiert (gemeinsame Schnittstelle für alle Maschinenformate) ✅
- [x] `IMachineProfile`-Interface (Defaults, Technologie-Mapping, Werkzeuge) ✅
- [x] XilogEmitter auf IEmitter refactored ✅
- [x] MachineProfile auf IMachineProfile refactored ✅
- [x] BiesseProfile implementiert ✅
- [x] BiesseEmitter Grundstruktur (Header, Drill, Cut) ✅
- [x] ExportService maschinenunabhängig (beide Formate unterstützt) ✅
- [x] E2E Tests erweitert für Interface-Validierung ✅
- [ ] Emitter-Registry / Factory-Pattern für dynamische Emitter-Auswahl — geplant

## ✅ Phase 2.5: Production-Quality XCS & New Operations

**Status**: KOMPLETT ✅ (23.03.2026)

Based on analysis of 36 real production XCS files from CAD+T/Maestro:

- [x] Production header format (comment blocks, compact numbers, CreateMessage) ✅
- [x] Production footer format (comment blocks, "Programm Ende") ✅
- [x] DZ format: `19` not `19.000` (matching CAD+T output) ✅
- [x] Setup offsets compact: `2.5,2.5,0,0` not `2.5,2.5,0.0,0.0` ✅
- [x] **Configurable Setup Offsets (Zugabe)**: SetupOffsetX/Y in IMachineProfile + UI ✅
- [x] **CreatePattern()** for drill grid arrays (122× in production!) ✅
  - New: `DrillPatternSpec`, `DRILLPAT_D{d}_Z{z}_X{nx}_Y{ny}_SX{sx}_SY{sy}` layer pattern
  - New: `EmitDrillPattern.cs`, `IEmitter.EmitDrillPattern()`
  - Biesse: BG with RTY=rpGRD grid repeat
- [x] **AddArc2PointCenterToPolyline()** for arc segments (12× in production) ✅
  - New: `PolySegment` record with IsArc/CenterX/CenterY/Clockwise
  - New: `IEmitter.EmitPolylinePassWithArcs()`
  - Biesse: ARC_EPCE macro support
- [x] **CreateWorkplane()** for horizontal/side drilling (40× in production) ✅
  - New: `HorizontalDrillSpec`, `HDRILL_D{d}_Z{z}_S{side}` layer pattern (L/R/V/H)
  - New: `EmitHorizontalDrill.cs`, `IEmitter.EmitWorkplane()`, `EmitSelectWorkplane()`
- [x] **ExportPanel UI**: "Zugabe X/Y (mm)" fields for configurable setup offsets ✅
- [x] All 80+ tests green ✅

## ✅ Sprint 1: Core Data Models + Pipeline Skeleton

**Status**: KOMPLETT ✅ (23.03.2026)

Foundation for the 3D-to-CNC block-based pipeline:

- [x] Core/Models/: Plate, Machining (8 subtypes), FittingBlock, ExportJob, PlateOrigin, Enums ✅
- [x] Core/Blocks/: BlockUserTextSchema, CncUserTextParser, MachiningFactory ✅
- [x] Core/Pipeline/: IMachiningBuilder, IEmitterRouter, IPlateExporter interfaces ✅
- [x] Core/Pipeline/: MachiningBuilder, EmitterRouter implementations ✅
- [x] Template expansion: {DZ}, {X}, {Y} placeholders with arithmetic ✅
- [x] 90+ unit tests, all green, 0 warnings ✅

## ✅ Sprint 2: Block Scanning + Starter Blocks

**Status**: KOMPLETT ✅ (23.03.2026)

Block detection, starter block definitions, assignment resolver, UI integration:

- [x] StarterBlockDefinitions: 5 starter blocks (Topfband_35, Lochreihe_32, Duebel_8x30, Duebel_8x30_Stirn, CLAMEX_P14) ✅
- [x] BlockScanner: Scans RhinoDoc for CNC_* block inserts, extracts position/rotation/layer ✅
- [x] AssignmentResolver: Layer-based block-to-plate assignment, CNC_Plate override ✅
- [x] BlockAwareExportService: Bridge between BlockScanner and ExportService, feature flag, fallback ✅
- [x] ExportPanel UI: Block detection checkbox, blocks list, scan button ✅
- [x] 36 new tests (schema validation, assignment, full pipeline integration) ✅
- [x] All 183 tests pass, 0 regressions ✅

## ✅ Sprint 3: Plate Detection + Coordinate Transform + CLAMEX

**Status**: KOMPLETT ✅ (23.03.2026)

- [x] ClamexMacroBuilder: SawCut_Lamello macro generation (vertical + horizontal) ✅
- [x] Validated against production XCS files (exact string match!) ✅
- [x] CoordinateTransformer: World→plate-local (flat, upright XZ, upright YZ) ✅
- [x] PlateDetector: Solid/Extrusion→Plate with auto orientation ✅
- [x] AssignmentResolver: Proximity-based block assignment ✅
- [x] Multi-plate export pipeline: per plate → separate .xcs file ✅
- [x] EmitterRouter: Full SawCut_Lamello CreateMacro emission ✅
- [x] 133 new tests (316 total), 0 regressions ✅

## ✅ Sprint 4: Multi-Platte Export + UI Erweiterung

**Status**: CODE COMPLETE ✅ (23.03.2026)

- [x] `ExportService3D` für Dokumentanalyse, Auto-Detection und Batch-Export ✅
- [x] `ExportModeResolver` für `Automatic` / `LegacyOnly` / `MultiPlate3D` ✅
- [x] `BatchExportPlanner` für Dateinamen, Selektion und Reports ✅
- [x] `ConfigurableMachineProfile` für UI-gesteuerte Setup-Offsets in XCS/CIX ✅
- [x] ExportPanel erweitert: Maschinenwahl, Export-Modus, Datei-/Ordner-Auswahl ✅
- [x] **Baumansicht** im ExportPanel: Platte → zugeordnete Blöcke ✅
- [x] Plattenauswahl via Checkboxen auf Root-Ebene ✅
- [x] Export-Report: "N Platten, M Bearbeitungen exportiert" ✅
- [x] Gezielte Sprint-4 Tests + Regressionssuiten grün ✅

**Next**: Sprint 5 — Produktionsvalidierung + Rhino-Smoketests

## 🟡 Sprint 5: Produktionsvalidierung + Rhino-Smoketests

**Status**: IN ARBEIT (24.03.2026)

- [x] Batch-Export gegen Produktionsbefund gehärtet: gleichnamige Platten erzeugen eindeutige Dateinamen (`_2`, `_3`, ...) ✅
- [x] Multi-Platte Selektion auf stabile Plate-Keys (`LayerPath` bevorzugt) umgestellt, damit gleichnamige Teile einzeln exportierbar bleiben ✅
- [x] AssignmentResolver-Tests auf die echte Plugin-Implementierung umgestellt ✅
- [x] Edge Case `Block zwischen zwei Platten` über nächstgelegene Plattenfläche validiert und im Resolver behoben ✅
- [x] Veraltete Sprint-4 Altmodelle (`ExportModeDetector`, altes `ExportMode`, altes `ExportReport`) aus dem Compile-Graph bereinigt ✅
- [x] Automatisierte Validierung ergänzt:
  - [x] Produktionsnamen-Kollisionen (`Schubladen_Doppel`, `Revisionsture`) ✅
  - [x] Sanitizing-Kollisionen bei Dateinamen ✅
  - [x] 24-Platten Batch-Regression als 20+ Platten Scale-Test ✅
- [x] Erste DWG-basierte Fixtures für Putzschrank / Legrabox abgeleitet (`SockelMont`, `Fertigauszug_Legrabox`) ✅
- [x] Normalisierte 3D-vs.-Produktions-XCS Vergleiche für aktuell unterstützte Referenzteile ergänzt ✅
- [x] Produktions-Gap für `Schubladen_Doppel` via BladeCut-/Sectioning-Referenztest dokumentiert ✅
- [x] Fixture für `Putz-Schrank.dwg` / `Staub_Seite_links.xcs` (RNT + Drill + Lochreihen, `PreserveMachiningOrder`) ✅
- [ ] Fixtures auf weitere komplexe Produktionsplatten (z. B. BladeCut-lastig) erweitern
- [ ] Rhino Smoke-Tests des neuen ExportPanels
- [ ] Face-Tagging/Plugin-Commands (`AddDrill`, `AddPocket`, `AddGroove`, `AddClamex`) von ADR zu erster Code-Implementierung bringen

---

## Phase 3: Biesse-Support (.cix / .bpp)

**Status**: Grundstruktur implementiert, Erweiterung geplant

### Format-Details (aus Research)
- **CIX** (bSolid, aktuell): Text-Format mit `BEGIN/END` Blöcken (kein XML!)
  - `BEGIN MAINDATA` → LPX/LPY/LPZ (Werkstück)
  - `BEGIN MACRO NAME=BG` → Bohren (X, Y, Dp, Dia)
  - `BEGIN MACRO NAME=ROUTG` → Fräsen (GID, Dia, Dp, RSP, WSP, CRC)
  - `BEGIN MACRO NAME=POCK` → Tasche (GID, Dia, Dp, TYP=ptZIG)
  - `BEGIN MACRO NAME=CUT_G` → Nut/Schnitt (X, Y, Xe, Ye, Dp, Ang)
  - Geometrie via `GEO` + `START_POINT` / `LINE_EP` / `ARC_EPCE` / `ENDPATH`
- **BPP** (BiesseWorks, legacy): INI-Style mit `[PROGRAM]` Sektion, `@`-prefixed Operationen
- **Referenz-Library**: [BppLib](https://github.com/viachpaliy/BppLib) (C#, liest/schreibt BPP+CIX)
- **Seiten**: SIDE=0 (top), 1 (bottom), 2 (left), 3 (right), 4 (front), 5 (back)

### Tasks
- [x] BiesseProfile erstellt (Technologie-Mapping, Werkzeug-IDs, Defaults) ✅
- [x] BiesseEmitter Grundstruktur implementiert (.cix-Generierung, BEGIN/END Blöcke) ✅
- [x] Basis-Operationen implementiert:
  - [x] Header → MAINDATA (LPX, LPY, LPZ) ✅
  - [x] DRILL → BG (Generic Boring) ✅
  - [x] CUT → ROUTG + GEO (Geometry + Routing) ✅
  - [x] RNT → Rectangular routing (no native RNT macro) ✅
- [x] Tests für Biesse-Emitter implementiert ✅
- [x] Beispiel-CIX Referenzdatei erstellt ✅
- [ ] Erweiterte Operationen:
  - [ ] POCKET → POCK + GEO
  - [ ] DRILLROW → BG mit Repeat (Nrp/Dx/Dy)
  - [ ] Komplexere Geometrien (Arcs, komplexe Konturen)
- [ ] Multi-Platte Export mit echtem Biesse-End-to-End Smoke-Test im Rhino UI validieren
- [ ] BppLib NuGet Integration evaluieren
- [ ] Optional: BPP-Ausgabe für ältere BiesseWorks-Installationen
- [ ] Biesse-spezifische Validierung

## Phase 4: Homag-Support (.mpr / .mprx)

**Status**: Geplant

### Format-Details (aus Research)
- **MPR** (woodWOP, alle Versionen): ASCII-Text, 5 Blöcke in fester Reihenfolge
  - `[H` → Header (VERSION, MAT, INCH=0 für mm)
  - `[001` → Variablen (L=Länge, W=Breite, T=Dicke)
  - `[K` → Koordinatensysteme
  - `]n` → Konturen (KP=Punkt, KL=Linie, KA=Bogen, KR=Rundung)
  - `<ID \Name\` → Operationen mit Parametern
- **Operations-IDs**:
  - `<100 \WerkStck\` → Werkstück (LA, BR, DI)
  - `<102 \BohrVert\` → Vertikalbohren (XA, YA, DU, TI)
  - `<103 \BohrHoriz\` → Horizontalbohren
  - `<105 \Konturfraesen\` → Konturfräsen (Referenziert Kontur ]n)
  - `<109 \Nuten\` → Nut/Sägeschnitt (XA, YA, LA, TI, NB=Breite)
  - `<112 \Tasche\` → Tasche (XA, YA, LA, BR, TI)
  - `<119 \Polygonzug\` → Polygon-Fräsen
- **MPRX** (woodWOP 6+): XML-basiert, für CAD/CAM-Plugin-Daten
- **MPRXE** (woodWOP 8+, 2021): Neues Austauschformat, schneller
- **Offizielle Spec**: "woodWOP Formatbeschreibung" (~70 Seiten, Dok-Nr. 9-080-42-7190)
- **Referenz**: [prgToMPR](https://github.com/mustafayildizmuh/prgToMPR) (C#), Autodesk Post Processor

### Tasks
- [ ] MPR-Referenzdatei beschaffen (Export aus woodWOP)
- [ ] HomagProfile erstellen (Technologie-Mapping, Werkzeug-Nummern)
- [ ] HomagEmitter implementieren (.mpr-Generierung, Sektionen [H, [0, ], <)
- [ ] Operationen mappen:
  - CUT → `<105 \Konturfraesen\` + Kontur `]n`
  - POCKET → `<112 \Tasche\`
  - DRILL → `<102 \BohrVert\`
  - DRILLROW → `<102 \BohrVert\` mit AN (Anzahl) + AB (Abstand)
  - RBNUT → `<109 \Nuten\`
- [ ] Konturen als `]n` Blöcke (KP, KL, KA Elemente) generieren
- [ ] Optional: MPRX-Ausgabe für neuere woodWOP-Versionen
- [ ] Tests gegen bekannte .mpr-Referenzdateien
- [ ] Homag-spezifische Validierung

## Phase 5: Toolpath-Visualisierung & Werkzeug-Datenbank

**Status**: IN ARBEIT 🟡 (24.03.2026) — KRITISCH für professionellen Einsatz

> **Entscheidung 23.03.2026:** Toolpath-Preview und Werkzeug-Datenbank sind nicht optional.
> Ohne visuelle Kontrolle = Black Box. Kein Schreiner vertraut einer Black Box.

### Werkzeug-Datenbank (Tool Library)
- [x] `ToolDefinition` Datenmodell: Name, Typ (Fräser/Bohrer/Säge), Nenndurchmesser, Schneidenlänge, Gesamtlänge, Drehzahl, Vorschub, E-Code ✅
- [x] Werkzeug-Datenbank pro MachineProfile (JSON Import/Export + Persistenz unter `%AppData%`) ✅
- [ ] Werkzeug-Manager Panel im Plugin (CRUD für Werkzeuge)
- [x] E-Code → Werkzeug Mapping in ToolLibrary/Preview-Planung ✅
- [x] Werkzeug-Vorschläge pro Bearbeitungstyp (Tasche → Fräser, Bohrung → Bohrer) ✅
- [ ] Mehrfach-Werkzeug pro Operation: Schruppfräser (E010) + Schlichtfräser (E015)

### Schrupp-/Schlicht-Strategie
- [ ] Pro Bearbeitung: Schrupp-Werkzeug + Schlicht-Werkzeug wählbar
- [x] Aufmass-Parameter für Schruppen (globales Preview-Aufmass im Panel) ✅
- [x] Zustellung (Stepdown) pro Werkzeug konfigurierbar im Datenmodell ✅
- [x] Automatische Multi-Pass Generierung (heuristische Rough/Finish-Preview-Planung) ✅
- [x] Reihenfolge: Alle Schrupp-Ops zuerst, dann Schlicht-Ops im Preview-Plan ✅

### Toolpath-Preview (Stufe 1 — Visualisierung)
- [x] Nach Berechnung: Werkzeugbahnen als Rhino-Curves auf Preview-Layer generieren ✅
- [ ] Farbkodierung:
  - Blau = Eilgang (Rapid)
  - Rot = Vorschub-Fräsen (Feed)
  - Orange = Schruppen
  - Grün = Schlichten
  - Gelb = Bohren (Eintauchen)
  - Gestrichelt = Anfahrwege / Rückzug
- [x] Bohrpunkte als Kreise mit Durchmesser-Darstellung ✅
- [x] Maschinen-Makros (CLAMEX, RNT) als vereinfachte Pfade ✅
- [x] Preview-Layer ein/ausblenden über Rhino-Layerbaum; "Vorschau löschen" im Panel ✅
- [x] "Vorschau generieren" Button im Panel ✅

### Toolpath-Preview (Stufe 2 — Interaktiv)
- [ ] Bearbeitungsreihenfolge per Drag&Drop ändern
- [ ] Anfahrstrategie pro Operation wählen (direkt, tangential, helikal)
- [ ] Zustellung visuell anpassen
- [ ] Werkzeug-Auswahl pro Operation im Preview ändern
- [ ] Simulation: Schritt-für-Schritt durchspielen (wie Video-Player)

### Core UI
- [ ] Maschinenauswahl im Export-Dialog (SCM / Biesse / Homag)
- [ ] Batch-Export (mehrere Formate gleichzeitig)
- [ ] Layer-Cheatsheet im Plugin (Schnellreferenz für Layer-Konventionen)
- [ ] Validierung & Warnings Panel (Layer-Mismatches, Geometrie-Probleme)

### Makro-Bibliothek (inspiriert von NC-HOPS / Maestro Lab)
- [ ] Vorgefertigte Block-Templates für Standard-Bearbeitungen:
  - Topfband 35mm (Bohrbild + Tasche)
  - Lochreihe System 32 (Regalbodenträger)
  - Rückwandnut Standard (10mm ab Kante)
  - Exzenter/Minifix (Bohrbild Korpus + Boden)
  - Scharnier-Bohrbild
- [ ] Template-Browser Panel (Drag&Drop auf Werkstück)
- [ ] Custom-Makro-Editor (User kann eigene Templates speichern)

### Optimierungen (inspiriert von NC-HOPS / woodWOP)
- [ ] Bohr-Optimierung (Clustering, kürzeste Verfahrwege)
- [ ] Werkzeugwechsel-Optimierung (gleiche Werkzeuge gruppieren)
- [ ] Verfahrweg-Optimierung (minimale Leerwege)
- [ ] Bearbeitungsreihenfolge-Optimierung (Bohren → Nuten → Taschen → Konturen)

## Phase 6: Yak Package & Distribution

### Build & Release
- [ ] Yak `manifest.yml` finalisieren (Name, Version, Keywords)
- [ ] Plugin-Icon (64x64 PNG)
- [ ] Build-Pipeline: `dotnet build -c Release` → `yak build --platform win`
- [ ] CI/CD: GitHub Actions mit `yak.exe` + `YAK_TOKEN`
- [ ] Test-Deployment auf `test.yak.rhino3d.com`
- [ ] Publish auf `yak.rhino3d.com`

### Dokumentation & Onboarding
- [ ] Benutzer-Dokumentation (Layer-Konventionen, Workflow, Troubleshooting)
- [ ] Beispieldateien (.3dm + erwartete Ausgaben pro Format)
- [ ] Video-Tutorial: "Vom Rhino-Modell zum CNC-Programm"
- [ ] Layer-Template .3dm (alle Layer voreingerichtet)
- [ ] Quick-Start Guide pro Maschinenformat (SCM / Biesse / Homag)

### Pricing & Distribution
- [ ] Free Tier: XCS-Export (SCM) — Einstieg
- [ ] Pro Tier: Alle Formate + Optimierungen + Makro-Bibliothek
- [ ] Food4Rhino Listing mit Screenshots + Feature-Vergleich
- [ ] Stripe Integration (via Solid AI)

## Phase 7: Nesting (Premium)

- [ ] Manuelles Nesting: Teile auf Rohplatte platzieren
- [ ] Auto-Nesting: Optimale Platzierung (Verschnitt minimieren)
- [ ] Maserungsrichtung beachten
- [ ] Haltebrücken/Tabs automatisch platzieren
- [ ] Schnittfolge-Optimierung
- [ ] Saugnapf-Positionsvorschlag (inspiriert von NC-HOPS WorkCenter)

## Phase 8: Erweiterte Features

### Parametrische Programme (inspiriert von woodWOP / NC-HOPS)
- [ ] Variablen-Support in Layer-Namen (z.B. `CUT_E010_Z{DZ}`)
- [ ] Formel-Support (z.B. Tiefe = "Materialdicke - 2")
- [ ] Ein Programm für variable Werkstückgrössen
- [ ] Parametrische Makro-Bibliothek (Korpus, Schublade, Tür)

### Erweiterte Bearbeitungen
- [ ] Horizontalbohrungen (Seitenbearbeitung)
- [ ] 5-Achs-Operationen
- [ ] Kantenbearbeitung (Anleimen-Sequenz)
- [ ] Sägeschnitte (Kreissäge-Makros)
- [ ] Tabs/Mikrostege via Layer-Flags (`_TAB3x10`)
- [ ] Lead-In/Out als Layer-Schalter (`_LI3_R`, `_LO3_T`)
- [ ] Unterseite (Bottom Workplane), Flip & Rotation

### Integration & Anbindung
- [ ] Grasshopper-Komponente für parametrische Workflows
- [ ] ERP-Import (Stückliste → automatische CNC-Programme)
- [ ] Barcode/QR-Label-Generierung pro Werkstück
- [ ] Material- & Werkzeug-DB (CSV/JSON Import)
- [ ] Kollisionserkennung (Premium, inspiriert von bSolid)

---

## Backlog / Ideen

- Weitere Postprozessoren (Heidenhain, Morbidelli) als zusätzliche Emitter
- BPP-Ausgabe für ältere Biesse-Installationen
- MPRXE-Ausgabe für neueste Homag-Maschinen (woodWOP 8+)
- Cloud-Export (CNC-Dateien direkt an Maschine senden)
- Multi-User: Shared Makro-Bibliothek im Netzwerk
- AI-gestützte Feature-Erkennung (3D-Modell → automatisch Bearbeitungen zuweisen)
