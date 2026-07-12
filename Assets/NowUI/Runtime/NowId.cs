using System;

namespace NowUI
{
    /// <summary>
    /// Stable explicit identity for controls, layout caches, effects and retained
    /// extension state. Default means "use the call site"; strings are hashed with
    /// NowUI's string id function, and integer ids avoid per-frame string work.
    ///
    /// Explicit ids are STABLE: resolving the same id any number of times, from
    /// any pass or code path, yields the same control id — that is what makes
    /// them cross-referenceable (focus a control from a shortcut handler,
    /// pre-claim its presses, read its state from outside its draw). An integer
    /// id is used verbatim — mint one with <see cref="NowInput.CombineId"/> from
    /// a parent control id — while a string id resolves within the active
    /// <see cref="NowControls.IdScope(string)"/>. Only call-site (default)
    /// identity is occurrence-salted for loops.
    /// </summary>
    public readonly struct NowId : IEquatable<NowId>
    {
        readonly string _stringValue;
        readonly int _intValue;
        readonly byte _kind;

        const byte NoneKind = 0;
        const byte StringKind = 1;
        const byte IntKind = 2;

        public static NowId None => default;

        public bool hasValue => _kind != NoneKind;

        public bool isString => _kind == StringKind;

        public bool isInt => _kind == IntKind;

        public string stringValue => _kind == StringKind ? _stringValue : null;

        public int intValue => _kind == IntKind ? _intValue : 0;

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
        {
            if (value == 0)
                throw new ArgumentException("Control id 0 is reserved.", nameof(value));

            _stringValue = null;
            _intValue = value;
            _kind = IntKind;
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
                IntKind => _intValue,
                _ => NowControls.GetControlId(site)
            };
        }

        /// <summary>
        /// Stable resolution for layout groups, caches and input cross-references.
        /// Follows the same contract as <see cref="ResolveControlId(int)"/>: ints
        /// verbatim, strings seeded by the active
        /// <see cref="NowControls.IdScope(string)"/> — so the same string names
        /// the same thing everywhere under one scope, and reusable panels under
        /// different scopes never collide. Use int ids for identities that must
        /// resolve identically from outside any scope.
        /// </summary>
        internal int ResolveStableId(int fallback)
        {
            return _kind switch
            {
                StringKind => NowControls.GetControlId(_stringValue),
                IntKind => _intValue,
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
