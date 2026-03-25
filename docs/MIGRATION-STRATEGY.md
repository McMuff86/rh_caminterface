# Migration-Strategie: 2D Layer → Block-basiert → 3D Pipeline

**Datum:** 23. März 2026  
**Version:** 1.0  
**Kernprinzip:** Rückwärtskompatibel. Nichts bricht. Jede Phase ist additiv.

---

## Übersicht

```
Phase 1 (BLEIBT)    Phase 2 (AKTIV)          Phase 3 (AKTIV)
2D Layer ─────────── + Block-Detection ──────── + 3D Solid-Pipeline
    ✅ funktioniert      ✅ parallel nutzbar         ✅ ExportService3D + UI
    ✅ bleibt bestehen   ✅ Koexistenz               ✅ Multi-Platte Export
    ✅ kein Umbau        ✅ fallbackfähig            ✅ Auto/Legacy/3D Routing
```

---

## Phase 1: Layer-Konventionen (Status Quo — bleibt)

### Was bleibt

```
ExportService.ExportWithEmitter()
  → LayerRegex.TryParse*()
  → GeometryUtils.ToPolyPoints()
  → Emit*.Emit()
  → IEmitter.EmitHeader/EmitDrill/...
```

**Keinerlei Änderungen** an:
- `LayerRegex.cs` — alle bestehenden Patterns bleiben
- `Specs.cs` — alle bestehenden DTOs bleiben
- `ExportService.cs` — bestehende Logik wird NICHT verändert
- `Emit*.cs` — bestehende Emitter-Klassen bleiben
- `IEmitter.cs` — Interface wird nur ERWEITERT, nicht geändert
- Alle bestehenden Tests bleiben grün

### Kompatibilitäts-Garantie

Ein Benutzer der **nur 2D-Layer-Konventionen** nutzt, merkt vom Block-System nichts:
1. Export-Dialog zeigt weiterhin "Export" Button
2. WK_PIECE + CUT/DRILL/POCKET Layer → funktioniert wie immer
3. Kein Zwang, Blöcke zu verwenden
4. Kein Zwang, 3D-Modell zu verwenden
5. Performance: Kein Overhead wenn keine Blöcke vorhanden

---

## Phase 2: Block-Detection (additiver Scanner)

### Architektur-Änderung

```
ExportService.ExportWithEmitter()        ← ERWEITERT, nicht ersetzt
  │
  ├─ [bestehend] LayerRegex-basierter Scan → Legacy Machinings
  │
  ├─ [NEU] BlockScanner.ScanDocument()    → List<FittingBlock>
  │         └─ AssignmentResolver          → Blöcke → Platten zuordnen
  │         └─ MachiningFactory            → Block → Machining(s)
  │
  └─ [NEU] MachiningBuilder.MergeAndDeduplicate()
           → Vereinigte Machining-Liste
           → EmitterRouter.GenerateProgram()
```

### Koexistenz-Regeln

1. **Legacy zuerst:** LayerParser läuft immer (wenn WK_PIECE vorhanden)
2. **Blocks additiv:** BlockScanner findet zusätzliche Bearbeitungen
3. **Deduplizierung:** Wenn ein Block dieselbe Position hat wie eine Legacy-Operation → Block gewinnt
4. **Kein Konflikt:** Legacy-Curves auf CUT/DRILL Layern + Blocks auf dem gleichen Layer = beide werden verarbeitet, Duplikate entfernt

### Beispiel: Gemischter Workflow

```
Datei enthält:
  WK_PIECE Layer     → 1 geschlossene Curve (800×400mm)
  CUT_E010_Z19 Layer → 1 geschlossene Curve (Ausschnitt)
  DRILL_D5_Z13 Layer → 3 Kreise (manuelle Bohrungen)
  + Topfband_35 Block auf "Seite_links" Layer → CNC_Type=DRILL
  + Lochreihe_32 Block auf "Seite_links" Layer → CNC_Type=DRILLPATTERN

Ergebnis:
  1. Legacy-Scan findet: WK_PIECE, 1 CUT, 3 DRILLs
  2. Block-Scan findet: 1 Topfband (DRILL), 1 Lochreihe (DRILLPATTERN)
  3. Merge: 1 CUT + 3 DRILLs + 1 Topfband + 1 Lochreihe = 6 Operationen
  4. Export: 1 .xcs Datei mit allen 6 Operationen
```

### Feature Flag

```csharp
// In IMachineProfile oder ExportSettings
public bool EnableBlockDetection { get; set; } = true;  // Aktueller UI-Default
```

**UI:** Checkbox in ExportPanel: `☑ Block-Detection aktivieren`

**Rollout:**
1. Phase 2 initial: Default OFF, nur manuell aktivierbar
2. Aktuell: Default ON, abschaltbar
3. Langfristig: Immer an (kein Flag mehr)

**Stand 24.03.2026:** Starter-Blöcke liegen aktuell als code-definierte `CNC_*` Dictionaries in `RhinoCNCExporter.Core/Blocks/StarterBlocks/StarterBlockDefinitions.cs`. Eine `.3dm`-basierte Block-Library mit `BlockLibraryService` ist weiterhin Future Work, nicht der heutige produktive Pfad.

---

## Phase 3: Volle 3D-Pipeline

### Architektur-Änderung

```
ExportService3D (NEUER Service, ExportService bleibt)
  │
  ├─ PlateDetector.DetectPlates()         → List<Plate> (Solids → Platten)
  │
  ├─ BlockScanner.ScanDocument()          → List<FittingBlock>
  │
  ├─ AssignmentResolver.Resolve()         → Blocks → Plates zuordnen
  │
  ├─ CoordinateTransformer                → 3D-Welt → Platten-Lokal
  │
  ├─ MachiningFactory + MachiningBuilder  → Machinings pro Platte
  │
  └─ EmitterRouter.GenerateProgram()      → Pro Platte eine CNC-Datei
```

### Koexistenz mit Phase 1 + 2

**Automatische Erkennung:**

```
if (doc enthält WK_PIECE Layer + 2D Curves)
  → Phase 1 Pipeline (Legacy ExportService)
  
if (doc enthält WK_PIECE + Blocks)
  → Phase 2 Pipeline (Legacy + Blocks)
  
if (doc enthält 3D Solids/Platten)
  → Phase 3 Pipeline (ExportService3D)
  
if (doc enthält alles gemischt)
  → Phase 3 Pipeline, sobald 3D-Platten erkannt werden
```

**User-Override:** Im ExportPanel:
```
Export-Modus:
  ⚪ Automatisch (empfohlen)
  ⚪ 2D Layer-Konventionen
  ⚪ 3D Multi-Platte
```

### Multi-Platte Export

**Phase 1:** 1 Platte → 1 Datei (WK_PIECE = die Platte)  
**Phase 3:** N Platten → N Dateien (jeder Sublayer = eine Platte)

```
Output-Ordner: ~/CNC-Export/Korpus_1/
  ├── Seite_links.xcs
  ├── Seite_rechts.xcs
  ├── Boden.xcs
  ├── Deckel.xcs
  └── Rueckwand.xcs
```

---

## Feature Flags & Settings

### UI-Erweiterung ExportPanel

```
Export-Einstellungen:
┌─────────────────────────────────────────────┐
│ Maschinenformat:  [SCM (XCS) ▼]            │
│                                             │
│ Zugabe X (mm):    [2.5     ]                │
│ Zugabe Y (mm):    [2.5     ]                │
│                                             │
│ ── Erweitert ──                             │
│ ☐ Block-Detection aktivieren                │
│ Export-Modus:                                │
│   ⚪ Automatisch                             │
│   ⚪ Nur 2D Layer-Konventionen               │
│   ⚪ Nur 3D Multi-Platte                     │
│                                             │
│ Baumansicht: Platte → zugeordnete Blöcke    │
│                                             │
│ [Exportieren]  [Abbrechen]                  │
└─────────────────────────────────────────────┘
```

### Settings-Persistenz

**Status Sprint 4:** Noch nicht persistent umgesetzt.  
Das ExportPanel hält den Zustand aktuell nur zur Laufzeit; Persistenz bleibt ein separater UI-/Settings-Task.

---

## Risiken & Mitigationen

| Risiko | Wahrscheinlichkeit | Mitigation |
|--------|-------------------|-----------|
| Block-Detection findet falsche Blöcke | Mittel | Strenge CNC_Type Validierung, nur CNC_*-Blöcke werden verarbeitet |
| Legacy + Block Duplikate | Hoch | MachiningBuilder.MergeAndDeduplicate() mit Positions-Toleranz |
| Phase 3 Koordinaten-Fehler | Hoch | Ausgiebiges Testing mit echten 3D-Modellen, Fallback auf Phase 1 |
| Performance-Einbruch bei vielen Blöcken | Niedrig | BlockScanner scannt einmal, cached Ergebnisse |
| User-Verwirrung (zu viele Modi) | Mittel | Default = Automatisch, advanced settings versteckt |

---

## Zusammenfassung: Was wann?

| Schritt | Wann | Was | Aufwand |
|---------|------|-----|---------|
| Core Models definieren | Sprint 1 | Plate, Machining, FittingBlock Records | S |
| BlockScanner implementieren | Sprint 2 | UserText lesen, FittingBlock DTOs erzeugen | M |
| MachiningFactory implementieren | Sprint 2 | DRILL, DRILLPATTERN Blöcke → Machinings | M |
| MergeAndDeduplicate | Sprint 2 | Legacy + Block Machinings zusammenführen | S |
| EmitterRouter implementieren | Sprint 2 | Machining → IEmitter Methoden dispatchen | M |
| ExportService erweitern | Sprint 2 | Block-Detection als optionalen Pfad einbauen | S |
| Feature Flag im UI | Sprint 2 | Checkbox "Block-Detection" | S |
| Starter-Blöcke definieren | Sprint 2 | 5 code-definierte CNC_* Starter-Definitionen | M |
| CLAMEX MachiningFactory | Sprint 3 | SawCut_Lamello Makro-Generation | L |
| PlateDetector | Sprint 3 | Solid→Platte Erkennung | L |
| CoordinateTransformer | Sprint 3 | 3D→Platten-Lokal Transform | L |
| ExportService3D | Sprint 4 | Multi-Platte Pipeline + Auto-Detection | L |

**S** = Small (1-2 Tage), **M** = Medium (3-5 Tage), **L** = Large (1-2 Wochen)

---

*Rückwärtskompatibel. Inkrementell. Kein Big-Bang-Migration.*
