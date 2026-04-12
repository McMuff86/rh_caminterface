namespace RhinoCNCExporter.UI;

/// <summary>
/// Stable semantic IDs for desktop UI automation and smoke tests.
/// Keep names stable even if labels or layout change.
/// </summary>
internal static class UiAutomationIds
{
    public const string CamPanel = "rhcam.main.panel";
    public const string MachineProfile = "rhcam.setup.machineProfile";

    public const string AddContour = "rhcam.feature.addContour";
    public const string AddPocket = "rhcam.feature.addPocket";
    public const string AddDrill = "rhcam.feature.addDrill";
    public const string AddGroove = "rhcam.feature.addGroove";

    public const string OperationsGrid = "rhcam.features.grid";
    public const string OperationsEmptyState = "rhcam.features.emptyState";

    public const string PropertyTool = "rhcam.props.tool";
    public const string PropertyDepth = "rhcam.props.depth";
    public const string PropertyEnabled = "rhcam.props.enabled";
    public const string PropertyStrategy = "rhcam.props.strategy";
    public const string PropertyWidth = "rhcam.props.width";
    public const string PropertyDiameter = "rhcam.props.diameter";
    public const string PropertyStepover = "rhcam.props.stepover";
    public const string PropertyRampEntry = "rhcam.props.rampEntry";
    public const string PropertyApply = "rhcam.props.apply";

    public const string GenerateAllToolpaths = "rhcam.preview.generateAll";
    public const string ClearAllToolpaths = "rhcam.preview.clearAll";
    public const string RefreshOperations = "rhcam.features.refresh";
    public const string Preview3dToggle = "rhcam.preview.mode3d";
    public const string Validate = "rhcam.validation.run";
    public const string ExportInteractive = "rhcam.export.run";
    public const string SimulationToggle = "rhcam.preview.simulationToggle";
    public const string SimulationSpeed = "rhcam.preview.simulationSpeed";
    public const string Cleanup = "rhcam.preview.cleanup";

    public const string ExportPanelPlateTree = "rhcam.export.plateTree";
    public const string ExportPanelWorkflowSummary = "rhcam.export.workflowSummary";
    public const string ExportPanelWorkflowFocus = "rhcam.export.workflowFocus";
    public const string ExportPanelWorkflowFocusAction = "rhcam.export.workflowFocusAction";
    public const string ExportPanelAssignDrills = "rhcam.export.assignDrills";
    public const string ExportPanelAssignInsideContours = "rhcam.export.assignInsideContours";
    public const string ExportPanelAssignOutsideContour = "rhcam.export.assignOutsideContour";
    public const string ExportPanelGeneratePreview = "rhcam.export.preview";
    public const string ExportPanelClearPreview = "rhcam.export.previewClear";
    public const string ExportPanelRunExport = "rhcam.export.batchRun";
    public const string ExportPanelToolManager = "rhcam.export.toolManager";
    public const string ExportPanelToolStrategy = "rhcam.export.toolStrategy";
    public const string ToolStrategyOnlyMissing = "rhcam.export.toolStrategy.onlyMissing";
}
