using RhinoCNCExporter.Core.Models;
using RhinoCNCExporter.Core.PlateDetection;
using Xunit;

namespace RhinoCNCExporter.Tests;

/// <summary>
/// Tests for CoordinateTransformer — pure math, no RhinoCommon needed.
/// Tests coordinate transformations from world space to plate-local space.
/// </summary>
public class CoordinateTransformerTests
{
    private const double Tol = 0.001;

    // === Identity Transform (Phase 1/2) ===

    [Fact]
    public void Identity_ReturnsUnchangedCoords()
    {
        var (x, y, z) = CoordinateTransformer.Identity(100, 200, 0);
        Assert.Equal(100, x);
        Assert.Equal(200, y);
        Assert.Equal(0, z);
    }

    // === WorldToPlateLocal: Flat Plate (Z-up normal) ===

    [Fact]
    public void FlatPlate_AtOrigin_IdentityTransform()
    {
        var origin = PlateOrigin.Identity;
        var (x, y, z) = CoordinateTransformer.WorldToPlateLocal(origin, 100, 50, 0);

        Assert.Equal(100, x, Tol);
        Assert.Equal(50, y, Tol);
        Assert.Equal(0, z, Tol);
    }

    [Fact]
    public void FlatPlate_Offset_CorrectLocalCoords()
    {
        var origin = CoordinateTransformer.CreateFlatOrigin(50, 100, 0);
        var (x, y, z) = CoordinateTransformer.WorldToPlateLocal(origin, 150, 300, 0);

        Assert.Equal(100, x, Tol);  // 150 - 50
        Assert.Equal(200, y, Tol);  // 300 - 100
        Assert.Equal(0, z, Tol);
    }

    [Fact]
    public void FlatPlate_PointAbove_PositiveZ()
    {
        var origin = CoordinateTransformer.CreateFlatOrigin(0, 0, 0);
        var (_, _, z) = CoordinateTransformer.WorldToPlateLocal(origin, 50, 50, 19);

        Assert.Equal(19, z, Tol); // 19mm above plate face
    }

    [Fact]
    public void FlatPlate_PointBelow_NegativeZ()
    {
        var origin = CoordinateTransformer.CreateFlatOrigin(0, 0, 10);
        var (_, _, z) = CoordinateTransformer.WorldToPlateLocal(origin, 0, 0, 0);

        Assert.Equal(-10, z, Tol);
    }

    // === WorldToPlateLocal: Upright Plate in XZ Plane ===

    [Fact]
    public void UprightXZ_AtOrigin_XisMapped()
    {
        // Plate stands upright in XZ plane (side panel)
        // Plate X = World X, Plate Y = World Z, Normal = World Y
        var origin = CoordinateTransformer.CreateUprightXZOrigin(0, 0, 0);
        var (x, y, z) = CoordinateTransformer.WorldToPlateLocal(origin, 100, 0, 200);

        Assert.Equal(100, x, Tol);  // World X → Plate X (along length)
        Assert.Equal(200, y, Tol);  // World Z → Plate Y (along height)
        Assert.Equal(0, z, Tol);    // World Y → Plate Z (normal/thickness)
    }

    [Fact]
    public void UprightXZ_WithOffset_CorrectLocal()
    {
        var origin = CoordinateTransformer.CreateUprightXZOrigin(10, 5, 0);
        var (x, y, z) = CoordinateTransformer.WorldToPlateLocal(origin, 110, 24, 300);

        Assert.Equal(100, x, Tol);  // 110 - 10 projected on X-axis
        Assert.Equal(300, y, Tol);  // 300 - 0 projected on Z-axis (YAxis = world Z)
        Assert.Equal(19, z, Tol);   // 24 - 5 projected on Y-axis (Normal = world Y)
    }

    [Fact]
    public void UprightXZ_ThicknessInY_MapsToLocalZ()
    {
        // Side panel: 19mm thick, sitting at Y=0
        var origin = CoordinateTransformer.CreateUprightXZOrigin(0, 0, 0);
        var (_, _, z) = CoordinateTransformer.WorldToPlateLocal(origin, 50, 19, 50);

        Assert.Equal(19, z, Tol); // 19mm into the plate
    }

    // === WorldToPlateLocal: Upright Plate in YZ Plane ===

    [Fact]
    public void UprightYZ_AtOrigin_YisMapped()
    {
        // Plate stands upright in YZ plane (back panel)
        // Plate X = World Y, Plate Y = World Z, Normal = World X
        var origin = CoordinateTransformer.CreateUprightYZOrigin(0, 0, 0);
        var (x, y, z) = CoordinateTransformer.WorldToPlateLocal(origin, 0, 200, 300);

        Assert.Equal(200, x, Tol);  // World Y → Plate X
        Assert.Equal(300, y, Tol);  // World Z → Plate Y
        Assert.Equal(0, z, Tol);    // World X → Plate Z (normal)
    }

    [Fact]
    public void UprightYZ_WithThickness_CorrectZ()
    {
        var origin = CoordinateTransformer.CreateUprightYZOrigin(5, 0, 0);
        var (_, _, z) = CoordinateTransformer.WorldToPlateLocal(origin, 15, 100, 200);

        Assert.Equal(10, z, Tol); // 15 - 5 = 10mm from plate face
    }

    // === DetermineSide Tests ===

    [Fact]
    public void DetermineSide_AtTopFace_ReturnsTop()
    {
        var side = CoordinateTransformer.DetermineSide(0, 19);
        Assert.Equal(MachiningSide.Top, side);
    }

    [Fact]
    public void DetermineSide_AtBottomFace_ReturnsBottom()
    {
        var side = CoordinateTransformer.DetermineSide(19, 19);
        Assert.Equal(MachiningSide.Bottom, side);
    }

    [Fact]
    public void DetermineSide_NearTopWithinTolerance_ReturnsTop()
    {
        var side = CoordinateTransformer.DetermineSide(0.5, 19);
        Assert.Equal(MachiningSide.Top, side);
    }

    [Fact]
    public void DetermineSide_MiddleOfPlate_DefaultsTop()
    {
        var side = CoordinateTransformer.DetermineSide(9.5, 19);
        Assert.Equal(MachiningSide.Top, side);
    }

    // === DetermineEdgeSide Tests ===

    [Fact]
    public void DetermineEdgeSide_AtLeftEdge_ReturnsLeft()
    {
        var side = CoordinateTransformer.DetermineEdgeSide(0, 100, 300, 200);
        Assert.Equal(MachiningSide.Left, side);
    }

    [Fact]
    public void DetermineEdgeSide_AtRightEdge_ReturnsRight()
    {
        var side = CoordinateTransformer.DetermineEdgeSide(300, 100, 300, 200);
        Assert.Equal(MachiningSide.Right, side);
    }

    [Fact]
    public void DetermineEdgeSide_AtFrontEdge_ReturnsFront()
    {
        var side = CoordinateTransformer.DetermineEdgeSide(150, 0, 300, 200);
        Assert.Equal(MachiningSide.Front, side);
    }

    [Fact]
    public void DetermineEdgeSide_AtBackEdge_ReturnsBack()
    {
        var side = CoordinateTransformer.DetermineEdgeSide(150, 200, 300, 200);
        Assert.Equal(MachiningSide.Back, side);
    }

    [Fact]
    public void DetermineEdgeSide_InCenter_ReturnsTop()
    {
        var side = CoordinateTransformer.DetermineEdgeSide(150, 100, 300, 200);
        Assert.Equal(MachiningSide.Top, side);
    }

    // === DetermineFeatureSide Tests ===

    [Fact]
    public void DetermineFeatureSide_OnTopFace_ReturnsTop()
    {
        var side = CoordinateTransformer.DetermineFeatureSide(150, 100, 0, 300, 200, 19);
        Assert.Equal(MachiningSide.Top, side);
    }

    [Fact]
    public void DetermineFeatureSide_OnBottomFace_ReturnsBottom()
    {
        var side = CoordinateTransformer.DetermineFeatureSide(150, 100, 19, 300, 200, 19);
        Assert.Equal(MachiningSide.Bottom, side);
    }

    [Fact]
    public void DetermineFeatureSide_OnLeftEdgeMidThickness_ReturnsLeft()
    {
        var side = CoordinateTransformer.DetermineFeatureSide(0, 100, 9.5, 300, 200, 19);
        Assert.Equal(MachiningSide.Left, side);
    }

    [Fact]
    public void DetermineFeatureSide_OnBackEdgeMidThickness_ReturnsBack()
    {
        var side = CoordinateTransformer.DetermineFeatureSide(150, 200, 9.5, 300, 200, 19);
        Assert.Equal(MachiningSide.Back, side);
    }

    [Fact]
    public void DetermineFeatureSide_InInteriorMidThickness_DefaultsTop()
    {
        var side = CoordinateTransformer.DetermineFeatureSide(150, 100, 9.5, 300, 200, 19);
        Assert.Equal(MachiningSide.Top, side);
    }

    // === CreateOrigin Factory Methods ===

    [Fact]
    public void CreateFlatOrigin_HasZUpNormal()
    {
        var origin = CoordinateTransformer.CreateFlatOrigin(10, 20, 0);

        Assert.Equal(10, origin.OriginX);
        Assert.Equal(20, origin.OriginY);
        Assert.Equal(0, origin.OriginZ);
        Assert.Equal((1, 0, 0), origin.XAxis);
        Assert.Equal((0, 1, 0), origin.YAxis);
        Assert.Equal((0, 0, 1), origin.Normal);
    }

    [Fact]
    public void CreateUprightXZOrigin_HasYNormal()
    {
        var origin = CoordinateTransformer.CreateUprightXZOrigin(0, 0, 0);

        Assert.Equal((1, 0, 0), origin.XAxis);
        Assert.Equal((0, 0, 1), origin.YAxis);
        Assert.Equal((0, 1, 0), origin.Normal);
    }

    [Fact]
    public void CreateUprightYZOrigin_HasXNormal()
    {
        var origin = CoordinateTransformer.CreateUprightYZOrigin(0, 0, 0);

        Assert.Equal((0, 1, 0), origin.XAxis);
        Assert.Equal((0, 0, 1), origin.YAxis);
        Assert.Equal((1, 0, 0), origin.Normal);
    }

    // === Roundtrip Tests ===

    [Fact]
    public void FlatPlate_BlockPositionRoundtrip()
    {
        // Place a block at world (150, 250, 0), plate origin at (50, 100, 0)
        var origin = CoordinateTransformer.CreateFlatOrigin(50, 100, 0);
        var (localX, localY, localZ) = CoordinateTransformer.WorldToPlateLocal(origin, 150, 250, 0);

        Assert.Equal(100, localX, Tol);
        Assert.Equal(150, localY, Tol);
        Assert.Equal(0, localZ, Tol);
    }

    [Fact]
    public void UprightPlate_SidePanel_BlockOnFace()
    {
        // Side panel standing upright at Y=0, 19mm thick in Y
        // Block placed at world (200, 0, 400) — should be at plate (200, 400, 0)
        var origin = CoordinateTransformer.CreateUprightXZOrigin(0, 0, 0);
        var (localX, localY, localZ) = CoordinateTransformer.WorldToPlateLocal(origin, 200, 0, 400);

        Assert.Equal(200, localX, Tol);
        Assert.Equal(400, localY, Tol);
        Assert.Equal(0, localZ, Tol);   // On the face
    }

    [Fact]
    public void UprightPlate_SidePanel_BlockOnEdge()
    {
        // Block at world (0, 9.5, 100) on a side panel at Y=0
        var origin = CoordinateTransformer.CreateUprightXZOrigin(0, 0, 0);
        var (localX, localY, localZ) = CoordinateTransformer.WorldToPlateLocal(origin, 0, 9.5, 100);

        Assert.Equal(0, localX, Tol);
        Assert.Equal(100, localY, Tol);  // World Z → plate Y
        Assert.Equal(9.5, localZ, Tol);  // World Y → plate Z (mid-thickness for 19mm plate)
    }
}
