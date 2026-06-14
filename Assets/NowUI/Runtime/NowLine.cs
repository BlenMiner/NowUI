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
            return this;
        }

        public NowLine SetColor(Vector4 color)
        {
            this.color = color;
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

            var color = ApplyColorMultiplier(line.color);

            if (color.w <= 0.0005f)
                return;

            _linePoints.Clear();

            if (line.cubic)
                FlattenLineCubic(line.from, line.control1, line.control2, line.to, ScreenPixelsToUiUnits(LineFlattenTolerance), ref _linePoints);
            else
            {
                AddLinePoint(ref _linePoints, line.from);
                AddLinePoint(ref _linePoints, line.to);
            }

            if (_linePoints.count < 2)
                return;

            _lineBuffer.Clear();

            if (line.dashLength > LineEpsilon && line.dashGap > LineEpsilon)
                EmitDashedLine(ref _linePoints, line, color, _lineBuffer);
            else
                EmitLineStroke(ref _linePoints, false, line.width, line.cap, color, _lineBuffer);

            if ((line.arrows & NowLineArrow.Start) != 0)
                EmitLineArrow(ref _linePoints, false, line, color, _lineBuffer);

            if ((line.arrows & NowLineArrow.End) != 0)
                EmitLineArrow(ref _linePoints, true, line, color, _lineBuffer);

            if (_lineBuffer.positions.count == 0 || _lineBuffer.indices.count == 0)
                return;

            var mask = ApplyAmbientMask(line.mask);

            if (mask.isEmpty)
                return;

            var mesh = UseMaterial(_defaultMaterial, ref _defaultMesh, NowMeshKind.Rectangle);

            if (mesh == null)
                return;

            mesh = EnsureMeshCapacity(mesh, _defaultMaterial, NowMeshKind.Rectangle, _lineBuffer.positions.count);
            mesh.AddGeometry(_lineBuffer, Vector2.zero, 1f, Vector4.one, mask);
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
            NowLottieDrawBuffer buffer)
        {
            float dash = Mathf.Max(line.dashLength, 0f);
            float gap = Mathf.Max(line.dashGap, 0f);
            float pattern = dash + gap;

            if (dash <= LineEpsilon || gap <= LineEpsilon || pattern <= LineEpsilon)
            {
                EmitLineStroke(ref points, false, line.width, line.cap, color, buffer);
                return;
            }

            float phase = Mathf.Repeat(line.dashOffset, pattern);
            bool drawing = phase < dash;
            float remaining = drawing ? dash - phase : pattern - phase;

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
                            AddLinePoint(ref _lineDashPoints, dashStart);
                        else
                            AddLinePointIfDistinct(ref _lineDashPoints, dashStart);

                        AddLinePointIfDistinct(ref _lineDashPoints, dashEnd);
                    }

                    walked = next;

                    if (remaining - step <= LineEpsilon)
                    {
                        if (drawing && _lineDashPoints.count >= 2)
                            EmitLineStroke(ref _lineDashPoints, false, line.width, line.cap, color, buffer);

                        _lineDashPoints.Clear();
                        drawing = !drawing;
                        remaining = drawing ? dash : gap;
                    }
                    else
                    {
                        remaining -= step;
                    }
                }
            }

            if (drawing && _lineDashPoints.count >= 2)
                EmitLineStroke(ref _lineDashPoints, false, line.width, line.cap, color, buffer);

            _lineDashPoints.Clear();
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

            float length = line.arrowLength > LineEpsilon
                ? line.arrowLength
                : Mathf.Max(line.width * 4f, 10f);
            float width = line.arrowWidth > LineEpsilon
                ? line.arrowWidth
                : Mathf.Max(line.width * 3f, length * 0.6f);

            Vector2 normal = new Vector2(direction.y, -direction.x);
            Vector2 baseCenter = tip - direction * length;
            Vector2 sideA = baseCenter + normal * (width * 0.5f);
            Vector2 sideB = baseCenter - normal * (width * 0.5f);

            _lineDashPoints.Clear();
            AddLinePoint(ref _lineDashPoints, sideA);
            AddLinePoint(ref _lineDashPoints, tip);
            EmitLineStroke(ref _lineDashPoints, false, line.width, NowLineCap.Round, color, buffer);

            _lineDashPoints.Clear();
            AddLinePoint(ref _lineDashPoints, tip);
            AddLinePoint(ref _lineDashPoints, sideB);
            EmitLineStroke(ref _lineDashPoints, false, line.width, NowLineCap.Round, color, buffer);

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
                EmitLineRoundCap(_lineStrokePoints.array[count - 1], endDirection, innerWidth, outerWidth, coreAlpha, color, buffer);
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
