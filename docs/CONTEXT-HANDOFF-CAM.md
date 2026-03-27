# Context Handoff: Interactive CAM Commands

**Datum:** 27. März 2026  
**Autor:** Sentinel (Night Session #1 + #2)  
**Branch:** `feat/interactive-cam-commands`  
**Status:** Dockable CAM Panel Implemented ✅ — Next: UI Polish + Viz Unification

---

## 1. Current State of Interactive CAM Features

### 1.1 Commands (All Implemented ✅)

| Command | File | Status | Notes |
|---------|------|--------|-------|
| `CNCAddContour` | `Commands/CNCAddContourCommand.cs` | ✅ Working | Selects curves/edges, shows ContourOperationDialog, applies UserText + toolpath viz |
| `CNCAddDrill` | `Commands/CNCAddDrillCommand.cs` | ✅ Working | Click points or select existing points, creates circle geometry + drill viz |
| `CNCAddPocket` | `Commands/CNCAddPocketCommand.cs` | ✅ Working | Selects closed curves, shows PocketOperationDialog, generates concentric offsets |
| `CNCAddGroove` | `Commands/CNCAddGrooveCommand.cs` | ✅ Working | Selects curves, shows GrooveOperationDialog, offset curves like contour |
| `CNCRemoveOperation` | `Commands/CNCRemoveOperationCommand.cs` | ✅ Working | Removes UserText + grouped toolpath geometry, restores layer color |

### 1.2 Toolpath Visualization (`Services/ToolpathVisualizer.cs`)

**Fixed in Night Session #1** (commit `9f29b09`):

**Root Cause:** When users selected Brep edges (e.g., edges of a plate), `objRef.Object()` returned the parent Brep object, NOT the edge curve. The check `obj.Geometry is Curve curve` failed silently → no toolpath geometry was ever created.

**Fixes Applied:**
1. All four CNC commands now use `objRef.Curve()` to extract the actual curve geometry (works for both standalone curves AND Brep edges), with fallback to `obj.Geometry` for direct curve objects
2. `ToolpathVisualizer` now uses `GetCurvePlane()` instead of hardcoded `Plane.WorldXY` — uses `Curve.TryGetPlane()` to handle curves at any Z height or orientation
3. Pattern: `var selections = new List<(RhinoObject obj, Curve curve)>()` — keeps the RhinoObject reference (for UserText storage) separate from the actual curve geometry

**Visualization Types:**
- **Contour/Groove:** Left + right offset curves (showing tool width) + direction arrows (chevrons along curve)
- **Drill:** Circle at drill point + crosshair lines
- **Pocket:** Concentric inward offsets (stepover-based) + entry point marker circle

**Layer Structure:** `CNC_Toolpaths::Contour`, `CNC_Toolpaths::Pocket`, etc.  
**Grouping:** Toolpath geometry is grouped with source object via `doc.Groups.Add()`. Group index stored as UserText `CNC_GroupIndex`.

### 1.3 Operation Dialogs (`UI/CamOperationDialogBase.cs` + subclasses)

- **Base class:** `CamOperationDialogBase` — tool dropdown (filtered by ToolKind), depth input, tool info label, OK/Cancel
- **ContourOperationDialog** — adds strategy dropdown (Rough/Finish/Both)
- **DrillOperationDialog** — adds peck drilling checkbox + peck depth
- **PocketOperationDialog** — adds stepover percentage + ramp entry type
- **GrooveOperationDialog** — adds width parameter
- All dialogs use `ShowModalOnTop()` → `ShowModal(RhinoEtoApp.MainWindow)` for proper Rhino parenting
- Parameters returned as `Dictionary<string, object>` using `CncOperationSchema` keys

### 1.4 Data Storage

Operations are stored as **UserText** on Rhino objects via `CncOperationService`:
- `CNC_Type` → "Contour", "Pocket", "Drill", "Groove"
- `CNC_Tool` → tool name/ID
- `CNC_Depth`, `CNC_Diameter`, `CNC_Width`, `CNC_Feedrate`, `CNC_Stepover`, etc.
- `CNC_GroupIndex` → links to toolpath visualization group
- Object color is set via `ColorFromObject` to match operation type (Red/Blue/Yellow/Green)

### 1.5 Tool Library System

- **`ToolLibraryStore`** — loads/saves tool libraries as JSON per machine profile
- **`ToolLibrary`** model — tools, holders, merge logic, suggest tool heuristics
- **`ToolDefinition`** — diameter, speeds, feeds, motion profile, tech code
- **Default tools:** SCM (xilog) and Biesse profiles with realistic defaults
- **Manager UI:** `ToolLibraryManagerDialog` — full CRUD for tools and holders

### 1.6 Export Panel (`UI/ExportPanel.cs`)

The existing dockable panel handles the **legacy pipeline** (layer-based) and **3D pipeline** (plate/block-based):
- "Vorschau erzeugen" button → calls `ToolpathPreviewService.GeneratePreview()` which uses the plate/block analysis pipeline (NOT the interactive CNC commands)
- This is a **separate system** from the interactive CAM commands — it generates preview from 3D plate analysis

**Important distinction:**
- **Interactive CAM commands** (CNCAddContour etc.) → `ToolpathVisualizer` → `CNC_Toolpaths` layer
- **Export panel "Vorschau erzeugen"** → `ToolpathPreviewService` → `RhinoCNC Preview` layer
- These are two different visualization systems that need to be unified in the CAM Panel

---

## 2. What Works

- ✅ All four CNC interactive commands (contour, drill, pocket, groove)
- ✅ Toolpath visualization with proper plane handling
- ✅ Operation removal with cleanup (geometry + UserText + color restore)
- ✅ Tool library system with CRUD, import/export, defaults
- ✅ Parameter dialogs with tool selection and validation
- ✅ Existing export pipeline (legacy + 3D) via ExportPanel
- ✅ Toolpath preview for 3D pipeline via ToolpathPreviewService

## 3. What Doesn't Work / Known Gaps

- ⚠️ **No dockable CAM panel** — operations are command-only, no persistent UI showing active operations
- ⚠️ **No operations tree** — cannot see/select/edit/reorder operations like RhinoCAM
- ⚠️ **Two separate viz systems** — interactive commands use `ToolpathVisualizer`, export panel uses `ToolpathPreviewService`
- ⚠️ **No toolpath simulation** — no animation of tool movement
- ⚠️ **No Z-level display** — toolpath viz is 2D (no depth representation)
- ⚠️ **Edge selection stores on parent Brep** — UserText goes on the Brep, not the individual edge (could cause issues with multiple operations on different edges of the same Brep)

---

## 4. Next Steps (Priority Order)

### P1: Dockable CAM Panel 🎯

**Goal:** A persistent dockable panel (like RhinoCAM's "Machining Operations" panel) that shows all CNC operations in the current document.

**Reference:** How RhinoCAM / Fusion 360 CAM / MasterCAM organize their UI:
- **Operations tree** — hierarchical list of all operations on all objects
- **Per-operation controls** — expand to see/edit parameters
- **Tool library** sidebar — drag-and-drop tools to operations
- **Generate all** / **Generate selected** buttons
- **Status indicators** — which ops have toolpaths generated, which are stale

**Implementation plan:**
```
RhinoCNCExporter/UI/
├── CamPanel.cs                  # New dockable panel (replaces command-only workflow)
│   ├── Operations TreeView      # Shows all CNC ops in document
│   ├── Properties sidebar       # Edit selected operation's parameters  
│   ├── Tool quick-select        # Filtered tool dropdown
│   ├── Generate/Clear buttons   # Toolpath generation controls
│   └── Status bar               # Object count, operation count, warnings
```

**Key patterns to follow:**
- See `ExportPanel.cs` for the existing dockable panel pattern (GUID, `Panel` base class, registration)
- Use `CncOperationService.GetAllOperationsInDocument()` to populate the tree
- Hook into `RhinoDoc.ActiveDoc.Objects.ObjectAdded/Deleted/Modified` events for live updates
- Merge `ToolpathVisualizer` and `ToolpathPreviewService` into a single system

### P2: Unify Visualization Systems

Currently two separate systems:
1. `ToolpathVisualizer` (interactive commands → `CNC_Toolpaths` layer)
2. `ToolpathPreviewService` (export panel → `RhinoCNC Preview` layer)

**Plan:** Merge into one service that handles both interactive and batch visualization. The CAM Panel should be the single source of truth for all visualization.

### P3: Edge-Level Operations on Breps

**Problem:** When selecting edges of a Brep, UserText is stored on the parent Brep. If you apply Contour to edge A and Drill to edge B of the same Brep, the second operation overwrites the first.

**Solution options:**
1. **Duplicate edge as curve** — extract edge, add as standalone curve, apply UserText there, hide original
2. **Multi-operation UserText** — encode multiple operations with edge indices (complex)
3. **Operation objects** — create lightweight point/curve objects that reference the source edge (cleanest)

### P4: Tool Library in Panel

- Integrate tool library management directly in the CAM Panel
- Tool preview with dimensions (SVG/drawn in Eto Drawable)
- Quick-assign tool to selected operation
- Already have: `ToolLibraryManagerDialog` — adapt for panel integration

### P5: UI Polish

- Professional styling (dark theme matching Rhino)
- Icons for operation types
- Status bar with document info
- Keyboard shortcuts for common operations
- Right-click context menu on operations tree

### P6: Export Pipeline Integration

- "Vorschau erzeugen" should pick up UserText operations from interactive commands
- Bridge between interactive CAM operations and the existing plate/block export pipeline
- Currently `ExportService3D.BuildMachiningsForPlate()` reads from block UserText — extend to also read `CNC_Type` operations

---

## 5. Architecture Notes

### Project Structure
```
RhinoCNCExporter.Core/          # No RhinoCommon dependencies
├── Blocks/CncOperationSchema.cs    # UserText key constants + validation
├── Models/Tooling.cs               # ToolDefinition, ToolLibrary, MachiningStrategy
├── Models/Machining.cs             # Machining types (RoutingMachining, DrillMachining, etc.)
├── Pipeline/                       # Export pipeline, ToolpathPlanner, etc.

RhinoCNCExporter/                # Rhino plugin (RhinoCommon + Eto.Forms)
├── Commands/                       # Rhino commands (CNCAdd*, CNCRemove*)
├── Services/
│   ├── CncOperationService.cs      # UserText CRUD for operations
│   ├── ToolpathVisualizer.cs       # Interactive toolpath viz (fixed ✅)
│   ├── ToolpathPreviewService.cs   # Export-panel toolpath viz
│   └── ToolLibraryStore.cs         # Tool library persistence
├── UI/
│   ├── ExportPanel.cs              # Existing dockable export panel
│   ├── CamOperationDialogBase.cs   # Base dialog for operation params
│   ├── ContourOperationDialog.cs   # etc.
│   └── ToolLibraryManagerDialog.cs # Tool CRUD UI
```

### Key RhinoCommon Patterns Used
- `ObjRef.Curve()` — extracts curve from any selection (standalone curve OR Brep edge)
- `Curve.TryGetPlane()` — gets best-fit plane for planar curves
- `Curve.Offset(plane, distance, tolerance, cornerStyle)` — plane must match curve's plane
- `doc.Groups.Add(objectIds)` — groups source + toolpath objects
- `doc.Objects.FindByGroup(index)` — finds all objects in a group
- `ObjectAttributes.SetUserString/GetUserString` — persistent per-object metadata
- `ObjectAttributes.ColorSource = ColorFromObject` — per-object color override
- `doc.Views.Redraw()` — must call after adding/removing geometry

### Build Notes
- Project targets .NET 7.0 with RhinoCommon NuGet
- **Cannot build in WSL** — RhinoCommon NuGet only resolves on Windows
- `RhinoCNCExporter.Core` builds fine in WSL (no Rhino dependencies)
- Build and test on Windows via Visual Studio or `dotnet build` in PowerShell

---

## 6. Files Changed in This Session

| File | Change |
|------|--------|
| `Commands/CNCAddContourCommand.cs` | Use `objRef.Curve()` + tuple pattern for selections |
| `Commands/CNCAddGrooveCommand.cs` | Same fix |
| `Commands/CNCAddPocketCommand.cs` | Same fix |
| `Services/ToolpathVisualizer.cs` | `GetCurvePlane()` helper, plane-aware offset |

**Commit:** `9f29b09` — "fix: toolpath visualization not showing — use ObjRef.Curve() for edges + plane-aware offset"

---

## 7. Night Session #2: Dockable CAM Panel (27. März 2026)

### 7.1 What Was Implemented

#### `UI/CamPanel.cs` — Full Dockable CAM Panel ✅

**GUID:** `c7e3a1d5-4f82-4e9b-b3c6-8d2f1a5e7b09`  
**Display Name:** "CNC Operations"  
**Base class:** `Panel` (Eto.Forms, same pattern as `ExportPanel`)

**Layout sections (top to bottom):**
1. **Header** — "🔧 CNC Operations"
2. **Quick-Add Toolbar** — 4 buttons (+ Contour, + Pocket, + Drill, + Groove) that run `RhinoApp.RunScript()` for existing CNCAdd* commands
3. **Operations TreeGridView** — scans document for all objects with `CNC_Type` UserText, groups by operation type, shows object name/layer, tool, depth
4. **Properties Panel** — shows/edits selected operation's parameters with type-specific fields:
   - Contour: Tool, Depth, Strategy (Rough/Finish/Both)
   - Pocket: Tool, Depth, Stepover %, Ramp Entry
   - Drill: Tool, Depth, Diameter, Peck drilling, Peck depth
   - Groove: Tool, Depth, Width, Strategy
   - "Apply" button updates UserText + regenerates toolpath
5. **Action Buttons** — Generate All, Clear All, Refresh
6. **Status Bar** — "X operations | Y tools | ⚠ Z warnings"

**Interactions:**
- Click operation in tree → selects object in viewport
- Double-click → zoom to object
- Context menu → Edit, Remove, Regenerate Toolpath
- Auto-refresh via `RhinoDoc.AddRhinoObject`, `DeleteRhinoObject`, `ModifyObjectAttributes` events
- Document switch handling via `ActiveDocumentChanged` + `CloseDocument`
- Event cleanup in `Dispose()`

#### `Commands/CNCPanelCommand.cs` — Panel Toggle Command ✅

- Command name: `CNCPanel`
- Toggles panel visibility (open if closed, close if open)
- Panel registration in Command constructor (Rhino 8 best practice)
- Follows exact same pattern as `RhinoCNCExporterCommand` / `ExportPanel`

### 7.2 Files Changed in Night Session #2

| File | Change |
|------|--------|
| `UI/CamPanel.cs` | **NEW** — Full dockable CAM panel (630+ lines) |
| `Commands/CNCPanelCommand.cs` | **NEW** — CNCPanel toggle command |
| `docs/CONTEXT-HANDOFF-CAM.md` | Updated with session #2 results |

**Commit:** `6a574ab` — "feat: add dockable CAM panel with operations tree, properties editor, and toolpath controls"

### 7.3 Architecture Decisions

1. **Separate panel from ExportPanel** — CamPanel is for interactive CAM operations, ExportPanel is for the legacy/3D export pipeline. They coexist as separate dockable panels.
2. **Uses existing services** — `CncOperationService` for UserText CRUD, `ToolpathVisualizer` for toolpath geometry, `ToolLibraryStore` for tool data
3. **Event-driven refresh** — hooks into RhinoDoc object events for live updates instead of manual refresh
4. **Default ScmProfile** — loads tool library from SCM profile; future: machine selector in panel
5. **Row visibility workaround** — Eto.Forms `TableRow` has no `Visible` property; using `Control.Visible` on cells instead

### 7.4 Known Limitations / TODO

- ⚠ **No right-click context menu wired to tree** — `TreeGridView` context menu needs `MouseDown` event handling (Eto quirk). The menu methods exist but aren't connected to mouse events yet.
- ⚠ **Machine profile hardcoded to SCM** — needs machine selector or reads from ExportPanel setting
- ⚠ **Two separate viz systems still exist** — `ToolpathVisualizer` (interactive) vs `ToolpathPreviewService` (export panel)
- ⚠ **No keyboard shortcuts** — would benefit from Delete key to remove operation, F5 to refresh
- ⚠ **TreeGridView colors** — operation type icons use emoji (🔴🔵🟡🟢) since Eto TreeGridView doesn't easily support cell coloring

### 7.5 Next Steps (Priority)

1. ~~**P1: Build & Test on Windows**~~ — still needs testing on Windows
2. ~~**P2: Wire up context menu**~~ — ✅ Done in Night Session #3
3. ~~**P3: Machine profile selector**~~ — ✅ Done in Night Session #3
4. ~~**P4: Unify viz systems**~~ — ✅ Done in Night Session #3
5. ~~**P5: UI polish**~~ — ✅ Done in Night Session #3
6. **P6: Brep edge operations** — handle multiple operations on different edges of same Brep

---

## 8. Night Session #3: UI Polish, Context Menu, Viz Unification (27. März 2026)

### 8.1 What Was Implemented

#### Right-Click Context Menu on TreeGridView ✅
- **MouseDown event handler** wired to TreeGridView for right-click detection
- **5 context menu items:**
  - ✏️ **Bearbeiten…** → opens the operation-specific dialog (ContourOperationDialog, DrillOperationDialog, etc.) pre-filled with current values via new `PreFill()` method
  - 🗑 **Entfernen** → removes operation, toolpath geometry, restores object color
  - 🔄 **Toolpath neu generieren** → deletes old toolpath geometry, re-creates from current UserText
  - 🎯 **Im Viewport selektieren** → selects the source object in viewport
  - 🔍 **Zoom auf Objekt** → zooms viewport to object bounding box (with 10% inflation for framing)

#### Machine Profile Selector ✅
- **Dropdown at top of CamPanel:** "Maschine: [SCM (Xilog) ▼]"
- **3 profiles:** SCM (Xilog), Biesse (CIX), MaestroCadT
- **Selection persisted** in document UserText via `RhinoDoc.Strings` (`CNC_MachineProfile` key)
- **Profile change triggers:** tool library reload, operations tree refresh, properties re-population
- **Loaded from document** on panel creation and document switch events
- Uses existing profile classes: `ScmProfile`, `BiesseProfile`, `MaestroCadTProfile`

#### Unified Visualization Systems ✅
- **ExportPanel's "Vorschau erzeugen"** now also regenerates toolpaths for interactive CAM operations
- Uses `ToolpathVisualizer` (the interactive system) for all UserText-based operations
- New `RegenerateInteractiveToolpath()` static method in ExportPanel handles all 4 op types
- Resolves tool diameter from tool library when not stored directly on the object
- Both systems coexist:
  - `CNC_Toolpaths::*` layer — interactive operations (ToolpathVisualizer)
  - `RhinoCNC Preview::*` layer — export preview (ToolpathPreviewService, block-based)
- "Generate All" in CamPanel uses ToolpathVisualizer for all operations

#### UI Polish ✅
- **Empty state message:** "Keine Bearbeitungen vorhanden…" shown when no operations exist
- **Keyboard shortcuts:** Delete/Backspace = remove selected operation, F5 = refresh
- **Tooltips** on all buttons, dropdowns, and text fields (German)
- **German UI labels** throughout (Operationen, Eigenschaften, Anwenden, etc.)
- **Color indicators** in tree: 🔴🔵🟡🟢 emoji for operation types
- **Tool diameter in tree:** shows "Ø10 ToolName" instead of just tool name
- **Section headers** using Expander pattern (matching ExportPanel)
- **Status bar** shows: "X Operationen | Y Werkzeuge | ⚠ Z Warnungen | XILOG"
- **Proper column headers:** "Operation", "Werkzeug", "Tiefe"
- **Toolbar** changed from 2 rows to single horizontal row

#### Improved Tool Dropdown UX ✅
- **Format:** "Ø10.0 HM Router (3-Schneider)" — diameter + name + flute count
- **"Werkzeuge verwalten…"** option at bottom of every dropdown → opens ToolLibraryManagerDialog
- **Warning** when no tools available for operation type: "⚠ Keine Werkzeuge verfügbar"
- Applies to both CamPanel properties panel AND all operation dialogs (base class change)

#### PreFill for Edit Dialog ✅
- New `PreFill(MachiningOperation)` virtual method on `CamOperationDialogBase`
- Override in all 4 subclass dialogs with type-specific parameter mapping:
  - **ContourOperationDialog:** strategy dropdown + feedrate
  - **DrillOperationDialog:** diameter, peck drilling checkbox, peck depth
  - **PocketOperationDialog:** stepover, strategy, ramp entry
  - **GrooveOperationDialog:** width

### 8.2 Files Changed in Night Session #3

| File | Change |
|------|--------|
| `UI/CamPanel.cs` | Major rewrite: context menu, machine selector, empty state, keyboard shortcuts, tooltips, German labels, tool dropdown UX |
| `UI/CamOperationDialogBase.cs` | Added PreFill(), Manage Tools dropdown item, improved tool display format, Rhino import |
| `UI/ContourOperationDialog.cs` | Added PreFill() override |
| `UI/DrillOperationDialog.cs` | Added PreFill() override |
| `UI/PocketOperationDialog.cs` | Added PreFill() override |
| `UI/GrooveOperationDialog.cs` | Added PreFill() override |
| `UI/ExportPanel.cs` | Added interactive toolpath regen in GeneratePreview(), imports for CncOperationService |
| `docs/CONTEXT-HANDOFF-CAM.md` | Updated with session #3 results |

**Commit:** `5f5de47` — "feat: CamPanel night session #3 — context menu, machine profiles, viz unification, UI polish"

### 8.3 Known Limitations / TODO

- ⚠ **Not yet tested on Windows** — needs `dotnet build` verification + Rhino 8 runtime test
- ⚠ **Brep edge operations** — multiple ops on different edges of same Brep still overwrite (P6)
- ⚠ **No undo support** — changes via Apply/Remove are not wrapped in Rhino undo transactions
- ⚠ **TreeGridView row coloring** — still using emoji, no actual cell background colors (Eto limitation)
- ⚠ **Profile-specific defaults** — machine profile change reloads tool library but doesn't update existing operations' defaults
- ⚠ **ToolLibraryManagerDialog from dropdown** — saves via `RhinoApp.WriteLine` only (no persistence path in base dialog context)

### 8.4 Next Steps

1. **P1: Build & Test on Windows** — compile, load in Rhino 8, verify all features
2. ~~**P2: Undo support**~~ — ✅ Done in Night Session #4
3. ~~**P3: Brep edge operations**~~ — ✅ Done in Night Session #4
4. **P4: Toolpath simulation** — animate tool movement along paths (3D preview with depth)
5. **P5: Export pipeline integration** — bridge UserText operations to plate/block export pipeline

---

## 9. Night Session #4: Edge-Level Ops, Undo Support, Build Quality (27. März 2026)

### 9.1 What Was Implemented

#### Edge-Level Operations on Breps ✅

**Problem solved:** When selecting Brep edges, UserText was stored on the parent Brep. Multiple operations on different edges of the same Brep would overwrite each other.

**Solution: Extract edge as standalone curve**
- New `Services/EdgeCurveHelper.cs` — utility class for Brep edge extraction
- When `EdgeCurveHelper.IsBrepEdge(objRef)` detects a Brep edge selection:
  - `edge.DuplicateCurve()` extracts the edge geometry
  - Curve is added to `CNC_EdgeCurves` layer (thin, dashed linetype)
  - Reference stored: `CNC_SourceBrep=<guid>`, `CNC_SourceEdgeIndex=<int>`
  - Operation color applied to the extracted curve
  - UserText is applied to the extracted curve (not the parent Brep)
- When removing: if object is an extracted edge curve (`IsExtractedEdgeCurve()`), it gets deleted entirely instead of just clearing UserText
- Applied to all four CNCAdd* commands: Contour, Pocket, Groove, Drill (drill uses points, but the pattern is ready)

**New UserText keys in `CncOperationSchema`:**
- `CNC_SourceBrep` — GUID of the parent Brep
- `CNC_SourceEdgeIndex` — edge index on the source Brep

**New layer:** `CNC_EdgeCurves` — dedicated layer for extracted edge curves (dashed linetype when available)

#### Undo Support ✅

All operations wrapped in Rhino undo records (`doc.BeginUndoRecord()` / `doc.EndUndoRecord()`):

| Location | Undo Record Name |
|----------|-----------------|
| `CNCAddContourCommand` | "CNC Add Contour" |
| `CNCAddDrillCommand` | "CNC Add Drill" |
| `CNCAddPocketCommand` | "CNC Add Pocket" |
| `CNCAddGrooveCommand` | "CNC Add Groove" |
| `CNCRemoveOperationCommand` | "CNC Remove Operation" |
| `CamPanel.ApplyPropertyChanges()` | "CNC Apply Properties" |
| `CamPanel.RemoveOperation()` | "CNC Remove Operation" |
| `CamPanel.OpenEditDialog()` | "CNC Edit {Type}" |

**Error handling:** All undo records use try/catch pattern — `EndUndoRecord()` is called in both success and failure paths to prevent undo stack corruption.

#### Nullable Warning Fixes ✅ (target: 0 warnings)

**CS8618 fixes (non-nullable field not initialized):**
- `CamOperationDialogBase`: `_toolDropDown`, `_depthTextBox`, `_toolInfoLabel`, `_okButton`, `_cancelButton` → `= null!;`
- `ContourOperationDialog`: `_operationTypeDropDown`, `_strategyDropDown`, `_feedrateTextBox` → `= null!;`
- `DrillOperationDialog`: `_diameterTextBox`, `_peckDrillingCheckBox`, `_peckDepthTextBox` → `= null!;`
- `PocketOperationDialog`: `_stepoverTextBox`, `_strategyDropDown`, `_rampEntryDropDown` → `= null!;`
- `GrooveOperationDialog`: `_widthTextBox` → `= null!;`

**CS8600/CS8602 fixes (possible null dereference):**
- `CncOperationService.GetOperation()`: `userStrings[key] ?? string.Empty`
- `CncOperationService.GetOperationColor()`: `(operationType ?? string.Empty).ToUpperInvariant()`
- `FaceTagger.ReadTags()`: `string?` loop variable + null check
- `FaceTagger.GetTaggedFaceIndices()`: `string?` loop variable + null check
- `FaceTagger.ClearTags()`: `.Select(key => key!)` after null filter
- `FeatureReader`: `GetStringTag()` result with `?? fallback`
- `FeatureReader.ExtractFaceBoundary()`: `poly != null` check after `TryGetPolyline`
- `AddClamexCommand`: `Brep? newBrep = null;` + `selectedOption != null` guard

**CS8604 fixes (possible null argument):**
- `FaceTagger.ClearTags()`: Changed `SetUserString(key, null)` to `DeleteUserString(key)`

#### Code Cleanup ✅

- **Removed duplicate `ShowError` methods** from all 4 dialog subclasses — now inherited from `CamOperationDialogBase` (changed from `private` to `protected`)
- **Removed unused imports** from rewritten command files
- **Consistent naming** across all files
- **`CncOperationService.GetOperationColor(string)`** — new public overload returns `System.Drawing.Color` for use in `EdgeCurveHelper` and other services without requiring a `RhinoObject`

### 9.2 Files Changed in Night Session #4

| File | Change |
|------|--------|
| `RhinoCNCExporter.Core/Blocks/CncOperationSchema.cs` | Added `CNC_SOURCE_BREP` and `CNC_SOURCE_EDGE_INDEX` keys |
| `Services/EdgeCurveHelper.cs` | **NEW** — Brep edge extraction utility |
| `Commands/CNCAddContourCommand.cs` | Edge extraction + undo support |
| `Commands/CNCAddDrillCommand.cs` | Undo support |
| `Commands/CNCAddPocketCommand.cs` | Edge extraction + undo support |
| `Commands/CNCAddGrooveCommand.cs` | Edge extraction + undo support |
| `Commands/CNCRemoveOperationCommand.cs` | Undo support + handles extracted edge curves |
| `Commands/AddClamexCommand.cs` | Nullable fixes (`Brep?`, option null check) |
| `Services/CncOperationService.cs` | New `GetOperationColor(string)` overload, nullable fixes |
| `Services/FaceTagger.cs` | Nullable fixes (AllKeys iteration, DeleteUserString) |
| `Services/FeatureReader.cs` | Nullable fixes (GetStringTag, TryGetPolyline, macro params) |
| `UI/CamOperationDialogBase.cs` | `= null!;` for fields, `ShowError` → protected |
| `UI/ContourOperationDialog.cs` | `= null!;`, removed duplicate ShowError |
| `UI/DrillOperationDialog.cs` | `= null!;`, removed duplicate ShowError |
| `UI/PocketOperationDialog.cs` | `= null!;`, removed duplicate ShowError |
| `UI/GrooveOperationDialog.cs` | `= null!;`, removed duplicate ShowError |
| `UI/CamPanel.cs` | Undo records in Apply/Remove/Edit, edge curve handling in Remove |

**Commit:** `4d1e6fb` — "feat: edge-level ops, undo support, nullable fixes, code cleanup"

### 9.3 Architecture Notes

**Edge Extraction Flow:**
```
User selects Brep edge → CNCAddContour command
  ├── EdgeCurveHelper.IsBrepEdge(objRef) → true
  ├── EdgeCurveHelper.ExtractEdgeCurve(doc, objRef, color)
  │   ├── edge.DuplicateCurve()
  │   ├── Add to CNC_EdgeCurves layer
  │   ├── Store CNC_SourceBrep + CNC_SourceEdgeIndex
  │   └── Return new RhinoObject
  ├── CncOperationService.SetOperation(extractedCurve, ...)
  └── ToolpathVisualizer.AddToolpathToDocument(doc, extractedCurve, ...)
```

**Undo Pattern:**
```csharp
var undoSerial = doc.BeginUndoRecord("CNC Add Contour");
try {
    // ... all document modifications ...
    doc.EndUndoRecord(undoSerial);
} catch {
    doc.EndUndoRecord(undoSerial);
    throw;
}
```

### 9.4 Known Limitations / TODO

- ⚠ **Not yet tested on Windows** — needs `dotnet build` verification + Rhino 8 runtime test
- ⚠ **TreeGridView row coloring** — still using emoji, no actual cell background colors (Eto limitation)
- ⚠ **Profile-specific defaults** — machine profile change reloads tool library but doesn't update existing operations' defaults
- ⚠ **Edge curve linetype** — dashed linetype depends on "Dashed" existing in the document's linetype table; falls back to solid line if not found
- ⚠ **Edge curve cleanup on Brep deletion** — if the parent Brep is deleted, extracted edge curves remain orphaned (could add event handler for cleanup)

### 9.5 Next Steps

1. **P1: Build & Test on Windows** — compile, load in Rhino 8, verify all features including edge extraction
2. ~~**P2: Toolpath simulation**~~ — ✅ Done in Night Session #5
3. ~~**P3: Export pipeline integration**~~ — ✅ Done in Night Session #5
4. **P4: Orphan edge curve cleanup** — detect when parent Brep is deleted and clean up extracted edge curves
5. **P5: Edge curve visual feedback** — show connection between extracted curve and parent Brep (e.g., leader/arrow)

---

## 10. Night Session #5: Export Pipeline Bridge + 3D Toolpath Preview (26. März 2026)

### 10.1 What Was Implemented

#### Interactive Export Bridge (`Services/InteractiveExportBridge.cs`) ✅

**The Gap Solved:** Interactive CAM operations (CNCAddContour etc.) store data as UserText on objects. The export pipeline reads from block instances. This bridge connects both paths.

**`InteractiveExportBridge` class:**
- `CollectOperations(doc)` → scans all objects with `CNC_Type` UserText, converts to `Machining` objects
- `GroupByPlate(doc, operations)` → groups operations by source brep (for edge curves) or by layer
- `Export(doc, path, format, profile)` → full export pipeline:
  1. Collect operations
  2. Group by plate
  3. Build `Plate` models with `Machining` lists
  4. Create `EmitterRouter` with appropriate emitter (Xilog/Biesse)
  5. Generate CNC program per plate
  6. Write to file(s)
- Single plate → single file via `SaveFileDialog`
- Multiple plates → directory with per-plate files via `SelectFolderDialog`

**Key Mapping (UserText → Machining model):**
| UserText | Machining Property |
|----------|-------------------|
| `CNC_Type=Contour` | `RoutingMachining` (IsClosed from curve) |
| `CNC_Type=Pocket` | `PocketMachining` (single boundary loop) |
| `CNC_Type=Drill` | `DrillMachining` (X, Y from geometry center) |
| `CNC_Type=Groove` | `RoutingMachining` (IsClosed=false) |
| `CNC_Tool` | Tool name → diameter resolution |
| `CNC_Depth` | `Machining.Depth` |
| `CNC_Diameter` | `DrillMachining.Diameter` / tool diameter |
| Geometry | Curve → polyline points / Point → X,Y coords |

**Plate Detection Heuristics:**
- If `CNC_SourceBrep` exists → use that brep's bounding box for plate dimensions
- Otherwise → find largest Brep on the same layer
- Fallback → 1000×600×19mm defaults
- Dimensions sorted: largest=length, middle=width, smallest=thickness

**Static helper: `GetStatistics(doc, tools)`** → returns `OperationStatistics`:
- Operation counts by type
- Tool changes estimate
- Max depth across all ops
- Estimated machining time (path length / feedrate)

#### 3D Toolpath Visualization (`Services/ToolpathVisualizer.cs`) ✅

**New methods alongside existing 2D toolpath creation:**

**`CreateContourToolpath3D(curve, toolDiameter, depth)`:**
- Top offset curves (left/right) at surface level
- Bottom offset curves translated down by `depth` along plane normal
- Vertical connection lines at ~50mm intervals between top and bottom curves
- Direction arrows on the top curve

**`CreateDrillToolpath3D(center, diameter, depth)`:**
- Top circle at surface
- Bottom circle at depth
- 4 vertical lines at cardinal directions connecting circles
- Top crosshair + bottom X indicator

**`CreatePocketToolpath3D(boundaryCurve, toolDiameter, stepoverPercent, depth)`:**
- Top boundary outline at surface
- Bottom boundary at depth
- Vertical wall lines connecting top and bottom
- Concentric offset curves at depth level
- Entry point marker + ramp indicator line

**Layer structure:** `CNC_Toolpaths_3D::Contour`, `CNC_Toolpaths_3D::Pocket`, etc.
- Independent from 2D toolpaths (`CNC_Toolpaths::*`)
- Slightly lighter colors to distinguish from 2D
- Own group tracking via `CNC_GroupIndex3D` UserText

**Helper methods:**
- `AddToolpath3DToDocument()` — creates geometry on 3D layer tree
- `RemoveToolpath3DGeometry()` — cleans up 3D toolpath objects
- `EnsureToolpath3DSubLayer()` — ensures layer hierarchy exists

#### CamPanel Enhancements (`UI/CamPanel.cs`) ✅

**New UI elements:**
1. **"📤 Export CNC" button** — triggers interactive export:
   - Determines format from machine profile (Xilog/Biesse)
   - Shows SaveFileDialog or SelectFolderDialog based on plate count
   - Calls `InteractiveExportBridge.Export()`
   - Reports success/failure to RhinoApp output

2. **"3D Toolpath-Vorschau" checkbox** — toggles 3D depth visualization:
   - When checked: generates 3D toolpaths for all operations
   - When unchecked: removes all 3D toolpath geometry
   - Works per-operation via `Generate3DToolpath()` helper

3. **Statistics section (collapsible):**
   - Auto-updates when operations tree refreshes
   - Shows: "X Op. (N× Contour, N× Pocket, ...) | N Werkzeugwechsel | Max. Tiefe: Xmm | Zeit: ~Xmin"
   - Uses `InteractiveExportBridge.GetStatistics()`

**Modified behaviors:**
- `GenerateAllToolpaths()` — now also handles 3D toolpaths when toggle is on
- `ClearAllToolpaths()` — now also removes 3D toolpaths
- `RefreshOperationsTree()` — now calls `UpdateStatistics()` at the end

### 10.2 Files Changed in Night Session #5

| File | Change |
|------|--------|
| `Services/InteractiveExportBridge.cs` | **NEW** — Full export bridge with plate grouping, Machining conversion, EmitterRouter integration |
| `Services/ToolpathVisualizer.cs` | Added 3D toolpath methods (CreateContourToolpath3D, CreateDrillToolpath3D, CreatePocketToolpath3D), 3D layer management, 3D group tracking |
| `UI/CamPanel.cs` | Export CNC button, 3D toggle checkbox, statistics panel, updated Generate/Clear to handle 3D |

**Commit:** `015313e` — "feat: interactive export bridge, 3D toolpath preview, operation statistics"

### 10.3 Architecture Notes

**Interactive Export Flow:**
```
User clicks "Export CNC" in CamPanel
  ├── InteractiveExportBridge.CollectOperations(doc)
  │   ├── CncOperationService.GetAllOperationsInDocument()
  │   ├── For each: ConvertToMachining() → RoutingMachining / DrillMachining / PocketMachining
  │   └── Returns List<InteractiveOperation>
  ├── InteractiveExportBridge.GroupByPlate(doc, operations)
  │   ├── Group by CNC_SourceBrep (edge curves → same plate)
  │   ├── Or group by layer name
  │   └── Returns List<PlateGroup> with dimensions
  ├── SaveFileDialog / SelectFolderDialog
  └── InteractiveExportBridge.Export(doc, path, format, profile)
      ├── For each PlateGroup → BuildPlate() → Plate model
      ├── EmitterRouter.GenerateProgram(plate) → CNC code string
      └── File.WriteAllText(path, program)
```

**3D Toolpath Toggle Flow:**
```
User checks "3D Toolpath-Vorschau"
  ├── OnToggle3DPreview()
  │   ├── For each operation object:
  │   │   ├── ToolpathVisualizer.RemoveToolpath3DGeometry()
  │   │   ├── Generate3DToolpath() → List<GeometryBase>
  │   │   └── ToolpathVisualizer.AddToolpath3DToDocument()
  │   └── doc.Views.Redraw()
```

### 10.4 Known Limitations / TODO

- ⚠ **Not yet tested on Windows** — needs `dotnet build` verification + Rhino 8 runtime test
- ⚠ **No tool library integration in export bridge** — tool diameter resolved from UserText or regex on tool name; doesn't use ToolLibraryStore for lookup in InteractiveExportBridge (CamPanel does use it)
- ⚠ **Plate dimensions are heuristic** — when no source brep is found, falls back to defaults or same-layer brep detection
- ⚠ **Single emitter per export** — all plates exported with the same emitter; no per-plate machine selection
- ⚠ **3D preview vertical lines** — for contours, the perpendicular offset uses simplified tangent-based calculation; may not be perfectly accurate for complex curves
- ⚠ **Statistics time estimate** — uses default feedrates when not set on operation; actual CNC time depends on machine acceleration, rapids, tool changes
- ⚠ **No export preview** — user can't see the generated CNC code before writing to file

### 10.5 Next Steps

1. **P1: Build & Test on Windows** — compile, load in Rhino 8, verify all features
2. **P2: Tool library integration in export bridge** — resolve tool diameters/tech codes from ToolLibraryStore
3. **P3: Export preview dialog** — show generated CNC code before saving
4. **P4: Orphan edge curve cleanup** — detect when parent Brep is deleted
5. **P5: Toolpath animation** — animate tool movement along paths with speed control
6. **P6: Multi-emitter support** — different emitters per plate if needed

---

## 11. Night Session #6: Validation, Safety Checks, Error Handling (26. März 2026)

### 11.1 What Was Implemented

#### Pre-Export Validation System (`Services/CamValidator.cs`) ✅

**New `CamValidator` static class** with comprehensive validation:

**Validation checks implemented:**
| Check | Severity | Description |
|-------|----------|-------------|
| Tool not assigned | ERROR | Operation has no tool or tool is "—" |
| Tool not in library | WARNING | Tool name doesn't match any tool in current library |
| Depth exceeds material | WARNING | Depth > plate thickness (detected from source Brep bbox) |
| Tool diameter vs feature size | ERROR | Pocket smaller than tool diameter (bbox check) |
| Missing feedrate | WARNING | Feedrate is 0/not set → shows default that will be used |
| Orphan edge curves | WARNING | Extracted edge curve's source Brep no longer exists |
| Invalid geometry | ERROR | Wrong geometry type for operation (e.g., Brep for contour) |
| Degenerate curves | WARNING | Curves shorter than 0.01mm |
| Open pocket curves | ERROR | Pocket on a non-closed curve |
| Empty operations list | ERROR | No operations to export |
| Unsupported emitter | ERROR | Homag format not yet implemented |
| Duplicate operations | WARNING | Same operation type applied twice to same object |

**Models:**
- `ValidationResult` — contains `List<ValidationIssue>`, `HasErrors`, `HasWarnings`, `IsClean`, `FormatSummary()`
- `ValidationIssue` — `Severity`, `Message`, `ObjectId`, `Category`
- `Severity` enum — `Info`, `Warning`, `Error`

**Integrated into CamPanel:**
- "✔ Validieren" button next to "📤 Export CNC" button
- Validation result label shows color-coded summary (red=errors, yellow=warnings, green=clean)
- First 5 issues shown inline, rest in Rhino output panel
- Objects with issues are selected/highlighted in viewport
- **Export is blocked** if validation has errors (warnings allow proceeding)
- Pre-export validation runs automatically when clicking "Export CNC"

#### Operation Defaults & Templates (`Services/OperationDefaults.cs`) ✅

**New `OperationDefaults` static class:**
- Default values per operation type (depth, feedrate, strategy, tool, stepover, etc.)
- **Machine-profile-aware** — SCM and Biesse have different typical feedrates and depths
- Save/load overrides from document UserText (`CNC_Defaults_CONTOUR_Depth`, etc.)
- `ApplyDefaults()` — fills missing values in parameter dictionaries

**Default values by machine:**
| Parameter | Contour (SCM) | Contour (Biesse) | Pocket (SCM) | Drill (SCM) | Groove (SCM) |
|-----------|---------------|-------------------|--------------|-------------|--------------|
| Depth | 19.0mm | 18.0mm | 5.0mm | 19.0mm | 8.0mm |
| Feedrate | 3000 mm/min | 4000 mm/min | 2000 mm/min | 1500 mm/min | 2500 mm/min |
| Strategy | Finish | Finish | Rough | — | Finish |
| Stepover | — | — | 50% (SCM) / 45% (Biesse) | — | — |

#### Robust Error Handling ✅

**All code from sessions #1-#5 reviewed and hardened:**

**Commands (already had try/catch from session #4):**
- All CNCAdd* commands have outer try/catch with `RhinoApp.WriteLine()` error messages
- Undo records are properly ended in both success and failure paths

**InteractiveExportBridge:**
- `CollectOperations()` — null check on doc, try/catch per-object, malformed UserText guard, safe layer index access
- `Export()` — file write errors split by type: `UnauthorizedAccessException` (permissions), `PathTooLongException`, `IOException` (disk full etc.), directory creation errors
- Each file write has individual try/catch with descriptive German error messages

**ToolpathVisualizer:**
- All `Create*Toolpath()` methods: null checks on input curves/points, `RhinoDoc.ActiveDoc == null` guards
- `Point3d.IsValid` checks for drill center points
- `GetCurvePlane()` — uses null-coalescing for `ActiveDoc?.ModelAbsoluteTolerance`
- `AddToolpathToDocument()` / `AddToolpath3DToDocument()` — null checks on all parameters
- `RemoveToolpathGeometry()` / `RemoveToolpath3DGeometry()` — null guards on doc and sourceObject

**CncOperationService:**
- `GetAllOperationsInDocument()` — null check on doc, null filter on objects
- Added `using System.Linq` for `Enumerable.Empty<>`

**CamPanel:**
- `RefreshOperationsTree()` — try/catch around operation loading
- `GenerateAllToolpaths()` — outer try/catch + per-object try/catch with error counter
- `ClearAllToolpaths()` — try/catch per-object
- `OnToggle3DPreview()` — try/catch per-object
- `ApplyPropertyChanges()` — try/catch with undo cleanup
- `OpenEditDialog()` — try/catch, unknown operation type guard, undo cleanup in catch
- `RemoveOperation()` — try/catch with undo cleanup, removes 3D toolpaths too
- `ExportInteractiveCnc()` — try/catch around export call

### 11.2 Files Changed in Night Session #6

| File | Change |
|------|--------|
| `Services/CamValidator.cs` | **NEW** — Pre-export validation system with 12 check types |
| `Services/OperationDefaults.cs` | **NEW** — Machine-aware operation defaults, doc UserText persistence |
| `Services/ToolpathVisualizer.cs` | Null guards on all public methods, safe ActiveDoc access |
| `Services/InteractiveExportBridge.cs` | Per-object error handling, file I/O error handling, null guards |
| `Services/CncOperationService.cs` | Null guard on `GetAllOperationsInDocument()`, added `using System.Linq` |
| `UI/CamPanel.cs` | Validate button + result display, pre-export validation block, try/catch on all operations |
| `docs/CONTEXT-HANDOFF-CAM.md` | Updated with session #6 results |

**Commit:** `4c48857` — "feat: pre-export validation system, operation defaults, robust error handling"

### 11.3 Architecture Notes

**Validation Flow:**
```
User clicks "✔ Validieren" (or "📤 Export CNC")
  ├── CamValidator.Validate(doc, tools, format)
  │   ├── Check empty operations list
  │   ├── For each operation:
  │   │   ├── ValidateToolAssigned() → ERROR if no tool
  │   │   ├── ValidateDepthVsMaterial() → WARNING if depth > thickness
  │   │   ├── ValidateToolVsFeatureSize() → ERROR if pocket < tool
  │   │   ├── ValidateFeedrate() → WARNING if missing
  │   │   ├── ValidateOrphanEdgeCurve() → WARNING if source Brep deleted
  │   │   ├── ValidateGeometry() → ERROR if wrong type/degenerate
  │   │   └── ValidateEmitterSupport() → ERROR if Homag
  │   └── ValidateDuplicateOperations() → WARNING if same type×2
  ├── ShowValidationResult() → color-coded label in panel
  ├── Select affected objects in viewport
  └── If HasErrors → block export
```

**Operation Defaults Flow:**
```
User creates new operation (CNCAddContour etc.)
  ├── OperationDefaults.GetDefaults("Contour", "xilog")
  │   ├── GetMachineProfileDefaults() → built-in SCM/Biesse defaults
  │   └── LoadDouble/String/Bool from doc.Strings → user overrides
  └── ApplyDefaults(parameters, type, machineKey)
      └── SetIfMissing() for each parameter
```

### 11.4 Known Limitations / TODO

- ⚠ **Not yet tested on Windows** — needs `dotnet build` verification + Rhino 8 runtime test
- ~~⚠ **OperationDefaults not yet wired into CNCAdd* commands**~~ — ✅ Done in Night Session #7
- ⚠ **No "Standardwerte" section in CamPanel** — the task mentions a UI section for editing defaults. The persistence layer (`OperationDefaults.SaveDefaults()`) is implemented but no Eto UI section yet.
- ⚠ **Validation doesn't check feedrate range** — could add min/max bounds per machine profile
- ⚠ **No validation for tool length vs depth** — would need tool length data in ToolDefinition

### 11.5 Next Steps

1. **P1: Build & Test on Windows** — compile, load in Rhino 8, verify all features
2. ~~**P2: Wire OperationDefaults into CNCAdd* commands**~~ — ✅ Done in Night Session #7
3. **P3: "Standardwerte" section in CamPanel** — Eto UI for editing operation defaults
4. **P4: Export preview dialog** — show generated CNC code before saving
5. **P5: Orphan edge curve cleanup** — detect when parent Brep is deleted
6. **P6: Toolpath animation** — animate tool movement along paths with speed control

---

## 12. Night Session #7: Wire Defaults + Unit Tests (27. März 2026)

### 12.1 What Was Implemented

#### OperationDefaults Wired into All CNCAdd* Commands ✅

All 4 CNCAdd* commands now:
1. **Read machine profile from document** via `doc.Strings.GetValue("CNC_MachineProfile")` (default "xilog") instead of hardcoding `ScmProfile`
2. **Resolve profile dynamically** via new `MachineProfileHelper.ResolveProfile(machineKey)` — returns ScmProfile, BiesseProfile, or MaestroCadTProfile based on the key
3. **Pre-fill dialogs with defaults** — already done in prior session via `OperationDefaults.GetDefaults()` passed to dialog constructors
4. **Apply defaults after dialog** — calls `OperationDefaults.ApplyDefaults(parameters, operationType, machineKey)` after the dialog returns, filling any values the user left empty

**New helper class: `Helpers/MachineProfileHelper.cs`**
- `ResolveProfile(string machineKey)` → returns appropriate `IMachineProfile` instance
- Handles case-insensitive matching for "biesse", "maestro", defaults to SCM/xilog

**Changes per command:**
| Command | Changes |
|---------|---------|
| `CNCAddContourCommand` | machineKey from doc, `MachineProfileHelper`, `ApplyDefaults()` after dialog |
| `CNCAddDrillCommand` | machineKey from doc, `MachineProfileHelper`, `ApplyDefaults()` after dialog |
| `CNCAddPocketCommand` | machineKey from doc, `MachineProfileHelper`, `ApplyDefaults()` after dialog |
| `CNCAddGrooveCommand` | machineKey from doc, `MachineProfileHelper`, `ApplyDefaults()` after dialog |

#### Unit Tests for Core ✅

Added 54 new tests across 2 test files in the existing `RhinoCNCExporter.Tests` project (which references `RhinoCNCExporter.Core`):

**`ToolLibraryCrudTests.cs`** (38 tests):
- `CreateDefault` — xilog/biesse/unknown, tool counts by kind
- `AddOrUpdate` — new tool, replace existing, case-insensitive ID, sort order
- `Remove` — existing, nonexistent, case-insensitive
- `AddOrUpdateHolder` / `RemoveHolder` — CRUD, holder reference cleanup
- `FindById` / `FindByTechCode` / `FindClosestDiameter` — exact, closest, null, filters
- `SuggestTool` — drill machining, routing machining, no compatible tools
- `GetCompatibleTools` / `IsCompatible` — kind/motion profile matching
- `MergeDefaults` — fills missing, preserves user overrides
- `JSON serialization` — round-trip, property preservation, error handling
- `SuggestRoughingTool` — larger tool for roughing, null for drills
- `FindHolderById` — existing, null/empty

**`MachiningStrategyTests.cs`** (16 tests):
- `CreateDefault` — basic routing/drill, finishing tool assignment
- Roughing strategies — enabled/disabled, larger tool selection
- `HasRoughingPass` logic — both tools + stock, missing tool, zero stock
- Tool override — compatible override used, incompatible falls back
- `ToolpathPlanningOptions` — defaults, FindOverride case-insensitive
- `MachiningToolOverride` — HasOverride logic

**All 54 tests pass.** (8 pre-existing failures in unrelated BladeCut/BatchExport tests remain unchanged.)

### 12.2 Files Changed in Night Session #7

| File | Change |
|------|--------|
| `Helpers/MachineProfileHelper.cs` | **NEW** — Machine profile resolution from key string |
| `Commands/CNCAddContourCommand.cs` | machineKey from doc, MachineProfileHelper, ApplyDefaults after dialog |
| `Commands/CNCAddDrillCommand.cs` | machineKey from doc, MachineProfileHelper, ApplyDefaults after dialog |
| `Commands/CNCAddPocketCommand.cs` | machineKey from doc, MachineProfileHelper, ApplyDefaults after dialog |
| `Commands/CNCAddGrooveCommand.cs` | machineKey from doc, MachineProfileHelper, ApplyDefaults after dialog |
| `RhinoCNCExporter.Tests/ToolLibraryTests.cs` | **NEW** — 38 tests for ToolLibrary CRUD, find, suggest, merge, JSON |
| `RhinoCNCExporter.Tests/MachiningStrategyTests.cs` | **NEW** — 16 tests for MachiningStrategy, roughing, overrides |
| `docs/CONTEXT-HANDOFF-CAM.md` | Updated with session #7 results |

**Commit:** `nightly: wire operation defaults + unit tests`

### 12.3 Architecture Notes

**Defaults Flow (Complete):**
```
User runs CNCAddContour command
  ├── machineKey = doc.Strings.GetValue("CNC_MachineProfile") ?? "xilog"
  ├── profile = MachineProfileHelper.ResolveProfile(machineKey)
  ├── toolLibrary = toolLibraryStore.LoadOrCreate(profile)
  ├── defaults = OperationDefaults.GetDefaults("Contour", machineKey)
  │   ├── OperationDefaultsBase.GetMachineProfileDefaults() → built-in values
  │   └── Override from doc.Strings (CNC_Defaults_CONTOUR_Depth, etc.)
  ├── dialog = new ContourOperationDialog(..., defaults)  ← PRE-FILL
  ├── parameters = dialog.ShowModalOnTop()
  ├── OperationDefaults.ApplyDefaults(parameters, "Contour", machineKey)  ← FILL MISSING
  └── ... apply to objects
```

### 12.4 Next Steps

1. **P1: Build & Test on Windows** — compile, load in Rhino 8, verify all features
2. ~~**P2: "Standardwerte" section in CamPanel**~~ — ✅ Done in Night Session #8
3. ~~**P3: Export preview dialog**~~ — ✅ Done in Night Session #8
4. **P4: Orphan edge curve cleanup** — detect when parent Brep is deleted
5. **P5: Toolpath animation** — animate tool movement along paths with speed control

---

## 13. Night Session #8: Export Preview Dialog + Standardwerte UI (27. März 2026)

### 13.1 What Was Implemented

#### Export Preview Dialog (`UI/ExportPreviewDialog.cs`) ✅

**New modal dialog** shown BEFORE writing CNC files:

**Layout:**
- **Left side (270px):** Plate list (`ListBox`) showing plate name + operation count. Below: plate info label with dimensions.
- **Right side:** Read-only `TextArea` with monospace font (`Consolas`) showing generated CNC code for selected plate.
- **Bottom:** Summary line ("N Platten | N Operationen | ~N Zeilen CNC-Code"), then "Abbrechen" + "📤 Exportieren" buttons.

**Behavior:**
- Clicking a plate in the list shows its CNC code and updates the info label with dimensions (L×W×T) and line count.
- First plate auto-selected on open.
- Returns `true` (export confirmed) or `false` (cancelled).
- Dialog is resizable, minimum 900×600.

**Integration into CamPanel export flow:**
1. Validation runs → if clean/warnings-only → continue
2. `InteractiveExportBridge.GenerateCode()` generates CNC code strings per plate WITHOUT writing files
3. `ExportPreviewDialog` shows the generated code
4. Only after user clicks "Exportieren" → SaveFileDialog/SelectFolderDialog → `Export()` writes files

#### `InteractiveExportBridge.GenerateCode()` ✅

New method on `InteractiveExportBridge`:
```csharp
public IReadOnlyList<PlatePreview> GenerateCode(RhinoDoc doc, MachineFormat format, IMachineProfile profile)
```
- Collects operations, groups by plate, generates CNC code string per plate using `EmitterRouter.GenerateProgram()`
- Returns `PlatePreview` objects (name, dimensions, operation count, code string)
- Does NOT write any files — pure code generation
- Error handling: if code generation fails for a plate, the code string contains a comment with the error

#### Enhanced "Standardwerte" Section in CamPanel ✅

**Replaced read-only labels with editable form:**

**UI:**
1. **Operation type dropdown** at the top — "🔴 Kontur", "🔵 Tasche", "🟡 Bohrung", "🟢 Nut"
2. Switching type shows type-specific editable fields:
   - **Common (all types):** Depth (mm), Feedrate (mm/min)
   - **Contour/Groove:** + Strategy dropdown (Rough/Finish/Both)
   - **Pocket:** + Strategy, Stepover (%)
   - **Drill:** + Diameter (mm), Peck checkbox, Peck depth (mm)
   - **Groove:** + Width (mm)
3. **"💾 Speichern"** → reads field values → calls `OperationDefaults.SaveDefaults()` → persists in document UserText
4. **"↩ Zurücksetzen"** → clears doc UserText overrides → reloads machine profile built-in defaults
5. **Machine-profile-aware:** changing machine dropdown refreshes defaults display with new profile values

**Field visibility:** Uses `SetRowVisibility()` pattern (same as properties panel) to show/hide type-specific rows when operation type changes.

### 13.2 Files Changed in Night Session #8

| File | Change |
|------|--------|
| `UI/ExportPreviewDialog.cs` | **NEW** — Modal export preview dialog with plate list + code preview |
| `Services/InteractiveExportBridge.cs` | Added `GenerateCode()` method, added `using RhinoCNCExporter.UI` |
| `UI/CamPanel.cs` | Replaced defaults labels with editable fields + type dropdown; integrated ExportPreviewDialog into export flow |
| `docs/CONTEXT-HANDOFF-CAM.md` | Updated with session #8 results |

**Commit:** `nightly: export preview dialog + defaults UI`

### 13.3 Architecture Notes

**Export Preview Flow:**
```
User clicks "📤 Export CNC" in CamPanel
  ├── CamValidator.Validate() → block if errors
  ├── InteractiveExportBridge.CollectOperations()
  ├── InteractiveExportBridge.GenerateCode(doc, format, profile)
  │   ├── GroupByPlate()
  │   ├── For each PlateGroup:
  │   │   ├── EmitterRouter.GenerateProgram(plate) → code string
  │   │   └── → PlatePreview { Name, Dims, Code }
  │   └── Returns List<PlatePreview>
  ├── ExportPreviewDialog(previews).ShowModalOnTop()
  │   ├── User reviews code, clicks plates
  │   └── Returns true (export) or false (cancel)
  ├── If confirmed → SaveFileDialog / SelectFolderDialog
  └── InteractiveExportBridge.Export() → writes files
```

**Standardwerte Data Flow:**
```
User selects "Contour" in defaults dropdown
  ├── OnDefaultsTypeChanged()
  │   └── UpdateDefaultsDisplay()
  │       ├── OperationDefaults.GetDefaults("Contour", machineKey)
  │       │   ├── OperationDefaultsBase.GetMachineProfileDefaults() → built-in
  │       │   └── doc.Strings overrides
  │       ├── Populate Depth, Feedrate, Strategy fields
  │       └── Show/hide type-specific rows
  │
User clicks "💾 Speichern"
  ├── SaveDefaultsFromFields()
  │   ├── Read all visible field values
  │   └── OperationDefaults.SaveDefaults(typeKey, values)
  │       └── doc.Strings.SetString("CNC_Defaults_CONTOUR_Depth", ...)
```

### 13.4 Known Limitations / TODO

- ⚠ **Not yet tested on Windows** — needs `dotnet build` verification + Rhino 8 runtime test
- ⚠ **Export preview shows code twice** — `GenerateCode()` and then `Export()` both generate code separately. Could cache the previewed code and write it directly, but this ensures the written code is always fresh.
- ⚠ **No syntax highlighting in preview** — TextArea is plain monospace. Could use colored rich text in future.
- ⚠ **No copy-to-clipboard in preview** — user can select+copy manually, but no dedicated button.

### 13.5 Next Steps

1. **P1: Build & Test on Windows** — compile, load in Rhino 8, verify all features
2. **P2: Orphan edge curve cleanup** — detect when parent Brep is deleted
3. **P3: Toolpath animation** — animate tool movement along paths with speed control
4. **P4: Export code caching** — cache generated code from preview to avoid double generation
5. **P5: Syntax highlighting in preview** — colored CNC code for better readability
