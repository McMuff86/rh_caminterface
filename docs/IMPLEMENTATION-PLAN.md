# Implementation Plan: 3D-to-CNC Pipeline

**Datum:** 23. März 2026  
**Version:** 1.0  
**Basis:** TECHNICAL-ARCHITECTURE.md, BLOCK-LIBRARY-SPEC.md, MIGRATION-STRATEGY.md

---

## Übersicht: 5 Sprints

```
Sprint 1 (Foundation)     → Datenmodell + Grundgerüst         ~1 Woche
Sprint 2 (Block-Scan)     → Starter-Blöcke + UserText-Parsing ~1.5 Wochen
Sprint 3 (Plate-Detect)   → Platten-Erkennung + CoordTransform ~2 Wochen
Sprint 4 (Multi-Export)   → Multi-Platte Export + UI           ~1.5 Wochen
Sprint 5 (Validation)     → Testing gegen Produktionsdaten     ~1 Woche
                                                        Total: ~7 Wochen
```

---

## Sprint 1: Datenmodell + Block-Detection Grundgerüst ✅ COMPLETE (23.03.2026)

**Ziel:** Core-DTOs definieren, Pipeline-Skeleton aufbauen, Tests für Datenmodell.
**Ergebnis:** Alle Tasks implementiert. 90+ neue Tests, 0 Warnings, 0 Regressions.

### Tasks

| # | Task | Modul | Abhängigkeit | Aufwand |
|---|------|-------|-------------|---------|
| 1.1 | `Plate` Record definieren | Core/Models/ | — | 2h |
| 1.2 | `Machining` Records definieren (alle Subtypen) | Core/Models/ | — | 3h |
| 1.3 | `FittingBlock` Record definieren | Core/Models/ | — | 2h |
| 1.4 | `ExportJob` Record definieren | Core/Models/ | — | 1h |
| 1.5 | `BlockUserTextSchema` (Validation, Constants) | Core/Blocks/ | 1.3 | 2h |
| 1.6 | `MachiningFactory` Skeleton (Dispatch-Pattern) | Core/Blocks/ | 1.2, 1.3 | 3h |
| 1.7 | `EmitterRouter` Skeleton (Switch-Expression) | Core/Pipeline/ | 1.2 | 4h |
| 1.8 | `MachiningBuilder.MergeAndDeduplicate()` | Core/Pipeline/ | 1.2 | 3h |
| 1.9 | Unit Tests für alle DTOs + Schema-Validierung | Tests/ | 1.1–1.5 | 3h |
| 1.10 | Unit Tests für EmitterRouter mit Mock-Emitter | Tests/ | 1.7 | 3h |

**Deliverables:** ✅ Alle erreicht
- ✅ Kompilierbares Core/Models/ Namespace (6 Dateien)
- ✅ Kompilierbares Core/Blocks/ Namespace (3 Dateien)
- ✅ Kompilierbares Core/Pipeline/ Namespace (5 Dateien)
- ✅ 90+ neue Unit Tests, alle grün (Ziel war 15+)
- ✅ Bestehende 80+ Tests: immer noch grün (0 Regression)

**Erkenntnisse:**
- Core.csproj brauchte Anpassung: Eigene Dateien werden per SDK auto-included,
  linked files aus Plugin/Core/ bleiben per explizitem Compile-Include
- InternalsVisibleTo für Tests-Zugriff auf interne Methoden nötig
- MachiningFactory: CUT/POCKET/GROOVE als Stubs (brauchen Geometrie aus Rhino)
- EmitterRouter: Macro-Ausgabe als Kommentar-Placeholder (konkrete Implementierung Sprint 2+)

**Risiken:** Keine — reine Core-Arbeit ohne Rhino-Abhängigkeit.

---

## Sprint 2: Starter-Blöcke + UserText-Parsing ✅ COMPLETE (23.03.2026)

**Ziel:** Block-Scanner implementieren, erste Blöcke erstellen, Block→Machining Konvertierung für DRILL und DRILLPATTERN.
**Ergebnis:** Alle Kern-Tasks implementiert. 36 neue Tests, 0 Regressions. BlockScanner, AssignmentResolver, StarterBlockDefinitions, BlockAwareExportService, UI-Erweiterung.

### Tasks

| # | Task | Modul | Abhängigkeit | Aufwand |
|---|------|-------|-------------|---------|
| 2.1 | `BlockScanner.ScanDocument()` implementieren | Plugin/BlockScanning/ | Sprint 1 | 4h |
| 2.2 | `BlockScanner.ScanSelection()` implementieren | Plugin/BlockScanning/ | 2.1 | 1h |
| 2.3 | `AssignmentResolver.Resolve()` (Layer-Match) | Plugin/BlockScanning/ | 2.1 | 3h |
| 2.4 | `MachiningFactory.CreateDrill()` implementieren | Core/Blocks/ | Sprint 1 | 2h |
| 2.5 | `MachiningFactory.CreateDrillPattern()` implementieren | Core/Blocks/ | Sprint 1 | 2h |
| 2.6 | `MachiningFactory.CreateMacro()` Skeleton (CLAMEX prep) | Core/Blocks/ | Sprint 1 | 3h |
| 2.7 | Starter-Block: `Topfband_35.3dm` erstellen | Plugin/BlockLibrary/ | — | 2h |
| 2.8 | Starter-Block: `Lochreihe_32.3dm` erstellen | Plugin/BlockLibrary/ | — | 2h |
| 2.9 | Starter-Block: `Duebel_8x30.3dm` erstellen | Plugin/BlockLibrary/ | — | 2h |
| 2.10 | Starter-Block: `Montageverbinder_35.3dm` erstellen | Plugin/BlockLibrary/ | — | 1h |
| 2.11 | `BlockLibraryService.EnsureStarterBlocks()` | Plugin/BlockLibrary/ | 2.7–2.10 | 3h |
| 2.12 | `ExportService` erweitern: Block-Detection Pfad | Services/ | 2.1–2.6 | 4h |
| 2.13 | Feature Flag "Block-Detection" in UI | UI/ | 2.12 | 2h |
| 2.14 | Integration Test: Legacy + Blocks gemischt | Tests/ | 2.12 | 3h |
| 2.15 | Unit Tests: BlockScanner, MachiningFactory | Tests/ | 2.1–2.6 | 3h |

**Deliverables:** ✅ Alle erreicht
- ✅ BlockScanner findet CNC_*-Blöcke im Dokument
- ✅ 5 Starter-Blöcke als Code-Definitionen (Topfband_35, Lochreihe_32, Duebel_8x30, Duebel_8x30_Stirn, CLAMEX_P14)
- ✅ DRILL, DRILLPATTERN, MACRO, HDRILL Blöcke → Machinings → XCS Code
- ✅ Feature Flag in UI (default ON — BlockDetection Checkbox)
- ✅ BlockAwareExportService: Legacy + Block-Detection, Fallback
- ✅ 36 neue Tests, alle grün (Total: 183 Tests)

**Erkenntnisse:**
- Starter-Blöcke als Code-Definitionen statt .3dm-Dateien: besser testbar, kein Rhino nötig
- .3dm-Dateien können in Sprint 3 ergänzt werden wenn Rhino auf Windows verfügbar
- AssignmentResolver: Reiner Layer-Match genügt für Phase 2, Proximity kommt Sprint 3
- BlockAwareExportService schreibt Block-Infos aktuell als Kommentare ans Dateiende
  (vollständige Integration in CNC-Output erst mit Plate-Detection in Sprint 3)

**Risiko:** Mitigiert — Starter-Blöcke als Code-Definitionen statt .3dm eliminiert Rhino-Abhängigkeit für Tests.

---

## Sprint 3: Platten-Erkennung + Koordinaten-Transformation ✅ COMPLETE (23.03.2026)

**Ziel:** 3D-Solids als Platten erkennen, Koordinaten von Weltkoordinaten in Platten-Lokalsystem transformieren.
**Ergebnis:** Alle Kern-Tasks implementiert. 133 neue Tests (316 total), 0 Regressions. ClamexMacroBuilder validiert gegen Produktions-XCS-Dateien (exakter String-Vergleich).

### Tasks

| # | Task | Modul | Abhängigkeit | Aufwand |
|---|------|-------|-------------|---------|
| 3.1 | `PlateDetector.DetectPlates()` — Solid/Extrusion→Plate | Plugin/PlateDetection/ | Sprint 1 | 6h |
| 3.2 | Platten-Dicke aus BoundingBox ableiten | Plugin/PlateDetection/ | 3.1 | 2h |
| 3.3 | Platten-Orientierung (Normal) bestimmen | Plugin/PlateDetection/ | 3.1 | 4h |
| 3.4 | `PlateOrigin` berechnen (lokales Koordinatensystem) | Plugin/PlateDetection/ | 3.3 | 4h |
| 3.5 | `CoordinateTransformer.WorldToPlateLocal()` | Plugin/PlateDetection/ | 3.4 | 3h |
| 3.6 | AssignmentResolver erweitern: Proximity-basiert | Plugin/BlockScanning/ | 3.1, Sprint 2 | 3h |
| 3.7 | Starter-Block: `CLAMEX_P14.3dm` + Makro-Mapping | Plugin/BlockLibrary/ | Sprint 2 | 4h |
| 3.8 | Starter-Block: `Exzenter_15.3dm` (Multi-Op) | Plugin/BlockLibrary/ | Sprint 2 | 3h |
| 3.9 | `MachiningFactory.CreateMacro()` für SawCut_Lamello | Core/Blocks/ | Sprint 2 | 6h |
| 3.10 | CLAMEX Parameter-Template (48 Parameter) | Core/Blocks/ | 3.9 | 4h |
| 3.11 | Test: Platte flach (Z=0) → Identity-Transform | Tests/ | 3.1–3.5 | 2h |
| 3.12 | Test: Platte aufrecht (Seite) → korrekte Rotation | Tests/ | 3.1–3.5 | 3h |
| 3.13 | Test: CLAMEX Block → SawCut_Lamello XCS Output | Tests/ | 3.9–3.10 | 3h |

**Deliverables:** ✅ Alle Kern-Deliverables erreicht
- ✅ PlateDetector erkennt Solids als Platten (Dicke, LPX, LPY, auto-Orientierung)
- ✅ PlateOrigin für flache und aufrechte Platten (XZ, YZ)
- ✅ CoordinateTransformer funktioniert für alle 3 Fälle (flat, upright XZ, upright YZ)
- ✅ CLAMEX_P14 Block → SawCut_Lamello Makro-Output (exakt wie Produktion!)
- ✅ ClamexMacroBuilder: Vertical + Horizontal, verschiedene E-Codes
- ✅ AssignmentResolver: Proximity-basiert erweitert
- ✅ Multi-Plate Export Pipeline: PlateDetector → BlockScanner → Resolver → Transform → Factory → Router → per-plate File
- ✅ EmitterRouter: Volle SawCut_Lamello CreateMacro Emission
- ⏩ Exzenter_15 Block verschoben auf Sprint 4 (benötigt Multi-Op Geometrie)
- ⏩ UI-Erweiterungen verschoben auf Sprint 4

**Erkenntnisse:**
- CoordinateTransformer ist pure Mathematik (Dot-Products) → lebt in Core, nicht Plugin
- ClamexMacroBuilder: Production-Vergleich als Test-Strategie extrem effektiv (Exact-Match!)
- Vertical vs. Horizontal CLAMEX: Nicht nur verschiedene E-Codes, auch unterschiedliche P-Werte
  (P16: 2 vs -1, P24: 0 vs -1, P31: "3" vs null, P40: 4 vs 2, Depth: 14.3 vs 14)
- Mittelseite-Varianten haben ClamexDepth=10.3 statt 14.3 → zukünftig parameterisieren

**Risiko:** Mitigiert — achsparallele Platten implementiert. Schräge Platten bleiben Zukunft.

---

## Sprint 4: Multi-Platte Export + UI Erweiterung ✅ COMPLETE (23.03.2026, Code Complete)

**Ziel:** Mehrere Platten aus einem 3D-Modell erkennen und pro Platte eine separate CNC-Datei exportieren.
**Ergebnis:** `ExportService3D`, Auto/Legacy/3D Routing, Baumansicht im ExportPanel, Batch-Export und Report implementiert. `dotnet build` grün, Sprint-4 Tests + gezielte Regressionsläufe grün.

### Tasks

| # | Task | Modul | Abhängigkeit | Aufwand |
|---|------|-------|-------------|---------|
| 4.1 | `ExportService3D` — neuer Service für 3D-Pipeline | Services/ | Sprint 3 | 6h |
| 4.2 | Multi-Platte Output (Ordner + Dateinamen) | Services/ | 4.1 | 3h |
| 4.3 | ExportPanel erweitern: Multi-Platte Mode | UI/ | 4.1 | 4h |
| 4.4 | Platten-Liste in UI anzeigen (Checkbox pro Platte) | UI/ | 4.1 | 4h |
| 4.5 | Export-Modus Selector (Auto/Legacy/3D) | UI/ | 4.3 | 2h |
| 4.6 | Auto-Detection: Welcher Modus passt? | Services/ | 4.1 | 3h |
| 4.7 | Batch-Export für alle Platten | Services/ | 4.1, 4.2 | 2h |
| 4.8 | Export-Report: "7 Platten, 28 Bearbeitungen exportiert" | UI/ | 4.7 | 2h |
| 4.9 | Integration Test: Einfacher Korpus (4 Platten) | Tests/ | 4.1–4.7 | 4h |
| 4.10 | Integration Test: Legacy-Modus unverändert | Tests/ | 4.1 | 2h |

**Deliverables:** ✅ Erreicht
- ✅ 3D-Modell → pro Platte eine `.xcs`/`.cix` Datei
- ✅ UI: Platten-Auswahl, Export-Modus, Report
- ✅ Automatische Modus-Erkennung
- ✅ Legacy-Export funktioniert weiterhin identisch
- ✅ Tests für ExportModeResolver, BatchExportPlanner, 4-Platten-Planung und gezielte Regressions-Suiten

**Zusätzliche Umsetzung:**
- `ExportPanel` nutzt eine **Baumansicht**: Root = Platte, Children = zugeordnete Blöcke
- Export-Ziel wechselt je nach Modus automatisch zwischen Datei- und Ordner-Auswahl
- `ConfigurableMachineProfile` erlaubt UI-Offsets auch im 3D-/CIX-Pfad

**Risiko:** UI-Komplexität. Mitigation: flache Baumansicht mit nur einer Hierarchieebene (`Platte → Blöcke`), keine tiefe Verschachtelung.

---

## Sprint 5: Testing gegen echte Produktionsdaten

**Ziel:** Validierung der gesamten Pipeline gegen echte CAD+T DWGs und bekannte XCS-Referenzdateien.
**Zwischenstand (24.03.2026):** Automatisierte Batch-Validierung gestartet. Duplicate-sichere Dateinamen für gleichnamige Produktionsplatten, LayerPath-basierte Selektion, echter `AssignmentResolver`-Test gegen die Plugin-Klasse und ein 24-Platten-Regressionstest sind umgesetzt. DWG-abgeleitete Fixtures: `Staub_SockelMont.xcs`, `NEW_Fertigauszug_Legrabox.xcs`, **`Staub_Seite_links.xcs`** (Kontur + RNT + Bohrungen + Lochreihen; `Plate.PreserveMachiningOrder` + korrigierte `CreatePattern`/`CreateDrill`-Reihenfolge im `XilogEmitter`) sowie **`Staub_Boden.xcs`** für `CreateWorkplane()`-basierte Horizontalbohrungen. Dabei wurde der `EmitterRouter` auf den produktionskonformen Horizontaldrill-Pfad umgestellt (korrekte L/R-Rotationen, kein doppeltes `SelectWorkplane`) und ein `NameService`-Bug bei truncierten 31-Zeichen-Kollisionen behoben, der sonst bei freien Ebenen in einen Endlos-Loop lief. Komplexe Produktionsplatten mit `CreateBladeCut` / `CreateSectioningMillingStrategy` bleiben offen; der Gap ist über `NEW_Schubladen_Doppel_1.xcs` dokumentiert. Rhino-Smoke-Tests bleiben offen.

### Tasks

| # | Task | Modul | Abhängigkeit | Aufwand |
|---|------|-------|-------------|---------|
| 5.1 | Test-Modell erstellen: Putzschrank (basierend auf DWG) | Tests/ | Sprint 4 | 4h |
| 5.2 | Test-Modell erstellen: Schubladenkorpus (Legrabox) | Tests/ | Sprint 4 | 4h |
| 5.3 | XCS-Referenz-Vergleich: Block-Export vs. CAD+T Output | Tests/ | 5.1, 5.2 | 4h |
| 5.4 | Edge Cases: Leere Platte, Platte ohne Blöcke | Tests/ | Sprint 4 | 2h |
| 5.5 | Edge Cases: Block ohne gültige CNC_Type | Tests/ | Sprint 4 | 1h |
| 5.6 | Edge Cases: Block zwischen zwei Platten | Tests/ | Sprint 4 | 2h |
| 5.7 | Performance-Test: 20+ Platten in einem Modell | Tests/ | Sprint 4 | 2h |
| 5.8 | Regressions-Tests: Alle 80+ bestehenden Tests grün | Tests/ | — | 1h |
| 5.9 | Dokumentation updaten: AGENTS.md, CONTEXT-HANDOFF.md | Docs/ | 5.1–5.8 | 2h |
| 5.10 | ROADMAP.md aktualisieren | Docs/ | 5.1–5.8 | 1h |

**Deliverables:**
- Erste DWG-basierte Produktionsfixtures für einfache Referenzteile (heute unterstützt)
- Normalisierte XCS-Output-Vergleiche: Unsere Plate-/3D-Pipeline vs. CAD+T Referenz
- Dokumentierter Feature-Gap für komplexe BladeCut-/Sectioning-Teile
- Alle Edge Cases abgedeckt
- 100+ Tests grün
- Dokumentation aktuell

**Bereits erreicht in diesem Sprint-5-Block:**
- Duplicate-sichere Batch-Dateinamen bei mehrfach vorkommenden Plattennamen
- Eindeutige Multi-Platte-Selektion über `LayerPath`, wenn vorhanden
- 4 neue Validierungs-Tests für Produktionsnamen, Sanitizing-Kollisionen und 24-Platten-Regression
- Echte `AssignmentResolver`-Tests statt lokaler Test-Nachbildung
- Proximity-Zuweisung für Blöcke zwischen zwei Platten auf closest-face Logik umgestellt
- DWG-abgeleitete Fixtures + Produktionsvergleichstests für `SockelMont` und `Fertigauszug_Legrabox`
- BladeCut-/Sectioning-Gap für `Schubladen_Doppel` als expliziter Referenztest abgesichert
- Produktionsvergleich für `Staub_Boden` deckt jetzt Horizontalbohrungen (`CreateWorkplane`) inkl. korrekter Router-Emission ab

**Risiko:** Echte Produktionsdaten können Fälle enthalten die wir nicht bedacht haben. Mitigation: Inkrementell testen, Fehler dokumentieren, in nächsten Sprint-Zyklus aufnehmen.

---

## Dependency-Graph

```
Sprint 1: [Datenmodell]
    │
    ├── 1.1-1.4 Models (parallel)
    ├── 1.5 Schema (nach 1.3)
    ├── 1.6 MachiningFactory (nach 1.2, 1.3)
    ├── 1.7 EmitterRouter (nach 1.2)
    ├── 1.8 MachiningBuilder (nach 1.2)
    └── 1.9-1.10 Tests (nach alles andere)

Sprint 2: [Block-Scan] ← abhängig von Sprint 1
    │
    ├── 2.1-2.3 BlockScanner + Resolver (parallel)
    ├── 2.4-2.6 MachiningFactory Impls (parallel mit 2.1-2.3)
    ├── 2.7-2.10 Starter-Blöcke (unabhängig, parallel)
    ├── 2.11 BlockLibraryService (nach 2.7-2.10)
    ├── 2.12 ExportService erweitern (nach 2.1-2.6)
    ├── 2.13 UI Flag (nach 2.12)
    └── 2.14-2.15 Tests (nach alles andere)

Sprint 3: [Plate-Detection] ← abhängig von Sprint 2
    │
    ├── 3.1-3.5 PlateDetector + Transformer (sequentiell!)
    ├── 3.6 Resolver erweitern (nach 3.1)
    ├── 3.7-3.8 Weitere Blöcke (parallel mit 3.1-3.5)
    ├── 3.9-3.10 CLAMEX Mapping (parallel mit 3.1-3.5)
    └── 3.11-3.13 Tests (nach alles andere)

Sprint 4: [Multi-Export] ← abhängig von Sprint 3
    │
    ├── 4.1-4.2 ExportService3D (sequentiell)
    ├── 4.3-4.5 UI Erweiterung (nach 4.1)
    ├── 4.6-4.8 Auto-Detection + Batch (nach 4.1)
    └── 4.9-4.10 Tests (nach alles andere)

Sprint 5: [Validation] ← abhängig von Sprint 4
    │
    └── Alle Tasks parallel
```

---

## Aufwands-Zusammenfassung

| Sprint | Tasks | Geschätzter Aufwand | Kumulativ |
|--------|-------|--------------------:|----------:|
| 1 | 10 | ~26h (~3.5 Tage) | 3.5 Tage |
| 2 | 15 | ~36h (~5 Tage) | 8.5 Tage |
| 3 | 13 | ~47h (~6 Tage) | 14.5 Tage |
| 4 | 10 | ~32h (~4 Tage) | 18.5 Tage |
| 5 | 10 | ~23h (~3 Tage) | 21.5 Tage |
| **Total** | **58** | **~164h (~21.5 Tage)** | **~7 Wochen** |

**Annahme:** Nacht-Sessions à 4-6 produktive Stunden, 3 Sessions pro Woche.

---

## Definition of Done (pro Sprint)

- [ ] Alle geplanten Tasks implementiert (nur Docs/Interfaces in diesem Plan → Code kommt in Nacht-Sessions)
- [ ] Alle neuen Tests grün
- [ ] Alle bestehenden Tests grün (Regression!)
- [ ] Code kompiliert ohne Warnings
- [ ] Dokumentation aktuell
- [ ] Branch erstellt und gepusht
- [ ] Morgen-Briefing für Adi vorbereitet

---

## Quick Wins (parallel, ohne Sprint-Abhängigkeit)

Diese Tasks bringen sofort Mehrwert und können jederzeit parallel gemacht werden:

| Task | Aufwand | Wert |
|------|---------|------|
| Remaining MSL-Befehle: CreateBladeCut, CreateSectioningMillingStrategy | M | Produktions-Kompatibilität |
| Homag-Emitter Grundgerüst (EmitHeader, EmitDrill) | M | Drittes Maschinenformat |
| Biesse-Emitter erweitern (POCKET, DRILLROW) | M | Vollständiger Biesse-Support |
| Block_Template.3dm erstellen (User-Anleitung) | S | User Onboarding |
| Yak Package finalisieren und testen | S | Distribution |

---

*Dieser Plan ist die Grundlage für Nacht-Sessions. Sprint-Backlog in `~/clawd/sprints/active.md` tracken.*

---

## Sprint 6: Werkzeug-Datenbank (nach Sprint 5)

**Ziel:** Werkzeugverwaltung im Plugin — E-Codes werden zu echten Werkzeugen mit Parametern

**Status 24.03.2026:** Werkzeug-DB als echte Basis implementiert. `ToolDefinition` wurde um Halter-Zuordnung und Schnittparameter erweitert, `ToolHolderDefinition` ergänzt die Library, Default-Libraries enthalten jetzt Werkzeuge + Halter pro Maschine, und ein Eto-basierter Werkzeugmanager für CRUD von Werkzeugen/Haltern ist aus dem `ExportPanel` erreichbar. Der Dialog nutzt jetzt resizable Split-Views plus schematische Live-Vorschau für Halter/Werkzeug inklusive Corner-Radius-Darstellung. `RNT066` wird in den Defaults als Rueckwandnuter-Scheibe mit fixer Aggregatbindung und nur linearen X/Y-Fahrwegen geführt; Bohrer werden als feste Werkzeuge im Bohraggregat mit zylindrischer Darstellung geführt. Noch offen sind per-Operation Overrides, echte Tool-Assembly-/Magazinlogik und ein vollwertiges 3D-Preview der Assemblies.

| Task | Beschreibung | Aufwand |
|------|-------------|---------|
| 6.1 | `ToolDefinition` Record: Name, Typ, Ø, Schneidenlänge, Drehzahl, Vorschub, E-Code | 2h |
| 6.2 | `ToolLibrary` Klasse: CRUD, JSON Import/Export, pro MachineProfile | 4h |
| 6.3 | Werkzeug-Manager Panel (Eto.Forms): Liste, Add/Edit/Delete, Halter-Zuordnung, Parameter-Editor | 6h |
| 6.4 | E-Code → Werkzeug Mapping in Emittern | 3h |
| 6.5 | Werkzeug-Vorschläge pro Bearbeitungstyp (Default-Zuordnung) | 3h |
| 6.6 | Tests | 3h |

**Geschätzt:** ~3 Tage

## Sprint 7: Schrupp-/Schlicht-Strategie (nach Sprint 6)

**Ziel:** Pro Bearbeitung Schrupp- und Schlichtwerkzeug + Aufmass konfigurierbar

**Status 24.03.2026:** Fundament implementiert. `MachiningStrategy` + heuristische Rough/Finish-Planung existieren im Core; das ist aktuell Preview-/Planungslogik, noch keine echte Export-Multi-Pass-Geometrie mit Offset-Konturen.

| Task | Beschreibung | Aufwand |
|------|-------------|---------|
| 7.1 | `MachiningStrategy` Record: RoughTool, FinishTool, Allowance, StepDown | 2h |
| 7.2 | Multi-Pass Generierung: Schrubben (mit Aufmass) → Schlichten (Endmass) | 6h |
| 7.3 | Offset-Berechnung für Schrupp-Kontur (Aufmass nach aussen) | 4h |
| 7.4 | UI: Pro Operation Werkzeug-Auswahl (Schruppen/Schlichten) | 4h |
| 7.5 | Reihenfolge-Logik: Alle Schrupp-Ops zuerst, dann Schlicht-Ops | 3h |
| 7.6 | Tests + Validierung gegen Produktionsdaten | 3h |

**Geschätzt:** ~3-4 Tage

## Sprint 8: Toolpath-Visualisierung (nach Sprint 7)

**Ziel:** Werkzeugbahnen als farbkodierte Curves im Rhino-Viewport sichtbar machen

**Status 24.03.2026:** Stufe 1 teilweise umgesetzt. `ToolpathPreviewService` erzeugt farbkodierte Preview-Curves auf Rhino-Layern, inklusive Bohrpunkte, Rapid-Linien und vereinfachter Makro-Pfade; Buttons "Vorschau generieren" / "Vorschau löschen" sind im `ExportPanel`. Interaktive Bearbeitung/Simulation fehlt noch.

**UI-Polish 24.03.2026:** Das `ExportPanel` wurde auf ein kompakteres 2-Spalten-Dashboard refactored. Analyse-/Setup-Bereiche sind jetzt einklappbar, die rechte Sidebar bündelt Einstellungen + Aktionen, und Report/Log liegen gemeinsam in einer Status-Ansicht mit Tabs. Ziel: weniger vertikales Scrollen bei gleicher Funktionsdichte.

| Task | Beschreibung | Aufwand |
|------|-------------|---------|
| 8.1 | `ToolpathVisualizer` Klasse: Machinings → Rhino Curves auf Preview-Layer | 6h |
| 8.2 | Farbkodierung: Rapid=Blau, Feed=Rot, Schruppen=Orange, Schlichten=Grün | 3h |
| 8.3 | Bohrpunkte als Kreise mit Ø-Darstellung | 2h |
| 8.4 | Makro-Pfade als vereinfachte Geometrie (CLAMEX, RNT) | 4h |
| 8.5 | "Vorschau generieren" / "Vorschau löschen" Buttons im Panel | 2h |
| 8.6 | Preview-Layer Management (ein/aus, auto-cleanup) | 2h |
| 8.7 | Werkzeug-Ø in Preview berücksichtigen (aus ToolLibrary) | 3h |
| 8.8 | Tests + visueller Abgleich | 3h |

**Geschätzt:** ~4 Tage

### Dependencies
```
Sprint 3 (Platten-Erkennung) 
  → Sprint 4 (Multi-Export + UI)
    → Sprint 5 (Validierung)
      → Sprint 6 (Werkzeug-DB)
        → Sprint 7 (Schrupp/Schlicht)
          → Sprint 8 (Toolpath-Visualisierung)
```
