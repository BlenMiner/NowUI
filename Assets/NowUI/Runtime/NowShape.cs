using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NowUI.Internal;
using UnityEngine;

namespace NowUI
{
    [NowBuilder]
    public struct NowCircle
    {
        public Vector2 center;
        public Vector2 radius;
        public NowRect mask;
        public Vector4 color;
        public Vector4 outlineColor;
        public float outline;
        public int segments;
        public bool fill;

        public NowCircle(Vector2 center, Vector2 radius)
        {
            this.center = center;
            this.radius = radius;
            mask = default;
            color = Vector4.one;
            outlineColor = default;
            outline = 0f;
            segments = 0;
            fill = true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowCircle SetPosition(Vector2 center, float radius)
        {
            this.center = center;
            this.radius = new Vector2(radius, radius);
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowCircle SetPosition(Vector2 center, Vector2 radius)
        {
            this.center = center;
            this.radius = radius;
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowCircle SetRect(NowRect rect)
        {
            center = rect.center;
            radius = new Vector2(rect.width * 0.5f, rect.height * 0.5f);
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowCircle SetMask(NowRect mask) { this.mask = mask; return this; }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowCircle SetColor(Color color) { this.color = color; fill = true; return this; }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowCircle SetColor(Vector4 color) { this.color = color; fill = true; return this; }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowCircle SetFill(bool fill = true) { this.fill = fill; return this; }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowCircle SetOutline(float outline) { this.outline = outline; return this; }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowCircle SetOutlineColor(Color color) { outlineColor = color; return this; }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowCircle SetOutlineColor(Vector4 color) { outlineColor = color; return this; }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowCircle SetSegments(int segments) { this.segments = segments; return this; }

        [NowConsumer]
        public NowCircle Draw()
        {
            Now.DrawCircle(this);
            return this;
        }
    }

    [NowBuilder]
    public struct NowTriangle
    {
        public Vector2 a;
        public Vector2 b;
        public Vector2 c;
        public NowRect mask;
        public Vector4 color;
        public Vector4 outlineColor;
        public float outline;
        public bool fill;

        public NowTriangle(Vector2 a, Vector2 b, Vector2 c)
        {
            this.a = a;
            this.b = b;
            this.c = c;
            mask = default;
            color = Vector4.one;
            outlineColor = default;
            outline = 0f;
            fill = true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowTriangle SetPosition(Vector2 a, Vector2 b, Vector2 c)
        {
            this.a = a;
            this.b = b;
            this.c = c;
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowTriangle SetMask(NowRect mask) { this.mask = mask; return this; }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowTriangle SetColor(Color color) { this.color = color; fill = true; return this; }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowTriangle SetColor(Vector4 color) { this.color = color; fill = true; return this; }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowTriangle SetFill(bool fill = true) { this.fill = fill; return this; }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowTriangle SetOutline(float outline) { this.outline = outline; return this; }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowTriangle SetOutlineColor(Color color) { outlineColor = color; return this; }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowTriangle SetOutlineColor(Vector4 color) { outlineColor = color; return this; }

        [NowConsumer]
        public NowTriangle Draw()
        {
            Now.DrawTriangle(this);
            return this;
        }
    }

    [NowBuilder]
    public struct NowPolygon
    {
        public Vector2[] points;
        public List<Vector2> pointList;
        public int start;
        public int count;
        public NowRect mask;
        public Vector4 color;
        public Vector4 outlineColor;
        public float outline;
        public bool fill;

        public NowPolygon(Vector2[] points, int start, int count)
        {
            this.points = points;
            pointList = null;
            this.start = start;
            this.count = count;
            mask = default;
            color = Vector4.one;
            outlineColor = default;
            outline = 0f;
            fill = true;
        }

        public NowPolygon(List<Vector2> points, int start, int count)
        {
            this.points = null;
            pointList = points;
            this.start = start;
            this.count = count;
            mask = default;
            color = Vector4.one;
            outlineColor = default;
            outline = 0f;
            fill = true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowPolygon SetPoints(Vector2[] points)
        {
            this.points = points;
            pointList = null;
            start = 0;
            count = points != null ? points.Length : 0;
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowPolygon SetPoints(Vector2[] points, int start, int count)
        {
            this.points = points;
            pointList = null;
            this.start = start;
            this.count = count;
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowPolygon SetPoints(List<Vector2> points)
        {
            this.points = null;
            pointList = points;
            start = 0;
            count = points != null ? points.Count : 0;
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowPolygon SetPoints(List<Vector2> points, int start, int count)
        {
            this.points = null;
            pointList = points;
            this.start = start;
            this.count = count;
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowPolygon SetMask(NowRect mask) { this.mask = mask; return this; }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowPolygon SetColor(Color color) { this.color = color; fill = true; return this; }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowPolygon SetColor(Vector4 color) { this.color = color; fill = true; return this; }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowPolygon SetFill(bool fill = true) { this.fill = fill; return this; }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowPolygon SetOutline(float outline) { this.outline = outline; return this; }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowPolygon SetOutlineColor(Color color) { outlineColor = color; return this; }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowPolygon SetOutlineColor(Vector4 color) { outlineColor = color; return this; }

        [NowConsumer]
        public NowPolygon Draw()
        {
            Now.DrawPolygon(this);
            return this;
        }
    }

    public static partial class Now
    {
        const float ShapeAaWidth = 0.75f;
        const float ShapeEpsilon = 0.0001f;

        static readonly NowLottieDrawBuffer _shapeBuffer = new NowLottieDrawBuffer();
        static readonly NowLottiePolyline _shapePolyline = new NowLottiePolyline();
        static readonly List<NowLottiePolyline> _shapePolylines = new List<NowLottiePolyline>(1);

        static StaticList<Vector2> _shapePoints = new StaticList<Vector2>(64);

        public static NowCircle Circle(Vector2 center, float radius)
        {
            return new NowCircle(center, new Vector2(radius, radius));
        }

        public static NowCircle Ellipse(NowRect rect)
        {
            return new NowCircle(rect.center, new Vector2(rect.width * 0.5f, rect.height * 0.5f));
        }

        public static NowCircle Ellipse(Vector2 center, Vector2 radius)
        {
            return new NowCircle(center, radius);
        }

        public static NowTriangle Triangle(Vector2 a, Vector2 b, Vector2 c)
        {
            return new NowTriangle(a, b, c);
        }

        public static NowPolygon Polygon(Vector2[] points)
        {
            return new NowPolygon(points, 0, points != null ? points.Length : 0);
        }

        public static NowPolygon Polygon(Vector2[] points, int count)
        {
            return new NowPolygon(points, 0, count);
        }

        public static NowPolygon Polygon(Vector2[] points, int start, int count)
        {
            return new NowPolygon(points, start, count);
        }

        public static NowPolygon Polygon(List<Vector2> points)
        {
            return new NowPolygon(points, 0, points != null ? points.Count : 0);
        }

        public static NowPolygon Polygon(List<Vector2> points, int count)
        {
            return new NowPolygon(points, 0, count);
        }

        public static NowPolygon Polygon(List<Vector2> points, int start, int count)
        {
            return new NowPolygon(points, start, count);
        }

        internal static void DrawCircle(in NowCircle circle)
        {
            bool hasTransform = _transformStack.Count > 0;

            Vector2 center = hasTransform ? ApplyTransform(circle.center) : circle.center;
            Vector2 radius = hasTransform ? ApplyTransformSize(circle.radius) : circle.radius;
            float scaledOutline = hasTransform ? ApplyTransformScalar(circle.outline) : circle.outline;

            float rx = Mathf.Abs(radius.x);
            float ry = Mathf.Abs(radius.y);

            if (rx <= ShapeEpsilon || ry <= ShapeEpsilon)
                return;

            _shapePoints.Clear();

            int segments = circle.segments > 2
                ? circle.segments
                : DefaultCircleSegments(UiUnitsToScreenPixels(Mathf.Max(rx, ry)));

            _shapePoints.EnsureCapacity(segments);

            if (segments <= MaxCachedCircleSegments)
            {
                var directions = GetCircleDirections(segments);

                for (int i = 0; i < segments; ++i)
                {
                    var direction = directions[i];
                    AddShapePoint(new Vector2(
                        center.x + direction.x * rx,
                        center.y + direction.y * ry));
                }
            }
            else
            {
                for (int i = 0; i < segments; ++i)
                {
                    float angle = (Mathf.PI * 2f * i) / segments;
                    AddShapePoint(new Vector2(
                        center.x + Mathf.Cos(angle) * rx,
                        center.y + Mathf.Sin(angle) * ry));
                }
            }

            DrawShapePoints(circle.mask, hasTransform, circle.fill, circle.color, scaledOutline, circle.outlineColor);
        }

        internal static void DrawTriangle(in NowTriangle triangle)
        {
            bool hasTransform = _transformStack.Count > 0;
            float scaledOutline = hasTransform ? ApplyTransformScalar(triangle.outline) : triangle.outline;

            _shapePoints.Clear();
            AddShapePoint(hasTransform ? ApplyTransform(triangle.a) : triangle.a);
            AddShapePoint(hasTransform ? ApplyTransform(triangle.b) : triangle.b);
            AddShapePoint(hasTransform ? ApplyTransform(triangle.c) : triangle.c);
            DrawShapePoints(triangle.mask, hasTransform, triangle.fill, triangle.color, scaledOutline, triangle.outlineColor);
        }

        internal static void DrawPolygon(in NowPolygon polygon)
        {
            int available = polygon.pointList != null
                ? polygon.pointList.Count
                : polygon.points != null
                    ? polygon.points.Length
                    : 0;

            int start = Mathf.Clamp(polygon.start, 0, available);
            int count = Mathf.Clamp(polygon.count, 0, available - start);

            if (count < 3)
                return;

            bool hasTransform = _transformStack.Count > 0;
            float scaledOutline = hasTransform ? ApplyTransformScalar(polygon.outline) : polygon.outline;

            _shapePoints.Clear();
            _shapePoints.EnsureCapacity(count);

            if (polygon.pointList != null)
            {
                for (int i = 0; i < count; ++i)
                {
                    Vector2 point = polygon.pointList[start + i];
                    AddShapePoint(hasTransform ? ApplyTransform(point) : point);
                }
            }
            else
            {
                for (int i = 0; i < count; ++i)
                {
                    Vector2 point = polygon.points[start + i];
                    AddShapePoint(hasTransform ? ApplyTransform(point) : point);
                }
            }

            DrawShapePoints(polygon.mask, hasTransform, polygon.fill, polygon.color, scaledOutline, polygon.outlineColor);
        }

        static void DrawShapePoints(
            NowRect mask,
            bool maskNeedsTransform,
            bool fill,
            Vector4 fillColor,
            float outline,
            Vector4 outlineColor)
        {
            if (_suppressDrawDepth > 0 || _defaultMaterial == null || _shapePoints.count < 3)
                return;

            if ((_shapePoints.array[0] - _shapePoints.array[_shapePoints.count - 1]).sqrMagnitude < ShapeEpsilon * ShapeEpsilon)
                --_shapePoints.count;

            if (_shapePoints.count < 3)
                return;

            _shapeBuffer.Clear();
            PrepareShapePolyline();
            float aaWidth = ScreenPixelsToUiUnits(ShapeAaWidth);

            var fillTint = ApplyColorMultiplier(fillColor);

            if (fill && fillTint.w > 0.0005f)
            {
                var paint = NowLottiePaint.Solid(fillTint);
                NowLottieTessellator.TessellateFill(_shapePolylines, null, false, false, paint, _shapeBuffer, aaWidth, 0f);
                NowLottieTessellator.EmitFillFringe(_shapePolylines, null, false, paint, _shapeBuffer, aaWidth);
            }

            var strokeTint = ApplyColorMultiplier(outlineColor);

            if (outline > 0f && strokeTint.w > 0.0005f)
            {
                var paint = NowLottiePaint.Solid(strokeTint);
                NowLottieTessellator.EmitStroke(_shapePolylines, outline, (int)NowLineCap.Butt, 1, paint, _shapeBuffer, aaWidth);
            }

            if (_shapeBuffer.positions.count == 0 || _shapeBuffer.indices.count == 0)
                return;

            if (maskNeedsTransform && !mask.isEmpty)
                mask = ApplyTransformRect(mask);

            var resolvedMask = ApplyAmbientMask(mask);

            if (resolvedMask.isEmpty)
                return;

            var mesh = UseMaterial(_defaultMaterial, NowMeshKind.Rectangle);

            if (mesh == null)
                return;

            mesh = EnsureMeshCapacity(mesh, _defaultMaterial, NowMeshKind.Rectangle, _shapeBuffer.positions.count);
            mesh.AddGeometry(_shapeBuffer, Vector2.zero, 1f, Vector4.one, resolvedMask);
        }

        static void PrepareShapePolyline()
        {
            _shapePolyline.Clear();
            _shapePolyline.closed = true;
            _shapePolyline.points.Capacity = Mathf.Max(_shapePolyline.points.Capacity, _shapePoints.count);

            for (int i = 0; i < _shapePoints.count; ++i)
                _shapePolyline.points.Add(_shapePoints.array[i]);

            _shapePolylines.Clear();
            _shapePolylines.Add(_shapePolyline);
        }

        static int DefaultCircleSegments(float radius)
        {
            int segments = Mathf.CeilToInt(Mathf.Sqrt(Mathf.Max(radius, 1f)) * 8f);
            return Mathf.Clamp(segments, 16, 128);
        }

        const int MaxCachedCircleSegments = 128;

        static readonly Dictionary<int, Vector2[]> _circleDirections = new Dictionary<int, Vector2[]>();

        /// <summary>
        /// Returned tables are shared and must never be mutated. The cache is bounded
        /// because callers only request counts up to <see cref="MaxCachedCircleSegments"/>.
        /// </summary>
        static Vector2[] GetCircleDirections(int segments)
        {
            if (!_circleDirections.TryGetValue(segments, out var directions))
            {
                directions = new Vector2[segments];

                for (int i = 0; i < segments; ++i)
                {
                    float angle = (Mathf.PI * 2f * i) / segments;
                    directions[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                }

                _circleDirections[segments] = directions;
            }

            return directions;
        }

        static void AddShapePoint(Vector2 point)
        {
            if (_shapePoints.count > 0 &&
                (point - _shapePoints.array[_shapePoints.count - 1]).sqrMagnitude < ShapeEpsilon * ShapeEpsilon)
            {
                return;
            }

            _shapePoints.EnsureCapacity(1);
            _shapePoints.array[_shapePoints.count++] = point;
        }
    }
}
