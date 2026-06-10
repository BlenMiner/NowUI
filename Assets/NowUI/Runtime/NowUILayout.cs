using System;
using System.Collections.Generic;
using UnityEngine;

public enum NowUILayoutAlign
{
    Start,
    Center,
    End
}

/// <summary>
/// Sizing and group options for <see cref="NowUILayout"/> calls, following the same
/// fluent builder pattern as <see cref="NowUIRectangle"/> and <see cref="NowUIText"/>.
/// A default instance means "auto": elements use their content size and groups
/// stretch across the parent's cross axis.
/// </summary>
public struct NowUILayoutOptions
{
    [Flags]
    internal enum Field : ushort
    {
        None = 0,
        Width = 1 << 0,
        Height = 1 << 1,
        MinWidth = 1 << 2,
        MaxWidth = 1 << 3,
        MinHeight = 1 << 4,
        MaxHeight = 1 << 5,
        StretchWidth = 1 << 6,
        StretchHeight = 1 << 7,
        Spacing = 1 << 8,
        Padding = 1 << 9,
        Align = 1 << 10
    }

    public float width;

    public float height;

    public float minWidth;

    public float maxWidth;

    public float minHeight;

    public float maxHeight;

    public float stretchWidth;

    public float stretchHeight;

    public float spacing;

    public Vector4 padding;

    public NowUILayoutAlign align;

    internal Field fields;

    internal bool Has(Field field)
    {
        return (fields & field) != 0;
    }

    public NowUILayoutOptions SetWidth(float width)
    {
        this.width = width;
        fields |= Field.Width;
        return this;
    }

    public NowUILayoutOptions SetHeight(float height)
    {
        this.height = height;
        fields |= Field.Height;
        return this;
    }

    public NowUILayoutOptions SetSize(float width, float height)
    {
        return SetWidth(width).SetHeight(height);
    }

    public NowUILayoutOptions SetMinWidth(float minWidth)
    {
        this.minWidth = minWidth;
        fields |= Field.MinWidth;
        return this;
    }

    public NowUILayoutOptions SetMaxWidth(float maxWidth)
    {
        this.maxWidth = maxWidth;
        fields |= Field.MaxWidth;
        return this;
    }

    public NowUILayoutOptions SetMinHeight(float minHeight)
    {
        this.minHeight = minHeight;
        fields |= Field.MinHeight;
        return this;
    }

    public NowUILayoutOptions SetMaxHeight(float maxHeight)
    {
        this.maxHeight = maxHeight;
        fields |= Field.MaxHeight;
        return this;
    }

    /// <summary>
    /// Inside a horizontal group the element takes a weighted share of the remaining
    /// width; inside a vertical group it fills the available width.
    /// </summary>
    public NowUILayoutOptions SetStretchWidth(float weight = 1f)
    {
        stretchWidth = weight;
        fields |= Field.StretchWidth;
        return this;
    }

    /// <summary>
    /// Inside a vertical group the element takes a weighted share of the remaining
    /// height; inside a horizontal group it fills the available height.
    /// </summary>
    public NowUILayoutOptions SetStretchHeight(float weight = 1f)
    {
        stretchHeight = weight;
        fields |= Field.StretchHeight;
        return this;
    }

    /// <summary>Gap inserted between consecutive children. Only used by groups.</summary>
    public NowUILayoutOptions SetSpacing(float spacing)
    {
        this.spacing = spacing;
        fields |= Field.Spacing;
        return this;
    }

    /// <summary>Inner padding as (left, top, right, bottom). Only used by groups.</summary>
    public NowUILayoutOptions SetPadding(Vector4 padding)
    {
        this.padding = padding;
        fields |= Field.Padding;
        return this;
    }

    public NowUILayoutOptions SetPadding(float all)
    {
        return SetPadding(new Vector4(all, all, all, all));
    }

    /// <summary>Cross-axis alignment of the element within the group.</summary>
    public NowUILayoutOptions SetAlign(NowUILayoutAlign align)
    {
        this.align = align;
        fields |= Field.Align;
        return this;
    }
}

/// <summary>
/// Disposable handle returned by <see cref="NowUILayout.Area(Vector4)"/>,
/// <see cref="NowUILayout.Horizontal()"/> and <see cref="NowUILayout.Vertical()"/>,
/// mirroring the <see cref="NowUIInput.Begin(Vector2)"/> flow: wrap it in a using
/// statement and the group ends when the scope is disposed.
/// </summary>
public struct NowUILayoutScope : IDisposable
{
    internal enum Kind : byte
    {
        Area,
        Horizontal,
        Vertical
    }

    public readonly Vector4 rect;

    readonly Kind _kind;

    bool _disposed;

    internal NowUILayoutScope(Kind kind, Vector4 rect)
    {
        _kind = kind;
        this.rect = rect;
        _disposed = false;
    }

    public float x => rect.x;

    public float y => rect.y;

    public float width => rect.z;

    public float height => rect.w;

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        switch (_kind)
        {
            case Kind.Area:
                NowUILayout.EndArea();
                break;
            case Kind.Horizontal:
                NowUILayout.EndHorizontal();
                break;
            case Kind.Vertical:
                NowUILayout.EndVertical();
                break;
        }
    }
}

/// <summary>
/// Fluent label builder returned by <see cref="NowUILayout.Label(string)"/>.
/// Like <see cref="NowUIRectangle"/> and <see cref="NowUIText"/>, nothing happens
/// until <see cref="Draw"/> is called — that is when the label is measured,
/// allocated at the current layout position and drawn:
///
/// <code>
/// NowUILayout.Label("Title").SetFontSize(24).SetColor(Color.white).Draw();
/// </code>
/// </summary>
public struct NowUILabel
{
    string _value;

    NowUIText _style;

    NowUILayoutOptions _options;

    Vector4 _rect;

    bool _reserved;

    internal NowUILabel(NowUIText style, string value, NowUILayoutOptions options)
    {
        _style = style;
        _value = value;
        _options = options;
        _rect = default;
        _reserved = false;
    }

    /// <summary>Layout rect allocated by <see cref="Reserve"/>. Throws if the label has not been reserved.</summary>
    public Vector4 rect
    {
        get
        {
            if (!_reserved)
                throw new InvalidOperationException("Call Reserve() before reading the label rect.");

            return _rect;
        }
    }

    public float x => rect.x;

    public float y => rect.y;

    public float width => rect.z;

    public float height => rect.w;

    public NowUILabel SetText(string value)
    {
        _value = value;
        return this;
    }

    public NowUILabel SetFont(NowFontAsset font)
    {
        _style = _style.SetFont(font);
        return this;
    }

    public NowUILabel SetFontSize(float fontSize)
    {
        _style = _style.SetFontSize(fontSize);
        return this;
    }

    public NowUILabel SetFontStyle(NowFontStyle fontStyle)
    {
        _style = _style.SetFontStyle(fontStyle);
        return this;
    }

    public NowUILabel SetBold(bool value = true)
    {
        _style = _style.SetBold(value);
        return this;
    }

    public NowUILabel SetItalic(bool value = true)
    {
        _style = _style.SetItalic(value);
        return this;
    }

    public NowUILabel SetColor(Color color)
    {
        _style = _style.SetColor(color);
        return this;
    }

    public NowUILabel SetColor(Vector4 color)
    {
        _style = _style.SetColor(color);
        return this;
    }

    /// <summary>
    /// Outline thickness relative to the font size (em units): 0.05 ≈ a
    /// 5%-of-em stroke at any size. Negative values inset the outline.
    /// </summary>
    public NowUILabel SetOutline(float outline)
    {
        _style = _style.SetOutline(outline);
        return this;
    }

    public NowUILabel SetOutlineColor(Vector4 color)
    {
        _style = _style.SetOutlineColor(color);
        return this;
    }

    /// <summary>Replaces all layout options at once; the setters below tweak them individually.</summary>
    public NowUILabel SetOptions(NowUILayoutOptions options)
    {
        _options = options;
        return this;
    }

    public NowUILabel SetWidth(float width)
    {
        _options = _options.SetWidth(width);
        return this;
    }

    public NowUILabel SetHeight(float height)
    {
        _options = _options.SetHeight(height);
        return this;
    }

    public NowUILabel SetLayoutSize(float width, float height)
    {
        _options = _options.SetSize(width, height);
        return this;
    }

    public NowUILabel SetMinWidth(float minWidth)
    {
        _options = _options.SetMinWidth(minWidth);
        return this;
    }

    public NowUILabel SetMaxWidth(float maxWidth)
    {
        _options = _options.SetMaxWidth(maxWidth);
        return this;
    }

    public NowUILabel SetMinHeight(float minHeight)
    {
        _options = _options.SetMinHeight(minHeight);
        return this;
    }

    public NowUILabel SetMaxHeight(float maxHeight)
    {
        _options = _options.SetMaxHeight(maxHeight);
        return this;
    }

    public NowUILabel SetStretchWidth(float weight = 1f)
    {
        _options = _options.SetStretchWidth(weight);
        return this;
    }

    public NowUILabel SetStretchHeight(float weight = 1f)
    {
        _options = _options.SetStretchHeight(weight);
        return this;
    }

    public NowUILabel SetAlign(NowUILayoutAlign align)
    {
        _options = _options.SetAlign(align);
        return this;
    }

    /// <summary>Content size of the text under the current style, without touching the layout.</summary>
    public Vector2 Measure()
    {
        return _style.Measure(_value);
    }

    /// <summary>
    /// Allocates this label's layout rect without drawing and stores it in
    /// <see cref="rect"/>, so a background or interaction can go behind the
    /// text before <see cref="Draw()"/> renders it:
    ///
    /// <code>
    /// var label = NowUILayout.Label("Save").Reserve();
    /// NowUI.Rectangle(label.rect).SetColor(bg).Draw();
    /// label.Draw();
    /// </code>
    /// </summary>
    public NowUILabel Reserve()
    {
        _rect = NowUILayout.ReserveLabel(_style, _value, _options);
        _reserved = true;
        return this;
    }

    /// <summary>Draws the label into an explicit rect, consuming no layout space.</summary>
    public NowUIText Draw(Vector4 rect)
    {
        return NowUILayout.DrawLabelAt(_style, _value, rect);
    }

    /// <summary>
    /// Draws the label. A reserved label renders into its <see cref="rect"/>;
    /// otherwise this measures and allocates at the current layout position.
    /// Returns the positioned style.
    /// </summary>
    public NowUIText Draw()
    {
        return _reserved
            ? NowUILayout.DrawLabelAt(_style, _value, _rect)
            : NowUILayout.PlaceLabel(_style, _value, _options);
    }
}

/// <summary>
/// Immediate-mode automatic layout for NowUI, similar in spirit to GUILayout.
/// Open an area over a screen rect, nest horizontal/vertical groups, and request
/// rects that are stacked automatically with spacing, padding, stretching and
/// flexible space. Group calls return a <see cref="NowUILayoutScope"/>, so groups
/// are closed with a using statement, mirroring the NowUIInput.Begin flow:
///
/// <code>
/// using (NowUILayout.Area(panelRect))
/// using (NowUILayout.Horizontal())
/// {
///     NowUILayout.Label("Hello").Draw();
/// }
/// </code>
///
/// Sizes that cannot be known up front in a single pass (auto-sized group
/// extents, stretch shares and flexible space) need a measurement source.
/// The callback form <see cref="Area(Vector4, Action)"/> runs the UI twice per
/// frame — a measure pass with draws suppressed and input passive, then the
/// real pass — so layout is exact every frame, like Unity's IMGUI Layout and
/// Repaint events. The scope form resolves from the previous frame's
/// measurements instead: cheaper, but sizes settle one frame after a layout
/// first appears or animates. Pass explicit ids to areas and groups whose
/// order or existence changes between frames to keep measurements stable.
/// </summary>
public static class NowUILayout
{
    struct Group
    {
        public int id;

        public bool horizontal;

        public bool isArea;

        public Vector4 rect;

        public Vector4 padding;

        public float spacing;

        public float cursor;

        public float maxCross;

        public float fixedMain;

        public float flexTotal;

        public int childCount;

        public int childIndex;

        /// <summary>Set when the parent sized this group's main axis from cached content.</summary>
        public bool parentMainAuto;

        public float parentMainAllocated;

        public float parentMainMin;

        public float parentMainMax;
    }

    struct CachedGroup
    {
        public float contentWidth;

        public float contentHeight;

        public float fixedMain;

        public float flexTotal;

        public double lastUsed;
    }

    const double CacheLifetimeSeconds = 10.0;

    const int AreaSeed = 0x4e6f774c;

    const float DefaultLabelFontSize = 16f;

    static NowUIText _labelStyle;

    static bool _hasLabelStyle;

    static Group[] _groups = new Group[16];

    static int _depth;

    static readonly Dictionary<int, CachedGroup> _cache = new Dictionary<int, CachedGroup>(64);

    static readonly List<int> _removeIds = new List<int>(8);

    static int _frame = int.MinValue;

    static int _areaCounter;

    static double _lastCleanupTime;

    static bool _measurePass;

    public static NowUILayoutOptions Width(float width)
    {
        return new NowUILayoutOptions().SetWidth(width);
    }

    public static NowUILayoutOptions Height(float height)
    {
        return new NowUILayoutOptions().SetHeight(height);
    }

    public static NowUILayoutOptions Size(float width, float height)
    {
        return new NowUILayoutOptions().SetSize(width, height);
    }

    public static NowUILayoutOptions StretchWidth(float weight = 1f)
    {
        return new NowUILayoutOptions().SetStretchWidth(weight);
    }

    public static NowUILayoutOptions StretchHeight(float weight = 1f)
    {
        return new NowUILayoutOptions().SetStretchHeight(weight);
    }

    /// <summary>True while the callback form of <see cref="Area(Vector4, Action)"/> runs its measure pass.</summary>
    public static bool isMeasurePass => _measurePass;

    /// <summary>
    /// Opens a root layout area over an absolute rect, laying out children
    /// vertically. Dispose the returned scope (ideally with a using statement)
    /// to close the area. Deferred sizes (auto group extents, stretch shares,
    /// flexible space) resolve from the previous frame; use the callback
    /// overloads for exact same-frame layout.
    /// </summary>
    public static NowUILayoutScope Area(Vector4 rect)
    {
        return Area(null, rect, default(NowUILayoutOptions));
    }

    public static NowUILayoutScope Area(Vector4 rect, NowUILayoutOptions options)
    {
        return Area(null, rect, options);
    }

    public static NowUILayoutScope Area(string id, Vector4 rect)
    {
        return Area(id, rect, default(NowUILayoutOptions));
    }

    public static NowUILayoutScope Area(string id, Vector4 rect, NowUILayoutOptions options)
    {
        OnFrameBoundary();

        int areaId = id != null ? NowUIInput.GetId(id) : HashCombine(AreaSeed, _areaCounter);
        _areaCounter++;

        Push(new Group
        {
            id = areaId,
            horizontal = false,
            isArea = true,
            rect = rect,
            padding = options.Has(NowUILayoutOptions.Field.Padding) ? options.padding : default,
            spacing = options.Has(NowUILayoutOptions.Field.Spacing) ? options.spacing : 0f,
            parentMainMax = float.MaxValue
        });

        return new NowUILayoutScope(NowUILayoutScope.Kind.Area, rect);
    }

    /// <summary>
    /// Runs <paramref name="ui"/> inside an area in two passes, like Unity's
    /// IMGUI Layout and Repaint events: first a measure pass with draws
    /// suppressed and input passive, then the real pass using this frame's
    /// measurements — so flexible space, stretching and auto-sized groups are
    /// exact every frame, including the first and while animating. The callback
    /// must not mutate state unconditionally (input is inert during measuring,
    /// so reacting to clicks is always safe); check <see cref="isMeasurePass"/>
    /// when in doubt.
    /// </summary>
    public static void Area(Vector4 rect, Action ui)
    {
        Area(null, rect, default, ui);
    }

    public static void Area(Vector4 rect, NowUILayoutOptions options, Action ui)
    {
        Area(null, rect, options, ui);
    }

    public static void Area(string id, Vector4 rect, Action ui)
    {
        Area(id, rect, default, ui);
    }

    public static void Area(string id, Vector4 rect, NowUILayoutOptions options, Action ui)
    {
        if (ui == null)
            throw new ArgumentNullException(nameof(ui));

        // Already inside an outer measure pass: run once inline; the outer real
        // pass will invoke this whole area again.
        if (_measurePass)
        {
            using (Area(id, rect, options))
                ui();

            return;
        }

        int areaCounter = BeginMeasurePass();

        try
        {
            using (Area(id, rect, options))
                ui();
        }
        finally
        {
            EndMeasurePass(areaCounter);
        }

        using (Area(id, rect, options))
            ui();
    }

    /// <summary>
    /// Enters measure mode: draws suppressed, input passive, layout calls record
    /// this frame's sizes. Run the UI once, call <see cref="EndMeasurePass"/> with
    /// the returned snapshot, then run the same UI again for real. Used by the
    /// callback Area overloads and by hosts that own a draw entry point (e.g.
    /// NowUIGraphic running DrawNowUI twice).
    /// </summary>
    internal static int BeginMeasurePass()
    {
        // Resolve the frame boundary before snapshotting so the counter rewind
        // hands the real pass the same anonymous area ids the measure pass used
        // (the boundary would otherwise reset the counter mid-measure).
        OnFrameBoundary();

        int areaCounter = _areaCounter;
        _measurePass = true;
        NowUI.BeginSuppressDraw();
        NowUIInput.BeginPassive();
        return areaCounter;
    }

    internal static void EndMeasurePass(int areaCounterSnapshot)
    {
        NowUIInput.EndPassive();
        NowUI.EndSuppressDraw();
        _measurePass = false;

        // Anonymous area ids are sequential per frame; rewind so the real pass
        // resolves the same ids (and therefore the fresh measurements).
        _areaCounter = areaCounterSnapshot;
    }

    public static void EndArea()
    {
        if (_depth == 0)
            throw new InvalidOperationException("EndArea called without a matching Area.");

        ref var group = ref Top();

        if (!group.isArea)
            throw new InvalidOperationException("EndArea called while a layout group is still open.");

        StoreCache(ref group);
        _depth--;
        CleanupCache();
    }

    public static NowUILayoutScope Horizontal()
    {
        return BeginGroup(true, null, default);
    }

    public static NowUILayoutScope Horizontal(NowUILayoutOptions options)
    {
        return BeginGroup(true, null, options);
    }

    public static NowUILayoutScope Horizontal(string id)
    {
        return BeginGroup(true, id, default);
    }

    public static NowUILayoutScope Horizontal(string id, NowUILayoutOptions options)
    {
        return BeginGroup(true, id, options);
    }

    public static void EndHorizontal()
    {
        EndGroup(true);
    }

    public static NowUILayoutScope Vertical()
    {
        return BeginGroup(false, null, default);
    }

    public static NowUILayoutScope Vertical(NowUILayoutOptions options)
    {
        return BeginGroup(false, null, options);
    }

    public static NowUILayoutScope Vertical(string id)
    {
        return BeginGroup(false, id, default);
    }

    public static NowUILayoutScope Vertical(string id, NowUILayoutOptions options)
    {
        return BeginGroup(false, id, options);
    }

    public static void EndVertical()
    {
        EndGroup(false);
    }

    /// <summary>Reserves a fixed-size rect at the current layout position.</summary>
    public static Vector4 Rect(float width, float height)
    {
        return Rect(new NowUILayoutOptions().SetWidth(width).SetHeight(height));
    }

    /// <summary>Reserves a rect sized by <paramref name="options"/> at the current layout position.</summary>
    public static Vector4 Rect(NowUILayoutOptions options)
    {
        ref var group = ref RequireGroup();
        return Allocate(ref group, options, Vector2.zero, false, out _, out _);
    }

    /// <summary>Advances the current group's cursor by a fixed amount.</summary>
    public static void Space(float pixels)
    {
        ref var group = ref RequireGroup();
        group.cursor += pixels;
        group.fixedMain += pixels;
    }

    /// <summary>
    /// Inserts a stretchable gap that absorbs a weighted share of the group's
    /// remaining main-axis space. Resolved from the previous frame's measurements,
    /// so it collapses on the first frame a layout appears.
    /// </summary>
    public static void FlexibleSpace(float weight = 1f)
    {
        ref var group = ref RequireGroup();
        group.cursor += FlexShare(ref group, weight);
        group.flexTotal += weight;
    }

    /// <summary>
    /// Style template used by <see cref="Label(string)"/> overloads that take no
    /// explicit style. Defaults to <see cref="NowUI.defaultFont"/> at a 16px font size.
    /// </summary>
    public static NowUIText labelStyle
    {
        get => _hasLabelStyle
            ? _labelStyle
            : new NowUIText(default, NowUI.defaultFont).SetFontSize(DefaultLabelFontSize);
        set
        {
            _labelStyle = value;
            _hasLabelStyle = true;
        }
    }

    /// <summary>
    /// Starts a text label builder using <see cref="labelStyle"/>. Like the other
    /// NowUI builders, nothing is measured, placed or drawn until
    /// <see cref="NowUILabel.Draw"/> is called:
    /// <c>NowUILayout.Label("Title").SetFontSize(24).Draw();</c>
    /// </summary>
    public static NowUILabel Label(string value)
    {
        return new NowUILabel(labelStyle, value, default);
    }

    public static NowUILabel Label(string value, NowUILayoutOptions options)
    {
        return new NowUILabel(labelStyle, value, options);
    }

    public static NowUILabel Label(string value, float fontSize, NowUILayoutOptions options = default)
    {
        return new NowUILabel(labelStyle.SetFontSize(fontSize), value, options);
    }

    public static NowUILabel Label(string value, float fontSize, Color color, NowUILayoutOptions options = default)
    {
        return new NowUILabel(labelStyle.SetFontSize(fontSize).SetColor(color), value, options);
    }

    public static NowUILabel Label(NowUIText style, string value)
    {
        return new NowUILabel(style, value, default);
    }

    public static NowUILabel Label(NowUIText style, string value, NowUILayoutOptions options)
    {
        return new NowUILabel(style, value, options);
    }

    /// <summary>Measures, allocates and draws a label at the current layout position.</summary>
    internal static NowUIText PlaceLabel(NowUIText style, string value, NowUILayoutOptions options)
    {
        var rect = ReserveLabel(style, value, options);
        return DrawLabelAt(style, value, rect);
    }

    /// <summary>Allocates layout space for a label without drawing it.</summary>
    internal static Vector4 ReserveLabel(NowUIText style, string value, NowUILayoutOptions options)
    {
        ref var group = ref RequireGroup();
        var measured = style.Measure(value);
        return Allocate(ref group, options, measured, false, out _, out _);
    }

    /// <summary>Draws a label into an already-reserved rect, consuming no layout space.</summary>
    internal static NowUIText DrawLabelAt(NowUIText style, string value, Vector4 rect)
    {
        style = style
            .SetPosition(rect)
            .SetMask(LabelMask(style, value, rect));

        if (style.font != null && !string.IsNullOrEmpty(value))
            style.Draw(value);

        return style;
    }

    /// <summary>Clears all layout state, including cached measurements. Intended for tests and domain reloads.</summary>
    public static void Reset()
    {
        _depth = 0;
        _areaCounter = 0;
        _frame = int.MinValue;
        _cache.Clear();
        _lastCleanupTime = 0.0;
        _labelStyle = default;
        _hasLabelStyle = false;
        _measurePass = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetForRuntimeLoad()
    {
        Reset();
    }

    static NowUILayoutScope BeginGroup(bool horizontal, string id, NowUILayoutOptions options)
    {
        ref var parent = ref RequireGroup("Layout groups require an open area. Call NowUILayout.Area first.");

        int groupId = id != null
            ? HashCombine(parent.id, NowUIInput.GetId(id))
            : HashCombine(parent.id, parent.childIndex);
        parent.childIndex++;

        Vector2 autoSize = default;

        if (_cache.TryGetValue(groupId, out var cached))
            autoSize = new Vector2(cached.contentWidth, cached.contentHeight);

        var rect = Allocate(ref parent, options, autoSize, true, out bool mainAuto, out float mainAllocated);

        bool mainIsWidth = parent.horizontal;

        Push(new Group
        {
            id = groupId,
            horizontal = horizontal,
            rect = rect,
            padding = options.Has(NowUILayoutOptions.Field.Padding) ? options.padding : default,
            spacing = options.Has(NowUILayoutOptions.Field.Spacing) ? options.spacing : 0f,
            parentMainAuto = mainAuto,
            parentMainAllocated = mainAllocated,
            parentMainMin = options.Has(mainIsWidth ? NowUILayoutOptions.Field.MinWidth : NowUILayoutOptions.Field.MinHeight)
                ? (mainIsWidth ? options.minWidth : options.minHeight)
                : 0f,
            parentMainMax = options.Has(mainIsWidth ? NowUILayoutOptions.Field.MaxWidth : NowUILayoutOptions.Field.MaxHeight)
                ? (mainIsWidth ? options.maxWidth : options.maxHeight)
                : float.MaxValue
        });

        return new NowUILayoutScope(
            horizontal ? NowUILayoutScope.Kind.Horizontal : NowUILayoutScope.Kind.Vertical,
            rect);
    }

    static void EndGroup(bool horizontal)
    {
        if (_depth == 0)
            throw new InvalidOperationException("Layout group ended without a matching begin call.");

        ref var group = ref Top();

        if (group.isArea)
            throw new InvalidOperationException("Layout group ended without a matching begin call.");

        if (group.horizontal != horizontal)
        {
            throw new InvalidOperationException(group.horizontal
                ? "EndVertical called while a horizontal group is open."
                : "EndHorizontal called while a vertical group is open.");
        }

        StoreCache(ref group, out float contentWidth, out float contentHeight);
        var ended = group;
        _depth--;

        // When the parent sized this group from stale cached content, retro-correct
        // the parent's cursor so siblings placed after the group stack correctly on
        // the very first frame.
        if (!ended.parentMainAuto || _depth == 0)
            return;

        ref var parent = ref Top();
        float actualMain = parent.horizontal ? contentWidth : contentHeight;
        actualMain = Mathf.Clamp(actualMain, ended.parentMainMin, ended.parentMainMax);
        float delta = actualMain - ended.parentMainAllocated;

        if (delta == 0f)
            return;

        parent.cursor += delta;
        parent.fixedMain += delta;
    }

    static void StoreCache(ref Group group)
    {
        StoreCache(ref group, out _, out _);
    }

    static void StoreCache(ref Group group, out float contentWidth, out float contentHeight)
    {
        float contentMain = group.cursor;
        float contentCross = group.maxCross;

        contentWidth = (group.horizontal ? contentMain : contentCross) + group.padding.x + group.padding.z;
        contentHeight = (group.horizontal ? contentCross : contentMain) + group.padding.y + group.padding.w;

        _cache[group.id] = new CachedGroup
        {
            contentWidth = contentWidth,
            contentHeight = contentHeight,
            fixedMain = group.fixedMain,
            flexTotal = group.flexTotal,
            lastUsed = Now()
        };
    }

    static Vector4 Allocate(
        ref Group group,
        NowUILayoutOptions options,
        Vector2 autoSize,
        bool stretchCrossByDefault,
        out bool mainAuto,
        out float mainAllocated)
    {
        float contentX = group.rect.x + group.padding.x;
        float contentY = group.rect.y + group.padding.y;
        float contentWidth = group.rect.z - group.padding.x - group.padding.z;
        float contentHeight = group.rect.w - group.padding.y - group.padding.w;

        bool mainIsWidth = group.horizontal;

        ResolveAxis(
            ref group,
            mainIsWidth,
            options.Has(NowUILayoutOptions.Field.Width),
            options.width,
            options.Has(NowUILayoutOptions.Field.StretchWidth),
            options.stretchWidth,
            options.Has(NowUILayoutOptions.Field.MinWidth),
            options.minWidth,
            options.Has(NowUILayoutOptions.Field.MaxWidth),
            options.maxWidth,
            autoSize.x,
            contentWidth,
            stretchCrossByDefault,
            out float width,
            out bool widthFlex,
            out float widthWeight,
            out bool widthAuto);

        ResolveAxis(
            ref group,
            !mainIsWidth,
            options.Has(NowUILayoutOptions.Field.Height),
            options.height,
            options.Has(NowUILayoutOptions.Field.StretchHeight),
            options.stretchHeight,
            options.Has(NowUILayoutOptions.Field.MinHeight),
            options.minHeight,
            options.Has(NowUILayoutOptions.Field.MaxHeight),
            options.maxHeight,
            autoSize.y,
            contentHeight,
            stretchCrossByDefault,
            out float height,
            out bool heightFlex,
            out float heightWeight,
            out bool heightAuto);

        float main = mainIsWidth ? width : height;
        float cross = mainIsWidth ? height : width;
        bool mainFlex = mainIsWidth ? widthFlex : heightFlex;
        float mainWeight = mainIsWidth ? widthWeight : heightWeight;
        mainAuto = mainIsWidth ? widthAuto : heightAuto;
        mainAllocated = main;

        float gap = group.childCount > 0 ? group.spacing : 0f;
        float mainPos = group.cursor + gap;

        float crossAvail = mainIsWidth ? contentHeight : contentWidth;
        float alignFactor = 0f;

        if (options.Has(NowUILayoutOptions.Field.Align))
        {
            alignFactor = options.align switch
            {
                NowUILayoutAlign.Center => 0.5f,
                NowUILayoutAlign.End => 1f,
                _ => 0f
            };
        }

        float crossOffset = Mathf.Max(0f, crossAvail - cross) * alignFactor;

        var rect = mainIsWidth
            ? new Vector4(contentX + mainPos, contentY + crossOffset, width, height)
            : new Vector4(contentX + crossOffset, contentY + mainPos, width, height);

        group.cursor = mainPos + main;
        group.maxCross = Mathf.Max(group.maxCross, cross);
        group.fixedMain += gap + (mainFlex ? 0f : main);

        if (mainFlex)
            group.flexTotal += mainWeight;

        group.childCount++;
        return rect;
    }

    static void ResolveAxis(
        ref Group group,
        bool isMainAxis,
        bool hasFixed,
        float fixedSize,
        bool hasStretch,
        float stretchWeight,
        bool hasMin,
        float min,
        bool hasMax,
        float max,
        float autoSize,
        float crossAvail,
        bool stretchCrossByDefault,
        out float size,
        out bool isFlex,
        out float weight,
        out bool isAuto)
    {
        isFlex = false;
        weight = 0f;
        isAuto = false;

        if (hasFixed)
        {
            size = fixedSize;
        }
        else if (hasStretch || (stretchCrossByDefault && !isMainAxis))
        {
            if (isMainAxis)
            {
                isFlex = true;
                weight = stretchWeight;
                size = FlexShare(ref group, weight);
            }
            else
            {
                size = crossAvail;
            }
        }
        else
        {
            size = autoSize;
            isAuto = isMainAxis;
        }

        if (hasMin)
            size = Mathf.Max(size, min);

        if (hasMax)
            size = Mathf.Min(size, max);
    }

    /// <summary>
    /// The layout rect comes from advance metrics, but glyphs can extend past it
    /// (descenders, italic overhang), so the mask covers the union of the rect and
    /// the measured visual bounds.
    /// </summary>
    static Vector4 LabelMask(in NowUIText style, string value, Vector4 rect)
    {
        float minX = rect.x;
        float minY = rect.y;
        float maxX = rect.x + rect.z;
        float maxY = rect.y + rect.w;

        if (style.font != null && !string.IsNullOrEmpty(value))
        {
            var bounds = style.MeasureBounds(value);

            if (bounds is { z: > 0f, w: > 0f })
            {
                minX = Mathf.Min(minX, rect.x + bounds.x);
                minY = Mathf.Min(minY, rect.y + bounds.y);
                maxX = Mathf.Max(maxX, rect.x + bounds.x + bounds.z);
                maxY = Mathf.Max(maxY, rect.y + bounds.y + bounds.w);
            }
        }

        return new Vector4(minX - 4f, minY - 4f, maxX - minX + 8f, maxY - minY + 8f);
    }

    static float FlexShare(ref Group group, float weight)
    {
        if (!_cache.TryGetValue(group.id, out var cached) || cached.flexTotal <= 0f)
            return 0f;

        float avail = group.horizontal
            ? group.rect.z - group.padding.x - group.padding.z
            : group.rect.w - group.padding.y - group.padding.w;

        float remaining = avail - cached.fixedMain;
        return remaining > 0f ? remaining * weight / cached.flexTotal : 0f;
    }

    static ref Group Top()
    {
        return ref _groups[_depth - 1];
    }

    static ref Group RequireGroup(string message = null)
    {
        if (_depth == 0)
        {
            throw new InvalidOperationException(
                message ?? "Layout calls require an open area. Call NowUILayout.Area first.");
        }

        return ref Top();
    }

    static void Push(in Group group)
    {
        if (_depth == _groups.Length)
            Array.Resize(ref _groups, _groups.Length * 2);

        _groups[_depth++] = group;
    }

    static void OnFrameBoundary()
    {
        int frame = Time.frameCount;

        if (frame == _frame)
            return;

        _frame = frame;
        _areaCounter = 0;

        if (_depth != 0)
        {
            Debug.LogError("NowUILayout: unbalanced Begin/End calls detected from a previous frame. Stack cleared.");
            _depth = 0;
        }
    }

    static void CleanupCache()
    {
        double now = Now();

        if (now - _lastCleanupTime < 1.0)
            return;

        _lastCleanupTime = now;
        _removeIds.Clear();

        foreach (var kvp in _cache)
        {
            if (now - kvp.Value.lastUsed > CacheLifetimeSeconds)
                _removeIds.Add(kvp.Key);
        }

        for (int i = 0; i < _removeIds.Count; ++i)
            _cache.Remove(_removeIds[i]);
    }

    static double Now()
    {
        return DateTime.UtcNow.Ticks / (double)TimeSpan.TicksPerSecond;
    }

    static int HashCombine(int a, int b)
    {
        unchecked
        {
            return (a * 397) ^ b;
        }
    }
}
