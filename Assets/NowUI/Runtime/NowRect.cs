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

        /// <summary>A rect of <paramref name="size"/> centered on <paramref name="center"/>.</summary>
        public static NowRect FromCenter(Vector2 center, Vector2 size)
        {
            return new NowRect(center.x - size.x * 0.5f, center.y - size.y * 0.5f, size.x, size.y);
        }

        /// <summary>A rect of the given size centered on <paramref name="center"/>.</summary>
        public static NowRect FromCenter(Vector2 center, float width, float height)
        {
            return FromCenter(center, new Vector2(width, height));
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

        public readonly Vector2 position => new Vector2(x, y);

        public readonly Vector2 size => new Vector2(width, height);

        public readonly Vector2 center => new Vector2(x + width * 0.5f, y + height * 0.5f);

        public readonly bool isEmpty => width <= 0f || height <= 0f;

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

        /// <summary>Creates a rect of the requested size centered inside this rect.</summary>
        public readonly NowRect Centered(float width, float height)
        {
            return Align(width, height, NowLayoutAlign.Center, NowLayoutAlign.Center);
        }

        /// <summary>Creates a rect of the requested size centered inside this rect.</summary>
        public readonly NowRect Centered(Vector2 size)
        {
            return Centered(size.x, size.y);
        }

        /// <summary>
        /// Creates a rect of the requested size aligned inside this rect. A child may
        /// be larger than its container; center and end alignment then place the
        /// overflow symmetrically or toward the start edge, respectively.
        /// </summary>
        public readonly NowRect Align(
            float width,
            float height,
            NowLayoutAlign horizontal,
            NowLayoutAlign vertical)
        {
            RequireNonNegativeFinite(width, nameof(width));
            RequireNonNegativeFinite(height, nameof(height));
            RequireAlign(horizontal, nameof(horizontal));
            RequireAlign(vertical, nameof(vertical));

            return new NowRect(
                AlignedPosition(x, this.width, width, horizontal),
                AlignedPosition(y, this.height, height, vertical),
                width,
                height);
        }

        /// <summary>Creates a rect of the requested size aligned inside this rect.</summary>
        public readonly NowRect Align(
            Vector2 size,
            NowLayoutAlign horizontal,
            NowLayoutAlign vertical)
        {
            return Align(size.x, size.y, horizontal, vertical);
        }

        /// <summary>
        /// Returns a slice from the top edge. The requested height is clamped to this
        /// rect, so the returned slice never extends beyond it.
        /// </summary>
        public readonly NowRect TakeTop(float height)
        {
            return TakeTop(height, out _);
        }

        /// <summary>
        /// Returns a slice from the top edge and the part below it. The requested
        /// height is clamped to this rect, so neither result has a negative height.
        /// The remainder may safely be assigned back to the receiver variable:
        /// <c>var top = rect.TakeTop(height, out rect)</c>.
        /// </summary>
        public readonly NowRect TakeTop(float height, out NowRect remainder)
        {
            RequireNonNegativeFinite(height, nameof(height));
            float available = Mathf.Max(0f, this.height);
            float taken = Mathf.Min(height, available);
            var slice = new NowRect(x, y, width, taken);
            remainder = new NowRect(x, y + taken, width, available - taken);
            return slice;
        }

        /// <summary>
        /// Returns a slice from the top edge and the part below it, skipping
        /// <paramref name="gapAfter"/> between them: the usual step of a vertical
        /// stack, <c>var title = rest.TakeTop(28f, 8f, out rest)</c>.
        /// </summary>
        public readonly NowRect TakeTop(float height, float gapAfter, out NowRect remainder)
        {
            var slice = TakeTop(height, out remainder);
            remainder.TakeTop(Mathf.Max(0f, gapAfter), out remainder);
            return slice;
        }

        /// <summary>
        /// Returns a slice from the bottom edge. The requested height is clamped to
        /// this rect, so the returned slice never extends beyond it.
        /// </summary>
        public readonly NowRect TakeBottom(float height)
        {
            return TakeBottom(height, out _);
        }

        /// <summary>
        /// Returns a slice from the bottom edge and the part above it. The requested
        /// height is clamped to this rect, so neither result has a negative height.
        /// </summary>
        public readonly NowRect TakeBottom(float height, out NowRect remainder)
        {
            RequireNonNegativeFinite(height, nameof(height));
            float available = Mathf.Max(0f, this.height);
            float taken = Mathf.Min(height, available);
            float remaining = available - taken;
            var slice = new NowRect(x, y + remaining, width, taken);
            remainder = new NowRect(x, y, width, remaining);
            return slice;
        }

        /// <summary>Returns a slice from the bottom edge and the part above it, skipping <paramref name="gapAfter"/> between them.</summary>
        public readonly NowRect TakeBottom(float height, float gapAfter, out NowRect remainder)
        {
            var slice = TakeBottom(height, out remainder);
            remainder.TakeBottom(Mathf.Max(0f, gapAfter), out remainder);
            return slice;
        }

        /// <summary>
        /// Returns a slice from the left edge. The requested width is clamped to this
        /// rect, so the returned slice never extends beyond it.
        /// </summary>
        public readonly NowRect TakeLeft(float width)
        {
            return TakeLeft(width, out _);
        }

        /// <summary>
        /// Returns a slice from the left edge and the part to its right. The requested
        /// width is clamped to this rect, so neither result has a negative width.
        /// </summary>
        public readonly NowRect TakeLeft(float width, out NowRect remainder)
        {
            RequireNonNegativeFinite(width, nameof(width));
            float available = Mathf.Max(0f, this.width);
            float taken = Mathf.Min(width, available);
            var slice = new NowRect(x, y, taken, height);
            remainder = new NowRect(x + taken, y, available - taken, height);
            return slice;
        }

        /// <summary>Returns a slice from the left edge and the part to its right, skipping <paramref name="gapAfter"/> between them.</summary>
        public readonly NowRect TakeLeft(float width, float gapAfter, out NowRect remainder)
        {
            var slice = TakeLeft(width, out remainder);
            remainder.TakeLeft(Mathf.Max(0f, gapAfter), out remainder);
            return slice;
        }

        /// <summary>
        /// Returns a slice from the right edge. The requested width is clamped to this
        /// rect, so the returned slice never extends beyond it.
        /// </summary>
        public readonly NowRect TakeRight(float width)
        {
            return TakeRight(width, out _);
        }

        /// <summary>
        /// Returns a slice from the right edge and the part to its left. The requested
        /// width is clamped to this rect, so neither result has a negative width.
        /// </summary>
        public readonly NowRect TakeRight(float width, out NowRect remainder)
        {
            RequireNonNegativeFinite(width, nameof(width));
            float available = Mathf.Max(0f, this.width);
            float taken = Mathf.Min(width, available);
            float remaining = available - taken;
            var slice = new NowRect(x + remaining, y, taken, height);
            remainder = new NowRect(x, y, remaining, height);
            return slice;
        }

        /// <summary>Returns a slice from the right edge and the part to its left, skipping <paramref name="gapAfter"/> between them.</summary>
        public readonly NowRect TakeRight(float width, float gapAfter, out NowRect remainder)
        {
            var slice = TakeRight(width, out remainder);
            remainder.TakeRight(Mathf.Max(0f, gapAfter), out remainder);
            return slice;
        }

        /// <summary>
        /// Fills <paramref name="columns"/> with equal-width columns across this rect,
        /// separated by <paramref name="gap"/>:
        /// <c>Span&lt;NowRect&gt; cards = stackalloc NowRect[3]; content.SplitColumns(cards, 16f);</c>
        /// </summary>
        public readonly void SplitColumns(Span<NowRect> columns, float gap = 0f)
        {
            Split(columns, default, gap, horizontal: true);
        }

        /// <summary>Fills <paramref name="columns"/> with columns sized by <paramref name="weights"/> (one per column).</summary>
        public readonly void SplitColumns(Span<NowRect> columns, ReadOnlySpan<float> weights, float gap = 0f)
        {
            RequireWeights(columns.Length, weights);
            Split(columns, weights, gap, horizontal: true);
        }

        /// <summary>Fills <paramref name="rows"/> with equal-height rows down this rect, separated by <paramref name="gap"/>.</summary>
        public readonly void SplitRows(Span<NowRect> rows, float gap = 0f)
        {
            Split(rows, default, gap, horizontal: false);
        }

        /// <summary>Fills <paramref name="rows"/> with rows sized by <paramref name="weights"/> (one per row).</summary>
        public readonly void SplitRows(Span<NowRect> rows, ReadOnlySpan<float> weights, float gap = 0f)
        {
            RequireWeights(rows.Length, weights);
            Split(rows, weights, gap, horizontal: false);
        }

        static void RequireWeights(int count, ReadOnlySpan<float> weights)
        {
            if (weights.Length != count)
                throw new ArgumentException("Pass one weight per slot.", nameof(weights));

            for (int i = 0; i < weights.Length; ++i)
            {
                if (weights[i] < 0f || float.IsNaN(weights[i]) || float.IsInfinity(weights[i]))
                    throw new ArgumentOutOfRangeException(nameof(weights), "Weights must be non-negative finite values.");
            }
        }

        readonly void Split(Span<NowRect> slots, ReadOnlySpan<float> weights, float gap, bool horizontal)
        {
            int count = slots.Length;

            if (count == 0)
                return;

            gap = Mathf.Max(0f, gap);
            float extent = Mathf.Max(0f, horizontal ? width : height);
            float free = Mathf.Max(0f, extent - gap * (count - 1));
            float totalWeight = 0f;

            if (!weights.IsEmpty)
            {
                for (int i = 0; i < count; ++i)
                    totalWeight += weights[i];
            }

            float cursor = horizontal ? x : y;

            for (int i = 0; i < count; ++i)
            {
                float size = weights.IsEmpty
                    ? free / count
                    : totalWeight > 0f ? free * weights[i] / totalWeight : 0f;

                slots[i] = horizontal
                    ? new NowRect(cursor, y, size, height)
                    : new NowRect(x, cursor, width, size);
                cursor += size + gap;
            }
        }

        static float AlignedPosition(float start, float available, float requested, NowLayoutAlign align)
        {
            return align switch
            {
                NowLayoutAlign.Start => start,
                NowLayoutAlign.Center => start + (available - requested) * 0.5f,
                NowLayoutAlign.End => start + available - requested,
                _ => start
            };
        }

        static void RequireNonNegativeFinite(float value, string paramName)
        {
            if (value < 0f || float.IsNaN(value) || float.IsInfinity(value))
                throw new ArgumentOutOfRangeException(paramName, "Rectangle sizes must be non-negative finite values.");
        }

        static void RequireAlign(NowLayoutAlign align, string paramName)
        {
            if ((uint)align > (uint)NowLayoutAlign.End)
                throw new ArgumentOutOfRangeException(paramName, align, "Unknown rectangle alignment.");
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
