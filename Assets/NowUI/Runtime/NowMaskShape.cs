using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace NowUI
{
    internal enum NowMaskShapeKind : byte
    {
        Rectangle = 0,
        RoundedRect = 1,
        Ellipse = 2,
        Capsule = 3
    }

    /// <summary>
    /// Immutable analytic geometry for an ambient <see cref="Now.Mask(NowMaskShape)"/>
    /// scope. Shape coordinates use NowUI's top-left-origin UI space. Feathering
    /// affects rendered coverage only; input remains bounded by the geometric edge.
    /// </summary>
    public readonly struct NowMaskShape : IEquatable<NowMaskShape>
    {
        readonly NowMaskShapeKind _kind;
        readonly NowRect _rect;
        readonly Vector4 _radii;
        readonly Vector2 _start;
        readonly Vector2 _end;
        readonly float _radius;
        readonly float _feather;
        readonly bool _valid;

        NowMaskShape(
            NowMaskShapeKind kind,
            NowRect rect,
            Vector4 radii,
            Vector2 start,
            Vector2 end,
            float radius,
            float feather,
            bool valid)
        {
            _kind = kind;
            _rect = rect;
            _radii = radii;
            _start = start;
            _end = end;
            _radius = radius;
            _feather = feather;
            _valid = valid;
        }

        /// <summary>The shape's geometric axis-aligned bounds in its authored space.</summary>
        public NowRect bounds => _rect;

        /// <summary>
        /// Additional edge softness in screen pixels beyond the renderer's
        /// derivative anti-aliasing. Zero keeps the default approximately one-pixel edge.
        /// </summary>
        public float feather => _feather;

        /// <summary>Whether this shape has no drawable interior.</summary>
        public bool isEmpty => !_valid || _rect.isEmpty;

        internal NowMaskShapeKind kind => _kind;

        internal NowRect rect => _rect;

        /// <summary>Corner radii packed as top-right, bottom-right, top-left, bottom-left.</summary>
        internal Vector4 radii => _radii;

        internal Vector2 start => _start;

        internal Vector2 end => _end;

        internal float radius => _radius;

        internal bool isValid => _valid;

        internal Vector4 shaderData => _kind switch
        {
            NowMaskShapeKind.RoundedRect => _radii,
            NowMaskShapeKind.Capsule => new Vector4(_start.x, _start.y, _end.x, _end.y),
            _ => default
        };

        /// <summary>Creates an anti-aliased rectangular mask.</summary>
        public static NowMaskShape Rectangle(NowRect rect)
        {
            return TryValidateRect(rect, out rect)
                ? new NowMaskShape(NowMaskShapeKind.Rectangle, rect, default, default, default, 0f, 0f, true)
                : default;
        }

        /// <summary>Creates a rounded-rectangle mask with one radius for every corner.</summary>
        public static NowMaskShape RoundedRect(NowRect rect, float radius)
        {
            return RoundedRect(rect, new Vector4(radius, radius, radius, radius));
        }

        /// <summary>Creates a rounded-rectangle mask with named corner radii.</summary>
        public static NowMaskShape RoundedRect(NowRect rect, NowCornerRadius radius)
        {
            return RoundedRect(rect, radius.packed);
        }

        /// <summary>
        /// Creates a rounded-rectangle mask. The raw vector uses the existing
        /// renderer-packed order: top-right, bottom-right, top-left, bottom-left.
        /// Prefer <see cref="RoundedRect(NowRect, NowCornerRadius)"/> for differing
        /// human-authored corners.
        /// </summary>
        public static NowMaskShape RoundedRect(NowRect rect, Vector4 radius)
        {
            if (!TryValidateRect(rect, out rect))
                return default;

            float maximum = Mathf.Min(rect.width, rect.height) * 0.5f;
            radius = new Vector4(
                ClampRadius(radius.x, maximum),
                ClampRadius(radius.y, maximum),
                ClampRadius(radius.z, maximum),
                ClampRadius(radius.w, maximum));

            return new NowMaskShape(
                NowMaskShapeKind.RoundedRect,
                rect,
                radius,
                default,
                default,
                0f,
                0f,
                true);
        }

        /// <summary>Creates a circular mask.</summary>
        public static NowMaskShape Circle(Vector2 center, float radius)
        {
            if (!IsFinite(center) || !IsFinite(radius) || radius <= 0f)
                return default;

            var rect = new NowRect(center.x - radius, center.y - radius, radius * 2f, radius * 2f);
            if (!TryValidateRect(rect, out rect))
                return default;

            return new NowMaskShape(NowMaskShapeKind.Ellipse, rect, default, default, default, 0f, 0f, true);
        }

        /// <summary>Creates an ellipse inscribed in <paramref name="rect"/>.</summary>
        public static NowMaskShape Ellipse(NowRect rect)
        {
            return TryValidateRect(rect, out rect)
                ? new NowMaskShape(NowMaskShapeKind.Ellipse, rect, default, default, default, 0f, 0f, true)
                : default;
        }

        /// <summary>Creates a capsule around the line segment from <paramref name="start"/> to <paramref name="end"/>.</summary>
        public static NowMaskShape Capsule(Vector2 start, Vector2 end, float radius)
        {
            if (!IsFinite(start) || !IsFinite(end) || !IsFinite(radius) || radius <= 0f)
                return default;

            float minX = Mathf.Min(start.x, end.x) - radius;
            float minY = Mathf.Min(start.y, end.y) - radius;
            float maxX = Mathf.Max(start.x, end.x) + radius;
            float maxY = Mathf.Max(start.y, end.y) + radius;
            var rect = new NowRect(minX, minY, maxX - minX, maxY - minY);
            if (!TryValidateRect(rect, out rect))
                return default;

            return new NowMaskShape(
                NowMaskShapeKind.Capsule,
                rect,
                default,
                start,
                end,
                radius,
                0f,
                true);
        }

        /// <summary>
        /// Creates the largest axis-aligned capsule that fits in <paramref name="rect"/>.
        /// The capsule runs along the rect's longest axis; a square produces a circle.
        /// </summary>
        public static NowMaskShape Capsule(NowRect rect)
        {
            if (!TryValidateRect(rect, out rect) || rect.isEmpty)
                return default;

            Vector2 center = rect.center;
            float radius;
            Vector2 start;
            Vector2 end;

            if (rect.width >= rect.height)
            {
                radius = rect.height * 0.5f;
                start = new Vector2(rect.x + radius, center.y);
                end = new Vector2(rect.xMax - radius, center.y);
            }
            else
            {
                radius = rect.width * 0.5f;
                start = new Vector2(center.x, rect.y + radius);
                end = new Vector2(center.x, rect.yMax - radius);
            }

            return new NowMaskShape(
                NowMaskShapeKind.Capsule,
                rect,
                default,
                start,
                end,
                radius,
                0f,
                true);
        }

        /// <summary>
        /// Returns this shape with additional edge softness in screen pixels beyond
        /// the default derivative anti-aliasing. Negative and non-finite values are
        /// treated as zero.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowMaskShape SetFeather(float screenPixels)
        {
            if (!IsFinite(screenPixels) || screenPixels < 0f)
                screenPixels = 0f;

            return new NowMaskShape(
                _kind,
                _rect,
                _radii,
                _start,
                _end,
                _radius,
                screenPixels,
                _valid);
        }

        internal bool Contains(Vector2 position)
        {
            if (isEmpty || !IsFinite(position))
                return false;

            switch (_kind)
            {
                case NowMaskShapeKind.Rectangle:
                    return ContainsInclusive(_rect, position);

                case NowMaskShapeKind.RoundedRect:
                    return ContainsRoundedRect(position);

                case NowMaskShapeKind.Ellipse:
                {
                    Vector2 halfSize = _rect.size * 0.5f;
                    Vector2 local = position - _rect.center;
                    float x = local.x / halfSize.x;
                    float y = local.y / halfSize.y;
                    return x * x + y * y <= 1f;
                }

                case NowMaskShapeKind.Capsule:
                {
                    Vector2 segment = _end - _start;
                    float lengthSquared = segment.sqrMagnitude;
                    float t = lengthSquared > 0f
                        ? Mathf.Clamp01(Vector2.Dot(position - _start, segment) / lengthSquared)
                        : 0f;
                    Vector2 delta = position - (_start + segment * t);
                    return delta.sqrMagnitude <= _radius * _radius;
                }

                default:
                    return false;
            }
        }

        bool ContainsRoundedRect(Vector2 position)
        {
            Vector2 halfSize = _rect.size * 0.5f;
            Vector2 local = position - _rect.center;
            float radius;

            if (local.x < 0f)
                radius = local.y < 0f ? _radii.z : _radii.w;
            else
                radius = local.y < 0f ? _radii.x : _radii.y;

            Vector2 q = new Vector2(Mathf.Abs(local.x), Mathf.Abs(local.y)) - halfSize + new Vector2(radius, radius);
            Vector2 outside = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f));
            float signedDistance = outside.magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - radius;
            return signedDistance <= 0f;
        }

        static bool ContainsInclusive(NowRect rect, Vector2 position)
        {
            return position.x >= rect.x && position.x <= rect.xMax &&
                   position.y >= rect.y && position.y <= rect.yMax;
        }

        static float ClampRadius(float radius, float maximum)
        {
            return IsFinite(radius) ? Mathf.Clamp(radius, 0f, maximum) : 0f;
        }

        static bool TryValidateRect(NowRect rect, out NowRect validated)
        {
            if (!IsFinite(rect.x) || !IsFinite(rect.y) ||
                !IsFinite(rect.width) || !IsFinite(rect.height) ||
                !IsFinite(rect.xMax) || !IsFinite(rect.yMax) ||
                rect.width < 0f || rect.height < 0f)
            {
                validated = default;
                return false;
            }

            validated = rect;
            return true;
        }

        static bool IsFinite(Vector2 value)
        {
            return IsFinite(value.x) && IsFinite(value.y);
        }

        static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        public bool Equals(NowMaskShape other)
        {
            return _kind == other._kind &&
                   _rect == other._rect &&
                   _radii.Equals(other._radii) &&
                   _start.Equals(other._start) &&
                   _end.Equals(other._end) &&
                   _radius == other._radius &&
                   _feather == other._feather &&
                   _valid == other._valid;
        }

        public override bool Equals(object obj)
        {
            return obj is NowMaskShape other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)_kind;
                hash = (hash * 397) ^ _rect.GetHashCode();
                hash = (hash * 397) ^ _radii.GetHashCode();
                hash = (hash * 397) ^ _start.GetHashCode();
                hash = (hash * 397) ^ _end.GetHashCode();
                hash = (hash * 397) ^ _radius.GetHashCode();
                hash = (hash * 397) ^ _feather.GetHashCode();
                hash = (hash * 397) ^ _valid.GetHashCode();
                return hash;
            }
        }

        public static bool operator ==(NowMaskShape left, NowMaskShape right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(NowMaskShape left, NowMaskShape right)
        {
            return !left.Equals(right);
        }
    }
}
