using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NowUI
{
    /// <summary>
    /// Per-type value drawer override for <see cref="NowInspector"/>: draws the
    /// control cell for a value of T (the row label is drawn by the inspector)
    /// and returns true when the value changed this frame.
    /// </summary>
    public delegate bool NowInspectorDrawer<T>(ref T value);

    /// <summary>
    /// Reflection-driven inspector, mirroring Unity's:
    /// <code>NowLayout.Inspector().Draw(ref settings);</code>
    /// Draws label + control rows for every field Unity would serialize —
    /// public fields plus [SerializeField] non-public ones, minus
    /// [NonSerialized] and [HideInInspector] — honoring [Header], [Space],
    /// [Range], [Min], [TextArea] and [Multiline]. Nested serializable types
    /// and arrays/lists render behind foldouts with add/remove and resize;
    /// null strings, lists and nested classes are auto-created like Unity's
    /// serializer (reported as a change). UnityEngine.Object references render
    /// read-only. Register <see cref="SetDrawer{T}"/> to take over the control
    /// cell for any type, including otherwise unsupported ones.
    ///
    /// Values are accessed through reflection every frame, so inspector rows
    /// box value-typed fields; this is a debugging/modding surface, not a
    /// per-frame hot path for thousands of fields.
    /// </summary>
    [NowBuilder]
    public struct NowInspector
    {
        NowId _id;
        readonly int _site;
        readonly NowRect _rect;
        readonly bool _hasRect;
        NowInspectorSettings _settings;

        const int AreaSeed = 0x4e495341;

        internal NowInspector(NowId id, int site)
        {
            _id = id;
            _site = site;
            _rect = default;
            _hasRect = false;
            _settings = NowInspectorSettings.Default;
        }

        internal NowInspector(NowRect rect, NowId id, int site) : this(id, site)
        {
            _rect = rect;
            _hasRect = true;
        }

        public NowInspector SetOptions(NowLayoutOptions options) { _settings.options = options; return this; }

        public NowInspector SetWidth(float width) { _settings.options = _settings.options.SetWidth(width); return this; }

        public NowInspector SetStretchWidth(float weight = 1f) { _settings.options = _settings.options.SetStretchWidth(weight); return this; }

        /// <summary>Explicit control id, decoupling identity from the call site.</summary>
        public NowInspector SetId(NowId id) { _id = id; return this; }

        /// <summary>Width of the label column; the control cell takes the rest.</summary>
        public NowInspector SetLabelWidth(float width) { _settings.labelWidth = Mathf.Max(0f, width); return this; }

        /// <summary>Vertical spacing between rows.</summary>
        public NowInspector SetSpacing(float spacing) { _settings.rowSpacing = Mathf.Max(0f, spacing); return this; }

        /// <summary>Indentation of foldout contents (nested objects, list elements).</summary>
        public NowInspector SetIndent(float indent) { _settings.indent = Mathf.Max(0f, indent); return this; }

        /// <summary>Recursion cap for nested objects and collections.</summary>
        public NowInspector SetMaxDepth(int maxDepth) { _settings.maxDepth = Mathf.Max(1, maxDepth); return this; }

        /// <summary>Today highlight for DateTime rows — caller-passed, no hidden clock.</summary>
        public NowInspector SetToday(DateTime today)
        {
            _settings.todayTicks = today.Date.Ticks;
            _settings.hasToday = true;
            return this;
        }

        /// <summary>
        /// Draws rows for the value's fields, mutating them in place. Returns
        /// true when any field changed this frame. Works for classes and
        /// structs alike.
        /// </summary>
        public bool Draw<T>(ref T value)
        {
            object boxed = value;
            bool changed = DrawBoxed(ref boxed);

            if (changed)
                value = (T)boxed;

            return changed;
        }

        /// <summary>
        /// Draws rows for an existing instance (class targets or pre-boxed
        /// structs); field edits mutate the instance in place.
        /// </summary>
        public bool Draw(object target)
        {
            return DrawBoxed(ref target);
        }

        bool DrawBoxed(ref object boxed)
        {
            int id = NowControls.GetControlId(_id, _site);
            bool changed;

            if (_hasRect)
            {
                using (NowLayout.Area(NowId.Resolved(NowInput.CombineId(id, AreaSeed)), _rect))
                using (NowLayout.Vertical(new NowLayoutOptions().SetSpacing(_settings.rowSpacing).SetStretchWidth()))
                    changed = DrawRoot(id, ref boxed);
            }
            else
            {
                using (NowLayout.Vertical(GroupOptions()))
                    changed = DrawRoot(id, ref boxed);
            }

            return changed;
        }

        bool DrawRoot(int id, ref object boxed)
        {
            using (NowControls.IdScope(id))
            {
                if (boxed == null)
                {
                    NowInspectorGui.DrawMutedLabel("(null target)");
                    return false;
                }

                Type type = boxed.GetType();

                if (NowInspectorGui.TryGetDrawer(type, out var drawer))
                {
                    var (drawerChanged, next) = drawer(boxed);

                    if (drawerChanged)
                        boxed = next;

                    return drawerChanged;
                }

                return NowInspectorGui.DrawMembers(type, ref boxed, _settings, 0);
            }
        }

        NowLayoutOptions GroupOptions()
        {
            var options = _settings.options;

            if (!options.Has(NowLayoutOptions.Field.Spacing))
                options = options.SetSpacing(_settings.rowSpacing);

            if (!options.Has(NowLayoutOptions.Field.Width) && !options.Has(NowLayoutOptions.Field.StretchWidth))
                options = options.SetStretchWidth();

            return options;
        }

        /// <summary>
        /// Takes over the control cell for every row (and root) of the given
        /// type; pass through to draw unsupported types or restyle built-in
        /// ones. Pass null to remove.
        /// </summary>
        public static void SetDrawer<T>(NowInspectorDrawer<T> drawer)
        {
            NowInspectorGui.SetDrawer(drawer);
        }

        public static void RemoveDrawer<T>()
        {
            NowInspectorGui.SetDrawer<T>(null);
        }
    }

    struct NowInspectorSettings
    {
        public NowLayoutOptions options;
        public float labelWidth;
        public float rowSpacing;
        public float cellSpacing;
        public float indent;
        public int maxDepth;
        public long todayTicks;
        public bool hasToday;

        public static NowInspectorSettings Default => new NowInspectorSettings
        {
            labelWidth = 140f,
            rowSpacing = 4f,
            cellSpacing = 6f,
            indent = 16f,
            maxDepth = 7
        };
    }

    static class NowInspectorGui
    {
        internal enum ValueKind
        {
            Unsupported,
            Bool,
            Int,
            Float,
            Double,
            WideInt,
            SmallInt,
            String,
            Char,
            Enum,
            Color,
            Color32,
            Vector2,
            Vector3,
            Vector4,
            Vector2Int,
            Vector3Int,
            Rect,
            RectInt,
            Quaternion,
            Gradient,
            AnimationCurve,
            LayerMask,
            DateTime,
            TimeSpan,
            ObjectReference,
            Bounds,
            BoundsInt,
            Collection,
            Composite
        }

        internal sealed class Member
        {
            public FieldInfo field;
            public string name;
            public string label;
            public ValueKind kind;
            public string header;
            public float space;
            public bool hasRange;
            public float min;
            public float max;
            public bool hasMin;
            public float minValue;
            public int multilineMin;
            public int multilineMax;
        }

        struct RowAttrs
        {
            public bool hasRange;
            public float min;
            public float max;
            public bool hasMin;
            public float minValue;
            public int multilineMin;
            public int multilineMax;
        }

        struct QuaternionEditState
        {
            public byte initialized;
            public Quaternion value;
            public Vector3 euler;
        }

        struct ObjectLabelCache
        {
            public byte initialized;
            public Object target;
            public string label;
        }

        static readonly Dictionary<Type, Member[]> _members = new Dictionary<Type, Member[]>(16);
        static readonly Dictionary<Type, ValueKind> _kinds = new Dictionary<Type, ValueKind>(32);
        static readonly Dictionary<Type, Func<object, (bool changed, object value)>> _drawers =
            new Dictionary<Type, Func<object, (bool changed, object value)>>(8);
        static readonly Dictionary<Type, EnumInfo> _enums = new Dictionary<Type, EnumInfo>(8);
        static readonly List<object> _referenceStack = new List<object>(8);
        static readonly List<string> _elementLabels = new List<string>(16);
        static readonly List<Type> _typeChain = new List<Type>(4);

        sealed class EnumInfo
        {
            public string[] names;
            public Array values;
            public ulong[] bits;
            public bool isFlags;
            public string[] maskNames;
            public ulong[] maskBits;
        }

        internal static void SetDrawer<T>(NowInspectorDrawer<T> drawer)
        {
            if (drawer == null)
            {
                _drawers.Remove(typeof(T));
                return;
            }

            _drawers[typeof(T)] = boxed =>
            {
                var value = (T)boxed;
                bool changed = drawer(ref value);
                return (changed, value);
            };
        }

        internal static bool TryGetDrawer(Type type, out Func<object, (bool changed, object value)> drawer)
        {
            return _drawers.TryGetValue(type, out drawer);
        }

        internal static bool DrawMembers(Type type, ref object target, in NowInspectorSettings settings, int depth)
        {
            var members = GetMembers(type);

            if (members.Length == 0)
            {
                DrawMutedLabel("(no editable fields)");
                return false;
            }

            bool pushed = false;

            if (!type.IsValueType)
            {
                for (int i = 0; i < _referenceStack.Count; ++i)
                {
                    if (ReferenceEquals(_referenceStack[i], target))
                    {
                        DrawMutedLabel("(circular reference)");
                        return false;
                    }
                }

                _referenceStack.Add(target);
                pushed = true;
            }

            bool changed = false;

            try
            {
                for (int i = 0; i < members.Length; ++i)
                {
                    var member = members[i];

                    if (member.header != null)
                        DrawHeaderLabel(member.header);

                    if (member.space > 0f)
                        NowLayout.ReserveRect(height: member.space, stretchWidth: true);

                    using (NowControls.IdScope(member.name))
                    {
                        object value = member.field.GetValue(target);
                        var attrs = new RowAttrs
                        {
                            hasRange = member.hasRange,
                            min = member.min,
                            max = member.max,
                            hasMin = member.hasMin,
                            minValue = member.minValue,
                            multilineMin = member.multilineMin,
                            multilineMax = member.multilineMax
                        };

                        if (DrawLabeledValue(member.label, member.kind, member.field.FieldType, attrs, ref value, settings, depth))
                        {
                            member.field.SetValue(target, value);
                            changed = true;
                        }
                    }
                }
            }
            finally
            {
                if (pushed)
                    _referenceStack.RemoveAt(_referenceStack.Count - 1);
            }

            return changed;
        }

        static bool DrawLabeledValue(
            string label,
            ValueKind kind,
            Type type,
            in RowAttrs attrs,
            ref object value,
            in NowInspectorSettings settings,
            int depth)
        {
            if (_drawers.TryGetValue(type, out var drawer))
            {
                bool drawerChanged;

                using (BeginRow(settings))
                {
                    DrawRowLabel(label, settings);
                    var (changed, next) = drawer(value);
                    drawerChanged = changed;

                    if (changed)
                        value = next;
                }

                return drawerChanged;
            }

            switch (kind)
            {
                case ValueKind.Collection:
                    return DrawCollection(label, type, ref value, settings, depth);
                case ValueKind.Composite:
                    return DrawComposite(label, type, ref value, settings, depth);
                case ValueKind.Bounds:
                    return DrawBounds(label, ref value, settings);
                case ValueKind.BoundsInt:
                    return DrawBoundsInt(label, ref value, settings);
                case ValueKind.Unsupported:
                    return false;
            }

            bool rowChanged;

            using (BeginRow(settings))
            {
                DrawRowLabel(label, settings);
                rowChanged = DrawControlCell(kind, type, attrs, ref value, settings);
            }

            return rowChanged;
        }

        static bool DrawControlCell(ValueKind kind, Type type, in RowAttrs attrs, ref object value, in NowInspectorSettings settings)
        {
            switch (kind)
            {
                case ValueKind.Bool:
                {
                    bool v = (bool)value;

                    if (!NowLayout.Checkbox().Draw(ref v))
                        return false;

                    value = v;
                    return true;
                }
                case ValueKind.Int:
                {
                    int v = (int)value;
                    bool changed = attrs.hasRange
                        ? NowLayout.Slider(attrs.min, attrs.max).SetStretchWidth().Draw(ref v)
                        : ConfigureIntField(attrs).Draw(ref v);

                    if (!changed)
                        return false;

                    value = v;
                    return true;
                }
                case ValueKind.Float:
                {
                    float v = (float)value;
                    bool changed = attrs.hasRange
                        ? NowLayout.Slider(attrs.min, attrs.max).SetStretchWidth().Draw(ref v)
                        : ConfigureFloatField(attrs).Draw(ref v);

                    if (!changed)
                        return false;

                    value = v;
                    return true;
                }
                case ValueKind.Double:
                {
                    double v = (double)value;

                    if (!ConfigureFloatField(attrs).Draw(ref v))
                        return false;

                    value = v;
                    return true;
                }
                case ValueKind.WideInt:
                    return DrawWideInt(type, attrs, ref value);
                case ValueKind.SmallInt:
                    return DrawSmallInt(type, attrs, ref value);
                case ValueKind.String:
                {
                    string v = value as string ?? string.Empty;
                    bool changed = attrs.multilineMin > 0
                        ? NowLayout.TextArea().SetLines(attrs.multilineMin, Mathf.Max(attrs.multilineMin, attrs.multilineMax)).SetStretchWidth().Draw(ref v)
                        : NowLayout.TextField().SetStretchWidth().Draw(ref v);

                    if (!changed)
                        return false;

                    value = v;
                    return true;
                }
                case ValueKind.Char:
                {
                    string v = ((char)value).ToString();

                    if (!NowLayout.TextField().SetStretchWidth().Draw(ref v) || v.Length == 0)
                        return false;

                    value = v[v.Length - 1];
                    return true;
                }
                case ValueKind.Enum:
                    return DrawEnum(type, ref value);
                case ValueKind.Color:
                {
                    var v = (Color)value;

                    if (!NowLayout.ColorPicker().SetStretchWidth().Draw(ref v))
                        return false;

                    value = v;
                    return true;
                }
                case ValueKind.Color32:
                {
                    Color v = (Color32)value;

                    if (!NowLayout.ColorPicker().SetStretchWidth().Draw(ref v))
                        return false;

                    value = (Color32)v;
                    return true;
                }
                case ValueKind.Vector2:
                {
                    var v = (Vector2)value;

                    if (!NowLayout.VectorField().SetStretchWidth().Draw(ref v))
                        return false;

                    value = v;
                    return true;
                }
                case ValueKind.Vector3:
                {
                    var v = (Vector3)value;

                    if (!NowLayout.VectorField().SetStretchWidth().Draw(ref v))
                        return false;

                    value = v;
                    return true;
                }
                case ValueKind.Vector4:
                {
                    var v = (Vector4)value;

                    if (!NowLayout.VectorField().SetStretchWidth().Draw(ref v))
                        return false;

                    value = v;
                    return true;
                }
                case ValueKind.Vector2Int:
                {
                    var v = (Vector2Int)value;

                    if (!NowLayout.VectorField().SetStretchWidth().Draw(ref v))
                        return false;

                    value = v;
                    return true;
                }
                case ValueKind.Vector3Int:
                {
                    var v = (Vector3Int)value;

                    if (!NowLayout.VectorField().SetStretchWidth().Draw(ref v))
                        return false;

                    value = v;
                    return true;
                }
                case ValueKind.Rect:
                {
                    var v = (UnityEngine.Rect)value;

                    if (!NowLayout.VectorField().SetStretchWidth().Draw(ref v))
                        return false;

                    value = v;
                    return true;
                }
                case ValueKind.RectInt:
                {
                    var v = (RectInt)value;

                    if (!NowLayout.VectorField().SetStretchWidth().Draw(ref v))
                        return false;

                    value = v;
                    return true;
                }
                case ValueKind.Quaternion:
                    return DrawQuaternion(ref value);
                case ValueKind.Gradient:
                {
                    var v = value as Gradient;
                    bool created = v == null;

                    if (created)
                        v = new Gradient();

                    bool changed = NowLayout.GradientField().SetStretchWidth().Draw(ref v);

                    if (!changed && !created)
                        return false;

                    value = v;
                    return true;
                }
                case ValueKind.AnimationCurve:
                {
                    var v = value as AnimationCurve;
                    bool created = v == null;

                    if (created)
                        v = AnimationCurve.Linear(0f, 0f, 1f, 1f);

                    bool changed = NowLayout.CurveField().SetStretchWidth().Draw(ref v);

                    if (!changed && !created)
                        return false;

                    value = v;
                    return true;
                }
                case ValueKind.LayerMask:
                {
                    var v = (LayerMask)value;

                    if (!NowLayout.LayerMaskField().SetStretchWidth().Draw(ref v))
                        return false;

                    value = v;
                    return true;
                }
                case ValueKind.DateTime:
                {
                    var v = (DateTime)value;
                    var picker = NowLayout.DatePicker().SetStretchWidth();

                    if (settings.hasToday)
                        picker = picker.SetToday(new DateTime(settings.todayTicks));

                    if (!picker.Draw(ref v))
                        return false;

                    value = v;
                    return true;
                }
                case ValueKind.TimeSpan:
                {
                    var v = (TimeSpan)value;

                    if (!NowLayout.TimePicker().SetStretchWidth().Draw(ref v))
                        return false;

                    value = v;
                    return true;
                }
                case ValueKind.ObjectReference:
                    DrawObjectReference(type, value);
                    return false;
            }

            return false;
        }

        static NowTextField ConfigureFloatField(in RowAttrs attrs)
        {
            var field = NowLayout.TextField().SetStretchWidth();

            if (attrs.hasMin)
                field = field.SetRange(attrs.minValue, float.MaxValue);

            return field;
        }

        static NowTextField ConfigureIntField(in RowAttrs attrs)
        {
            var field = NowLayout.TextField().SetStretchWidth().SetSpinner();

            if (attrs.hasMin)
                field = field.SetRange(Mathf.CeilToInt(attrs.minValue), int.MaxValue);

            return field;
        }

        static bool DrawWideInt(Type type, in RowAttrs attrs, ref object value)
        {
            long v = type == typeof(ulong) ? unchecked((long)(ulong)value) : Convert.ToInt64(value);
            long previous = v;
            var field = NowLayout.TextField().SetStretchWidth();

            if (type == typeof(uint))
                field = field.SetRange(0f, uint.MaxValue);

            field.Draw(ref v);

            if (v == previous)
                return false;

            if (type == typeof(long))
            {
                value = v;
            }
            else if (type == typeof(ulong))
            {
                value = v < 0 ? 0UL : (ulong)v;
            }
            else
            {
                value = (uint)Mathf.Clamp(v, 0, uint.MaxValue);
            }

            return true;
        }

        static bool DrawSmallInt(Type type, in RowAttrs attrs, ref object value)
        {
            int v = Convert.ToInt32(value);
            int previous = v;
            int min = attrs.hasMin ? Mathf.CeilToInt(attrs.minValue) : int.MinValue;

            var field = NowLayout.TextField().SetStretchWidth().SetSpinner();

            if (type == typeof(byte))
                field = field.SetRange(Mathf.Max(min, byte.MinValue), byte.MaxValue);
            else if (type == typeof(sbyte))
                field = field.SetRange(Mathf.Max(min, sbyte.MinValue), sbyte.MaxValue);
            else if (type == typeof(short))
                field = field.SetRange(Mathf.Max(min, short.MinValue), short.MaxValue);
            else
                field = field.SetRange(Mathf.Max(min, ushort.MinValue), ushort.MaxValue);

            if (!field.Draw(ref v) || v == previous)
                return false;

            if (type == typeof(byte))
                value = (byte)v;
            else if (type == typeof(sbyte))
                value = (sbyte)v;
            else if (type == typeof(short))
                value = (short)v;
            else
                value = (ushort)v;

            return true;
        }

        static bool DrawEnum(Type type, ref object value)
        {
            var info = GetEnumInfo(type);

            if (info.isFlags)
            {
                int mask = MaskFromEnum(info, value);

                if (!NowLayout.MaskField(info.maskNames).SetStretchWidth().Draw(ref mask))
                    return false;

                value = EnumFromMask(type, info, mask);
                return true;
            }

            ulong bits = EnumBits(type, value);
            int index = -1;

            for (int i = 0; i < info.bits.Length; ++i)
            {
                if (info.bits[i] == bits)
                {
                    index = i;
                    break;
                }
            }

            if (!NowLayout.Dropdown(info.names).SetStretchWidth().Draw(ref index) ||
                index < 0 ||
                index >= info.values.Length)
            {
                return false;
            }

            value = info.values.GetValue(index);
            return true;
        }

        static bool DrawQuaternion(ref object value)
        {
            var quaternion = (Quaternion)value;
            ref var state = ref NowControlState.Get<QuaternionEditState>(NowControls.GetControlId("quaternion-euler"));

            if (state.initialized == 0 || !SameQuaternion(state.value, quaternion))
            {
                state.initialized = 1;
                state.value = quaternion;
                state.euler = quaternion.eulerAngles;
            }

            var euler = state.euler;

            if (!NowLayout.VectorField().SetStretchWidth().Draw(ref euler))
                return false;

            state.euler = euler;
            state.value = Quaternion.Euler(euler);
            value = state.value;
            return true;
        }

        static void DrawObjectReference(Type type, object value)
        {
            var obj = value as Object;
            ref var cache = ref NowControlState.Get<ObjectLabelCache>(NowControls.GetControlId("object-label"));

            if (cache.initialized == 0 || !ReferenceEquals(cache.target, obj) || cache.label == null)
            {
                cache.initialized = 1;
                cache.target = obj;
                cache.label = obj != null ? $"{obj.name} ({type.Name})" : $"None ({type.Name})";
            }

            NowLayout.Label(NowTheme.themeAsset.ResolveText(NowTextStyle.Muted), cache.label).SetStretchWidth().Draw();
        }

        static bool DrawBounds(string label, ref object value, in NowInspectorSettings settings)
        {
            var bounds = (Bounds)value;
            var center = bounds.center;
            var extents = bounds.extents;
            bool changed = false;

            using (BeginRow(settings))
            {
                DrawRowLabel(label, settings);

                using (NowLayout.Vertical(new NowLayoutOptions().SetSpacing(settings.rowSpacing).SetStretchWidth()))
                {
                    changed |= DrawSubVector("Center", ref center, settings);
                    changed |= DrawSubVector("Extent", ref extents, settings);
                }
            }

            if (!changed)
                return false;

            value = new Bounds(center, extents * 2f);
            return true;
        }

        static bool DrawBoundsInt(string label, ref object value, in NowInspectorSettings settings)
        {
            var bounds = (BoundsInt)value;
            var position = bounds.position;
            var size = bounds.size;
            bool changed = false;

            using (BeginRow(settings))
            {
                DrawRowLabel(label, settings);

                using (NowLayout.Vertical(new NowLayoutOptions().SetSpacing(settings.rowSpacing).SetStretchWidth()))
                {
                    changed |= DrawSubVector("Position", ref position, settings);
                    changed |= DrawSubVector("Size", ref size, settings);
                }
            }

            if (!changed)
                return false;

            value = new BoundsInt(position, size);
            return true;
        }

        static bool DrawSubVector(string label, ref Vector3 value, in NowInspectorSettings settings)
        {
            using (NowControls.IdScope(label))
            using (NowLayout.Horizontal(new NowLayoutOptions().SetSpacing(settings.cellSpacing).SetAlignItems(NowLayoutAlign.Center).SetStretchWidth()))
            {
                NowLayout.Label(label).SetWidth(52f).Draw();
                return NowLayout.VectorField().SetStretchWidth().Draw(ref value);
            }
        }

        static bool DrawSubVector(string label, ref Vector3Int value, in NowInspectorSettings settings)
        {
            using (NowControls.IdScope(label))
            using (NowLayout.Horizontal(new NowLayoutOptions().SetSpacing(settings.cellSpacing).SetAlignItems(NowLayoutAlign.Center).SetStretchWidth()))
            {
                NowLayout.Label(label).SetWidth(52f).Draw();
                return NowLayout.VectorField().SetStretchWidth().Draw(ref value);
            }
        }

        static bool DrawComposite(string label, Type type, ref object value, in NowInspectorSettings settings, int depth)
        {
            if (depth + 1 >= settings.maxDepth)
            {
                DrawDisabledRow(label, "(max depth)", settings);
                return false;
            }

            bool changed = false;

            if (value == null)
            {
                value = TryCreateInstance(type);

                if (value == null)
                {
                    DrawDisabledRow(label, "null", settings);
                    return false;
                }

                changed = true;
            }

            bool expanded = NowLayout.Foldout(label).Draw();

            if (!expanded)
                return changed;

            using (BeginIndent(settings))
                changed |= DrawMembers(value.GetType(), ref value, settings, depth + 1);

            return changed;
        }

        static bool DrawCollection(string label, Type type, ref object value, in NowInspectorSettings settings, int depth)
        {
            if (depth + 1 >= settings.maxDepth)
            {
                DrawDisabledRow(label, "(max depth)", settings);
                return false;
            }

            bool isArray = type.IsArray;
            Type elementType = isArray ? type.GetElementType() : type.GetGenericArguments()[0];
            bool changed = false;

            if (value == null)
            {
                value = isArray
                    ? Array.CreateInstance(elementType, 0)
                    : Activator.CreateInstance(type);
                changed = true;
            }

            var list = (IList)value;
            int count = list.Count;
            int requestedCount = count;
            bool expanded;

            using (BeginRow(settings))
            {
                expanded = NowLayout.Foldout(label).SetStretchWidth().Draw();
                NowLayout.TextField().SetWidth(64f).SetRange(0, 8192).Draw(ref requestedCount);
            }

            if (expanded)
            {
                using (BeginIndent(settings))
                {
                    var elementKind = Classify(elementType);

                    for (int i = 0; i < list.Count; ++i)
                    {
                        using (NowControls.IdScope(i + 1))
                        {
                            object element = list[i];

                            if (DrawLabeledValue(ElementLabel(i), elementKind, elementType, default, ref element, settings, depth + 1))
                            {
                                list[i] = element;
                                changed = true;
                            }
                        }
                    }

                    using (NowLayout.Horizontal(new NowLayoutOptions().SetSpacing(settings.cellSpacing).SetStretchWidth()))
                    {
                        NowLayout.ReserveRect(stretchWidth: true);

                        if (NowLayout.Button("+").SetWidth(30f).Draw())
                            requestedCount = list.Count + 1;

                        if (NowLayout.Button("-").SetWidth(30f).Draw() && list.Count > 0)
                            requestedCount = list.Count - 1;
                    }
                }
            }

            requestedCount = Mathf.Clamp(requestedCount, 0, 8192);

            if (requestedCount != count)
            {
                value = Resize(value, isArray, elementType, requestedCount);
                changed = true;
            }

            return changed;
        }

        static object Resize(object collection, bool isArray, Type elementType, int newCount)
        {
            if (isArray)
            {
                var source = (Array)collection;
                var next = Array.CreateInstance(elementType, newCount);
                int copy = Mathf.Min(source.Length, newCount);
                Array.Copy(source, next, copy);

                for (int i = copy; i < newCount; ++i)
                    next.SetValue(DefaultElement(elementType), i);

                return next;
            }

            var list = (IList)collection;

            while (list.Count > newCount)
                list.RemoveAt(list.Count - 1);

            while (list.Count < newCount)
                list.Add(DefaultElement(elementType));

            return list;
        }

        static object DefaultElement(Type elementType)
        {
            if (elementType == typeof(string))
                return string.Empty;

            if (typeof(Object).IsAssignableFrom(elementType))
                return null;

            if (elementType.IsValueType)
                return Activator.CreateInstance(elementType);

            return TryCreateInstance(elementType);
        }

        static object TryCreateInstance(Type type)
        {
            if (type.IsAbstract || typeof(Object).IsAssignableFrom(type))
                return null;

            try
            {
                return Activator.CreateInstance(type);
            }
            catch (Exception)
            {
                return null;
            }
        }

        static string ElementLabel(int index)
        {
            while (_elementLabels.Count <= index)
                _elementLabels.Add("Element " + _elementLabels.Count);

            return _elementLabels[index];
        }

        static NowLayoutScope BeginRow(in NowInspectorSettings settings)
        {
            return NowLayout.Horizontal(new NowLayoutOptions()
                .SetSpacing(settings.cellSpacing)
                .SetAlignItems(NowLayoutAlign.Center)
                .SetStretchWidth());
        }

        static NowLayoutScope BeginIndent(in NowInspectorSettings settings)
        {
            return NowLayout.Vertical(new NowLayoutOptions()
                .SetPadding(new Vector4(settings.indent, 0f, 0f, 0f))
                .SetSpacing(settings.rowSpacing)
                .SetStretchWidth());
        }

        static void DrawRowLabel(string label, in NowInspectorSettings settings)
        {
            NowLayout.Label(label).SetWidth(settings.labelWidth).Draw();
        }

        static void DrawHeaderLabel(string header)
        {
            NowLayout.Label(NowTheme.themeAsset.ResolveText(NowTextStyle.BodyStrong), header).Draw();
        }

        internal static void DrawMutedLabel(string text)
        {
            NowLayout.Label(NowTheme.themeAsset.ResolveText(NowTextStyle.Muted), text).Draw();
        }

        static void DrawDisabledRow(string label, string text, in NowInspectorSettings settings)
        {
            using (BeginRow(settings))
            {
                DrawRowLabel(label, settings);
                NowLayout.Label(NowTheme.themeAsset.ResolveText(NowTextStyle.Muted), text).SetStretchWidth().Draw();
            }
        }

        static bool SameQuaternion(Quaternion a, Quaternion b)
        {
            return Mathf.Approximately(a.x, b.x) &&
                Mathf.Approximately(a.y, b.y) &&
                Mathf.Approximately(a.z, b.z) &&
                Mathf.Approximately(a.w, b.w);
        }

        internal static Member[] GetMembers(Type type)
        {
            if (_members.TryGetValue(type, out var cached))
                return cached;

            _typeChain.Clear();

            for (Type current = type; current != null && current != typeof(object) && current != typeof(ValueType); current = current.BaseType)
                _typeChain.Add(current);

            var result = new List<Member>(8);

            for (int t = _typeChain.Count - 1; t >= 0; --t)
            {
                var fields = _typeChain[t].GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                Array.Sort(fields, (a, b) => a.MetadataToken.CompareTo(b.MetadataToken));

                for (int i = 0; i < fields.Length; ++i)
                {
                    var member = BuildMember(fields[i]);

                    if (member != null)
                        result.Add(member);
                }
            }

            var members = result.ToArray();
            _members[type] = members;
            return members;
        }

        static Member BuildMember(FieldInfo field)
        {
            if (field.IsInitOnly || field.IsLiteral)
                return null;

            if (!field.IsPublic && !field.IsDefined(typeof(SerializeField), inherit: false))
                return null;

            if (field.IsDefined(typeof(NonSerializedAttribute), inherit: false) ||
                field.IsDefined(typeof(HideInInspector), inherit: false))
            {
                return null;
            }

            if (typeof(Delegate).IsAssignableFrom(field.FieldType) || field.FieldType.IsPointer)
                return null;

            var member = new Member
            {
                field = field,
                name = field.Name,
                label = NicifyName(field.Name),
                kind = Classify(field.FieldType)
            };

            var header = field.GetCustomAttribute<HeaderAttribute>(inherit: false);
            member.header = header?.header;

            var space = field.GetCustomAttribute<SpaceAttribute>(inherit: false);
            member.space = space?.height ?? 0f;

            var range = field.GetCustomAttribute<RangeAttribute>(inherit: false);

            if (range != null)
            {
                member.hasRange = true;
                member.min = range.min;
                member.max = range.max;
            }

            var min = field.GetCustomAttribute<MinAttribute>(inherit: false);

            if (min != null)
            {
                member.hasMin = true;
                member.minValue = min.min;
            }

            var textArea = field.GetCustomAttribute<TextAreaAttribute>(inherit: false);

            if (textArea != null)
            {
                member.multilineMin = Mathf.Max(1, textArea.minLines);
                member.multilineMax = Mathf.Max(member.multilineMin, textArea.maxLines);
            }

            var multiline = field.GetCustomAttribute<MultilineAttribute>(inherit: false);

            if (multiline != null)
            {
                member.multilineMin = Mathf.Max(1, multiline.lines);
                member.multilineMax = member.multilineMin;
            }

            return member;
        }

        internal static ValueKind Classify(Type type)
        {
            if (_kinds.TryGetValue(type, out var cached))
                return cached;

            var kind = ClassifyUncached(type);
            _kinds[type] = kind;
            return kind;
        }

        static ValueKind ClassifyUncached(Type type)
        {
            if (type == typeof(bool))
                return ValueKind.Bool;

            if (type == typeof(int))
                return ValueKind.Int;

            if (type == typeof(float))
                return ValueKind.Float;

            if (type == typeof(double))
                return ValueKind.Double;

            if (type == typeof(long) || type == typeof(ulong) || type == typeof(uint))
                return ValueKind.WideInt;

            if (type == typeof(byte) || type == typeof(sbyte) || type == typeof(short) || type == typeof(ushort))
                return ValueKind.SmallInt;

            if (type == typeof(string))
                return ValueKind.String;

            if (type == typeof(char))
                return ValueKind.Char;

            if (type.IsEnum)
                return ValueKind.Enum;

            if (type == typeof(Color))
                return ValueKind.Color;

            if (type == typeof(Color32))
                return ValueKind.Color32;

            if (type == typeof(Vector2))
                return ValueKind.Vector2;

            if (type == typeof(Vector3))
                return ValueKind.Vector3;

            if (type == typeof(Vector4))
                return ValueKind.Vector4;

            if (type == typeof(Vector2Int))
                return ValueKind.Vector2Int;

            if (type == typeof(Vector3Int))
                return ValueKind.Vector3Int;

            if (type == typeof(UnityEngine.Rect))
                return ValueKind.Rect;

            if (type == typeof(RectInt))
                return ValueKind.RectInt;

            if (type == typeof(Quaternion))
                return ValueKind.Quaternion;

            if (type == typeof(Gradient))
                return ValueKind.Gradient;

            if (type == typeof(AnimationCurve))
                return ValueKind.AnimationCurve;

            if (type == typeof(LayerMask))
                return ValueKind.LayerMask;

            if (type == typeof(DateTime))
                return ValueKind.DateTime;

            if (type == typeof(TimeSpan))
                return ValueKind.TimeSpan;

            if (type == typeof(Bounds))
                return ValueKind.Bounds;

            if (type == typeof(BoundsInt))
                return ValueKind.BoundsInt;

            if (typeof(Object).IsAssignableFrom(type))
                return ValueKind.ObjectReference;

            if (type.IsArray)
            {
                return type.GetArrayRank() == 1 && Classify(type.GetElementType()) != ValueKind.Unsupported
                    ? ValueKind.Collection
                    : ValueKind.Unsupported;
            }

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                return Classify(type.GetGenericArguments()[0]) != ValueKind.Unsupported
                    ? ValueKind.Collection
                    : ValueKind.Unsupported;
            }

            if (type.IsPrimitive || type.IsGenericType || typeof(IEnumerable).IsAssignableFrom(type))
                return ValueKind.Unsupported;

            if (type.IsValueType || (type.IsClass && !type.IsAbstract))
                return ValueKind.Composite;

            return ValueKind.Unsupported;
        }

        static EnumInfo GetEnumInfo(Type type)
        {
            if (_enums.TryGetValue(type, out var cached))
                return cached;

            var names = Enum.GetNames(type);
            var values = Enum.GetValues(type);
            var bits = new ulong[names.Length];

            for (int i = 0; i < names.Length; ++i)
                bits[i] = EnumBits(type, values.GetValue(i));

            var info = new EnumInfo
            {
                names = names,
                values = values,
                bits = bits,
                isFlags = type.IsDefined(typeof(FlagsAttribute), inherit: false)
            };

            if (info.isFlags)
            {
                var maskNames = new List<string>(names.Length);
                var maskBits = new List<ulong>(names.Length);

                for (int i = 0; i < names.Length && maskNames.Count < 32; ++i)
                {
                    if (bits[i] == 0)
                        continue;

                    maskNames.Add(names[i]);
                    maskBits.Add(bits[i]);
                }

                info.maskNames = maskNames.ToArray();
                info.maskBits = maskBits.ToArray();
            }

            _enums[type] = info;
            return info;
        }

        internal static ulong EnumBits(Type type, object value)
        {
            switch (Type.GetTypeCode(Enum.GetUnderlyingType(type)))
            {
                case TypeCode.SByte:
                    return unchecked((ulong)(sbyte)value);
                case TypeCode.Int16:
                    return unchecked((ulong)(short)value);
                case TypeCode.Int32:
                    return unchecked((ulong)(int)value);
                case TypeCode.Int64:
                    return unchecked((ulong)(long)value);
                case TypeCode.Byte:
                    return (byte)value;
                case TypeCode.UInt16:
                    return (ushort)value;
                case TypeCode.UInt32:
                    return (uint)value;
                default:
                    return (ulong)value;
            }
        }

        static int MaskFromEnum(EnumInfo info, object value)
        {
            ulong bits = EnumBits(value.GetType(), value);
            int mask = 0;

            for (int i = 0; i < info.maskBits.Length; ++i)
            {
                if ((bits & info.maskBits[i]) == info.maskBits[i])
                    mask |= 1 << i;
            }

            return mask;
        }

        static object EnumFromMask(Type type, EnumInfo info, int mask)
        {
            ulong bits = 0;

            for (int i = 0; i < info.maskBits.Length; ++i)
            {
                if ((mask & (1 << i)) != 0)
                    bits |= info.maskBits[i];
            }

            return Enum.ToObject(type, unchecked((long)bits));
        }

        internal static string NicifyName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return string.Empty;

            int start = 0;

            if (name.StartsWith("m_", StringComparison.Ordinal))
                start = 2;
            else if (name.Length > 1 && name[0] == 'k' && char.IsUpper(name[1]))
                start = 1;

            while (start < name.Length && name[start] == '_')
                ++start;

            if (start >= name.Length)
                return name;

            var builder = new StringBuilder(name.Length + 4);

            for (int i = start; i < name.Length; ++i)
            {
                char c = name[i];

                if (i == start)
                {
                    builder.Append(char.ToUpperInvariant(c));
                    continue;
                }

                char previous = name[i - 1];

                if (char.IsUpper(c))
                {
                    bool nextIsLower = i + 1 < name.Length && char.IsLower(name[i + 1]);

                    if (!char.IsUpper(previous) || nextIsLower)
                        builder.Append(' ');
                }
                else if (char.IsDigit(c) && !char.IsDigit(previous))
                {
                    builder.Append(' ');
                }

                builder.Append(c);
            }

            return builder.ToString();
        }

        internal static void ClearCachesForTests()
        {
            _members.Clear();
            _kinds.Clear();
            _enums.Clear();
            _drawers.Clear();
            _referenceStack.Clear();
        }
    }

    public static partial class Now
    {
        public static NowInspector Inspector(NowRect rect, NowId id = default, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowInspector(rect, id, NowControls.SiteId(file, line));
        }
    }

    public static partial class NowLayout
    {
        public static NowInspector Inspector(NowId id = default, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowInspector(id, NowControls.SiteId(file, line));
        }
    }
}
