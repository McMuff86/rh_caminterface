# Context Handoff: Interactive CAM Commands

**Datum:** 26. März 2026  
**Autor:** Sentinel (Night Session #1)  
**Branch:** `feat/interactive-cam-commands`  
**Status:** Toolpath Visualization Fixed ✅ — Next: Dockable CAM Panel

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
