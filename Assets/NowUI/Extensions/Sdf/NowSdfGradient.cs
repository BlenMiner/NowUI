using UnityEngine;

namespace NowUI.Sdf
{
    /// <summary>
    /// Gradient fill state of a graph. While <see cref="active"/>, each appended
    /// shape captures it and is filled with the ramp laid over its own box (the
    /// same box texture fills use) instead of the solid color. Geometry uses the
    /// normalized parameters of <see cref="NowText"/> gradients.
    /// </summary>
    struct NowSdfGradientStyle
    {
        public bool active;
        public NowGradientKind kind;
        public NowGradientShape shape;
        public NowGradientSpread spread;
        public Vector4 parameters;
        public Vector4 from;
        public Vector4 to;
        public Gradient ramp;
        public int rampRevision;
        public float repetitions;

        public void SetColors(Vector4 fromColor, Vector4 toColor)
        {
            active = true;
            from = fromColor;
            to = toColor;
            ramp = null;
            rampRevision = 0;
        }

        public void SetRamp(Gradient gradient, int revision)
        {
            active = gradient != null;
            ramp = gradient;
            rampRevision = revision;
        }

        public void SetLinear(Vector2 direction)
        {
            active = true;
            kind = NowGradientKind.Linear;
            parameters = new Vector4(direction.x, direction.y, 0f, 0f);
        }

        public void SetLinear(NowGradientDirection direction)
        {
            const float diagonal = 0.70710678118f;

            switch (direction)
            {
                case NowGradientDirection.ToTop: SetLinear(new Vector2(0f, -1f)); break;
                case NowGradientDirection.ToTopRight: SetLinear(new Vector2(diagonal, -diagonal)); break;
                case NowGradientDirection.ToRight: SetLinear(new Vector2(1f, 0f)); break;
                case NowGradientDirection.ToBottomRight: SetLinear(new Vector2(diagonal, diagonal)); break;
                case NowGradientDirection.ToBottomLeft: SetLinear(new Vector2(-diagonal, diagonal)); break;
                case NowGradientDirection.ToLeft: SetLinear(new Vector2(-1f, 0f)); break;
                case NowGradientDirection.ToTopLeft: SetLinear(new Vector2(-diagonal, -diagonal)); break;
                default: SetLinear(new Vector2(0f, 1f)); break;
            }
        }

        public void SetLinear(float angleDegrees)
        {
            float radians = angleDegrees * Mathf.Deg2Rad;
            SetLinear(new Vector2(Mathf.Sin(radians), -Mathf.Cos(radians)));
        }

        public void SetRadial(Vector2 center, Vector2 radius, NowGradientShape radialShape)
        {
            active = true;
            kind = NowGradientKind.Radial;
            shape = radialShape;
            parameters = new Vector4(center.x, center.y, radius.x, radius.y);
        }

        public void SetConic(Vector2 center, float startAngle)
        {
            active = true;
            kind = NowGradientKind.Conic;
            parameters = new Vector4(center.x, center.y, startAngle / 360f, 0f);
        }

        /// <summary>Resolves this style against a shape's unrotated box in scene units.</summary>
        public NowSdfNodeGradient Resolve(NowRect box)
        {
            if (!active ||
                !Now.TryResolveGradientPayload(box, kind, shape, spread, parameters, repetitions, out Vector4 payload, out int flags))
            {
                return default;
            }

            return new NowSdfNodeGradient
            {
                enabled = true,
                payload = payload,
                flags = flags,
                from = from,
                to = to,
                ramp = ramp,
                rampRevision = rampRevision
            };
        }
    }

    /// <summary>
    /// A shape's resolved gradient: its payload in scene units and the ramp key.
    /// The atlas row is looked up when the scene uploads, so a rebuilt ramp atlas
    /// never leaves a cached graph pointing at a stale row.
    /// </summary>
    struct NowSdfNodeGradient
    {
        public bool enabled;
        public Vector4 payload;
        public int flags;
        public Vector4 from;
        public Vector4 to;
        public Gradient ramp;
        public int rampRevision;

        public float EncodeRamp()
        {
            NowGradientRampHandle handle = ramp != null
                ? NowGradientRampCache.Get(ramp, rampRevision)
                : NowGradientRampCache.Get(from, to);
            return Now.EncodeGradientRamp(handle, flags);
        }

        /// <summary>
        /// The unrotated box a shape's fill is laid over, in scene units: the same
        /// box <c>shapeUv</c> computes in NowSdfShaderV2.cginc. Glyphs and images
        /// have no gradient fill and return false.
        /// </summary>
        public static bool TryGetFillBox(NowSdfShapeType type, Vector4 data1, Vector4 data2, out NowRect box)
        {
            Vector2 min;
            Vector2 max;

            switch (type)
            {
                case NowSdfShapeType.Circle:
                    min = new Vector2(data1.x - data1.z, data1.y - data1.z);
                    max = new Vector2(data1.x + data1.z, data1.y + data1.z);
                    break;
                case NowSdfShapeType.Box:
                case NowSdfShapeType.RoundedBox:
                case NowSdfShapeType.Ellipse:
                case NowSdfShapeType.ChamferedBox:
                    min = new Vector2(data1.x - data1.z * 0.5f, data1.y - data1.w * 0.5f);
                    max = new Vector2(data1.x + data1.z * 0.5f, data1.y + data1.w * 0.5f);
                    break;
                case NowSdfShapeType.Capsule:
                    min = Vector2.Min(new Vector2(data1.x, data1.y), new Vector2(data1.z, data1.w)) - new Vector2(data2.x, data2.x);
                    max = Vector2.Max(new Vector2(data1.x, data1.y), new Vector2(data1.z, data1.w)) + new Vector2(data2.x, data2.x);
                    break;
                case NowSdfShapeType.Arc:
                case NowSdfShapeType.Pie:
                {
                    float extent = type == NowSdfShapeType.Arc ? data1.z + data1.w : data1.z;
                    min = new Vector2(data1.x - extent, data1.y - extent);
                    max = new Vector2(data1.x + extent, data1.y + extent);
                    break;
                }
                case NowSdfShapeType.Triangle:
                {
                    float scale = Mathf.Max(data2.w, 1.17549435e-38f);
                    Vector2 minNormalized = Vector2.Min(Vector2.zero, Vector2.Min(new Vector2(data1.z, data1.w), new Vector2(data2.x, data2.y)));
                    Vector2 maxNormalized = Vector2.Max(Vector2.zero, Vector2.Max(new Vector2(data1.z, data1.w), new Vector2(data2.x, data2.y)));
                    min = new Vector2(data1.x, data1.y) + minNormalized * scale;
                    max = new Vector2(data1.x, data1.y) + maxNormalized * scale;
                    break;
                }
                default:
                    box = default;
                    return false;
            }

            box = new NowRect(min.x, min.y, max.x - min.x, max.y - min.y);
            return true;
        }
    }
}
