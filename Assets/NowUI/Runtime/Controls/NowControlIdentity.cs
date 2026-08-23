namespace NowUI
{
    /// <summary>
    /// Opaque identity for a captured source call site. This is deliberately a
    /// distinct type so a call-site token cannot be reused accidentally as an
    /// authored integer <see cref="NowId"/> (or vice versa).
    /// </summary>
    public readonly struct NowCallSiteId : System.IEquatable<NowCallSiteId>
    {
        readonly int _token;

        internal NowCallSiteId(int token)
        {
            if (token == 0)
                throw new System.ArgumentOutOfRangeException(
                    nameof(token),
                    "Call-site token 0 is reserved.");

            _token = token;
        }

        /// <summary>True for a token returned by <see cref="NowControls.SiteId"/>.</summary>
        public bool hasValue => _token != 0;

        internal int token
        {
            get
            {
                if (_token == 0)
                    throw new System.InvalidOperationException(
                        "A non-empty NowCallSiteId is required.");

                return _token;
            }
        }

        public bool Equals(NowCallSiteId other) => _token == other._token;

        public override bool Equals(object obj) =>
            obj is NowCallSiteId other && Equals(other);

        public override int GetHashCode() => _token;

        public static bool operator ==(NowCallSiteId left, NowCallSiteId right) =>
            left.Equals(right);

        public static bool operator !=(NowCallSiteId left, NowCallSiteId right) =>
            !left.Equals(right);
    }

    /// <summary>
    /// Builder-friendly discriminated identity: either an authored local
    /// <see cref="NowId"/> or an already-resolved <see cref="NowResolvedId"/>.
    /// Custom control builders can store this type and expose both SetId
    /// overloads without risking a second scope/domain resolution.
    /// </summary>
    public readonly struct NowControlIdentity
    {
        readonly NowId _authored;

        readonly NowResolvedId _resolved;

        readonly bool _isResolved;

        public NowControlIdentity(NowId authored)
        {
            _authored = authored;
            _resolved = NowResolvedId.None;
            _isResolved = false;
        }

        public NowControlIdentity(NowResolvedId resolved)
        {
            if (!resolved.hasValue)
                throw new System.ArgumentException(
                    "A resolved control identity cannot be empty.",
                    nameof(resolved));

            _authored = NowId.None;
            _resolved = resolved;
            _isResolved = true;
        }

        public bool hasValue => _isResolved ? _resolved.hasValue : _authored.hasValue;

        public bool isResolved => _isResolved;

        public NowId authored => _authored;

        public NowResolvedId resolved => _resolved;

        public NowResolvedId Resolve(NowCallSiteId fallbackIdentity)
        {
            return _isResolved
                ? _resolved
                : NowControls.GetControlId(_authored, fallbackIdentity);
        }

        internal NowResolvedId Resolve(int fallbackIdentity)
        {
            return _isResolved
                ? _resolved
                : NowControls.GetControlId(_authored, fallbackIdentity);
        }

        public static implicit operator NowControlIdentity(NowId authored)
        {
            return new NowControlIdentity(authored);
        }

        public static implicit operator NowControlIdentity(NowResolvedId resolved)
        {
            return new NowControlIdentity(resolved);
        }
    }
}
