using System;
using NowUI.Internal;
using UnityEngine;

namespace NowUI
{
    public enum NowLineCap
    {
        Butt = 1,
        Round = 2,
        Square = 3
    }

    [System.Flags]
    public enum NowLineArrow
    {
        None = 0,
        Start = 1,
        End = 2,
        Both = Start | End
    }

    [NowBuilder]
    public struct NowLine
    {
        public Vector2 from;

        public Vector2 to;

        public Vector2 control1;

        public Vector2 control2;

        public NowRect mask;

        public Vector4 color;

        public Vector4 colorEnd;

        public bool gradient;

        public float width;

        public NowLineCap cap;

        public bool cubic;

        public float dashLength;

        public float dashGap;

        public float dashOffset;

        public NowLineArrow arrows;

        public float arrowLength;

        public float arrowWidth;

        public NowLine(Vector2 from, Vector2 to)
        {
            this.from = from;
            this.to = to;
            control1 = from;
            control2 = to;
            mask = default;
            color = Vector4.one;
            colorEnd = Vector4.one;
            gradient = false;
            width = 1f;
            cap = NowLineCap.Butt;
            cubic = false;
            dashLength = 0f;
            dashGap = 0f;
            dashOffset = 0f;
            arrows = NowLineArrow.None;
            arrowLength = 0f;
            arrowWidth = 0f;
        }

        public NowLine(Vector2 from, Vector2 control1, Vector2 control2, Vector2 to)
            : this(from, to)
        {
            this.control1 = control1;
            this.control2 = control2;
            cubic = true;
        }

        public NowLine SetPosition(Vector2 from, Vector2 to)
        {
            this.from = from;
            this.to = to;
            control1 = from;
            control2 = to;
            cubic = false;
            return this;
        }

        public NowLine SetBezier(Vector2 from, Vector2 control1, Vector2 control2, Vector2 to)
        {
            this.from = from;
            this.control1 = control1;
            this.control2 = control2;
            this.to = to;
            cubic = true;
            return this;
        }

        public NowLine SetMask(NowRect mask)
        {
            this.mask = mask;
            return this;
        }

        public NowLine SetColor(Color color)
        {
            this.color = color;
            gradient = false;
            return this;
        }

        public NowLine SetColor(Vector4 color)
        {
            this.color = color;
            gradient = false;
            return this;
        }

        public NowLine SetGradient(Color from, Color to)
        {
            color = from;
            colorEnd = to;
            gradient = true;
            return this;
        }

        public NowLine SetGradient(Vector4 from, Vector4 to)
        {
            color = from;
            colorEnd = to;
            gradient = true;
            return this;
        }

        public NowLine SetWidth(float width)
        {
            this.width = width;
            return this;
        }

        public NowLine SetCap(NowLineCap cap)
        {
            this.cap = cap;
            return this;
        }

        public NowLine SetDash(float dashLength, float gapLength, float offset = 0f)
        {
            this.dashLength = dashLength;
            dashGap = gapLength;
            dashOffset = offset;
            return this;
        }

        public NowLine SetSolid()
        {
            dashLength = 0f;
            dashGap = 0f;
            dashOffset = 0f;
            return this;
        }

        public NowLine SetArrow(NowLineArrow arrows = NowLineArrow.End, float length = 0f, float width = 0f)
        {
            this.arrows = arrows;
            arrowLength = length;
            arrowWidth = width;
            return this;
        }

        [NowConsumer]
        public NowLine Draw()
        {
            Now.DrawLine(this);
            return this;
        }
    }

    public static partial class Now
    {
        const float LineFlattenTolerance = 0.2f;

        const float LineAaWidth = 0.75f;

        const float LineEpsilon = 0.0001f;

        static readonly NowLottieDrawBuffer _lineBuffer = new NowLottieDrawBuffer();

        static StaticList<Vector2> _linePoints = new StaticList<Vector2>(64);

        static StaticList<Vector2> _lineDashPoints = new StaticList<Vector2>(16);

        static StaticList<Vector2> _lineStrokePoints = new StaticList<Vector2>(64);

        static StaticList<Vector2> _lineStrokeNormals = new StaticList<Vector2>(64);

        static StaticList<float> _lineStrokeArcs = new StaticList<float>(64);

        static Material _bezierMaterial;

        static int _bezierMesh = -1;

        static StaticList<Vector2> _bezierPoints = new StaticList<Vector2>(128);

        static StaticList<float> _bezierT = new StaticList<float>(128);

        static StaticList<Vector2> _bezierNormals = new StaticList<Vector2>(128);

        static Material GetBezierMaterial()
        {
            if (_bezierMaterial != null)
                return _bezierMaterial;

            var template = Resources.Load<Material>("NowUI/BezierMaterial");

            if (template != null)
                _bezierMaterial = new Material(template);
            else
            {
                var shader = Shader.Find("NowUI/UI Bezier");

                if (shader == null)
                    return null;

                _bezierMaterial = new Material(shader);
            }

            _bezierMaterial.name = "NowUI Bezier";
            _bezierMaterial.hideFlags = HideFlags.HideAndDontSave;

            return _bezierMaterial;
        }

        public static NowLine Line(Vector2 from, Vector2 to)
        {
            return new NowLine(from, to);
        }

        public static NowLine Line(float x0, float y0, float x1, float y1)
        {
            return new NowLine(new Vector2(x0, y0), new Vector2(x1, y1));
        }

        public static NowLine Bezier(Vector2 from, Vector2 control1, Vector2 control2, Vector2 to)
        {
            return new NowLine(from, control1, control2, to);
        }

        internal static void DrawLine(NowLine line)
        {
            if (_suppressDrawDepth > 0 || _defaultMaterial == null || line.width <= 0f)
                return;

            bool hasTransform = _transformStack.Count > 0;

            var color = ApplyColorMultiplier(line.color);
            var colorEnd = line.gradient ? ApplyColorMultiplier(line.colorEnd) : color;

            if (color.w <= 0.0005f && colorEnd.w <= 0.0005f)
                return;

            _linePoints.Clear();

            // Transform line positions if transform is active
            Vector2 from = hasTransform ? ApplyTransform(line.from) : line.from;
            Vector2 to = hasTransform ? ApplyTransform(line.to) : line.to;
            Vector2 control1 = hasTransform ? ApplyTransform(line.control1) : line.control1;
            Vector2 control2 = hasTransform ? ApplyTransform(line.control2) : line.control2;

            // Scale width by transform
            float scaledWidth = hasTransform ? ApplyTransformScalar(line.width) : line.width;

            // Solid cubic Beziers use a dedicated shader that AA's against the true
            // curve. It's immediate-mode only, so skip it during canvas/capture builds.
            bool solidCubic = line.cubic
                && !hasTransform
                && !_captureMesh
                && !(line.dashLength > LineEpsilon && line.dashGap > LineEpsilon)
                && line.arrows == NowLineArrow.None;

            if (solidCubic && GetBezierMaterial() != null)
            {
                var bezierMask = line.mask;

                if (hasTransform && !bezierMask.isEmpty)
                    bezierMask = ApplyTransformRect(bezierMask);

                bezierMask = ApplyAmbientMask(bezierMask);

                if (!bezierMask.isEmpty)
                    DrawBezierStroke(from, control1, control2, to, scaledWidth, color, colorEnd, bezierMask, line.cap);

                return;
            }

            // Scale tolerance by transform for consistent flattening
            float tolerance = ScreenPixelsToUiUnits(LineFlattenTolerance);

            if (line.cubic)
                FlattenLineCubic(from, control1, control2, to, tolerance, ref _linePoints);
            else
            {
                AddLinePoint(ref _linePoints, from);
                AddLinePoint(ref _linePoints, to);
            }

            if (_linePoints.count < 2)
                return;

            _lineBuffer.Clear();

            if (line.dashLength > LineEpsilon && line.dashGap > LineEpsilon)
                EmitDashedLine(ref _linePoints, line, color, colorEnd, _lineBuffer);
            else
                EmitLineStroke(ref _linePoints, false, scaledWidth, line.cap, color, colorEnd, _lineBuffer);

            if ((line.arrows & NowLineArrow.Start) != 0)
                EmitLineArrow(ref _linePoints, false, line, color, _lineBuffer);

            if ((line.arrows & NowLineArrow.End) != 0)
                EmitLineArrow(ref _linePoints, true, line, colorEnd, _lineBuffer);

            if (_lineBuffer.positions.count == 0 || _lineBuffer.indices.count == 0)
                return;

            var mask = line.mask;

            if (hasTransform && !mask.isEmpty)
                mask = ApplyTransformRect(mask);

            mask = ApplyAmbientMask(mask);

            if (mask.isEmpty)
                return;

            var mesh = UseMaterial(_defaultMaterial, ref _defaultMesh, NowMeshKind.Rectangle);

            if (mesh == null)
                return;

            mesh = EnsureMeshCapacity(mesh, _defaultMaterial, NowMeshKind.Rectangle, _lineBuffer.positions.count);
            mesh.AddGeometry(_lineBuffer, Vector2.zero, 1f, Vector4.one, mask);
        }

        internal static void DrawPolyline(ReadOnlySpan<Vector2> points, float width, NowLineCap cap, Vector4 color, NowRect mask = default)
        {
            if (_suppressDrawDepth > 0 || _defaultMaterial == null || width <= 0f || points.Length < 2)
                return;

            bool hasTransform = _transformStack.Count > 0;

            color = ApplyColorMultiplier(color);

            if (color.w <= 0.0005f)
                return;

            _linePoints.Clear();

            for (int i = 0; i < points.Length; ++i)
            {
                Vector2 point = hasTransform ? ApplyTransform(points[i]) : points[i];
                AddLinePointIfDistinct(ref _linePoints, point);
            }

            if (_linePoints.count < 2)
                return;

            _lineBuffer.Clear();
            float scaledWidth = hasTransform ? ApplyTransformScalar(width) : width;
            EmitLineStroke(ref _linePoints, false, scaledWidth, cap, color, color, _lineBuffer);

            if (_lineBuffer.positions.count == 0 || _lineBuffer.indices.count == 0)
                return;

            if (hasTransform && !mask.isEmpty)
                mask = ApplyTransformRect(mask);

            mask = ApplyAmbientMask(mask);

            if (mask.isEmpty)
                return;

            var mesh = UseMaterial(_defaultMaterial, ref _defaultMesh, NowMeshKind.Rectangle);

            if (mesh == null)
                return;

            mesh = EnsureMeshCapacity(mesh, _defaultMaterial, NowMeshKind.Rectangle, _lineBuffer.positions.count);
            mesh.AddGeometry(_lineBuffer, Vector2.zero, 1f, Vector4.one, mask);
        }

        static void AddBezierPoint(Vector2 pos, float t)
        {
            _bezierPoints.EnsureCapacity(1);
            _bezierT.EnsureCapacity(1);
            _bezierPoints.array[_bezierPoints.count++] = pos;
            _bezierT.array[_bezierT.count++] = t;
        }

        static void FlattenCubicT(
            Vector2 p0,
            Vector2 p1,
            Vector2 p2,
            Vector2 p3,
            float t0,
            float t1,
            float tolerance,
            int depth)
        {
            if (depth >= 18 || IsLineCubicFlat(p0, p1, p2, p3, tolerance))
            {
                AddBezierPoint(p3, t1);
                return;
            }

            Vector2 p01 = (p0 + p1) * 0.5f;
            Vector2 p12 = (p1 + p2) * 0.5f;
            Vector2 p23 = (p2 + p3) * 0.5f;
            Vector2 p012 = (p01 + p12) * 0.5f;
            Vector2 p123 = (p12 + p23) * 0.5f;
            Vector2 mid = (p012 + p123) * 0.5f;
            float tm = (t0 + t1) * 0.5f;

            FlattenCubicT(p0, p01, p012, mid, t0, tm, tolerance, depth + 1);
            FlattenCubicT(mid, p123, p23, p3, tm, t1, tolerance, depth + 1);
        }

        static int AddBezierVertex(
            NowMesh mesh,
            Vector2 uiPos,
            float t,
            Vector4 cp01,
            Vector4 cp23,
            Vector4 color,
            float halfWidth,
            float aaWidth,
            Vector4 mask)
        {
            return mesh.AddRawVertexUnchecked(
                new Vector3(uiPos.x, -uiPos.y, 0f),
                Vector2.zero,
                cp01,
                cp23,
                color,
                Vector4.zero,
                new Vector4(halfWidth, aaWidth, t, 0f),
                mask,
                new Vector4(uiPos.x, uiPos.y, 0f, 0f));
        }

        static void DrawBezierStroke(
            Vector2 p0,
            Vector2 p1,
            Vector2 p2,
            Vector2 p3,
            float width,
            Vector4 color,
            Vector4 colorEnd,
            NowRect mask,
            NowLineCap cap)
        {
            float halfWidth = width * 0.5f;

            if (halfWidth <= 0f)
                return;

            float aaWidth = ScreenPixelsToUiUnits(LineAaWidth);
            float flattenTolerance = ScreenPixelsToUiUnits(1f);
            float extend = halfWidth + aaWidth + ScreenPixelsToUiUnits(2f);
            bool roundCap = cap == NowLineCap.Round;

            _bezierPoints.Clear();
            _bezierT.Clear();
            AddBezierPoint(p0, 0f);
            FlattenCubicT(p0, p1, p2, p3, 0f, 1f, flattenTolerance, 0);

            int count = _bezierPoints.count;

            if (count < 2)
                return;

            _bezierNormals.Clear();
            _bezierNormals.EnsureCapacity(count);

            for (int i = 0; i < count; ++i)
            {
                Vector2 cur = _bezierPoints.array[i];
                Vector2 prev = i > 0 ? _bezierPoints.array[i - 1] : cur;
                Vector2 next = i < count - 1 ? _bezierPoints.array[i + 1] : cur;

                Vector2 inDir = NormalizeLineVector(cur - prev);
                Vector2 outDir = NormalizeLineVector(next - cur);

                if (inDir == Vector2.zero)
                    inDir = outDir;

                if (outDir == Vector2.zero)
                    outDir = inDir;

                Vector2 inNormal = new Vector2(inDir.y, -inDir.x);
                Vector2 outNormal = new Vector2(outDir.y, -outDir.x);
                Vector2 average = NormalizeLineVector(inNormal + outNormal);

                if (average == Vector2.zero)
                    average = inNormal;

                float miter = Vector2.Dot(average, outNormal);
                float scale = 1f / Mathf.Max(0.35f, Mathf.Abs(miter));
                _bezierNormals.array[_bezierNormals.count++] = average * scale;
            }

            var material = GetBezierMaterial();

            if (material == null)
                return;

            int segmentCount = count - 1;
            var mesh = UseMaterial(material, ref _bezierMesh, NowMeshKind.Bezier);

            if (mesh == null)
                return;

            mesh = EnsureMeshCapacity(mesh, material, NowMeshKind.Bezier, segmentCount * 4);
            mesh.EnsureRawCapacity(segmentCount * 4, segmentCount * 6);

            var maskVec = new Vector4(mask.x, mask.y, mask.width, mask.height);
            var cp01 = new Vector4(p0.x, p0.y, p1.x, p1.y);
            var cp23 = new Vector4(p2.x, p2.y, p3.x, p3.y);

            for (int i = 0; i < segmentCount; ++i)
            {
                Vector2 a = _bezierPoints.array[i];
                Vector2 b = _bezierPoints.array[i + 1];
                Vector2 na = _bezierNormals.array[i] * extend;
                Vector2 nb = _bezierNormals.array[i + 1] * extend;
                float ta = _bezierT.array[i];
                float tb = _bezierT.array[i + 1];

                Vector2 aPos = a;
                Vector2 bPos = b;

                if (roundCap)
                {
                    Vector2 segDir = NormalizeLineVector(b - a);

                    if (i == 0)
                        aPos = a - segDir * extend;

                    if (i == segmentCount - 1)
                        bPos = b + segDir * extend;
                }

                Vector4 colorA = colorEnd == color ? color : Vector4.Lerp(color, colorEnd, ta);
                Vector4 colorB = colorEnd == color ? color : Vector4.Lerp(color, colorEnd, tb);

                int v0 = AddBezierVertex(mesh, aPos + na, ta, cp01, cp23, colorA, halfWidth, aaWidth, maskVec);
                int v1 = AddBezierVertex(mesh, bPos + nb, tb, cp01, cp23, colorB, halfWidth, aaWidth, maskVec);
                int v2 = AddBezierVertex(mesh, bPos - nb, tb, cp01, cp23, colorB, halfWidth, aaWidth, maskVec);
                int v3 = AddBezierVertex(mesh, aPos - na, ta, cp01, cp23, colorA, halfWidth, aaWidth, maskVec);

                mesh.AddRawTriangleUnchecked(v0, v1, v2);
                mesh.AddRawTriangleUnchecked(v0, v2, v3);
            }
        }

        static void FlattenLineCubic(
            Vector2 p0,
            Vector2 c1,
            Vector2 c2,
            Vector2 p1,
            float tolerance,
            ref StaticList<Vector2> output)
        {
            AddLinePoint(ref output, p0);
            FlattenLineCubicRecursive(p0, c1, c2, p1, tolerance, ref output, 0);
        }

        static void FlattenLineCubicRecursive(
            Vector2 p0,
            Vector2 c1,
            Vector2 c2,
            Vector2 p1,
            float tolerance,
            ref StaticList<Vector2> output,
            int depth)
        {
            if (depth >= 18 || IsLineCubicFlat(p0, c1, c2, p1, tolerance))
            {
                AddLinePoint(ref output, p1);
                return;
            }

            Vector2 p01 = (p0 + c1) * 0.5f;
            Vector2 p12 = (c1 + c2) * 0.5f;
            Vector2 p23 = (c2 + p1) * 0.5f;
            Vector2 p012 = (p01 + p12) * 0.5f;
            Vector2 p123 = (p12 + p23) * 0.5f;
            Vector2 mid = (p012 + p123) * 0.5f;

            FlattenLineCubicRecursive(p0, p01, p012, mid, tolerance, ref output, depth + 1);
            FlattenLineCubicRecursive(mid, p123, p23, p1, tolerance, ref output, depth + 1);
        }

        static bool IsLineCubicFlat(Vector2 p0, Vector2 c1, Vector2 c2, Vector2 p1, float tolerance)
        {
            Vector2 chord = p1 - p0;
            float chordLengthSquared = chord.sqrMagnitude;

            if (chordLengthSquared < LineEpsilon * LineEpsilon)
                return (c1 - p0).magnitude + (c2 - p0).magnitude < tolerance;

            float d1 = Mathf.Abs((c1.x - p0.x) * chord.y - (c1.y - p0.y) * chord.x);
            float d2 = Mathf.Abs((c2.x - p0.x) * chord.y - (c2.y - p0.y) * chord.x);
            return (d1 + d2) / Mathf.Sqrt(chordLengthSquared) <= tolerance;
        }

        static void EmitDashedLine(
            ref StaticList<Vector2> points,
            NowLine line,
            Vector4 color,
            Vector4 colorTo,
            NowLottieDrawBuffer buffer)
        {
            bool hasTransform = _transformStack.Count > 0;
            float scaledWidth = hasTransform ? ApplyTransformScalar(line.width) : line.width;
            float scalar = hasTransform ? ApplyTransformScalar(1f) : 1f;

            float dash = Mathf.Max(line.dashLength * scalar, 0f);
            float gap = Mathf.Max(line.dashGap * scalar, 0f);
            float pattern = dash + gap;

            if (dash <= LineEpsilon || gap <= LineEpsilon || pattern <= LineEpsilon)
            {
                EmitLineStroke(ref points, false, scaledWidth, line.cap, color, colorTo, buffer);
                return;
            }

            bool hasGradient = colorTo != color;
            float totalLength = 0f;

            if (hasGradient)
            {
                for (int i = 1; i < points.count; ++i)
                    totalLength += (points.array[i] - points.array[i - 1]).magnitude;

                if (totalLength <= LineEpsilon)
                    hasGradient = false;
            }

            float phase = Mathf.Repeat(line.dashOffset * scalar, pattern);
            bool drawing = phase < dash;
            float remaining = drawing ? dash - phase : pattern - phase;
            float traveled = 0f;
            float dashStartDistance = 0f;

            _lineDashPoints.Clear();

            for (int i = 1; i < points.count; ++i)
            {
                Vector2 start = points.array[i - 1];
                Vector2 end = points.array[i];
                Vector2 delta = end - start;
                float length = delta.magnitude;

                if (length <= LineEpsilon)
                    continue;

                float walked = 0f;

                while (walked < length - LineEpsilon)
                {
                    float step = Mathf.Min(remaining, length - walked);
                    float next = walked + step;

                    if (drawing)
                    {
                        Vector2 dashStart = start + delta * (walked / length);
                        Vector2 dashEnd = start + delta * (next / length);

                        if (_lineDashPoints.count == 0)
                        {
                            dashStartDistance = traveled + walked;
                            AddLinePoint(ref _lineDashPoints, dashStart);
                        }
                        else
                        {
                            AddLinePointIfDistinct(ref _lineDashPoints, dashStart);
                        }

                        AddLinePointIfDistinct(ref _lineDashPoints, dashEnd);
                    }

                    walked = next;

                    if (remaining - step <= LineEpsilon)
                    {
                        if (drawing && _lineDashPoints.count >= 2)
                        {
                            EmitDashStroke(
                                scaledWidth,
                                line.cap,
                                color,
                                colorTo,
                                hasGradient,
                                dashStartDistance,
                                traveled + walked,
                                totalLength,
                                buffer);
                        }

                        _lineDashPoints.Clear();
                        drawing = !drawing;
                        remaining = drawing ? dash : gap;
                    }
                    else
                    {
                        remaining -= step;
                    }
                }

                traveled += length;
            }

            if (drawing && _lineDashPoints.count >= 2)
            {
                EmitDashStroke(
                    scaledWidth,
                    line.cap,
                    color,
                    colorTo,
                    hasGradient,
                    dashStartDistance,
                    traveled,
                    totalLength,
                    buffer);
            }

            _lineDashPoints.Clear();
        }

        static void EmitDashStroke(
            float width,
            NowLineCap cap,
            Vector4 color,
            Vector4 colorTo,
            bool hasGradient,
            float startDistance,
            float endDistance,
            float totalLength,
            NowLottieDrawBuffer buffer)
        {
            Vector4 from = color;
            Vector4 to = color;

            if (hasGradient)
            {
                from = Vector4.Lerp(color, colorTo, Mathf.Clamp01(startDistance / totalLength));
                to = Vector4.Lerp(color, colorTo, Mathf.Clamp01(endDistance / totalLength));
            }

            EmitLineStroke(ref _lineDashPoints, false, width, cap, from, to, buffer);
        }

        static void EmitLineArrow(
            ref StaticList<Vector2> points,
            bool atEnd,
            NowLine line,
            Vector4 color,
            NowLottieDrawBuffer buffer)
        {
            if (!TryGetLineTangent(ref points, atEnd, out var tip, out var direction))
                return;

            bool hasTransform = _transformStack.Count > 0;
            float scaledWidth = hasTransform ? ApplyTransformScalar(line.width) : line.width;

            float length = line.arrowLength > LineEpsilon
                ? line.arrowLength
                : Mathf.Max(line.width * 4f, 10f);
            float width = line.arrowWidth > LineEpsilon
                ? line.arrowWidth
                : Mathf.Max(line.width * 3f, length * 0.6f);

            // Scale arrow dimensions by transform
            if (hasTransform)
            {
                float avgScale = (Mathf.Abs(currentTransform.scale.x) + Mathf.Abs(currentTransform.scale.y)) * 0.5f;
                length *= avgScale;
                width *= avgScale;
            }

            Vector2 normal = new Vector2(direction.y, -direction.x);
            Vector2 baseCenter = tip - direction * length;
            Vector2 sideA = baseCenter + normal * (width * 0.5f);
            Vector2 sideB = baseCenter - normal * (width * 0.5f);

            _lineDashPoints.Clear();
            AddLinePoint(ref _lineDashPoints, sideA);
            AddLinePoint(ref _lineDashPoints, tip);
            EmitLineStroke(ref _lineDashPoints, false, scaledWidth, NowLineCap.Round, color, color, buffer);

            _lineDashPoints.Clear();
            AddLinePoint(ref _lineDashPoints, tip);
            AddLinePoint(ref _lineDashPoints, sideB);
            EmitLineStroke(ref _lineDashPoints, false, scaledWidth, NowLineCap.Round, color, color, buffer);

            _lineDashPoints.Clear();
        }

        static bool TryGetLineTangent(
            ref StaticList<Vector2> points,
            bool atEnd,
            out Vector2 tip,
            out Vector2 direction)
        {
            if (atEnd)
            {
                tip = points.array[points.count - 1];

                for (int i = points.count - 2; i >= 0; --i)
                {
                    direction = tip - points.array[i];

                    if (direction.sqrMagnitude > LineEpsilon * LineEpsilon)
                    {
                        direction.Normalize();
                        return true;
                    }
                }
            }
            else
            {
                tip = points.array[0];

                for (int i = 1; i < points.count; ++i)
                {
                    direction = tip - points.array[i];

                    if (direction.sqrMagnitude > LineEpsilon * LineEpsilon)
                    {
                        direction.Normalize();
                        return true;
                    }
                }
            }

            tip = default;
            direction = default;
            return false;
        }

        static void EmitLineStroke(
            ref StaticList<Vector2> source,
            bool closed,
            float width,
            NowLineCap cap,
            Vector4 color,
            Vector4 colorTo,
            NowLottieDrawBuffer buffer)
        {
            float halfWidth = width * 0.5f;

            if (halfWidth <= 0f)
                return;

            _lineStrokePoints.Clear();

            for (int i = 0; i < source.count; ++i)
            {
                Vector2 point = source.array[i];

                if (_lineStrokePoints.count == 0 ||
                    (point - _lineStrokePoints.array[_lineStrokePoints.count - 1]).sqrMagnitude > LineEpsilon * LineEpsilon)
                {
                    AddLinePoint(ref _lineStrokePoints, point);
                }
            }

            int count = _lineStrokePoints.count;

            if (count < 2)
                return;

            if (closed && count > 1 &&
                (_lineStrokePoints.array[count - 1] - _lineStrokePoints.array[0]).sqrMagnitude < LineEpsilon * LineEpsilon)
            {
                --_lineStrokePoints.count;
                --count;
            }

            if (count < 2)
                return;

            if (!closed && cap == NowLineCap.Square)
            {
                Vector2 startDirection = NormalizeLineVector(_lineStrokePoints.array[0] - _lineStrokePoints.array[1]);
                Vector2 endDirection = NormalizeLineVector(_lineStrokePoints.array[count - 1] - _lineStrokePoints.array[count - 2]);
                _lineStrokePoints.array[0] += startDirection * halfWidth;
                _lineStrokePoints.array[count - 1] += endDirection * halfWidth;
            }

            float aaWidth = ScreenPixelsToUiUnits(LineAaWidth);
            float innerWidth = Mathf.Max(halfWidth - aaWidth, 0f);
            float outerWidth = halfWidth + aaWidth;
            float coreAlpha = halfWidth < aaWidth ? halfWidth / aaWidth : 1f;

            _lineStrokeNormals.Clear();

            for (int i = 0; i < count; ++i)
            {
                Vector2 inDirection;
                Vector2 outDirection;

                if (closed)
                {
                    inDirection = NormalizeLineVector(_lineStrokePoints.array[i] - _lineStrokePoints.array[(i - 1 + count) % count]);
                    outDirection = NormalizeLineVector(_lineStrokePoints.array[(i + 1) % count] - _lineStrokePoints.array[i]);
                }
                else
                {
                    inDirection = i > 0
                        ? NormalizeLineVector(_lineStrokePoints.array[i] - _lineStrokePoints.array[i - 1])
                        : Vector2.zero;
                    outDirection = i < count - 1
                        ? NormalizeLineVector(_lineStrokePoints.array[i + 1] - _lineStrokePoints.array[i])
                        : Vector2.zero;

                    if (inDirection == Vector2.zero)
                        inDirection = outDirection;

                    if (outDirection == Vector2.zero)
                        outDirection = inDirection;
                }

                Vector2 inNormal = new Vector2(inDirection.y, -inDirection.x);
                Vector2 outNormal = new Vector2(outDirection.y, -outDirection.x);
                Vector2 average = NormalizeLineVector(inNormal + outNormal);

                if (average == Vector2.zero)
                    average = inNormal;

                float miter = Vector2.Dot(average, outNormal);
                float scale = 1f / Mathf.Max(0.35f, Mathf.Abs(miter));
                AddLinePoint(ref _lineStrokeNormals, average * scale);
            }

            bool hasGradient = colorTo != color;
            float totalArc = 0f;

            if (hasGradient)
            {
                _lineStrokeArcs.Clear();
                _lineStrokeArcs.EnsureCapacity(count);
                _lineStrokeArcs.array[_lineStrokeArcs.count++] = 0f;

                for (int i = 1; i < count; ++i)
                {
                    totalArc += (_lineStrokePoints.array[i] - _lineStrokePoints.array[i - 1]).magnitude;
                    _lineStrokeArcs.array[_lineStrokeArcs.count++] = totalArc;
                }

                if (totalArc <= LineEpsilon)
                    hasGradient = false;
            }

            int firstRing = -1;
            int previousRing = -1;

            Vector4 coreColor = color;
            coreColor.w *= coreAlpha;
            Vector4 edgeColor = coreColor;
            edgeColor.w = 0f;

            for (int i = 0; i < count; ++i)
            {
                Vector2 position = _lineStrokePoints.array[i];
                Vector2 normal = _lineStrokeNormals.array[i];

                if (hasGradient)
                {
                    coreColor = Vector4.Lerp(color, colorTo, _lineStrokeArcs.array[i] / totalArc);
                    coreColor.w *= coreAlpha;
                    edgeColor = coreColor;
                    edgeColor.w = 0f;
                }

                int ring = buffer.AddVertex(position + normal * outerWidth, edgeColor);
                buffer.AddVertex(position + normal * innerWidth, coreColor);
                buffer.AddVertex(position - normal * innerWidth, coreColor);
                buffer.AddVertex(position - normal * outerWidth, edgeColor);

                if (i == 0)
                    firstRing = ring;

                if (i > 0)
                    ConnectLineStrokeRings(buffer, previousRing, ring);

                previousRing = ring;
            }

            if (closed)
                ConnectLineStrokeRings(buffer, previousRing, firstRing);

            if (!closed && cap == NowLineCap.Round)
            {
                Vector2 startDirection = NormalizeLineVector(_lineStrokePoints.array[0] - _lineStrokePoints.array[1]);
                Vector2 endDirection = NormalizeLineVector(_lineStrokePoints.array[count - 1] - _lineStrokePoints.array[count - 2]);
                EmitLineRoundCap(_lineStrokePoints.array[0], startDirection, innerWidth, outerWidth, coreAlpha, color, buffer);
                EmitLineRoundCap(_lineStrokePoints.array[count - 1], endDirection, innerWidth, outerWidth, coreAlpha, hasGradient ? colorTo : color, buffer);
            }
        }

        static void ConnectLineStrokeRings(NowLottieDrawBuffer buffer, int ringA, int ringB)
        {
            buffer.AddQuad(ringA + 0, ringB + 0, ringB + 1, ringA + 1);
            buffer.AddQuad(ringA + 1, ringB + 1, ringB + 2, ringA + 2);
            buffer.AddQuad(ringA + 2, ringB + 2, ringB + 3, ringA + 3);
        }

        static void EmitLineRoundCap(
            Vector2 center,
            Vector2 direction,
            float innerWidth,
            float outerWidth,
            float coreAlpha,
            Vector4 color,
            NowLottieDrawBuffer buffer)
        {
            Vector2 normal = new Vector2(direction.y, -direction.x);

            Vector4 coreColor = color;
            coreColor.w *= coreAlpha;
            Vector4 edgeColor = coreColor;
            edgeColor.w = 0f;

            int segments = Mathf.Clamp(Mathf.CeilToInt(UiUnitsToScreenPixels(outerWidth) * 0.6f) + 3, 4, 24);
            int centerIndex = buffer.AddVertex(center, coreColor);

            int previousInner = -1;
            int previousOuter = -1;

            for (int i = 0; i <= segments; ++i)
            {
                float angle = Mathf.PI * i / segments;
                Vector2 radial = normal * Mathf.Cos(angle) + direction * Mathf.Sin(angle);

                int inner = buffer.AddVertex(center + radial * innerWidth, coreColor);
                int outer = buffer.AddVertex(center + radial * outerWidth, edgeColor);

                if (i > 0)
                {
                    buffer.AddTriangle(centerIndex, previousInner, inner);
                    buffer.AddQuad(previousInner, previousOuter, outer, inner);
                }

                previousInner = inner;
                previousOuter = outer;
            }
        }

        static void AddLinePoint(ref StaticList<Vector2> list, Vector2 point)
        {
            list.EnsureCapacity(1);
            list.array[list.count++] = point;
        }

        static void AddLinePointIfDistinct(ref StaticList<Vector2> list, Vector2 point)
        {
            if (list.count == 0 ||
                (point - list.array[list.count - 1]).sqrMagnitude > LineEpsilon * LineEpsilon)
            {
                AddLinePoint(ref list, point);
            }
        }

        static Vector2 NormalizeLineVector(Vector2 vector)
        {
            float magnitude = vector.magnitude;
            return magnitude > LineEpsilon ? vector / magnitude : Vector2.zero;
        }
    }
}
