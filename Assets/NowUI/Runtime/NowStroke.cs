using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace NowUI
{
    /// <summary>
    /// A styled stroke through caller-owned sample points. It shares
    /// <see cref="NowLine"/>'s width, cap, color/gradient, dash, arrow, and mask
    /// styling, and can close into a loop.
    /// </summary>
    /// <remarks>
    /// The builder stores a reference to the caller's array or list; the points
    /// are read when <see cref="Draw"/> runs, not when the builder is created.
    /// Nothing is retained after <see cref="Draw"/> returns.
    /// </remarks>
    [NowBuilder]
    public struct NowPolyline
    {
        public Vector2[] points;

        public List<Vector2> pointList;

        public int start;

        public int count;

        public bool closed;

        internal NowStrokeStyle style;

        public NowPolyline(Vector2[] points, int start, int count)
        {
            this.points = points;
            pointList = null;
            this.start = start;
            this.count = count;
            closed = false;
            style = NowStrokeStyle.solid;
        }

        public NowPolyline(List<Vector2> points, int start, int count)
        {
            this.points = null;
            pointList = points;
            this.start = start;
            this.count = count;
            closed = false;
            style = NowStrokeStyle.solid;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowPolyline SetPoints(Vector2[] points)
        {
            this.points = points;
            pointList = null;
            start = 0;
            count = points != null ? points.Length : 0;
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowPolyline SetPoints(Vector2[] points, int start, int count)
        {
            this.points = points;
            pointList = null;
            this.start = start;
            this.count = count;
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowPolyline SetPoints(List<Vector2> points)
        {
            this.points = null;
            pointList = points;
            start = 0;
            count = points != null ? points.Count : 0;
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowPolyline SetPoints(List<Vector2> points, int start, int count)
        {
            this.points = null;
            pointList = points;
            this.start = start;
            this.count = count;
            return this;
        }

        /// <summary>
        /// Joins the last point back to the first with a mitered seam. Closed
        /// paths ignore caps and arrow heads, and their dash pattern continues
        /// across the seam. A closing point equal to the first is optional.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowPolyline SetClosed(bool closed = true)
        {
            this.closed = closed;
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowPolyline SetMask(NowRect mask)
        {
            style.mask = mask;
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowPolyline SetColor(Color color)
        {
            style.SetColor(color);
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowPolyline SetColor(Vector4 color)
        {
            style.SetColor(color);
            return this;
        }

        /// <summary>
        /// Blends from <paramref name="from"/> at the first point to
        /// <paramref name="to"/> at the end of the path, by distance along the
        /// whole path. A closed path ends back at its first point.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowPolyline SetGradient(Color from, Color to)
        {
            style.SetGradient(from, to);
            return this;
        }

        /// <inheritdoc cref="SetGradient(Color, Color)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowPolyline SetGradient(Vector4 from, Vector4 to)
        {
            style.SetGradient(from, to);
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowPolyline SetWidth(float width)
        {
            style.width = width;
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowPolyline SetCap(NowLineCap cap)
        {
            style.cap = cap;
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowPolyline SetDash(float dashLength, float gapLength, float offset = 0f)
        {
            style.SetDash(dashLength, gapLength, offset);
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowPolyline SetSolid()
        {
            style.SetDash(0f, 0f, 0f);
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowPolyline SetArrow(NowLineArrow arrows = NowLineArrow.End, float length = 0f, float width = 0f)
        {
            style.SetArrow(arrows, length, width);
            return this;
        }

        [NowConsumer]
        public NowPolyline Draw()
        {
            Now.DrawStrokePolyline(this);
            return this;
        }
    }

    /// <summary>
    /// A stroked circular arc with <see cref="NowLine"/> styling. Angles are
    /// radians: <c>0</c> points right and a positive sweep turns clockwise on
    /// screen (UI space is y-down), matching the SDF <c>Arc</c> primitive.
    /// A sweep of a full turn or more draws one seamless closed ring.
    /// </summary>
    [NowBuilder]
    public struct NowArc
    {
        public Vector2 center;

        public float radius;

        public float startAngle;

        public float sweep;

        public int segments;

        internal NowStrokeStyle style;

        public NowArc(Vector2 center, float radius, float startAngle, float sweep)
        {
            this.center = center;
            this.radius = radius;
            this.startAngle = startAngle;
            this.sweep = sweep;
            segments = 0;
            style = NowStrokeStyle.solid;
        }

        /// <summary>
        /// Overrides the number of straight segments across the sweep (the
        /// whole circle for a ring). Zero or less restores the default, which
        /// keeps the chord error under 0.2 screen pixels after the active
        /// transform and UI scale.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowArc SetSegments(int segments)
        {
            this.segments = segments;
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowArc SetMask(NowRect mask)
        {
            style.mask = mask;
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowArc SetColor(Color color)
        {
            style.SetColor(color);
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowArc SetColor(Vector4 color)
        {
            style.SetColor(color);
            return this;
        }

        /// <summary>
        /// Blends from <paramref name="from"/> at the start angle to
        /// <paramref name="to"/> at the end of the sweep. A ring meets its start
        /// color again at the seam.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowArc SetGradient(Color from, Color to)
        {
            style.SetGradient(from, to);
            return this;
        }

        /// <inheritdoc cref="SetGradient(Color, Color)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowArc SetGradient(Vector4 from, Vector4 to)
        {
            style.SetGradient(from, to);
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowArc SetWidth(float width)
        {
            style.width = width;
            return this;
        }

        /// <summary>Caps both ends of a partial arc. Rings have no ends.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowArc SetCap(NowLineCap cap)
        {
            style.cap = cap;
            return this;
        }

        /// <summary>
        /// Dashes by distance along the true circle, starting at the start
        /// angle. On a ring the pattern continues across the seam, so a
        /// circumference that is a multiple of <c>dash + gap</c> spaces every
        /// dash evenly.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowArc SetDash(float dashLength, float gapLength, float offset = 0f)
        {
            style.SetDash(dashLength, gapLength, offset);
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowArc SetSolid()
        {
            style.SetDash(0f, 0f, 0f);
            return this;
        }

        /// <summary>Adds arrow heads to a partial arc. Rings ignore arrows.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowArc SetArrow(NowLineArrow arrows = NowLineArrow.End, float length = 0f, float width = 0f)
        {
            style.SetArrow(arrows, length, width);
            return this;
        }

        [NowConsumer]
        public NowArc Draw()
        {
            Now.DrawArc(this);
            return this;
        }
    }

    public static partial class Now
    {
        const int MaxArcSegments = 4096;

        const float FullTurn = Mathf.PI * 2f;

        /// <summary>
        /// Starts a styled stroke through every point of <paramref name="points"/>.
        /// The array is read when the stroke is drawn.
        /// </summary>
        public static NowPolyline Polyline(Vector2[] points)
        {
            return new NowPolyline(points, 0, points != null ? points.Length : 0);
        }

        public static NowPolyline Polyline(Vector2[] points, int count)
        {
            return new NowPolyline(points, 0, count);
        }

        public static NowPolyline Polyline(Vector2[] points, int start, int count)
        {
            return new NowPolyline(points, start, count);
        }

        /// <summary>
        /// Starts a styled stroke through every point of <paramref name="points"/>.
        /// The list is read when the stroke is drawn.
        /// </summary>
        public static NowPolyline Polyline(List<Vector2> points)
        {
            return new NowPolyline(points, 0, points != null ? points.Count : 0);
        }

        public static NowPolyline Polyline(List<Vector2> points, int count)
        {
            return new NowPolyline(points, 0, count);
        }

        public static NowPolyline Polyline(List<Vector2> points, int start, int count)
        {
            return new NowPolyline(points, start, count);
        }

        /// <summary>
        /// Starts a stroked circular arc. Angles are radians; <c>0</c> points
        /// right and a positive <paramref name="sweep"/> turns clockwise on
        /// screen. A sweep of a full turn (<c>2 * PI</c>) or more draws a closed
        /// ring; a zero sweep or radius draws nothing.
        /// </summary>
        public static NowArc Arc(Vector2 center, float radius, float startAngle, float sweep)
        {
            return new NowArc(center, radius, startAngle, sweep);
        }

        /// <summary>
        /// Starts a stroked circular arc over <paramref name="sweep"/>, for example
        /// <c>Now.Arc(c, r, NowSweep.Clock(0f, 270f))</c> from 12 o'clock.
        /// </summary>
        public static NowArc Arc(Vector2 center, float radius, NowSweep sweep)
        {
            return new NowArc(center, radius, sweep.from, sweep.sweep);
        }

        internal static void DrawStrokePolyline(in NowPolyline polyline)
        {
            var style = polyline.style;

            if (_suppressDrawDepth > 0 || _defaultMaterial == null || !(style.width > 0f))
                return;

            int available = polyline.pointList != null
                ? polyline.pointList.Count
                : polyline.points != null
                    ? polyline.points.Length
                    : 0;

            int start = Mathf.Clamp(polyline.start, 0, available);
            int count = Mathf.Clamp(polyline.count, 0, available - start);

            if (count < 2)
                return;

            bool hasTransform = _transformStack.Count > 0;

            var color = ApplyColorMultiplier(style.color);
            var colorEnd = style.gradient ? ApplyColorMultiplier(style.colorEnd) : color;

            if (color.w <= 0.0005f && colorEnd.w <= 0.0005f)
                return;

            _linePoints.Clear();
            _linePoints.EnsureCapacity(count);

            float minX = float.MaxValue;
            float minY = float.MaxValue;
            float maxX = float.MinValue;
            float maxY = float.MinValue;

            for (int i = 0; i < count; ++i)
            {
                Vector2 point = polyline.pointList != null
                    ? polyline.pointList[start + i]
                    : polyline.points[start + i];

                if (hasTransform)
                    point = ApplyTransform(point);

                AddLinePointIfDistinct(ref _linePoints, point);
                minX = Mathf.Min(minX, point.x);
                minY = Mathf.Min(minY, point.y);
                maxX = Mathf.Max(maxX, point.x);
                maxY = Mathf.Max(maxY, point.y);
            }

            bool closed = polyline.closed;

            // An explicit closing point duplicates the seam the loop adds itself.
            if (closed && _linePoints.count > 2 &&
                (_linePoints.array[_linePoints.count - 1] - _linePoints.array[0]).sqrMagnitude <= LineEpsilon * LineEpsilon)
            {
                --_linePoints.count;
            }

            if (_linePoints.count < 2)
                return;

            // Two distinct points cannot enclose anything; they draw open.
            closed &= _linePoints.count >= 3;

            float scaledWidth = hasTransform ? ApplyTransformScalar(style.width) : style.width;
            var mask = ResolveStrokeMask(style.mask, hasTransform);

            if (mask.isEmpty || !StrokeBoundsOverlapMask(minX, minY, maxX, maxY, style, scaledWidth, hasTransform, mask))
                return;

            EmitStrokePath(
                style,
                closed,
                color,
                colorEnd,
                scaledWidth,
                hasTransform ? ApplyTransformScalar(1f) : 1f,
                mask);
        }

        internal static void DrawArc(in NowArc arc)
        {
            var style = arc.style;

            if (_suppressDrawDepth > 0 || _defaultMaterial == null || !(style.width > 0f))
                return;

            float radius = arc.radius;
            float sweep = arc.sweep;

            // Comparisons are written so NaN inputs draw nothing.
            if (!(radius > LineEpsilon) || float.IsInfinity(radius) ||
                !(Mathf.Abs(sweep) > LineEpsilon) || !(Mathf.Abs(arc.startAngle) < float.PositiveInfinity))
            {
                return;
            }

            bool hasTransform = _transformStack.Count > 0;

            var color = ApplyColorMultiplier(style.color);
            var colorEnd = style.gradient ? ApplyColorMultiplier(style.colorEnd) : color;

            if (color.w <= 0.0005f && colorEnd.w <= 0.0005f)
                return;

            Vector2 scale = hasTransform ? currentTransform.scale : Vector2.one;
            Vector2 center = hasTransform ? ApplyTransform(arc.center) : arc.center;
            float radiusX = radius * Mathf.Abs(scale.x);
            float radiusY = radius * Mathf.Abs(scale.y);
            float scaledWidth = hasTransform ? ApplyTransformScalar(style.width) : style.width;
            var mask = ResolveStrokeMask(style.mask, hasTransform);

            // The whole circle bounds every sweep; this rejects before sampling.
            if (mask.isEmpty || !StrokeBoundsOverlapMask(
                    center.x - radiusX,
                    center.y - radiusY,
                    center.x + radiusX,
                    center.y + radiusY,
                    style,
                    scaledWidth,
                    hasTransform,
                    mask))
            {
                return;
            }

            // Tolerate float error in callers' full turns so a full ring never opens.
            bool closed = Mathf.Abs(sweep) >= FullTurn - 0.0001f;

            if (closed)
                sweep = sweep > 0f ? FullTurn : -FullTurn;

            int segments = arc.segments > 0
                ? arc.segments
                : DefaultArcSegments(Mathf.Max(radiusX, radiusY), Mathf.Abs(sweep));
            segments = Mathf.Clamp(segments, closed ? 3 : 1, MaxArcSegments);

            // Sample in local space and transform each point, so a non-uniform
            // transform turns the circle into an ellipse.
            int pointCount = closed ? segments : segments + 1;
            float step = sweep / segments;

            _linePoints.Clear();
            _linePoints.EnsureCapacity(pointCount);

            for (int i = 0; i < pointCount; ++i)
            {
                float angle = arc.startAngle + step * i;
                Vector2 point = new Vector2(
                    arc.center.x + Mathf.Cos(angle) * radius,
                    arc.center.y + Mathf.Sin(angle) * radius);
                AddLinePointIfDistinct(ref _linePoints, hasTransform ? ApplyTransform(point) : point);
            }

            if (_linePoints.count < 2)
                return;

            // Chords are shorter than the arc they replace. Shrink the dash
            // pattern by the same ratio so dash lengths measure the true circle
            // and a ring's circumference divides into whole patterns exactly.
            float halfStep = Mathf.Abs(step) * 0.5f;
            float chordRatio = halfStep > 0.00001f ? Mathf.Sin(halfStep) / halfStep : 1f;

            EmitStrokePath(
                style,
                closed && _linePoints.count >= 3,
                color,
                colorEnd,
                scaledWidth,
                (hasTransform ? ApplyTransformScalar(1f) : 1f) * chordRatio,
                mask);
        }

        /// <summary>
        /// Segment count that keeps an arc's chord error (sagitta) within the
        /// line flattening tolerance, with at most 45 degrees per segment.
        /// <paramref name="radius"/> is in transformed UI units.
        /// </summary>
        static int DefaultArcSegments(float radius, float sweep)
        {
            double tolerance = ScreenPixelsToUiUnits(LineFlattenTolerance);
            double step = radius > tolerance
                ? 2.0 * Math.Acos(1.0 - tolerance / radius)
                : Math.PI * 0.25;
            step = Math.Min(step, Math.PI * 0.25);

            double segments = Math.Ceiling(sweep / Math.Max(step, 1e-9));
            return (int)Math.Min(Math.Max(segments, 1.0), MaxArcSegments);
        }
    }
}
