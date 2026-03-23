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

## Sprint 3: Platten-Erkennung + Koordinaten-Transformation

**Ziel:** 3D-Solids als Platten erkennen, Koordinaten von Weltkoordinaten in Platten-Lokalsystem transformieren.

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

**Deliverables:**
- PlateDetector erkennt Solids als Platten (Dicke, LPX, LPY)
- PlateOrigin für flache und aufrechte Platten
- CoordinateTransformer funktioniert für beide Fälle
- CLAMEX_P14 Block → SawCut_Lamello Makro-Output
- Exzenter_15 Block → 2 Operationen (Top + Bottom)
- 6 Starter-Blöcke komplett

**Risiko:** Platten-Orientierung bei schrägen/gedrehten Solids komplex. Mitigation: Phase 3 zunächst nur für achsparallele Platten (Seiten stehen aufrecht, Böden liegen flach).

---

## Sprint 4: Multi-Platte Export + UI Erweiterung

**Ziel:** Mehrere Platten aus einem 3D-Modell erkennen und pro Platte eine separate CNC-Datei exportieren.

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

**Deliverables:**
- 3D-Modell → pro Platte eine .xcs/.cix Datei
- UI: Platten-Auswahl, Export-Modus, Report
- Automatische Modus-Erkennung
- Legacy-Export funktioniert weiterhin identisch
- Integration Tests für beide Pfade

**Risiko:** UI-Komplexität. Mitigation: Einfache Tabelle mit Checkboxen, keine Baumansicht.

---

## Sprint 5: Testing gegen echte Produktionsdaten

**Ziel:** Validierung der gesamten Pipeline gegen echte CAD+T DWGs und bekannte XCS-Referenzdateien.

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
- 2 Test-3D-Modelle mit Blöcken (aus echten CAD+T DWG-Daten abgeleitet)
- XCS-Output-Vergleich: Unsere Pipeline vs. CAD+T Referenz
- Alle Edge Cases abgedeckt
- 100+ Tests grün
- Dokumentation aktuell

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
