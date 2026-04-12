using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Rhino;
using Rhino.Display;
using Rhino.DocObjects;
using Rhino.Geometry;
using RhinoCNCExporter.Core.Blocks;

namespace RhinoCNCExporter.Services;

/// <summary>
/// Toolpath animation/simulation: synthesizes a simple 3D tool motion from the
/// interactive CAM operations, including clearance, plunge/feed and retract moves.
/// </summary>
public sealed class ToolpathAnimator : IDisposable
{
    private const double MinimumSegmentLength = 0.01;
    private const double MinimumClearance = 5.0;

    /// <summary>Playback speed multiplier.</summary>
    public double SpeedMultiplier { get; set; } = 1.0;

    /// <summary>True while the animation is running.</summary>
    public bool IsRunning => _running;

    /// <summary>Raised when playback starts or stops (for UI button state).</summary>
    public event Action<bool>? RunningChanged;

    // --- Internal state ---
    private readonly List<AnimationSegment> _segments = new();
    private int _currentSegmentIndex;
    private double _t; // normalized 0→1 within current segment
    private bool _running;
    private DateTime _lastFrameTime;
    private readonly ToolConduit _conduit = new();
    private RhinoDoc? _doc;
    private bool _disposed;

    /// <summary>
    /// Loads operations for animation. Prefers a more faithful cut path than the raw
    /// source geometry by synthesizing 3D feed/plunge/retract motion from the actual
    /// operation parameters.
    /// </summary>
    public ToolpathAnimationLoadResult Load(RhinoDoc doc, IEnumerable<RhinoObject>? operationObjects = null)
    {
        ArgumentNullException.ThrowIfNull(doc);

        Stop();
        _doc = doc;
        _segments.Clear();
        _currentSegmentIndex = 0;
        _t = 0;
        _conduit.ToolPosition = Point3d.Unset;
        _conduit.Segments = Array.Empty<AnimationSegment>();
        _conduit.CurrentSegmentIndex = -1;
        _conduit.CurrentSegmentProgress = 0;

        var objects = (operationObjects?.ToList()
                      ?? CncOperationService.GetEnabledOperationsInDocument(doc).ToList())
            .Where(CncOperationService.IsOperationEnabled)
            .ToList();
        var skippedReasons = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        Point3d? currentSafePoint = null;

        static void AddSkippedReason(IDictionary<string, int> reasons, string reason)
        {
            reasons[reason] = reasons.TryGetValue(reason, out var count) ? count + 1 : 1;
        }

        foreach (var obj in objects)
        {
            var op = CncOperationService.GetOperation(obj);
            if (op == null)
            {
                AddSkippedReason(skippedReasons, "ohne CNC-Operation");
                continue;
            }

            var type = op.Type;
            var toolDiameter = GetToolDiameter(op);
            var depth = GetDepth(op);

            if (toolDiameter <= 0)
            {
                AddSkippedReason(skippedReasons, "ohne gültigen Werkzeugdurchmesser");
                continue;
            }

            if (type.Equals(CncOperationSchema.TYPE_DRILL, StringComparison.OrdinalIgnoreCase) && depth <= 0)
            {
                AddSkippedReason(skippedReasons, "Drill-Tiefe ist 0 oder fehlt");
                continue;
            }

            var segments = BuildSegmentsForOperation(doc, obj, op, currentSafePoint);
            if (segments.Count == 0)
            {
                if (type.Equals(CncOperationSchema.TYPE_DRILL, StringComparison.OrdinalIgnoreCase))
                {
                    if (obj.Geometry is not Rhino.Geometry.Point && obj.Geometry is not Curve)
                        AddSkippedReason(skippedReasons, "Drill-Geometrie wird nicht unterstützt");
                    else
                        AddSkippedReason(skippedReasons, "Drill: keine verwertbaren Segmente");
                }
                else if (obj.Geometry is not Curve)
                {
                    AddSkippedReason(skippedReasons, $"{type}: Geometrie ist keine Kurve");
                }
                else if (obj.Geometry is Curve curve && curve.GetLength() < MinimumSegmentLength)
                {
                    AddSkippedReason(skippedReasons, $"{type}: Kurve zu kurz");
                }
                else
                {
                    AddSkippedReason(skippedReasons, $"{type}: keine verwertbaren Segmente");
                }

                continue;
            }

            _segments.AddRange(segments);
            currentSafePoint = segments[^1].Curve.PointAtEnd;
        }

        if (_segments.Count > 0)
        {
            var first = _segments[0];
            _conduit.Segments = _segments;
            _conduit.CurrentSegmentIndex = 0;
            _conduit.CurrentSegmentProgress = 0;
            _conduit.ToolPosition = first.Curve.PointAtStart;
            _conduit.SegmentStart = first.Curve.PointAtStart;
            _conduit.ToolDiameter = first.ToolDiameter;
            _conduit.ToolAxis = first.ToolAxis;
            _conduit.ToolTangent = GetSegmentDirection(first.Curve);
            _conduit.OperationType = first.OperationType;
            _conduit.SegmentKind = first.Kind;
        }

        return new ToolpathAnimationLoadResult(objects.Count, _segments.Count, skippedReasons);
    }

    /// <summary>
    /// Starts the animation. The conduit is enabled and RhinoApp.Idle drives frame updates.
    /// </summary>
    public void Start()
    {
        if (_running || _segments.Count == 0) return;

        _currentSegmentIndex = 0;
        _t = 0;
        _lastFrameTime = DateTime.UtcNow;
        _running = true;

        _conduit.Enabled = true;
        RhinoApp.Idle += OnIdle;
        _doc?.Views.Redraw();

        RunningChanged?.Invoke(true);
    }

    /// <summary>Stops the animation and hides the conduit.</summary>
    public void Stop()
    {
        if (!_running) return;

        RhinoApp.Idle -= OnIdle;
        _conduit.Enabled = false;
        _running = false;

        _doc?.Views.Redraw();
        RunningChanged?.Invoke(false);
    }

    /// <summary>
    /// Toggle: if running, stop; if stopped, start.
    /// </summary>
    public void Toggle()
    {
        if (_running) Stop();
        else Start();
    }

    // ---- Frame update via Idle ----

    private void OnIdle(object? sender, EventArgs e)
    {
        if (!_running || _segments.Count == 0) return;

        var now = DateTime.UtcNow;
        var dt = (now - _lastFrameTime).TotalSeconds;
        _lastFrameTime = now;

        // Cap delta to avoid jumps when UI is busy
        if (dt > 0.2) dt = 0.2;

        var seg = _segments[_currentSegmentIndex];
        var curveLength = seg.Curve.GetLength();
        if (curveLength < MinimumSegmentLength) curveLength = 1.0;

        var speed = GetSegmentSpeed(seg.Kind) * SpeedMultiplier;
        var tIncrement = (speed * dt) / curveLength;
        _t += tIncrement;

        if (_t >= 1.0)
        {
            _currentSegmentIndex++;
            _t = 0;

            if (_currentSegmentIndex >= _segments.Count)
            {
                Stop();
                return;
            }

            seg = _segments[_currentSegmentIndex];
        }

        var currentSeg = _segments[_currentSegmentIndex];
        var clampedT = Math.Max(0, Math.Min(1, _t));

        if (currentSeg.Curve.NormalizedLengthParameter(clampedT, out var param))
        {
            var position = currentSeg.Curve.PointAt(param);
            var tangent = currentSeg.Curve.TangentAt(param);
            if (tangent.IsTiny())
                tangent = GetSegmentDirection(currentSeg.Curve);

            _conduit.ToolPosition = position;
            _conduit.SegmentStart = currentSeg.Curve.PointAtStart;
            _conduit.ToolDiameter = currentSeg.ToolDiameter;
            _conduit.ToolTangent = tangent;
            _conduit.ToolAxis = currentSeg.ToolAxis;
            _conduit.OperationType = currentSeg.OperationType;
            _conduit.SegmentKind = currentSeg.Kind;
            _conduit.CurrentSegmentIndex = _currentSegmentIndex;
            _conduit.CurrentSegmentProgress = clampedT;
        }

        _doc?.Views.Redraw();
    }

    // ---- Segment synthesis ----

    private static List<AnimationSegment> BuildSegmentsForOperation(
        RhinoDoc doc,
        RhinoObject obj,
        MachiningOperation op,
        Point3d? currentSafePoint)
    {
        var type = op.Type;
        var toolDiameter = GetToolDiameter(op);
        var depth = GetDepth(op);

        return type.ToUpperInvariant() switch
        {
            CncOperationSchema.TYPE_DRILL => BuildDrillSegments(obj, op, toolDiameter, depth, currentSafePoint),
            CncOperationSchema.TYPE_POCKET => BuildPocketSegments(doc, obj, op, toolDiameter, depth, currentSafePoint),
            CncOperationSchema.TYPE_CONTOUR or CncOperationSchema.TYPE_GROOVE => BuildRoutingSegments(obj, type, toolDiameter, depth, currentSafePoint),
            _ => new List<AnimationSegment>()
        };
    }

    private static List<AnimationSegment> BuildRoutingSegments(
        RhinoObject obj,
        string operationType,
        double toolDiameter,
        double depth,
        Point3d? currentSafePoint)
    {
        if (obj.Geometry is not Curve sourceCurve || sourceCurve.GetLength() < MinimumSegmentLength)
            return new List<AnimationSegment>();

        var axis = GetToolAxis(sourceCurve);
        var clearance = GetClearance(toolDiameter, depth);
        var cutCurve = TranslateCurve(sourceCurve, -axis * depth);
        if (cutCurve == null || cutCurve.GetLength() < MinimumSegmentLength)
            return new List<AnimationSegment>();

        return BuildCurveSegments(new[] { cutCurve }, operationType, toolDiameter, axis, clearance, currentSafePoint);
    }

    private static List<AnimationSegment> BuildPocketSegments(
        RhinoDoc doc,
        RhinoObject obj,
        MachiningOperation op,
        double toolDiameter,
        double depth,
        Point3d? currentSafePoint)
    {
        if (obj.Geometry is not Curve boundaryCurve || boundaryCurve.GetLength() < MinimumSegmentLength)
            return new List<AnimationSegment>();

        var axis = GetToolAxis(boundaryCurve);
        var clearance = GetClearance(toolDiameter, depth);
        var stepover = GetStepover(obj);
        var cutCurves = CreatePocketCutCurves(doc, boundaryCurve, toolDiameter, stepover)
            .Select(curve => TranslateCurve(curve, -axis * depth))
            .Where(curve => curve != null && curve.GetLength() >= MinimumSegmentLength)
            .Cast<Curve>()
            .ToList();

        if (cutCurves.Count == 0)
        {
            var fallback = TranslateCurve(boundaryCurve, -axis * depth);
            if (fallback != null && fallback.GetLength() >= MinimumSegmentLength)
                cutCurves.Add(fallback);
        }

        return BuildPocketCurveSegments(cutCurves, op, toolDiameter, axis, clearance, currentSafePoint);
    }

    private static List<AnimationSegment> BuildDrillSegments(
        RhinoObject obj,
        MachiningOperation op,
        double toolDiameter,
        double depth,
        Point3d? currentSafePoint)
    {
        Point3d center;
        switch (obj.Geometry)
        {
            case Rhino.Geometry.Point pt:
                center = pt.Location;
                break;
            case Curve curve:
                center = curve.PointAtStart;
                break;
            default:
                return new List<AnimationSegment>();
        }

        if (!center.IsValid)
            return new List<AnimationSegment>();

        var axis = Vector3d.ZAxis;
        var clearance = GetClearance(toolDiameter, depth);
        var safeStart = center + axis * clearance;
        var bottom = center - axis * depth;
        var safeEnd = bottom + axis * clearance;

        var segments = new List<AnimationSegment>();
        AddTravelIfNeeded(segments, currentSafePoint, safeStart, toolDiameter, CncOperationSchema.TYPE_DRILL, axis);

        var peckDepth = GetPeckDepth(op, depth);
        if (peckDepth > 0 && peckDepth < depth - MinimumSegmentLength)
        {
            var cycleStart = safeStart;
            var currentDepth = 0.0;
            var chipBreakHeight = Math.Min(clearance, Math.Max(toolDiameter, peckDepth));

            while (currentDepth < depth - MinimumSegmentLength)
            {
                var targetDepth = Math.Min(depth, currentDepth + peckDepth);
                var targetPoint = center - axis * targetDepth;
                AddSegmentIfValid(segments, new LineCurve(cycleStart, targetPoint), toolDiameter, CncOperationSchema.TYPE_DRILL, AnimationSegmentKind.Drill, axis);

                currentDepth = targetDepth;
                if (currentDepth >= depth - MinimumSegmentLength)
                    break;

                var chipBreakPoint = center + axis * chipBreakHeight;
                AddSegmentIfValid(segments, new LineCurve(targetPoint, chipBreakPoint), toolDiameter, CncOperationSchema.TYPE_DRILL, AnimationSegmentKind.Retract, axis);
                cycleStart = chipBreakPoint;
            }
        }
        else
        {
            AddSegmentIfValid(segments, new LineCurve(safeStart, bottom), toolDiameter, CncOperationSchema.TYPE_DRILL, AnimationSegmentKind.Drill, axis);
        }

        AddSegmentIfValid(segments, new LineCurve(bottom, safeEnd), toolDiameter, CncOperationSchema.TYPE_DRILL, AnimationSegmentKind.Retract, axis);
        return segments;
    }

    private static List<AnimationSegment> BuildPocketCurveSegments(
        IReadOnlyList<Curve> cutCurves,
        MachiningOperation op,
        double toolDiameter,
        Vector3d axis,
        double clearance,
        Point3d? currentSafePoint)
    {
        var loops = cutCurves
            .Where(curve => curve != null && curve.GetLength() >= MinimumSegmentLength)
            .Select(curve => curve.DuplicateCurve())
            .Where(curve => curve != null)
            .Cast<Curve>()
            .ToList();

        if (loops.Count == 0)
            return new List<AnimationSegment>();

        var segments = new List<AnimationSegment>();
        var firstLoop = loops[0];
        var startCut = firstLoop.PointAtStart;
        var safeStart = startCut + axis * clearance;

        AddTravelIfNeeded(segments, currentSafePoint, safeStart, toolDiameter, CncOperationSchema.TYPE_POCKET, axis);

        var activeFirstLoop = firstLoop;
        if (!TryAddPocketEntrySegments(segments, firstLoop, op, toolDiameter, axis, clearance, out activeFirstLoop))
            AddSegmentIfValid(segments, new LineCurve(safeStart, startCut), toolDiameter, CncOperationSchema.TYPE_POCKET, AnimationSegmentKind.Plunge, axis);

        AddSegmentIfValid(segments, activeFirstLoop, toolDiameter, CncOperationSchema.TYPE_POCKET, AnimationSegmentKind.Feed, axis);

        var currentPoint = activeFirstLoop.PointAtEnd;
        for (var index = 1; index < loops.Count; index++)
        {
            var nextLoop = AlignClosedCurveStartToNearestPoint(loops[index], currentPoint);
            if (nextLoop == null || nextLoop.GetLength() < MinimumSegmentLength)
                continue;

            AddSegmentIfValid(segments, new LineCurve(currentPoint, nextLoop.PointAtStart), toolDiameter, CncOperationSchema.TYPE_POCKET, AnimationSegmentKind.Feed, axis);
            AddSegmentIfValid(segments, nextLoop, toolDiameter, CncOperationSchema.TYPE_POCKET, AnimationSegmentKind.Feed, axis);
            currentPoint = nextLoop.PointAtEnd;
        }

        var safeEnd = currentPoint + axis * clearance;
        AddSegmentIfValid(segments, new LineCurve(currentPoint, safeEnd), toolDiameter, CncOperationSchema.TYPE_POCKET, AnimationSegmentKind.Retract, axis);
        return segments;
    }

    private static List<AnimationSegment> BuildCurveSegments(
        IReadOnlyList<Curve> cutCurves,
        string operationType,
        double toolDiameter,
        Vector3d axis,
        double clearance,
        Point3d? currentSafePoint)
    {
        var segments = new List<AnimationSegment>();
        Point3d? safePoint = currentSafePoint;

        foreach (var cutCurve in cutCurves)
        {
            if (cutCurve == null || cutCurve.GetLength() < MinimumSegmentLength)
                continue;

            var startCut = cutCurve.PointAtStart;
            var endCut = cutCurve.PointAtEnd;
            var safeStart = startCut + axis * clearance;
            var safeEnd = endCut + axis * clearance;

            AddTravelIfNeeded(segments, safePoint, safeStart, toolDiameter, operationType, axis);
            AddSegmentIfValid(segments, new LineCurve(safeStart, startCut), toolDiameter, operationType, AnimationSegmentKind.Plunge, axis);
            AddSegmentIfValid(segments, cutCurve, toolDiameter, operationType, AnimationSegmentKind.Feed, axis);
            AddSegmentIfValid(segments, new LineCurve(endCut, safeEnd), toolDiameter, operationType, AnimationSegmentKind.Retract, axis);

            safePoint = safeEnd;
        }

        return segments;
    }

    private static void AddTravelIfNeeded(
        List<AnimationSegment> segments,
        Point3d? from,
        Point3d to,
        double toolDiameter,
        string operationType,
        Vector3d axis)
    {
        if (!from.HasValue || from.Value.DistanceTo(to) < MinimumSegmentLength)
            return;

        AddSegmentIfValid(segments, new LineCurve(from.Value, to), toolDiameter, operationType, AnimationSegmentKind.Rapid, axis);
    }

    private static void AddSegmentIfValid(
        List<AnimationSegment> segments,
        Curve? curve,
        double toolDiameter,
        string operationType,
        AnimationSegmentKind kind,
        Vector3d axis)
    {
        if (curve == null || curve.GetLength() < MinimumSegmentLength)
            return;

        var normalizedAxis = axis;
        if (normalizedAxis.IsTiny())
            normalizedAxis = Vector3d.ZAxis;
        normalizedAxis.Unitize();

        segments.Add(new AnimationSegment(curve, toolDiameter, operationType, kind, normalizedAxis));
    }

    private static bool TryAddPocketEntrySegments(
        List<AnimationSegment> segments,
        Curve firstLoop,
        MachiningOperation op,
        double toolDiameter,
        Vector3d axis,
        double clearance,
        out Curve feedLoop)
    {
        feedLoop = firstLoop;
        var rampEntry = op.RampEntry;
        if (string.IsNullOrWhiteSpace(rampEntry))
            return false;

        if (!firstLoop.IsClosed)
            return false;

        var rampDistance = GetRampDistance(rampEntry, toolDiameter, clearance, firstLoop.GetLength());
        if (rampDistance <= MinimumSegmentLength)
            return false;

        if (!firstLoop.LengthParameter(rampDistance, out var rampParam))
            return false;

        var rampSource = firstLoop.Trim(firstLoop.Domain.T0, rampParam);
        if (rampSource == null || rampSource.GetLength() < MinimumSegmentLength)
            return false;

        var rampCurve = CreateRampCurve(rampSource, axis, clearance);
        if (rampCurve == null || rampCurve.GetLength() < MinimumSegmentLength)
            return false;

        var shiftedLoop = firstLoop.DuplicateCurve();
        if (shiftedLoop == null)
            return false;

        if (shiftedLoop.IsClosed && !shiftedLoop.ChangeClosedCurveSeam(rampParam))
            return false;

        AddSegmentIfValid(segments, rampCurve, toolDiameter, CncOperationSchema.TYPE_POCKET, AnimationSegmentKind.Entry, axis);
        feedLoop = shiftedLoop;
        return true;
    }

    private static Curve? CreateRampCurve(Curve rampSource, Vector3d axis, double clearance)
    {
        var sampleCount = Math.Max(4, (int)Math.Ceiling(rampSource.GetLength() / 10.0));
        var points = new List<Point3d>(sampleCount + 1);

        for (var index = 0; index <= sampleCount; index++)
        {
            var t = (double)index / sampleCount;
            if (!rampSource.NormalizedLengthParameter(t, out var param))
                continue;

            var point = rampSource.PointAt(param);
            var zOffset = clearance * (1.0 - t);
            points.Add(point + axis * zOffset);
        }

        if (points.Count < 2)
            return null;

        return new PolylineCurve(points);
    }

    private static Curve? AlignClosedCurveStartToNearestPoint(Curve sourceCurve, Point3d referencePoint)
    {
        var curve = sourceCurve.DuplicateCurve();
        if (curve == null)
            return null;

        if (!curve.IsClosed)
            return curve;

        if (!curve.ClosestPoint(referencePoint, out var seamParam))
            return curve;

        curve.ChangeClosedCurveSeam(seamParam);
        return curve;
    }

    private static Curve? TranslateCurve(Curve sourceCurve, Vector3d translation)
    {
        var curve = sourceCurve.DuplicateCurve();
        if (curve == null)
            return null;

        curve.Transform(Transform.Translation(translation));
        return curve;
    }

    private static List<Curve> CreatePocketCutCurves(
        RhinoDoc doc,
        Curve boundaryCurve,
        double toolDiameter,
        double stepoverPercent)
    {
        var result = new List<Curve>();
        var radius = toolDiameter / 2.0;
        var stepover = toolDiameter * (stepoverPercent / 100.0);
        if (radius <= 0.01 || stepover <= 0.01)
            return result;

        var tolerance = doc.ModelAbsoluteTolerance;
        var plane = GetCurvePlane(boundaryCurve);
        var currentOffset = radius;
        const int maxIterations = 200;

        for (var iteration = 0; iteration < maxIterations; iteration++)
        {
            var offsets = CreateLikelyInwardOffsets(boundaryCurve, plane, currentOffset, tolerance);
            if (offsets.Count == 0)
                break;

            var addedAny = false;
            foreach (var offset in offsets)
            {
                if (offset.GetLength() <= toolDiameter * 0.5)
                    continue;

                result.Add(offset);
                addedAny = true;
            }

            if (!addedAny)
                break;

            currentOffset += stepover;
        }

        return result;
    }

    private static List<Curve> CreateLikelyInwardOffsets(Curve boundaryCurve, Plane plane, double offsetDistance, double tolerance)
    {
        var negative = boundaryCurve.Offset(plane, -offsetDistance, tolerance, CurveOffsetCornerStyle.Sharp);
        var positive = boundaryCurve.Offset(plane, offsetDistance, tolerance, CurveOffsetCornerStyle.Sharp);

        var negativeCurves = negative?.Where(curve => curve != null).ToList() ?? new List<Curve>();
        var positiveCurves = positive?.Where(curve => curve != null).ToList() ?? new List<Curve>();

        if (negativeCurves.Count == 0) return positiveCurves;
        if (positiveCurves.Count == 0) return negativeCurves;

        var sourceLength = boundaryCurve.GetLength();
        var negativeLength = negativeCurves.Sum(curve => curve.GetLength());
        var positiveLength = positiveCurves.Sum(curve => curve.GetLength());
        var negativeLooksInward = negativeLength < sourceLength;
        var positiveLooksInward = positiveLength < sourceLength;

        if (negativeLooksInward != positiveLooksInward)
            return negativeLooksInward ? negativeCurves : positiveCurves;

        return negativeLength <= positiveLength ? negativeCurves : positiveCurves;
    }

    private static Vector3d GetToolAxis(Curve curve)
    {
        var plane = GetCurvePlane(curve);
        var axis = plane.ZAxis;
        if (axis.IsTiny())
            axis = Vector3d.ZAxis;

        if (axis.Z < 0)
            axis = -axis;

        axis.Unitize();
        return axis;
    }

    private static Plane GetCurvePlane(Curve curve)
    {
        var tolerance = RhinoDoc.ActiveDoc?.ModelAbsoluteTolerance ?? 0.001;
        if (curve.TryGetPlane(out var curvePlane, tolerance))
            return curvePlane;

        var startPoint = curve.PointAtStart;
        var plane = Plane.WorldXY;
        plane.Origin = new Point3d(0, 0, startPoint.Z);
        return plane;
    }

    private static Vector3d GetSegmentDirection(Curve curve)
    {
        var direction = curve.PointAtEnd - curve.PointAtStart;
        if (direction.IsTiny())
            return Vector3d.XAxis;

        direction.Unitize();
        return direction;
    }

    private static double GetClearance(double toolDiameter, double depth)
    {
        return Math.Max(MinimumClearance, Math.Max(toolDiameter, depth * 0.25));
    }

    private static double GetSegmentSpeed(AnimationSegmentKind kind)
    {
        return kind switch
        {
            AnimationSegmentKind.Rapid => 120.0,
            AnimationSegmentKind.Entry => 18.0,
            AnimationSegmentKind.Plunge => 12.0,
            AnimationSegmentKind.Retract => 24.0,
            AnimationSegmentKind.Drill => 8.0,
            _ => 35.0
        };
    }

    // ---- Helpers ----

    private static double GetToolDiameter(MachiningOperation op)
    {
        if (op.Parameters.TryGetValue(CncOperationSchema.CNC_DIAMETER, out var dStr)
            && double.TryParse(dStr, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var d) && d > 0)
            return d;
        return 10.0;
    }

    private static double GetDepth(MachiningOperation op)
    {
        if (op.Parameters.TryGetValue(CncOperationSchema.CNC_DEPTH, out var dStr)
            && double.TryParse(dStr, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var d) && d > 0)
            return d;
        return 19.0;
    }

    private static double GetStepover(RhinoObject obj)
    {
        var stepoverStr = obj.Attributes.GetUserString(CncOperationSchema.CNC_STEPOVER);
        return double.TryParse(
            stepoverStr,
            System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture,
            out var stepover) && stepover > 0
            ? stepover
            : 50.0;
    }

    private static double GetPeckDepth(MachiningOperation op, double depth)
    {
        if (op.Peck != true)
            return 0.0;

        var peckDepth = op.PeckDepth.GetValueOrDefault();
        if (peckDepth <= MinimumSegmentLength)
            peckDepth = Math.Max(1.0, depth / 3.0);

        return Math.Min(peckDepth, depth);
    }

    private static double GetRampDistance(string rampEntry, double toolDiameter, double clearance, double curveLength)
    {
        var requested = rampEntry.Trim();
        var baseDistance = requested.Equals(CncOperationSchema.RAMP_PROFILE, StringComparison.OrdinalIgnoreCase)
            ? Math.Max(toolDiameter * 3.0, clearance * 1.5)
            : requested.Equals(CncOperationSchema.RAMP_SPIRAL, StringComparison.OrdinalIgnoreCase)
                ? Math.Max(toolDiameter * 4.0, clearance * 2.0)
                : requested.Equals(CncOperationSchema.RAMP_STRAIGHT, StringComparison.OrdinalIgnoreCase)
                    ? Math.Max(toolDiameter * 1.5, clearance)
                    : 0.0;

        return Math.Min(baseDistance, Math.Max(MinimumSegmentLength, curveLength * 0.45));
    }

    public void Dispose()
    {
        if (_disposed) return;
        Stop();
        _conduit.Enabled = false;
        _disposed = true;
    }

    // ---- Data ----

    private enum AnimationSegmentKind
    {
        Rapid,
        Entry,
        Plunge,
        Feed,
        Retract,
        Drill
    }

    private sealed record AnimationSegment(
        Curve Curve,
        double ToolDiameter,
        string OperationType,
        AnimationSegmentKind Kind,
        Vector3d ToolAxis);

    // ============================================================
    // DisplayConduit — draws the tool marker in the viewport
    // ============================================================

    private sealed class ToolConduit : DisplayConduit
    {
        public IReadOnlyList<AnimationSegment> Segments { get; set; } = Array.Empty<AnimationSegment>();
        public int CurrentSegmentIndex { get; set; } = -1;
        public double CurrentSegmentProgress { get; set; }
        public Point3d ToolPosition { get; set; }
        public Point3d SegmentStart { get; set; }
        public double ToolDiameter { get; set; } = 10;
        public Vector3d ToolTangent { get; set; }
        public Vector3d ToolAxis { get; set; } = Vector3d.ZAxis;
        public AnimationSegmentKind SegmentKind { get; set; }
        public string OperationType { get; set; } = string.Empty;

        private static readonly Color ColorContour = Color.FromArgb(200, 255, 60, 60);
        private static readonly Color ColorPocket = Color.FromArgb(200, 60, 120, 255);
        private static readonly Color ColorDrill = Color.FromArgb(200, 255, 200, 40);
        private static readonly Color ColorGroove = Color.FromArgb(200, 60, 200, 80);
        private static readonly Color ColorRapid = Color.FromArgb(180, 220, 220, 220);
        private static readonly Color OverlayTextColor = Color.Black;

        protected override void PostDrawObjects(DrawEventArgs e)
        {
            base.PostDrawObjects(e);

            if (!ToolPosition.IsValid)
                return;

            var color = GetSegmentColor();
            var radius = GetToolRadius();

            if (SegmentKind == AnimationSegmentKind.Drill)
                DrawDrillTool(e, color, radius);
            else
                DrawRoutingTool(e, color, radius);
        }

        protected override void DrawForeground(DrawEventArgs e)
        {
            base.DrawForeground(e);

            if (!ToolPosition.IsValid)
                return;

            var color = GetSegmentColor();
            var radius = GetToolRadius();

            DrawCompletedTrail(e);
            DrawActiveSegmentOverlay(e, color);

            if (SegmentKind == AnimationSegmentKind.Drill)
                DrawDrillTool(e, WithAlpha(color, 255), radius);
            else
                DrawRoutingTool(e, WithAlpha(color, 255), radius);

            e.Display.DrawDot(ToolPosition, GetStatusLabel(), WithAlpha(color, 245), OverlayTextColor);
        }

        private double GetToolRadius()
        {
            var radius = ToolDiameter / 2.0;
            return radius < 0.5 ? 5.0 : radius;
        }

        private Color GetSegmentColor()
        {
            return SegmentKind == AnimationSegmentKind.Rapid
                ? ColorRapid
                : OperationType switch
                {
                    CncOperationSchema.TYPE_CONTOUR => ColorContour,
                    CncOperationSchema.TYPE_POCKET => ColorPocket,
                    CncOperationSchema.TYPE_DRILL => ColorDrill,
                    CncOperationSchema.TYPE_GROOVE => ColorGroove,
                    _ => Color.White
                };
        }

        private void DrawCompletedTrail(DrawEventArgs e)
        {
            if (Segments.Count == 0 || CurrentSegmentIndex < 0)
                return;

            var lastCompletedIndex = Math.Min(CurrentSegmentIndex - 1, Segments.Count - 1);
            for (var index = 0; index <= lastCompletedIndex; index++)
            {
                var segment = Segments[index];
                if (!ShouldDrawTrail(segment.Kind))
                    continue;

                e.Display.DrawCurve(segment.Curve, WithAlpha(GetColorForSegment(segment), 150), 4);
            }
        }

        private void DrawActiveSegmentOverlay(DrawEventArgs e, Color color)
        {
            if (Segments.Count == 0 || CurrentSegmentIndex < 0 || CurrentSegmentIndex >= Segments.Count)
                return;

            var currentSegment = Segments[CurrentSegmentIndex];
            e.Display.DrawCurve(currentSegment.Curve, WithAlpha(color, 80), 3);

            if (!ShouldDrawTrail(currentSegment.Kind))
                return;

            var visitedCurve = CreateVisitedCurve(currentSegment.Curve, CurrentSegmentProgress);
            if (visitedCurve == null)
                return;

            e.Display.DrawCurve(visitedCurve, WithAlpha(color, 255), 6);
        }

        private static bool ShouldDrawTrail(AnimationSegmentKind kind)
        {
            return kind is AnimationSegmentKind.Entry
                or AnimationSegmentKind.Plunge
                or AnimationSegmentKind.Feed
                or AnimationSegmentKind.Drill;
        }

        private static Curve? CreateVisitedCurve(Curve sourceCurve, double normalizedProgress)
        {
            var clampedProgress = Math.Max(0, Math.Min(1, normalizedProgress));
            if (clampedProgress <= 0.001)
                return null;

            if (clampedProgress >= 0.999)
                return sourceCurve.DuplicateCurve();

            if (!sourceCurve.NormalizedLengthParameter(clampedProgress, out var endParameter))
                return null;

            return sourceCurve.Trim(sourceCurve.Domain.T0, endParameter);
        }

        private string GetStatusLabel()
        {
            var motion = SegmentKind switch
            {
                AnimationSegmentKind.Rapid => "Rapid",
                AnimationSegmentKind.Entry => "Entry",
                AnimationSegmentKind.Plunge => "Plunge",
                AnimationSegmentKind.Feed => "Cut",
                AnimationSegmentKind.Retract => "Retract",
                AnimationSegmentKind.Drill => "Drill",
                _ => "Tool"
            };

            var operation = OperationType switch
            {
                CncOperationSchema.TYPE_CONTOUR => "Contour",
                CncOperationSchema.TYPE_POCKET => "Pocket",
                CncOperationSchema.TYPE_DRILL => "Drill",
                CncOperationSchema.TYPE_GROOVE => "Groove",
                _ => "CAM"
            };

            return $"{operation} {motion}";
        }

        private static Color GetColorForSegment(AnimationSegment segment)
        {
            return segment.Kind == AnimationSegmentKind.Rapid
                ? ColorRapid
                : segment.OperationType switch
                {
                    CncOperationSchema.TYPE_CONTOUR => ColorContour,
                    CncOperationSchema.TYPE_POCKET => ColorPocket,
                    CncOperationSchema.TYPE_DRILL => ColorDrill,
                    CncOperationSchema.TYPE_GROOVE => ColorGroove,
                    _ => Color.White
                };
        }

        private static Color WithAlpha(Color color, int alpha)
        {
            var clamped = Math.Max(0, Math.Min(255, alpha));
            return Color.FromArgb(clamped, color.R, color.G, color.B);
        }

        private void DrawRoutingTool(DrawEventArgs e, Color color, double radius)
        {
            var circle = new Circle(ToolPosition, radius);
            e.Display.DrawCircle(circle, color, 2);

            var axis = ToolAxis;
            if (axis.IsTiny())
                axis = Vector3d.ZAxis;
            axis.Unitize();

            var shankHeight = Math.Max(radius * 3.0, 15.0);
            var shankTop = ToolPosition + axis * shankHeight;
            e.Display.DrawLine(ToolPosition, shankTop, color, 2);

            var tangent = ToolTangent;
            if (!tangent.IsTiny())
            {
                tangent.Unitize();
                var arrowTip = ToolPosition + tangent * (radius * 2.5);
                e.Display.DrawArrow(new Line(ToolPosition, arrowTip), color, 0, 0);
            }

            var crossSize = radius * 0.4;
            e.Display.DrawLine(
                new Point3d(ToolPosition.X - crossSize, ToolPosition.Y, ToolPosition.Z),
                new Point3d(ToolPosition.X + crossSize, ToolPosition.Y, ToolPosition.Z),
                color, 1);
            e.Display.DrawLine(
                new Point3d(ToolPosition.X, ToolPosition.Y - crossSize, ToolPosition.Z),
                new Point3d(ToolPosition.X, ToolPosition.Y + crossSize, ToolPosition.Z),
                color, 1);
        }

        private void DrawDrillTool(DrawEventArgs e, Color color, double radius)
        {
            var circle = new Circle(ToolPosition, radius);
            e.Display.DrawCircle(circle, color, 2);
            e.Display.DrawLine(SegmentStart, ToolPosition, color, 2);

            var crossSize = radius * 0.5;
            e.Display.DrawLine(
                new Point3d(ToolPosition.X - crossSize, ToolPosition.Y, ToolPosition.Z),
                new Point3d(ToolPosition.X + crossSize, ToolPosition.Y, ToolPosition.Z),
                color, 1);
            e.Display.DrawLine(
                new Point3d(ToolPosition.X, ToolPosition.Y - crossSize, ToolPosition.Z),
                new Point3d(ToolPosition.X, ToolPosition.Y + crossSize, ToolPosition.Z),
                color, 1);

            var axis = SegmentStart - ToolPosition;
            if (!axis.IsTiny())
            {
                axis.Unitize();
                var tipSize = radius * 0.6;
                e.Display.DrawLine(
                    ToolPosition - Vector3d.XAxis * tipSize,
                    ToolPosition - axis * tipSize,
                    color,
                    1);
                e.Display.DrawLine(
                    ToolPosition + Vector3d.XAxis * tipSize,
                    ToolPosition - axis * tipSize,
                    color,
                    1);
            }
        }
    }
}

public sealed record ToolpathAnimationLoadResult(
    int RequestedOperationCount,
    int LoadedSegmentCount,
    IReadOnlyDictionary<string, int> SkippedReasons)
{
    public int SkippedOperationCount => SkippedReasons.Values.Sum();

    public string FormatSummary()
    {
        if (LoadedSegmentCount > 0 && SkippedOperationCount == 0)
            return $"{LoadedSegmentCount} Segment(e) aus {RequestedOperationCount} Operation(en) geladen.";

        var reasons = SkippedReasons.Count == 0
            ? "keine verwertbaren Operationen gefunden"
            : string.Join(", ",
                SkippedReasons
                    .OrderByDescending(pair => pair.Value)
                    .ThenBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(pair => $"{pair.Value}× {pair.Key}"));

        return LoadedSegmentCount > 0
            ? $"{LoadedSegmentCount} Segment(e) geladen, übersprungen: {reasons}."
            : $"0 Segmente geladen, Gründe: {reasons}.";
    }
}
