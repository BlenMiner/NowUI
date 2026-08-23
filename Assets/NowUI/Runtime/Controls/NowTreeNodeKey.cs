using System;

namespace NowUI
{
    /// <summary>
    /// Host-independent semantic identity for a tree data node. Tree state stores
    /// these keys, while each draw derives a separate host-owned control id for
    /// interaction and focus. Keys are deterministic but opaque; persist your own
    /// domain key and reconstruct the path instead of serializing this runtime type.
    /// </summary>
    public readonly struct NowTreeNodeKey : IEquatable<NowTreeNodeKey>
    {
        const ulong TreeSemanticRootNonce = 0x4E6F775472656532UL;

        static readonly NowResolvedId RootId =
            NowResolvedId.CreateOwnerRoot(TreeSemanticRootNonce)
                .InDomain(NowIdDomain.Scope);

        readonly NowResolvedId _path;

        NowTreeNodeKey(NowResolvedId path)
        {
            _path = path;
        }

        public static NowTreeNodeKey None => default;

        public bool hasValue => _path.hasValue;

        /// <summary>Creates a root-level semantic node key.</summary>
        public static NowTreeNodeKey From(NowId key)
        {
            if (!key.hasValue)
                throw new ArgumentException("A tree node key cannot be empty.", nameof(key));

            return new NowTreeNodeKey(RootId.Derive(NowIdDomain.Scope, key));
        }

        public static NowTreeNodeKey From(string key) => From(new NowId(key));

        public static NowTreeNodeKey From(int key) => From(new NowId(key));

        /// <summary>Derives a semantic child beneath this node.</summary>
        public NowTreeNodeKey Child(NowId key)
        {
            if (!hasValue)
                throw new InvalidOperationException("A semantic child requires a non-empty parent key.");

            if (!key.hasValue)
                throw new ArgumentException("A tree node key cannot be empty.", nameof(key));

            return new NowTreeNodeKey(_path.Derive(NowIdDomain.Scope, key));
        }

        public NowTreeNodeKey Child(string key) => Child(new NowId(key));

        public NowTreeNodeKey Child(int key) => Child(new NowId(key));

        internal static NowTreeNodeKey Root => new NowTreeNodeKey(RootId);

        internal NowTreeNodeKey PositionalChild(int position)
        {
            if (!hasValue)
                throw new InvalidOperationException("A positional child requires a non-empty parent key.");

            return new NowTreeNodeKey(_path.Derive(NowIdDomain.Occurrence, position));
        }

        public bool Equals(NowTreeNodeKey other) => _path == other._path;

        public override bool Equals(object obj) => obj is NowTreeNodeKey other && Equals(other);

        public override int GetHashCode() => _path.GetHashCode();

        public override string ToString() => _path.ToString();

        public static bool operator ==(NowTreeNodeKey left, NowTreeNodeKey right) => left.Equals(right);

        public static bool operator !=(NowTreeNodeKey left, NowTreeNodeKey right) => !left.Equals(right);
    }
}
