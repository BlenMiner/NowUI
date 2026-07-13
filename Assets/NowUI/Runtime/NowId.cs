using System;

namespace NowUI
{
    /// <summary>
    /// Stable explicit identity for controls, layout caches, effects and retained
    /// extension state. Default means "use the call site"; strings and integers
    /// resolve within the active host/<see cref="NowControls.IdScope(string)"/>,
    /// while integer ids avoid per-frame string work.
    ///
    /// Explicit ids are STABLE: resolving the same id any number of times, from
    /// any pass or code path, yields the same control id — that is what makes
    /// them cross-referenceable (focus a control from a shortcut handler,
    /// pre-claim its presses, read its state from outside its draw). Both string
    /// and integer ids are local to their active id scope, so reusable hosts and
    /// panels cannot silently share state. Use <see cref="Resolved(int)"/> only
    /// for an id that has already been fully resolved, such as a value returned
    /// by a host's ResolveControlId method or <see cref="NowInput.CombineId"/>.
    /// Only call-site (default) identity is occurrence-salted for loops.
    /// </summary>
    public readonly struct NowId : IEquatable<NowId>
    {
        readonly string _stringValue;
        readonly int _intValue;
        readonly byte _kind;

        const byte NoneKind = 0;
        const byte StringKind = 1;
        const byte IntKind = 2;
        const byte ResolvedIntKind = 3;

        public static NowId None => default;

        public bool hasValue => _kind != NoneKind;

        public bool isString => _kind == StringKind;

        public bool isInt => _kind == IntKind || _kind == ResolvedIntKind;

        /// <summary>True when this integer already contains its complete scope ancestry.</summary>
        public bool isResolved => _kind == ResolvedIntKind;

        public string stringValue => _kind == StringKind ? _stringValue : null;

        public int intValue => isInt ? _intValue : 0;

        public NowId(string value)
        {
            if (value == null)
            {
                _stringValue = null;
                _intValue = 0;
                _kind = NoneKind;
                return;
            }

            if (value.Length == 0)
                throw new ArgumentException("Control id strings cannot be empty.", nameof(value));

            _stringValue = value;
            _intValue = 0;
            _kind = StringKind;
        }

        public NowId(int value)
            : this(value, IntKind)
        {
        }

        NowId(int value, byte kind)
        {
            if (value == 0)
                throw new ArgumentException("Control id 0 is reserved.", nameof(value));

            _stringValue = null;
            _intValue = value;
            _kind = kind;
        }

        /// <summary>
        /// Wraps an integer that already contains its complete host and nested
        /// scope ancestry. Ordinary integer ids should use <c>new NowId(value)</c>
        /// (or the implicit conversion) so they remain local to the active host.
        /// </summary>
        public static NowId Resolved(int value)
        {
            return new NowId(value, ResolvedIntKind);
        }

        public static implicit operator NowId(string value)
        {
            return new NowId(value);
        }

        public static implicit operator NowId(int value)
        {
            return new NowId(value);
        }

        internal int ResolveControlId(int site)
        {
            // Explicit ids resolve deterministically (see the type summary);
            // only the call-site fallback goes through occurrence salting.
            return _kind switch
            {
                StringKind => NowControls.GetControlId(_stringValue),
                IntKind => NowControls.ResolveScopedControlId(_intValue),
                ResolvedIntKind => _intValue,
                _ => NowControls.GetControlId(site)
            };
        }

        /// <summary>
        /// Stable resolution for layout groups, caches and input cross-references.
        /// Follows the same contract as <see cref="ResolveControlId(int)"/>:
        /// ordinary strings and integers are seeded by the active
        /// <see cref="NowControls.IdScope(string)"/>, while
        /// <see cref="Resolved(int)"/> values pass through unchanged.
        /// </summary>
        internal int ResolveStableId(int fallback)
        {
            return _kind switch
            {
                StringKind => NowControls.GetControlId(_stringValue),
                IntKind => NowControls.ResolveScopedControlId(_intValue),
                ResolvedIntKind => _intValue,
                _ => fallback != 0 ? fallback : 1
            };
        }

        public bool Equals(NowId other)
        {
            return _kind == other._kind &&
                _intValue == other._intValue &&
                string.Equals(_stringValue, other._stringValue, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is NowId other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = _kind;
                hash = (hash * 397) ^ _intValue;
                hash = (hash * 397) ^ (_stringValue != null ? _stringValue.GetHashCode() : 0);
                return hash;
            }
        }

        public override string ToString()
        {
            return _kind switch
            {
                StringKind => _stringValue,
                IntKind => _intValue.ToString(),
                ResolvedIntKind => $"Resolved({_intValue})",
                _ => string.Empty
            };
        }

        public static bool operator ==(NowId left, NowId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(NowId left, NowId right)
        {
            return !left.Equals(right);
        }
    }
}
