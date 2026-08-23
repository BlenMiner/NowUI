using System;
using System.Globalization;

namespace NowUI
{
    /// <summary>
    /// A fully resolved, runtime-local NowUI identity. Unlike <see cref="NowId"/>,
    /// this value already contains its owner, domain and complete path ancestry and
    /// must not be resolved against an ambient scope again.
    /// </summary>
    public readonly struct NowResolvedId : IEquatable<NowResolvedId>
    {
        readonly ulong _value;

        internal NowResolvedId(ulong value)
        {
            if (value == 0UL)
                throw new ArgumentException("Resolved id 0 is reserved.", nameof(value));

            _value = value;
        }

        /// <summary>The empty identity, used only as a sentinel for "no control".</summary>
        public static NowResolvedId None => default;

        /// <summary>True when this value identifies a resolved runtime object.</summary>
        public bool hasValue => _value != 0UL;

        /// <summary>
        /// Derives a custom-control child path from this resolved identity. The
        /// child remains beneath the same owner and cannot cancel existing path
        /// ancestry. A default authored id is not a valid child segment.
        /// </summary>
        public NowResolvedId Child(NowId child)
        {
            return NowIdHash.Derive(this, NowIdDomain.ExtensionPath, child);
        }

        /// <summary>Derives a string child path from this resolved identity.</summary>
        public NowResolvedId Child(string child)
        {
            if (child == null)
                throw new ArgumentNullException(nameof(child));

            return Child(new NowId(child));
        }

        /// <summary>Derives an integer child path from this resolved identity.</summary>
        public NowResolvedId Child(int child)
        {
            return Child(new NowId(child));
        }

        internal ulong value => _value;

        internal static NowResolvedId CreateOwnerRoot(ulong ownerNonce)
        {
            return NowIdHash.CreateOwnerRoot(ownerNonce);
        }

        internal static NowResolvedId FromLegacy(int value)
        {
            return NowIdHash.FromLegacy(value);
        }

        internal NowResolvedId Derive(NowIdDomain domain, NowId segment)
        {
            return NowIdHash.Derive(this, domain, segment);
        }

        internal NowResolvedId Derive(NowIdDomain domain, string segment)
        {
            return NowIdHash.Derive(this, domain, segment);
        }

        internal NowResolvedId Derive(NowIdDomain domain, int segment)
        {
            return NowIdHash.Derive(this, domain, segment);
        }

        internal NowResolvedId InDomain(NowIdDomain domain)
        {
            return NowIdHash.EnterDomain(this, domain);
        }

        public bool Equals(NowResolvedId other)
        {
            return _value == other._value;
        }

        public override bool Equals(object obj)
        {
            return obj is NowResolvedId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return unchecked((int)_value ^ (int)(_value >> 32));
        }

        public override string ToString()
        {
            return _value.ToString("X16", CultureInfo.InvariantCulture);
        }

        public static bool operator ==(NowResolvedId left, NowResolvedId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(NowResolvedId left, NowResolvedId right)
        {
            return !left.Equals(right);
        }
    }
}
