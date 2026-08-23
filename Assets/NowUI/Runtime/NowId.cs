using System;

namespace NowUI
{
    /// <summary>
    /// Authored local identity for controls, layout caches, effects and retained
    /// extension state. Default means "use the call site"; strings and integers
    /// resolve exactly once within the active host and id scope.
    ///
    /// Explicit ids are STABLE: resolving the same id any number of times, from
    /// any pass or code path, yields the same control id — that is what makes
    /// them cross-referenceable (focus a control from a shortcut handler,
    /// pre-claim its presses, read its state from outside its draw). Both string
    /// and integer ids are local to their active id scope, so reusable hosts and
    /// panels cannot silently share state. Fully resolved runtime identity is a
    /// separate <see cref="NowResolvedId"/> type and cannot be smuggled through
    /// this authored-key type. Only call-site (default) identity is occurrence-
    /// salted for loops.
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
        {
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
            return NowIdHash.AuthoredHashCode(this);
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
