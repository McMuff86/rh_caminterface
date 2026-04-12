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
/// Simple toolpath animation/simulation: draws a tool marker (circle + direction arrow)
/// moving along toolpath curves using a DisplayConduit.
/// Uses RhinoApp.Idle for frame updates to stay on the main thread.
/// </summary>
public sealed class ToolpathAnimator : IDisposable
{
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
    private bool _disposed;

    /// <summary>
    /// Loads operations for animation. Collects all toolpath curves from the given
    /// operation objects (or all CNC operations in the document if none specified).
    /// </summary>
    public void Load(RhinoDoc doc, IEnumerable<RhinoObject>? operationObjects = null)
    {
        Stop();
        _segments.Clear();

        var objects = operationObjects?.ToList()
                      ?? CncOperationService.GetAllOperationsInDocument(doc).ToList();

        foreach (var obj in objects)
        {
            var op = CncOperationService.GetOperation(obj);
            if (op == null) continue;

            var type = op.Type;
            var toolDiameter = GetToolDiameter(op);
            var depth = GetDepth(op);

            if (type == CncOperationSchema.TYPE_DRILL)
            {
                // For drills: create a vertical plunge segment
                var geom = obj.Geometry;
                Point3d center;
                if (geom is Rhino.Geometry.Point pt)
                    center = pt.Location;
                else if (geom is Curve c)
                    center = c.PointAtStart;
                else
                    continue;

                if (!center.IsValid) continue;

                // Create a vertical line from surface to depth
                var topPt = center;
                var bottomPt = new Point3d(center.X, center.Y, center.Z - depth);
                var plungeCurve = new LineCurve(topPt, bottomPt);
                _segments.Add(new AnimationSegment(plungeCurve, toolDiameter, type, IsDrill: true));
            }
            else
            {
                // For contour/pocket/groove: use the source geometry curve
                Curve? curve = null;
                if (obj.Geometry is Curve crv)
                    curve = crv;
                else
                    continue;

                if (curve == null || curve.GetLength() < 0.01) continue;

                _segments.Add(new AnimationSegment(curve, toolDiameter, type, IsDrill: false));
            }
        }
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
        RhinoDoc.ActiveDoc?.Views.Redraw();

        RunningChanged?.Invoke(true);
    }

    /// <summary>Stops the animation and hides the conduit.</summary>
    public void Stop()
    {
        if (!_running) return;

        RhinoApp.Idle -= OnIdle;
        _conduit.Enabled = false;
        _running = false;

        RhinoDoc.ActiveDoc?.Views.Redraw();
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
        if (curveLength < 0.01) curveLength = 1.0;

        // Feed speed: ~3000 mm/min base (50 mm/s) × multiplier
        var feedSpeed = 50.0 * SpeedMultiplier;
        // Drill plunges are slower
        if (seg.IsDrill) feedSpeed *= 0.3;

        var tIncrement = (feedSpeed * dt) / curveLength;
        _t += tIncrement;

        if (_t >= 1.0)
        {
            // Move to next segment
            _currentSegmentIndex++;
            _t = 0;

            if (_currentSegmentIndex >= _segments.Count)
            {
                // Done — loop back or stop
                Stop();
                return;
            }
        }

        // Update conduit position
        var currentSeg = _segments[_currentSegmentIndex];
        var clampedT = Math.Max(0, Math.Min(1, _t));

        if (currentSeg.Curve.NormalizedLengthParameter(clampedT, out var param))
        {
            var position = currentSeg.Curve.PointAt(param);
            var tangent = currentSeg.Curve.TangentAt(param);

            _conduit.ToolPosition = position;
            _conduit.ToolDiameter = currentSeg.ToolDiameter;
            _conduit.ToolTangent = tangent;
            _conduit.IsDrill = currentSeg.IsDrill;
            _conduit.DrillDepth = currentSeg.IsDrill ? currentSeg.Curve.GetLength() * clampedT : 0;
            _conduit.OperationType = currentSeg.OperationType;
        }

        // Request viewport redraw
        RhinoDoc.ActiveDoc?.Views.Redraw();
    }

    // ---- Helpers ----

    private static double GetToolDiameter(MachiningOperation op)
    {
        if (op.Parameters.TryGetValue(CncOperationSchema.CNC_DIAMETER, out var dStr)
            && double.TryParse(dStr, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var d) && d > 0)
            return d;
        // Fallback: try to get from tool name (e.g. "Ø10 HM Router")
        return 10.0; // reasonable default
    }

    private static double GetDepth(MachiningOperation op)
    {
        if (op.Parameters.TryGetValue(CncOperationSchema.CNC_DEPTH, out var dStr)
            && double.TryParse(dStr, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var d) && d > 0)
            return d;
        return 19.0;
    }

    public void Dispose()
    {
        if (_disposed) return;
        Stop();
        _conduit.Enabled = false;
        _disposed = true;
    }

    // ---- Data ----

    private sealed record AnimationSegment(Curve Curve, double ToolDiameter, string OperationType, bool IsDrill);

    // ============================================================
    // DisplayConduit — draws the tool marker in the viewport
    // ============================================================

    private sealed class ToolConduit : DisplayConduit
    {
        public Point3d ToolPosition { get; set; }
        public double ToolDiameter { get; set; } = 10;
        public Vector3d ToolTangent { get; set; }
        public bool IsDrill { get; set; }
        public double DrillDepth { get; set; }
        public string OperationType { get; set; } = "";

        private static readonly Color ColorContour = Color.FromArgb(200, 255, 60, 60);
        private static readonly Color ColorPocket = Color.FromArgb(200, 60, 120, 255);
        private static readonly Color ColorDrill = Color.FromArgb(200, 255, 200, 40);
        private static readonly Color ColorGroove = Color.FromArgb(200, 60, 200, 80);

        protected override void PostDrawObjects(DrawEventArgs e)
        {
            base.PostDrawObjects(e);

            if (!ToolPosition.IsValid) return;

            var color = OperationType switch
            {
                CncOperationSchema.TYPE_CONTOUR => ColorContour,
                CncOperationSchema.TYPE_POCKET => ColorPocket,
                CncOperationSchema.TYPE_DRILL => ColorDrill,
                CncOperationSchema.TYPE_GROOVE => ColorGroove,
                _ => Color.White
            };

            var radius = ToolDiameter / 2.0;
            if (radius < 0.5) radius = 5;

            if (IsDrill)
            {
                DrawDrillTool(e, color, radius);
            }
            else
            {
                DrawRoutingTool(e, color, radius);
            }
        }

        private void DrawRoutingTool(DrawEventArgs e, Color color, double radius)
        {
            // Filled circle at tool position (in the curve's plane, typically XY)
            var circle = new Circle(ToolPosition, radius);
            e.Display.DrawCircle(circle, color, 2);

            // Direction arrow
            if (!ToolTangent.IsZero)
            {
                var tangent = ToolTangent;
                tangent.Unitize();
                var arrowTip = ToolPosition + tangent * (radius * 2.5);
                e.Display.DrawArrow(new Line(ToolPosition, arrowTip), color, 0, 0);
            }

            // Cross at center
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
            // Circle at current position
            var circle = new Circle(ToolPosition, radius);
            e.Display.DrawCircle(circle, color, 2);

            // Vertical line from surface descending to current depth
            var topPt = new Point3d(ToolPosition.X, ToolPosition.Y, ToolPosition.Z + DrillDepth);
            e.Display.DrawLine(topPt, ToolPosition, color, 2);

            // Crosshair at tool bottom
            var crossSize = radius * 0.5;
            e.Display.DrawLine(
                new Point3d(ToolPosition.X - crossSize, ToolPosition.Y, ToolPosition.Z),
                new Point3d(ToolPosition.X + crossSize, ToolPosition.Y, ToolPosition.Z),
                color, 1);
            e.Display.DrawLine(
                new Point3d(ToolPosition.X, ToolPosition.Y - crossSize, ToolPosition.Z),
                new Point3d(ToolPosition.X, ToolPosition.Y + crossSize, ToolPosition.Z),
                color, 1);

            // Drill point indicator (V shape) below the circle
            var vSize = radius * 0.6;
            e.Display.DrawLine(
                new Point3d(ToolPosition.X - vSize, ToolPosition.Y, ToolPosition.Z),
                new Point3d(ToolPosition.X, ToolPosition.Y, ToolPosition.Z - vSize),
                color, 1);
            e.Display.DrawLine(
                new Point3d(ToolPosition.X + vSize, ToolPosition.Y, ToolPosition.Z),
                new Point3d(ToolPosition.X, ToolPosition.Y, ToolPosition.Z - vSize),
                color, 1);
        }
    }
}
