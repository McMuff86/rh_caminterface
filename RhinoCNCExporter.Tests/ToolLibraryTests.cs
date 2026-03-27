using RhinoCNCExporter.Core.Models;
using Xunit;

namespace RhinoCNCExporter.Tests;

/// <summary>
/// Tests for ToolLibrary: tool management, merge logic, find/suggest operations.
/// </summary>
public class ToolLibraryCrudTests
{
    #region CreateDefault

    [Fact]
    public void CreateDefault_Xilog_HasTools()
    {
        var library = ToolLibrary.CreateDefault("xilog");

        Assert.NotEmpty(library.Tools);
        Assert.NotEmpty(library.Holders);
        Assert.Equal("xilog", library.MachineKey);
    }

    [Fact]
    public void CreateDefault_Biesse_HasTools()
    {
        var library = ToolLibrary.CreateDefault("biesse");

        Assert.NotEmpty(library.Tools);
        Assert.NotEmpty(library.Holders);
        Assert.Equal("biesse", library.MachineKey);
    }

    [Fact]
    public void CreateDefault_UnknownKey_DefaultsToXilog()
    {
        var library = ToolLibrary.CreateDefault("unknown_machine");
        Assert.Equal("xilog", library.MachineKey);
    }

    [Fact]
    public void CreateDefault_Xilog_ContainsRouters()
    {
        var library = ToolLibrary.CreateDefault("xilog");
        var routers = library.Tools.Where(t => t.Kind == ToolKind.Router).ToList();
        Assert.True(routers.Count >= 4, "Should have at least 4 router tools");
    }

    [Fact]
    public void CreateDefault_Xilog_ContainsDrills()
    {
        var library = ToolLibrary.CreateDefault("xilog");
        var drills = library.Tools.Where(t => t.Kind == ToolKind.Drill).ToList();
        Assert.True(drills.Count >= 3, "Should have at least 3 drill tools");
    }

    [Fact]
    public void CreateDefault_Xilog_ContainsSaw()
    {
        var library = ToolLibrary.CreateDefault("xilog");
        var saws = library.Tools.Where(t => t.Kind == ToolKind.Saw).ToList();
        Assert.Single(saws);
    }

    #endregion

    #region AddOrUpdate Tool

    [Fact]
    public void AddOrUpdate_NewTool_AddsToList()
    {
        var library = CreateEmptyLibrary();
        var tool = CreateRouter("test1", "Test Router", 8.0);

        var updated = library.AddOrUpdate(tool);

        Assert.Single(updated.Tools);
        Assert.Equal("test1", updated.Tools[0].Id);
    }

    [Fact]
    public void AddOrUpdate_ExistingTool_Replaces()
    {
        var library = CreateEmptyLibrary();
        var tool1 = CreateRouter("test1", "Original", 8.0);
        var tool2 = CreateRouter("test1", "Updated", 10.0);

        var updated = library.AddOrUpdate(tool1).AddOrUpdate(tool2);

        Assert.Single(updated.Tools);
        Assert.Equal("Updated", updated.Tools[0].Name);
        Assert.Equal(10.0, updated.Tools[0].NominalDiameter);
    }

    [Fact]
    public void AddOrUpdate_CaseInsensitiveId()
    {
        var library = CreateEmptyLibrary();
        var tool1 = CreateRouter("Test1", "Original", 8.0);
        var tool2 = CreateRouter("test1", "Updated", 10.0);

        var updated = library.AddOrUpdate(tool1).AddOrUpdate(tool2);

        Assert.Single(updated.Tools);
        Assert.Equal("Updated", updated.Tools[0].Name);
    }

    [Fact]
    public void AddOrUpdate_MultipleDifferentTools_SortedByKindThenDiameter()
    {
        var library = CreateEmptyLibrary();
        var big = CreateRouter("r1", "Big Router", 12.0);
        var small = CreateRouter("r2", "Small Router", 6.0);
        var drill = CreateDrill("d1", "Drill 5", 5.0);

        var updated = library.AddOrUpdate(big).AddOrUpdate(drill).AddOrUpdate(small);

        // Drill first (enum order), then routers by diameter
        Assert.Equal(ToolKind.Router, updated.Tools[0].Kind);
        Assert.Equal(6.0, updated.Tools[0].NominalDiameter);
        Assert.Equal(12.0, updated.Tools[1].NominalDiameter);
        Assert.Equal(ToolKind.Drill, updated.Tools[2].Kind);
    }

    #endregion

    #region Remove Tool

    [Fact]
    public void Remove_ExistingTool_RemovesIt()
    {
        var library = CreateEmptyLibrary()
            .AddOrUpdate(CreateRouter("r1", "Router 1", 8.0))
            .AddOrUpdate(CreateRouter("r2", "Router 2", 10.0));

        var updated = library.Remove("r1");

        Assert.Single(updated.Tools);
        Assert.Equal("r2", updated.Tools[0].Id);
    }

    [Fact]
    public void Remove_NonExistentTool_NoChange()
    {
        var library = CreateEmptyLibrary()
            .AddOrUpdate(CreateRouter("r1", "Router 1", 8.0));

        var updated = library.Remove("nonexistent");

        Assert.Single(updated.Tools);
    }

    [Fact]
    public void Remove_CaseInsensitive()
    {
        var library = CreateEmptyLibrary()
            .AddOrUpdate(CreateRouter("Test1", "Router", 8.0));

        var updated = library.Remove("test1");

        Assert.Empty(updated.Tools);
    }

    #endregion

    #region AddOrUpdateHolder / RemoveHolder

    [Fact]
    public void AddOrUpdateHolder_NewHolder_Adds()
    {
        var library = CreateEmptyLibrary();
        var holder = new ToolHolderDefinition { Id = "h1", Name = "Holder 1" };

        var updated = library.AddOrUpdateHolder(holder);

        Assert.Single(updated.Holders);
        Assert.Equal("h1", updated.Holders[0].Id);
    }

    [Fact]
    public void RemoveHolder_ClearsToolReference()
    {
        var library = CreateEmptyLibrary()
            .AddOrUpdateHolder(new ToolHolderDefinition { Id = "h1", Name = "Holder 1" })
            .AddOrUpdate(CreateRouter("r1", "Router", 8.0) with { HolderId = "h1" });

        var updated = library.RemoveHolder("h1");

        Assert.Empty(updated.Holders);
        Assert.Single(updated.Tools);
        Assert.Null(updated.Tools[0].HolderId);
    }

    #endregion

    #region FindById / FindByTechCode / FindClosestDiameter

    [Fact]
    public void FindById_ExistingTool_ReturnsIt()
    {
        var library = ToolLibrary.CreateDefault("xilog");
        var tool = library.FindById("scm_router_12");
        Assert.NotNull(tool);
        Assert.Equal(12.0, tool.NominalDiameter);
    }

    [Fact]
    public void FindById_NonExistent_ReturnsNull()
    {
        var library = ToolLibrary.CreateDefault("xilog");
        Assert.Null(library.FindById("nonexistent_tool"));
    }

    [Fact]
    public void FindById_Null_ReturnsNull()
    {
        var library = ToolLibrary.CreateDefault("xilog");
        Assert.Null(library.FindById(null));
    }

    [Fact]
    public void FindByTechCode_ExistingCode_ReturnsCorrectTool()
    {
        var library = ToolLibrary.CreateDefault("xilog");
        var tool = library.FindByTechCode("E013");
        Assert.NotNull(tool);
        Assert.Equal("scm_router_12", tool.Id);
    }

    [Fact]
    public void FindByTechCode_WithKindFilter_ReturnsCorrectType()
    {
        var library = ToolLibrary.CreateDefault("xilog");
        var tool = library.FindByTechCode("D5", ToolKind.Drill);
        Assert.NotNull(tool);
        Assert.Equal(ToolKind.Drill, tool.Kind);
    }

    [Fact]
    public void FindByTechCode_Null_ReturnsNull()
    {
        var library = ToolLibrary.CreateDefault("xilog");
        Assert.Null(library.FindByTechCode(null));
    }

    [Fact]
    public void FindClosestDiameter_ExactMatch()
    {
        var library = ToolLibrary.CreateDefault("xilog");
        var tool = library.FindClosestDiameter(12.0, ToolKind.Router);
        Assert.NotNull(tool);
        Assert.Equal(12.0, tool.NominalDiameter);
    }

    [Fact]
    public void FindClosestDiameter_ClosestMatch()
    {
        var library = ToolLibrary.CreateDefault("xilog");
        var tool = library.FindClosestDiameter(7.0, ToolKind.Router);
        Assert.NotNull(tool);
        // Should be 6mm (closest to 7mm among available routers)
        Assert.Equal(6.0, tool.NominalDiameter);
    }

    [Fact]
    public void FindClosestDiameter_NoToolsOfKind_ReturnsNull()
    {
        var library = CreateEmptyLibrary()
            .AddOrUpdate(CreateRouter("r1", "Router", 8.0));

        Assert.Null(library.FindClosestDiameter(8.0, ToolKind.Drill));
    }

    #endregion

    #region SuggestTool

    [Fact]
    public void SuggestTool_DrillMachining_SuggestsDrill()
    {
        var library = ToolLibrary.CreateDefault("xilog");
        var machining = new DrillMachining
        {
            Name = "Test Drill",
            X = 50, Y = 50, Depth = 13, Diameter = 5.0
        };

        var tool = library.SuggestTool(machining);

        Assert.NotNull(tool);
        Assert.Equal(ToolKind.Drill, tool.Kind);
        Assert.Equal(5.0, tool.NominalDiameter);
    }

    [Fact]
    public void SuggestTool_RoutingMachining_SuggestsRouter()
    {
        var library = ToolLibrary.CreateDefault("xilog");
        var machining = new RoutingMachining
        {
            Name = "Test Route",
            Points = new[] { (0.0, 0.0), (100.0, 0.0), (100.0, 100.0), (0.0, 100.0) },
            Depth = 19,
            ToolDiameter = 6.0,
            IsClosed = true
        };

        var tool = library.SuggestTool(machining);

        Assert.NotNull(tool);
        Assert.Equal(ToolKind.Router, tool.Kind);
    }

    [Fact]
    public void SuggestTool_NoCompatibleTools_ReturnsNull()
    {
        var library = CreateEmptyLibrary();
        var machining = new DrillMachining
        {
            Name = "Test", X = 0, Y = 0, Depth = 10, Diameter = 5
        };

        Assert.Null(library.SuggestTool(machining));
    }

    #endregion

    #region GetCompatibleTools / IsCompatible

    [Fact]
    public void GetCompatibleTools_DrillMachining_OnlyReturnsFixedAggregates()
    {
        var library = ToolLibrary.CreateDefault("xilog");
        var machining = new DrillMachining
        {
            Name = "Test", X = 0, Y = 0, Depth = 10, Diameter = 8
        };

        var compatible = library.GetCompatibleTools(machining);

        Assert.All(compatible, t => Assert.Equal(ToolKind.Drill, t.Kind));
        Assert.All(compatible, t => Assert.True(t.IsFixedAggregate));
    }

    [Fact]
    public void IsCompatible_RouterWithDrillMachining_ReturnsFalse()
    {
        var library = ToolLibrary.CreateDefault("xilog");
        var machining = new DrillMachining
        {
            Name = "Test", X = 0, Y = 0, Depth = 10, Diameter = 8
        };
        var router = library.FindById("scm_router_12")!;

        Assert.False(library.IsCompatible(machining, router));
    }

    [Fact]
    public void IsCompatible_DrillWithDrillMachining_ReturnsTrue()
    {
        var library = ToolLibrary.CreateDefault("xilog");
        var machining = new DrillMachining
        {
            Name = "Test", X = 0, Y = 0, Depth = 10, Diameter = 8
        };
        var drill = library.FindById("scm_drill_8")!;

        Assert.True(library.IsCompatible(machining, drill));
    }

    #endregion

    #region MergeDefaults

    [Fact]
    public void MergeDefaults_FillsMissingProperties()
    {
        var defaults = ToolLibrary.CreateDefault("xilog");
        var sparse = new ToolLibrary
        {
            Name = "User Library",
            MachineKey = "xilog",
            Tools = new[]
            {
                new ToolDefinition
                {
                    Id = "scm_router_12",
                    Name = "My Router 12",
                    Kind = ToolKind.Router,
                    NominalDiameter = 12.0,
                    // Missing: HolderId, ShankDiameter, etc.
                }
            }
        };

        var merged = sparse.MergeDefaults(defaults);

        var tool = merged.FindById("scm_router_12");
        Assert.NotNull(tool);
        Assert.Equal("My Router 12", tool.Name); // User name preserved
        Assert.NotNull(tool.HolderId); // Filled from defaults
    }

    [Fact]
    public void MergeDefaults_PreservesUserOverrides()
    {
        var defaults = ToolLibrary.CreateDefault("xilog");
        var custom = new ToolLibrary
        {
            Name = "Custom",
            MachineKey = "xilog",
            Tools = new[]
            {
                new ToolDefinition
                {
                    Id = "scm_router_12",
                    Name = "Custom Name",
                    Kind = ToolKind.Router,
                    NominalDiameter = 12.0,
                    ShankDiameter = 99.9, // User override
                    HolderId = "scm_er32_collet"
                }
            }
        };

        var merged = custom.MergeDefaults(defaults);
        var tool = merged.FindById("scm_router_12");

        Assert.NotNull(tool);
        Assert.Equal(99.9, tool.ShankDiameter); // Preserved
    }

    #endregion

    #region JSON Serialization

    [Fact]
    public void ToJson_FromJson_RoundTrip()
    {
        var library = ToolLibrary.CreateDefault("xilog");
        var json = library.ToJson();
        var deserialized = ToolLibrary.FromJson(json);

        Assert.Equal(library.Name, deserialized.Name);
        Assert.Equal(library.MachineKey, deserialized.MachineKey);
        Assert.Equal(library.Tools.Count, deserialized.Tools.Count);
        Assert.Equal(library.Holders.Count, deserialized.Holders.Count);
    }

    [Fact]
    public void ToJson_FromJson_PreservesToolProperties()
    {
        var library = ToolLibrary.CreateDefault("xilog");
        var json = library.ToJson();
        var deserialized = ToolLibrary.FromJson(json);

        var original = library.FindById("scm_router_12")!;
        var roundtripped = deserialized.FindById("scm_router_12")!;

        Assert.Equal(original.Name, roundtripped.Name);
        Assert.Equal(original.NominalDiameter, roundtripped.NominalDiameter);
        Assert.Equal(original.Kind, roundtripped.Kind);
        Assert.Equal(original.TechCode, roundtripped.TechCode);
        Assert.Equal(original.MotionProfile, roundtripped.MotionProfile);
    }

    [Fact]
    public void FromJson_InvalidJson_Throws()
    {
        Assert.Throws<System.Text.Json.JsonException>(() => ToolLibrary.FromJson("not valid json"));
    }

    [Fact]
    public void FromJson_EmptyString_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => ToolLibrary.FromJson(""));
    }

    #endregion

    #region SuggestRoughingTool

    [Fact]
    public void SuggestRoughingTool_FindsLargerTool()
    {
        var library = ToolLibrary.CreateDefault("xilog");
        var machining = new RoutingMachining
        {
            Name = "Test",
            Points = new[] { (0.0, 0.0), (100.0, 0.0) },
            Depth = 19,
            ToolDiameter = 6.0,
            IsClosed = false
        };
        var finishTool = library.FindClosestDiameter(6.0, ToolKind.Router);

        var roughing = library.SuggestRoughingTool(machining, finishTool);

        Assert.NotNull(roughing);
        Assert.True(roughing.NominalDiameter > finishTool!.NominalDiameter);
    }

    [Fact]
    public void SuggestRoughingTool_DrillMachining_ReturnsNull()
    {
        var library = ToolLibrary.CreateDefault("xilog");
        var machining = new DrillMachining
        {
            Name = "Test", X = 0, Y = 0, Depth = 10, Diameter = 5
        };

        Assert.Null(library.SuggestRoughingTool(machining, null));
    }

    #endregion

    #region FindHolderById

    [Fact]
    public void FindHolderById_Existing_ReturnsHolder()
    {
        var library = ToolLibrary.CreateDefault("xilog");
        var holder = library.FindHolderById("scm_er32_collet");
        Assert.NotNull(holder);
        Assert.Contains("HSK63F", holder.Name);
    }

    [Fact]
    public void FindHolderById_NullOrEmpty_ReturnsNull()
    {
        var library = ToolLibrary.CreateDefault("xilog");
        Assert.Null(library.FindHolderById(null));
        Assert.Null(library.FindHolderById(""));
        Assert.Null(library.FindHolderById("   "));
    }

    #endregion

    #region Helpers

    private static ToolLibrary CreateEmptyLibrary() => new()
    {
        Name = "Test",
        MachineKey = "xilog",
        Tools = Array.Empty<ToolDefinition>(),
        Holders = Array.Empty<ToolHolderDefinition>()
    };

    private static ToolDefinition CreateRouter(string id, string name, double diameter) => new()
    {
        Id = id,
        Name = name,
        Kind = ToolKind.Router,
        NominalDiameter = diameter,
        MotionProfile = ToolMotionProfile.Freeform2D
    };

    private static ToolDefinition CreateDrill(string id, string name, double diameter) => new()
    {
        Id = id,
        Name = name,
        Kind = ToolKind.Drill,
        NominalDiameter = diameter,
        MotionProfile = ToolMotionProfile.PointOnly,
        IsFixedAggregate = true
    };

    #endregion
}
