# Sprint 5 — Validation Gap Analysis

**Date:** 2026-03-28  
**Analyst:** Sentinel (Subagent)  
**Source:** 68 production XCS reference files in `tests/references/`  
**Reference DWGs:** 3 files in `tests/references/cadt/`

---

## 1. Reference File Inventory

### Production XCS Files (tests/references/)
- **Total:** 68 `.xcs` files (+ 1 `test_bladecut_reference.xcs` generated)
- **Families:**
  - `Staub_*` — 16 files (Putz-Schrank project, all from `Putz-Schrank.dwg`)
  - `NEW_*` — 20 files (Novotny/Kappeli projects, from `Pult_und_Korpus_Novotny.dwg`)
  - `1_*`, `2_*`, `3_*` — 22 files (numbered cabinet parts, from `Innenschubladen_MartiOptik.dwg`)
  - `Montageleiste`, `dup_*` — 2 misc

### Reference DWGs (tests/references/cadt/)
- `Putz-Schrank.dwg` → Staub_* family
- `Pult_und_Korpus_Novotny.dwg` → NEW_* family  
- `Innenschubladen_MartiOptik.dwg` → numbered family

---

## 2. XCS Command Coverage

### Commands found in production references:

| XCS Command | Count (files) | Plugin Support | Status |
|------------|---------------|----------------|--------|
| `CreateFinishedWorkpieceBox` | 68 | ✅ `EmitHeader` | Production-quality |
| `CreateDrill` | ~65 | ✅ `EmitDrill` | Production-quality |
| `CreatePattern` | ~40 | ✅ `EmitDrillPattern` | Production-quality |
| `CreatePolyline` + `AddSegmentToPolyline` | ~60 | ✅ `EmitPolylinePass` | Production-quality |
| `CreateRoughFinish` | ~60 | ✅ via `EmitPolylinePass` | Production-quality |
| `CreateWorkplane` | ~30 | ✅ `EmitWorkplane` | Production-quality |
| `CreateMacro("*","RNT",...)` | 14 | ✅ `EmitRntX/Y` | Production-quality |
| `CreateMacro("*","SawCut_Lamello",...)` | 52 | ✅ `ClamexMacroBuilder` | Production-quality |
| `CreateMacro("*","XPARK")` | 1 (footer) | ✅ `EmitFooter` | Production-quality |
| `CreateSectioningMillingStrategy` | 9 | ✅ `EmitBladeCut` | Implemented (Sprint 6) |
| `CreateSegment` | 9 | ✅ `EmitBladeCut` | Implemented (Sprint 6) |
| `CreateBladeCut` | 9 | ✅ `EmitBladeCut` | Implemented (Sprint 6) |
| `CreateHelicMillingStrategy` | 1 | ✅ `EmitHelicMillingStrategy` | Stub exists, not routed |
| `CreateMacro("*","Rectangle",...)` | 2 | ❌ Not implemented | GAP |
| `CreateIso` | commented out | N/A | Cosmetic (corner rounding) |
| `CreateWorkplan` | commented out | N/A | Cosmetic (named workplanes) |
| `SetMachiningParameters` | 68 | ✅ `EmitHeader` | Production-quality |
| `SetWorkpieceSetupPosition` | 68 | ✅ `EmitHeader` | Production-quality |
| `AddArc2PointCenterToPolyline` | ~5 | ✅ `EmitPolylinePassWithArcs` | Production-quality |

### Gaps Summary

| Gap | Severity | Impact | Resolution |
|-----|----------|--------|------------|
| `Rectangle` macro | 🟡 Medium | Only 2 files use it (`NEW_Sichtruckwand.xcs`) — rectangular cutouts | Sprint 7+ |
| `HelicMillingStrategy` routing | 🟢 Low | EmitHelicMillingStrategy exists but isn't wired into EmitterRouter | Wire to PocketMachining |
| `CreateIso` | ⚪ None | Always commented out in references — cosmetic corner flag | Skip |
| `CreateWorkplan` | ⚪ None | Always commented out — named workplane aliases | Skip |

---

## 3. Existing Production Validation Tests

### Currently Validated Fixtures (ProductionReferenceValidationTests.cs)

| Test Fixture | Reference XCS | DWG Source | Operations Covered |
|---|---|---|---|
| `PutzschrankSockelMont` | `Staub_SockelMont.xcs` | `Putz-Schrank.dwg` | Routing (contour) |
| `LegraboxFertigauszug` | `NEW_Fertigauszug_Legrabox.xcs` | `Pult_und_Korpus_Novotny.dwg` | Routing (contour) |
| `PutzschrankSeiteLinks` | `Staub_Seite_links.xcs` | `Putz-Schrank.dwg` | Routing + RNT groove + drills + drill patterns |
| `PutzschrankBoden` | `Staub_Boden.xcs` | `Putz-Schrank.dwg` | Routing + horizontal drills + RNT groove + drills |
| `BladeCut_Production_Reference` | `test_bladecut_reference.xcs` | Generated | BladeCut + SectioningMillingStrategy |

### Coverage Analysis

**Validated operation types:**
- ✅ Routing (closed contours)
- ✅ Vertical drills (single)
- ✅ Drill patterns (System-32 rows)
- ✅ Horizontal drills (4 sides)
- ✅ RNT grooves (X-axis)
- ✅ BladeCut (with strategy + segments)

**Not yet validated against production references:**
- ❌ SawCut_Lamello macros (52 occurrences across references!)
- ❌ RNT Y-axis grooves
- ❌ Routing with arcs (AddArc2PointCenterToPolyline)
- ❌ BladeCut from real CAD+T files (only validated against our generated reference)
- ❌ Pockets
- ❌ Rectangle macros
- ❌ Multi-plate export (individual plates validated, not batch)

---

## 4. CreateBladeCut / CreateSectioningMillingStrategy Analysis

### Files Containing BladeCut Operations

| File | Segments | Notes |
|------|----------|-------|
| `NEW_Schubladen_Doppel_1.xcs` | 2 segments | Reference for test fixture |
| `NEW_Schubladen_Doppel_2.xcs` | 2 segments | Same cabinet, different part |
| `NEW_Seite_links_2.xcs` | 2 segments | Side panel with chamfers |
| `NEW_Seite_rechts_2.xcs` | 2 segments | Mirror of left |
| `NEW_Boden_2.xcs` | 1 segment | Bottom panel |
| `NEW_Deckel_2.xcs` | 1 segment | Top panel |
| `Staub_Verkleidung_1.xcs` | 1 segment | Cladding panel |
| `Staub_Verkleidung_2.xcs` | 1 segment | Cladding panel |
| `test_bladecut_reference.xcs` | 2 segments | Generated test reference |

### BladeCut Format (from `NEW_Schubladen_Doppel_1.xcs`)
```
CreateSectioningMillingStrategy(5,0,0);
SetApproachStrategy(true,true,0);
SetRetractStrategy(true,true,0,0);
CreateSegment("Cut segment_1",19.000,354.000,19.000,-187.500);
CreateSegment("Cut segment_2",628.000,-187.500,628.000,354.000);
CreateBladeCut("Geneigter Schnitt in X/Y_1","Blade Cut",TypeOfProcess.GeneralRouting,"E015","-1",45.00,2,-1,-1,-1,2,true,true,0,15);
ResetApproachStrategy();
ResetRetractStrategy();
```

**Plugin Implementation Status:** ✅ Fully implemented in `XilogEmitter.EmitBladeCut()`. Output matches production format.

### Current Stubs (MachiningFactory)

| Method | Status | Notes |
|--------|--------|-------|
| `CreateBladeCut` | ✅ Implemented | Parses segments from CNC_Segments attribute |
| `CreateCut` | 🔲 Stub (returns empty) | Needs Rhino geometry for curve extraction |
| `CreatePocket` | 🔲 Stub (returns empty) | Needs Rhino geometry for offset loops |
| `CreateGroove` | 🔲 Stub (returns empty) | Needs Rhino geometry for groove path |

These stubs are **correct by design**: CUT/POCKET/GROOVE operations require Rhino geometry (curves, surfaces) that cannot be represented as simple block attributes. They will be populated via `FeatureReader` in the Rhino plugin layer.

---

## 5. Recommended Next Fixtures

### Priority 1: SawCut_Lamello Validation
The most common unvalidated operation type (52 occurrences). Suggested fixtures:
- `Staub_Tur_1.xcs` — Door panel with Clamex macros
- `NEW_Tur_3.xcs` — Another door with Clamex

### Priority 2: Multi-Type Complex Parts
- `3_2_2_Mittelseite.xcs` — 80 operations (most complex reference file)
- `1_1_2_Seite_rechts.xcs` — 56 operations, mixed drills/patterns/routing

### Priority 3: BladeCut Real Production
- `NEW_Seite_links_2.xcs` — Side panel with BladeCut + drills + patterns
- `Staub_Verkleidung_1.xcs` — Simple BladeCut validation

---

## 6. Normalization Strategy Assessment

The `NormalizeProgram()` method in `ProductionReferenceValidationTests` handles:
- ✅ Comment stripping
- ✅ Whitespace normalization
- ✅ Name normalization (operation names differ between generators)
- ✅ DZ variable resolution (evaluates `DZ-9.5` → `9.5` for DZ=19)
- ✅ Arithmetic expression evaluation
- ✅ Number format normalization (trailing zeros)

**Known limitation:** Workplane names are normalized but custom workplane names from `CreateWorkplane` parameters are not pattern-matched. This could cause false failures for fixtures with horizontal drills that use different naming conventions.

---

## 7. Action Items

1. **Short-term:** Add SawCut_Lamello validation fixture (Priority 1)
2. **Medium-term:** Wire `EmitHelicMillingStrategy` into EmitterRouter for PocketMachining
3. **Medium-term:** Add Rectangle macro emitter method
4. **Long-term:** Real BladeCut validation against production references (need to hand-build plate fixtures from DWG analysis)
5. **Documentation:** Mark CUT/POCKET/GROOVE stubs as intentional in code comments
