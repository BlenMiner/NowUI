using System;

namespace NowUI
{
    /// <summary>
    /// Stable explicit identity for controls, layout caches, effects and retained
    /// extension state. Default means "use the call site"; strings are hashed with
    /// NowUI's string id function, and integer ids avoid per-frame string work.
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
            return _kind switch
            {
                StringKind => NowControls.GetControlId(_stringValue),
                IntKind => NowControls.GetControlId(_intValue),
                _ => NowControls.GetControlId(site)
            };
        }

        internal int ResolveStableId(int fallback)
        {
            return _kind switch
            {
                StringKind => NowInput.GetId(_stringValue),
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
