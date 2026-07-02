using System;
using System.Collections.Generic;
using System.Globalization;

namespace NowUI.Markup
{
    public enum NowMarkupEventKind
    {
        Click,
        Change,
        Action
    }

    public readonly struct NowMarkupEvent
    {
        public readonly NowMarkupEventKind kind;
        public readonly string id;
        public readonly string name;
        public readonly string value;

        public NowMarkupEvent(NowMarkupEventKind kind, string id, string name = null, string value = null)
        {
            this.kind = kind;
            this.id = id ?? string.Empty;
            this.name = name ?? string.Empty;
            this.value = value ?? string.Empty;
        }

        public override string ToString()
        {
            return string.IsNullOrEmpty(value)
                ? $"{kind}:{id}:{name}"
                : $"{kind}:{id}:{name}={value}";
        }
    }

    public readonly struct NowMarkupResult
    {
        public readonly IReadOnlyList<NowMarkupEvent> events;

        internal NowMarkupResult(IReadOnlyList<NowMarkupEvent> events)
        {
            this.events = events;
        }

        public bool Clicked(string id)
        {
            return HasEvent(id, NowMarkupEventKind.Click);
        }

        public bool Changed(string id)
        {
            return HasEvent(id, NowMarkupEventKind.Change);
        }

        public bool Action(string name)
        {
            if (events == null)
                return false;

            for (int i = 0; i < events.Count; ++i)
            {
                var item = events[i];

                if (item.kind == NowMarkupEventKind.Action &&
                    string.Equals(item.name, name, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        public bool HasEvent(string id, NowMarkupEventKind kind)
        {
            if (events == null)
                return false;

            for (int i = 0; i < events.Count; ++i)
            {
                var item = events[i];

                if (item.kind == kind && string.Equals(item.id, id, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Caller-owned mutable state for markup documents. Controls read and write
    /// keys here, and action attributes can update the same keys.
    /// </summary>
    public sealed class NowMarkupState
    {
        readonly Dictionary<string, bool> _bools = new Dictionary<string, bool>(8);
        readonly Dictionary<string, int> _ints = new Dictionary<string, int>(8);
        readonly Dictionary<string, float> _floats = new Dictionary<string, float>(8);
        readonly Dictionary<string, string> _strings = new Dictionary<string, string>(8);

        public void Clear()
        {
            _bools.Clear();
            _ints.Clear();
            _floats.Clear();
            _strings.Clear();
        }

        public bool Has(string key)
        {
            key = NormalizeKey(key);
            return key.Length > 0 &&
                (_bools.ContainsKey(key) || _ints.ContainsKey(key) ||
                    _floats.ContainsKey(key) || _strings.ContainsKey(key));
        }

        public bool GetBool(string key, bool fallback = false)
        {
            key = NormalizeKey(key);

            if (key.Length == 0)
                return fallback;

            if (_bools.TryGetValue(key, out bool value))
                return value;

            if (_ints.TryGetValue(key, out int intValue))
                return intValue != 0;

            if (_floats.TryGetValue(key, out float floatValue))
                return Math.Abs(floatValue) > 0.0001f;

            if (_strings.TryGetValue(key, out string stringValue))
            {
                if (TryParseBool(stringValue, out value))
                    return value;

                return !string.IsNullOrEmpty(stringValue);
            }

            return fallback;
        }

        public void SetBool(string key, bool value)
        {
            key = NormalizeKey(key);

            if (key.Length == 0)
                return;

            _bools[key] = value;
        }

        public bool Toggle(string key, bool fallback = false)
        {
            bool value = !GetBool(key, fallback);
            SetBool(key, value);
            return value;
        }

        public int GetInt(string key, int fallback = 0)
        {
            key = NormalizeKey(key);

            if (key.Length == 0)
                return fallback;

            if (_ints.TryGetValue(key, out int value))
                return value;

            if (_floats.TryGetValue(key, out float floatValue))
                return (int)Math.Round(floatValue);

            if (_bools.TryGetValue(key, out bool boolValue))
                return boolValue ? 1 : 0;

            if (_strings.TryGetValue(key, out string stringValue) &&
                int.TryParse(stringValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            {
                return value;
            }

            return fallback;
        }

        public void SetInt(string key, int value)
        {
            key = NormalizeKey(key);

            if (key.Length == 0)
                return;

            _ints[key] = value;
        }

        public int StepInt(string key, int delta, int count = 0, bool wrap = true)
        {
            int value = GetInt(key) + delta;

            if (count > 0)
            {
                if (wrap)
                {
                    value %= count;

                    if (value < 0)
                        value += count;
                }
                else
                {
                    value = Math.Max(0, Math.Min(count - 1, value));
                }
            }

            SetInt(key, value);
            return value;
        }

        public float GetFloat(string key, float fallback = 0f)
        {
            key = NormalizeKey(key);

            if (key.Length == 0)
                return fallback;

            if (_floats.TryGetValue(key, out float value))
                return value;

            if (_ints.TryGetValue(key, out int intValue))
                return intValue;

            if (_bools.TryGetValue(key, out bool boolValue))
                return boolValue ? 1f : 0f;

            if (_strings.TryGetValue(key, out string stringValue) &&
                float.TryParse(stringValue, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            {
                return value;
            }

            return fallback;
        }

        public void SetFloat(string key, float value)
        {
            key = NormalizeKey(key);

            if (key.Length == 0)
                return;

            _floats[key] = value;
        }

        public string GetString(string key, string fallback = "")
        {
            key = NormalizeKey(key);

            if (key.Length == 0)
                return fallback ?? string.Empty;

            if (_strings.TryGetValue(key, out string value))
                return value ?? string.Empty;

            if (_bools.TryGetValue(key, out bool boolValue))
                return boolValue ? "true" : "false";

            if (_ints.TryGetValue(key, out int intValue))
                return intValue.ToString(CultureInfo.InvariantCulture);

            if (_floats.TryGetValue(key, out float floatValue))
                return floatValue.ToString(CultureInfo.InvariantCulture);

            return fallback ?? string.Empty;
        }

        public void SetString(string key, string value)
        {
            key = NormalizeKey(key);

            if (key.Length == 0)
                return;

            _strings[key] = value ?? string.Empty;
        }

        public static bool TryParseBool(string value, out bool result)
        {
            result = false;

            if (string.IsNullOrWhiteSpace(value))
                return false;

            value = value.Trim();

            if (bool.TryParse(value, out result))
                return true;

            if (value == "1" || value.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
                value.Equals("on", StringComparison.OrdinalIgnoreCase))
            {
                result = true;
                return true;
            }

            if (value == "0" || value.Equals("no", StringComparison.OrdinalIgnoreCase) ||
                value.Equals("off", StringComparison.OrdinalIgnoreCase))
            {
                result = false;
                return true;
            }

            return false;
        }

        static string NormalizeKey(string key)
        {
            return string.IsNullOrWhiteSpace(key) ? string.Empty : key.Trim();
        }
    }
}
