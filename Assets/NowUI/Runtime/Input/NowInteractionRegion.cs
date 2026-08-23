using System;
using UnityEngine;

namespace NowUI
{
    /// <summary>
    /// A local-space interaction rectangle with up to four local-space holes.
    /// Composite controls use exclusions to reserve child-control rectangles so
    /// a parent row cannot hover, press, or click through its children. All
    /// storage is inline; building and testing a region allocates no collections.
    /// </summary>
    public struct NowInteractionRegion
    {
        public const int MaxExclusions = 4;

        NowRect _bounds;
        NowRect _exclusion0;
        NowRect _exclusion1;
        NowRect _exclusion2;
        NowRect _exclusion3;
        byte _exclusionCount;

        public NowInteractionRegion(NowRect bounds)
        {
            _bounds = bounds;
            _exclusion0 = default;
            _exclusion1 = default;
            _exclusion2 = default;
            _exclusion3 = default;
            _exclusionCount = 0;
        }

        public NowInteractionRegion(Rect bounds)
            : this((NowRect)bounds)
        {
        }

        public readonly NowRect bounds => _bounds;

        public readonly int exclusionCount => _exclusionCount;

        public static NowInteractionRegion From(NowRect bounds)
        {
            return new NowInteractionRegion(bounds);
        }

        public static NowInteractionRegion From(Rect bounds)
        {
            return new NowInteractionRegion(bounds);
        }

        /// <summary>
        /// Adds a local-space child rectangle to the holes in this region and
        /// returns the updated value for fluent construction. Empty rectangles
        /// are ignored.
        /// </summary>
        public NowInteractionRegion Exclude(NowRect exclusion)
        {
            if (exclusion.isEmpty)
                return this;

            switch (_exclusionCount)
            {
                case 0:
                    _exclusion0 = exclusion;
                    break;
                case 1:
                    _exclusion1 = exclusion;
                    break;
                case 2:
                    _exclusion2 = exclusion;
                    break;
                case 3:
                    _exclusion3 = exclusion;
                    break;
                default:
                    throw new InvalidOperationException(
                        $"An interaction region supports at most {MaxExclusions} exclusions.");
            }

            ++_exclusionCount;
            return this;
        }

        public NowInteractionRegion Exclude(Rect exclusion)
        {
            return Exclude((NowRect)exclusion);
        }

        /// <summary>
        /// True when a local-space point is inside the outer bounds and outside
        /// every child exclusion.
        /// </summary>
        public readonly bool Contains(Vector2 localPosition)
        {
            return _bounds.Contains(localPosition) && !IsExcluded(localPosition);
        }

        internal readonly bool IsExcluded(Vector2 localPosition)
        {
            if (_exclusionCount > 0 && _exclusion0.Contains(localPosition))
                return true;

            if (_exclusionCount > 1 && _exclusion1.Contains(localPosition))
                return true;

            if (_exclusionCount > 2 && _exclusion2.Contains(localPosition))
                return true;

            return _exclusionCount > 3 && _exclusion3.Contains(localPosition);
        }
    }
}
