using System;
using UnityEngine;

namespace NowUI
{
    /// <summary>
    /// NowUI's rectangle currency: screen-space, top-left origin, in pixels.
    /// Converts implicitly to and from <see cref="Vector4"/> (x, y, z=width,
    /// w=height) and <see cref="UnityEngine.Rect"/>, so it composes with both
    /// existing NowUI code and Unity APIs.
    /// </summary>
    [Serializable]
    public struct NowRect : IEquatable<NowRect>
    {
        public float x;

        public float y;

        public float width;

        public float height;

        public NowRect(float x, float y, float width, float height)
        {
            this.x = x;
            this.y = y;
            this.width = width;
            this.height = height;
        }

        public NowRect(Vector2 position, Vector2 size)
        {
            x = position.x;
            y = position.y;
            width = size.x;
            height = size.y;
        }

        public readonly float xMax => x + width;

        public readonly float yMax => y + height;

        public Vector2 position => new Vector2(x, y);

        public Vector2 size => new Vector2(width, height);

        public Vector2 center => new Vector2(x + width * 0.5f, y + height * 0.5f);

        public bool isEmpty => width <= 0f || height <= 0f;

        public readonly bool Contains(Vector2 point)
        {
            return point.x >= x && point.x < xMax && point.y >= y && point.y < yMax;
        }

        public readonly bool Overlaps(NowRect other)
        {
            return other.x < xMax && other.xMax > x && other.y < yMax && other.yMax > y;
        }

        /// <summary>Shrinks the rect inward by the given amount on every edge.</summary>
        public readonly NowRect Inset(float all)
        {
            return Inset(all, all, all, all);
        }

        public readonly NowRect Inset(float horizontal, float vertical)
        {
            return Inset(horizontal, vertical, horizontal, vertical);
        }

        public readonly NowRect Inset(float left, float top, float right, float bottom)
        {
            return new NowRect(x + left, y + top, width - left - right, height - top - bottom);
        }

        /// <summary>Grows the rect outward by the given amount on every edge.</summary>
        public readonly NowRect Outset(float all)
        {
            return Inset(-all, -all, -all, -all);
        }

        public readonly NowRect Outset(float horizontal, float vertical)
        {
            return Inset(-horizontal, -vertical, -horizontal, -vertical);
        }

        public readonly NowRect Outset(float left, float top, float right, float bottom)
        {
            return Inset(-left, -top, -right, -bottom);
        }

        /// <summary>Smallest rect containing both this rect and <paramref name="other"/>.</summary>
        public readonly NowRect Union(NowRect other)
        {
            float minX = Mathf.Min(x, other.x);
            float minY = Mathf.Min(y, other.y);
            float maxX = Mathf.Max(xMax, other.xMax);
            float maxY = Mathf.Max(yMax, other.yMax);
            return new NowRect(minX, minY, maxX - minX, maxY - minY);
        }

        /// <summary>Overlapping region of this rect and <paramref name="other"/>; empty when they do not overlap.</summary>
        public readonly NowRect Intersect(NowRect other)
        {
            float minX = Mathf.Max(x, other.x);
            float minY = Mathf.Max(y, other.y);
            float maxX = Mathf.Min(xMax, other.xMax);
            float maxY = Mathf.Min(yMax, other.yMax);
            return new NowRect(minX, minY, Mathf.Max(0f, maxX - minX), Mathf.Max(0f, maxY - minY));
        }

        /// <summary>The rect translated by the given offset.</summary>
        public readonly NowRect Offset(Vector2 delta)
        {
            return new NowRect(x + delta.x, y + delta.y, width, height);
        }

        public readonly NowRect Offset(float dx, float dy)
        {
            return new NowRect(x + dx, y + dy, width, height);
        }

        public static implicit operator Vector4(NowRect rect)
        {
            return new Vector4(rect.x, rect.y, rect.width, rect.height);
        }

        public static implicit operator NowRect(Vector4 value)
        {
            return new NowRect(value.x, value.y, value.z, value.w);
        }

        public static implicit operator Rect(NowRect rect)
        {
            return new Rect(rect.x, rect.y, rect.width, rect.height);
        }

        public static implicit operator NowRect(Rect rect)
        {
            return new NowRect(rect.x, rect.y, rect.width, rect.height);
        }

        public static bool operator ==(NowRect a, NowRect b)
        {
            return a.x == b.x && a.y == b.y && a.width == b.width && a.height == b.height;
        }

        public static bool operator !=(NowRect a, NowRect b)
        {
            return !(a == b);
        }

        public bool Equals(NowRect other)
        {
            return this == other;
        }

        public override bool Equals(object obj)
        {
            return obj is NowRect other && this == other;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = x.GetHashCode();
                hash = (hash * 397) ^ y.GetHashCode();
                hash = (hash * 397) ^ width.GetHashCode();
                hash = (hash * 397) ^ height.GetHashCode();
                return hash;
            }
        }

        public override string ToString()
        {
            return $"(x:{x:0.##}, y:{y:0.##}, w:{width:0.##}, h:{height:0.##})";
        }
    }
}
