using System;
using System.Collections.Generic;
using UnityEngine;

namespace NowUI
{
    public enum NowLayoutAlign
    {
        Start,
        Center,
        End
    }

    /// <summary>
    /// Sizing and group options for <see cref="NowLayout"/> calls, following the same
    /// fluent builder pattern as <see cref="NowRectangle"/> and <see cref="NowText"/>.
    /// A default instance means "auto": elements use their content size and groups
    /// stretch across the parent's cross axis.
    /// </summary>
    public struct NowLayoutOptions
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
            Align = 1 << 10,
            AlignItems = 1 << 11
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

        public NowLayoutAlign align;

        public NowLayoutAlign alignItems;

        internal Field fields;

        internal readonly bool Has(Field field)
        {
            return (fields & field) != 0;
        }

        public NowLayoutOptions SetWidth(float width)
        {
            this.width = width;
            fields |= Field.Width;
            return this;
        }

        public NowLayoutOptions SetHeight(float height)
        {
            this.height = height;
            fields |= Field.Height;
            return this;
        }

        public NowLayoutOptions SetSize(float width, float height)
        {
            return SetWidth(width).SetHeight(height);
        }

        public NowLayoutOptions SetMinWidth(float minWidth)
        {
            this.minWidth = minWidth;
            fields |= Field.MinWidth;
            return this;
        }

        public NowLayoutOptions SetMaxWidth(float maxWidth)
        {
            this.maxWidth = maxWidth;
            fields |= Field.MaxWidth;
            return this;
        }

        public NowLayoutOptions SetMinHeight(float minHeight)
        {
            this.minHeight = minHeight;
            fields |= Field.MinHeight;
            return this;
        }

        public NowLayoutOptions SetMaxHeight(float maxHeight)
        {
            this.maxHeight = maxHeight;
            fields |= Field.MaxHeight;
            return this;
        }

        /// <summary>
        /// Inside a horizontal group the element takes a weighted share of the remaining
        /// width; inside a vertical group it fills the available width.
        /// </summary>
        public NowLayoutOptions SetStretchWidth(float weight = 1f)
        {
            stretchWidth = weight;
            fields |= Field.StretchWidth;
            return this;
        }

        /// <summary>
        /// Inside a vertical group the element takes a weighted share of the remaining
        /// height; inside a horizontal group it fills the available height.
        /// </summary>
        public NowLayoutOptions SetStretchHeight(float weight = 1f)
        {
            stretchHeight = weight;
            fields |= Field.StretchHeight;
            return this;
        }

        /// <summary>Gap inserted between consecutive children. Only used by groups.</summary>
        public NowLayoutOptions SetSpacing(float spacing)
        {
            this.spacing = spacing;
            fields |= Field.Spacing;
            return this;
        }

        /// <summary>Inner padding as (left, top, right, bottom). Only used by groups.</summary>
        public NowLayoutOptions SetPadding(Vector4 padding)
        {
            this.padding = padding;
            fields |= Field.Padding;
            return this;
        }

        public NowLayoutOptions SetPadding(float all)
        {
            return SetPadding(new Vector4(all, all, all, all));
        }

        /// <summary>Cross-axis alignment of the element within the group.</summary>
        public NowLayoutOptions SetAlign(NowLayoutAlign align)
        {
            this.align = align;
            fields |= Field.Align;
            return this;
        }

        /// <summary>
        /// Default cross-axis alignment for the group's children (flexbox
        /// align-items). Only used by groups; a child's own <see cref="SetAlign"/>
        /// overrides it.
        /// </summary>
        public NowLayoutOptions SetAlignItems(NowLayoutAlign align)
        {
            alignItems = align;
            fields |= Field.AlignItems;
            return this;
        }
    }

    /// <summary>
    /// Handle returned by <see cref="NowLayout.ContentRect()"/>: the reserved
    /// rect plus the site-keyed slot the measured height reports back into.
    /// </summary>
    public readonly struct NowContentRect
    {
        /// <summary>The reserved rect — stretch width, last frame's height.</summary>
        public readonly NowRect rect;

        readonly int _slot;

        internal NowContentRect(NowRect rect, int slot)
        {
            this.rect = rect;
            _slot = slot;
        }

        public static implicit operator NowRect(NowContentRect content)
        {
            return content.rect;
        }

        /// <summary>
        /// Reports the height the content actually produced; requests a repaint
        /// while the reservation is still converging toward it.
        /// </summary>
        public void End(float measuredHeight)
        {
            ref float lastHeight = ref NowControlState.Get<float>(_slot);

            if (Mathf.Abs(measuredHeight - lastHeight) <= 0.5f)
                return;

            lastHeight = measuredHeight;
            NowControlState.RequestRepaint();
        }
    }

    /// <summary>
    /// Disposable handle returned by <see cref="NowLayout.Area(NowRect)"/>,
    /// <see cref="NowLayout.Horizontal()"/> and <see cref="NowLayout.Vertical()"/>,
    /// mirroring the <see cref="NowInput.Begin(Vector2)"/> flow: wrap it in a using
    /// statement and the group ends when the scope is disposed.
    /// </summary>
    public struct NowLayoutScope : IDisposable
    {
        internal enum Kind : byte
        {
            Area,
            Horizontal,
            Vertical
        }

        public readonly NowRect rect;

        readonly Kind _kind;

        bool _disposed;

        internal NowLayoutScope(Kind kind, NowRect rect)
        {
            _kind = kind;
            this.rect = rect;
            _disposed = false;
        }

        public float x => rect.x;

        public float y => rect.y;

        public float width => rect.width;

        public float height => rect.height;

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            switch (_kind)
            {
                case Kind.Area:
                    NowLayout.EndArea();
                    break;
                case Kind.Horizontal:
                    NowLayout.EndHorizontal();
                    break;
                case Kind.Vertical:
                    NowLayout.EndVertical();
                    break;
            }
        }
    }

    /// <summary>
    /// Fluent label builder returned by <see cref="NowLayout.Label(string)"/>.
    /// Like <see cref="NowRectangle"/> and <see cref="NowText"/>, nothing happens
    /// until <see cref="Draw"/> is called — that is when the label is measured,
    /// allocated at the current layout position and drawn:
    ///
    /// <code>
    /// NowLayout.Label("Title").SetFontSize(24).SetColor(Color.white).Draw();
    /// </code>
    /// </summary>
    [NowBuilder]
    public struct NowLabel
    {
        string _value;

        NowText _style;

        NowLayoutOptions _options;

        NowRect _rect;

        bool _reserved;

        internal NowLabel(NowText style, string value, NowLayoutOptions options)
        {
            _style = style;
            _value = value;
            _options = options;
            _rect = default;
            _reserved = false;
        }

        /// <summary>Layout rect allocated by <see cref="Reserve"/>. Throws if the label has not been reserved.</summary>
        public NowRect rect
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

        public float width => rect.width;

        public float height => rect.height;

        public NowLabel SetText(string value)
        {
            _value = value;
            return this;
        }

        public NowLabel SetFont(NowFontAsset font)
        {
            _style = _style.SetFont(font);
            return this;
        }

        public NowLabel SetFontSize(float fontSize)
        {
            _style = _style.SetFontSize(fontSize);
            return this;
        }

        public NowLabel SetFontStyle(NowFontStyle fontStyle)
        {
            _style = _style.SetFontStyle(fontStyle);
            return this;
        }

        public NowLabel SetBold(bool value = true)
        {
            _style = _style.SetBold(value);
            return this;
        }

        public NowLabel SetItalic(bool value = true)
        {
            _style = _style.SetItalic(value);
            return this;
        }

        public NowLabel SetColor(Color color)
        {
            _style = _style.SetColor(color);
            return this;
        }

        public NowLabel SetColor(Vector4 color)
        {
            _style = _style.SetColor(color);
            return this;
        }

        /// <summary>
        /// Outline thickness relative to the font size (em units): 0.05 ≈ a
        /// 5%-of-em stroke at any size. Negative values inset the outline.
        /// </summary>
        public NowLabel SetOutline(float outline)
        {
            _style = _style.SetOutline(outline);
            return this;
        }

        public NowLabel SetOutlineColor(Vector4 color)
        {
            _style = _style.SetOutlineColor(color);
            return this;
        }

        /// <summary>Replaces all layout options at once.</summary>
        public NowLabel SetOptions(NowLayoutOptions options)
        {
            _options = options;
            return this;
        }

        public NowLabel SetWidth(float width)
        {
            _options = _options.SetWidth(width);
            return this;
        }

        public NowLabel SetHeight(float height)
        {
            _options = _options.SetHeight(height);
            return this;
        }

        public NowLabel SetLayoutSize(float width, float height)
        {
            _options = _options.SetSize(width, height);
            return this;
        }

        public NowLabel SetMinWidth(float minWidth)
        {
            _options = _options.SetMinWidth(minWidth);
            return this;
        }

        public NowLabel SetMaxWidth(float maxWidth)
        {
            _options = _options.SetMaxWidth(maxWidth);
            return this;
        }

        public NowLabel SetMinHeight(float minHeight)
        {
            _options = _options.SetMinHeight(minHeight);
            return this;
        }

        public NowLabel SetMaxHeight(float maxHeight)
        {
            _options = _options.SetMaxHeight(maxHeight);
            return this;
        }

        public NowLabel SetStretchWidth(float weight = 1f)
        {
            _options = _options.SetStretchWidth(weight);
            return this;
        }

        public NowLabel SetStretchHeight(float weight = 1f)
        {
            _options = _options.SetStretchHeight(weight);
            return this;
        }

        public NowLabel SetAlign(NowLayoutAlign align)
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
        /// var label = NowLayout.Label("Save").Reserve();
        /// Now.Rectangle(label.rect).SetColor(bg).Draw();
        /// label.Draw();
        /// </code>
        /// </summary>
        [NowConsumer]
        public NowLabel Reserve()
        {
            _rect = NowLayout.ReserveLabel(_style, _value, _options);
            _reserved = true;
            return this;
        }

        /// <summary>Draws the label into an explicit rect, consuming no layout space.</summary>
        [NowConsumer]
        public NowText Draw(NowRect rect)
        {
            return NowLayout.DrawLabelAt(_style, _value, rect);
        }

        /// <summary>
        /// Draws the label. A reserved label renders into its <see cref="rect"/>;
        /// otherwise this measures and allocates at the current layout position.
        /// Returns the positioned style.
        /// </summary>
        [NowConsumer]
        public NowText Draw()
        {
            return _reserved
                ? NowLayout.DrawLabelAt(_style, _value, _rect)
                : NowLayout.PlaceLabel(_style, _value, _options);
        }
    }

    /// <summary>
    /// Fluent Lottie builder returned by <see cref="NowLayout.Lottie(NowLottieAsset)"/>.
    /// Nothing happens until <see cref="Draw()"/> is called. Sized to the animation's
    /// native dimensions by default; fixing exactly one dimension derives the other
    /// from the animation's aspect ratio.
    /// </summary>
    [NowBuilder]
    public struct NowLottieBuilder
    {
        NowLottie _style;

        NowLayoutOptions _options;

        NowRect _rect;

        string _url;

        bool _reserved;

        internal NowLottieBuilder(NowLottie style, NowLayoutOptions options)
        {
            _style = style;
            _options = options;
            _rect = default;
            _url = null;
            _reserved = false;
        }

        internal NowLottieBuilder(NowLottie style, NowLayoutOptions options, string url)
        {
            _style = style;
            _options = options;
            _rect = default;
            _url = url;
            _reserved = false;
        }

        /// <summary>Layout rect allocated by <see cref="Reserve"/>. Throws if the animation has not been reserved.</summary>
        public NowRect rect
        {
            get
            {
                if (!_reserved)
                    throw new InvalidOperationException("Call Reserve() before reading the animation rect.");

                return _rect;
            }
        }

        public float x => rect.x;

        public float y => rect.y;

        public float width => rect.width;

        public float height => rect.height;

        public NowLottieBuilder SetTime(float seconds)
        {
            _style = _style.SetTime(seconds);
            return this;
        }

        public NowLottieBuilder SetNormalizedTime(float normalizedTime)
        {
            _style = _style.SetNormalizedTime(normalizedTime);
            return this;
        }

        public NowLottieBuilder SetFrame(float frame)
        {
            _style = _style.SetFrame(frame);
            return this;
        }

        public NowLottieBuilder SetLoop(bool loop)
        {
            _style = _style.SetLoop(loop);
            return this;
        }

        public NowLottieBuilder SetPreserveAspect(bool preserveAspect)
        {
            _style = _style.SetPreserveAspect(preserveAspect);
            return this;
        }

        public NowLottieBuilder SetPlaybackFrameRate(float framesPerSecond)
        {
            _style = _style.SetPlaybackFrameRate(framesPerSecond);
            return this;
        }

        public NowLottieBuilder SetColor(Color color)
        {
            _style = _style.SetColor(color);
            return this;
        }

        public NowLottieBuilder SetColor(Vector4 color)
        {
            _style = _style.SetColor(color);
            return this;
        }

        /// <summary>Replaces all layout options at once.</summary>
        public NowLottieBuilder SetOptions(NowLayoutOptions options)
        {
            _options = options;
            return this;
        }

        public NowLottieBuilder SetWidth(float width)
        {
            _options = _options.SetWidth(width);
            return this;
        }

        public NowLottieBuilder SetHeight(float height)
        {
            _options = _options.SetHeight(height);
            return this;
        }

        public NowLottieBuilder SetLayoutSize(float width, float height)
        {
            _options = _options.SetSize(width, height);
            return this;
        }

        public NowLottieBuilder SetMinWidth(float minWidth)
        {
            _options = _options.SetMinWidth(minWidth);
            return this;
        }

        public NowLottieBuilder SetMaxWidth(float maxWidth)
        {
            _options = _options.SetMaxWidth(maxWidth);
            return this;
        }

        public NowLottieBuilder SetMinHeight(float minHeight)
        {
            _options = _options.SetMinHeight(minHeight);
            return this;
        }

        public NowLottieBuilder SetMaxHeight(float maxHeight)
        {
            _options = _options.SetMaxHeight(maxHeight);
            return this;
        }

        public NowLottieBuilder SetStretchWidth(float weight = 1f)
        {
            _options = _options.SetStretchWidth(weight);
            return this;
        }

        public NowLottieBuilder SetStretchHeight(float weight = 1f)
        {
            _options = _options.SetStretchHeight(weight);
            return this;
        }

        public NowLottieBuilder SetAlign(NowLayoutAlign align)
        {
            _options = _options.SetAlign(align);
            return this;
        }

        /// <summary>Content size before allocation: the native animation size, aspect-adjusted by fixed options.</summary>
        public Vector2 Measure()
        {
            var style = ResolveStyle();
            return NowLayout.LottieAutoSize(style, _options);
        }

        /// <summary>Allocates the layout rect without drawing and stores it in <see cref="rect"/>.</summary>
        [NowConsumer]
        public NowLottieBuilder Reserve()
        {
            _style = ResolveStyle();
            _rect = NowLayout.ReserveLottie(_style, _options);
            _reserved = true;
            return this;
        }

        /// <summary>Draws the animation into an explicit rect, consuming no layout space.</summary>
        [NowConsumer]
        public NowLottie Draw(NowRect rect)
        {
            _style = ResolveStyle();
            return NowLayout.DrawLottieAt(_style, rect);
        }

        /// <summary>
        /// Draws the animation. A reserved animation renders into its <see cref="rect"/>;
        /// otherwise this allocates at the current layout position.
        /// </summary>
        [NowConsumer]
        public NowLottie Draw()
        {
            _style = ResolveStyle();

            return _reserved
                ? NowLayout.DrawLottieAt(_style, _rect)
                : NowLayout.DrawLottieAt(_style, NowLayout.ReserveLottie(_style, _options));
        }

        NowLottie ResolveStyle()
        {
            if (string.IsNullOrEmpty(_url))
                return _style;

            var state = NowLottieCache.GetState(_url, out var asset, out _);
            _style.asset = asset;

            if (state == NowLottieCacheState.Loading)
                NowControlState.RequestRepaint();

            return _style;
        }
    }

    /// <summary>
    /// Immediate-mode automatic layout for NowUI, similar in spirit to GUILayout.
    /// Open an area over a screen rect, nest horizontal/vertical groups, and request
    /// rects that are stacked automatically with spacing, padding, stretching and
    /// flexible space. Group calls return a <see cref="NowLayoutScope"/>, so groups
    /// are closed with a using statement, mirroring the NowInput.Begin flow:
    ///
    /// <code>
    /// using (NowLayout.Area(panelRect))
    /// using (NowLayout.Horizontal())
    /// {
    ///     NowLayout.Label("Hello").Draw();
    /// }
    /// </code>
    ///
    /// Sizes that cannot be known up front in a single pass (auto-sized group
    /// extents, stretch shares and flexible space) need a measurement source.
    /// The callback form <see cref="Area(NowRect, Action)"/> runs the UI twice per
    /// frame — a measure pass with draws suppressed and input passive, then the
    /// real pass — so layout is exact every frame, like Unity's IMGUI Layout and
    /// Repaint events. The scope form resolves from the previous frame's
    /// measurements instead: cheaper, but sizes settle one frame after a layout
    /// first appears or animates. Pass explicit ids to areas and groups whose
    /// order or existence changes between frames to keep measurements stable.
    /// </summary>
    public static partial class NowLayout
    {
        struct Group
        {
            public int id;

            public bool horizontal;

            public bool isArea;

            public NowRect rect;

            public Vector4 padding;

            public float spacing;

            public float cursor;

            public float maxCross;

            public NowLayoutAlign alignItems;

            public float fixedMain;

            public float flexTotal;

            public int childCount;

            public int childIndex;

            /// <summary>Set when the parent sized this group's main axis from cached content.</summary>
            public bool parentMainAuto;

            public float parentMainAllocated;

            public float parentMainMin;

            public float parentMainMax;

            /// <summary>Set when this group's cross axis was the default parent-filling stretch.</summary>
            public bool parentCrossAuto;

            public float parentCrossBefore;

            public float parentCrossMin;

            public float parentCrossMax;

            /// <summary>Set when a cached measurement existed for this group at begin;
            /// flex shares read the snapshot below instead of re-probing the cache.</summary>
            public bool hasCache;

            public float cachedFixedMain;

            public float cachedFlexTotal;
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

        static NowText _labelStyle;

        static bool _hasLabelStyle;

        static NowText _defaultLabelStyle;

        static NowThemeAsset _defaultLabelStyleTheme;

        static NowFontAsset _defaultLabelStyleFont;

        static int _defaultLabelStyleVersion;

        static Group[] _groups = new Group[16];

        static int _depth;

        static readonly Dictionary<int, CachedGroup> _cache = new Dictionary<int, CachedGroup>(64);

        static readonly List<int> _removeIds = new List<int>(8);

        static int _frame = int.MinValue;

        static int _areaCounter;

        static double _lastCleanupTime;

        static bool _measurePass;

        public static NowLayoutOptions Width(float width)
        {
            return new NowLayoutOptions().SetWidth(width);
        }

        public static NowLayoutOptions Height(float height)
        {
            return new NowLayoutOptions().SetHeight(height);
        }

        public static NowLayoutOptions Size(float width, float height)
        {
            return new NowLayoutOptions().SetSize(width, height);
        }

        public static NowLayoutOptions StretchWidth(float weight = 1f)
        {
            return new NowLayoutOptions().SetStretchWidth(weight);
        }

        public static NowLayoutOptions StretchHeight(float weight = 1f)
        {
            return new NowLayoutOptions().SetStretchHeight(weight);
        }

        /// <summary>True while the callback form of <see cref="Area(NowRect, Action)"/> runs its measure pass.</summary>
        public static bool isMeasurePass => _measurePass;

        /// <summary>
        /// Opens a root layout area over an absolute rect, laying out children
        /// vertically. Dispose the returned scope (ideally with a using statement)
        /// to close the area. Deferred sizes (auto group extents, stretch shares,
        /// flexible space) resolve from the previous frame; use the callback
        /// overloads for exact same-frame layout.
        /// </summary>
        /// <summary>
        /// Starts a root area at an explicit rect. The common settings are
        /// optional parameters (<c>Area(rect, padding: 16, spacing: 8)</c>);
        /// pass a <see cref="NowLayoutOptions"/> for anything beyond them.
        /// </summary>
        public static NowLayoutScope Area(
            NowRect rect,
            float spacing = 0f,
            float padding = 0f,
            NowLayoutAlign alignItems = NowLayoutAlign.Start)
        {
            return Area(default(NowId), rect, GroupOptions(spacing, padding, alignItems, 0f, 0f, false, false));
        }

        public static NowLayoutScope Area(
            NowRect rect,
            Vector4 padding,
            float spacing = 0f,
            NowLayoutAlign alignItems = NowLayoutAlign.Start)
        {
            return Area(default(NowId), rect, GroupOptions(spacing, padding, alignItems, 0f, 0f, false, false));
        }

        public static NowLayoutScope Area(NowRect rect, in NowLayoutOptions options)
        {
            return Area(default(NowId), rect, options);
        }

        public static NowLayoutScope Area(
            NowId id,
            NowRect rect,
            float spacing = 0f,
            float padding = 0f,
            NowLayoutAlign alignItems = NowLayoutAlign.Start)
        {
            return Area(id, rect, GroupOptions(spacing, padding, alignItems, 0f, 0f, false, false));
        }

        public static NowLayoutScope Area(
            NowId id,
            NowRect rect,
            Vector4 padding,
            float spacing = 0f,
            NowLayoutAlign alignItems = NowLayoutAlign.Start)
        {
            return Area(id, rect, GroupOptions(spacing, padding, alignItems, 0f, 0f, false, false));
        }

        public static NowLayoutScope Area(NowId id, NowRect rect, in NowLayoutOptions options)
        {
            OnFrameBoundary();
            return Area(id.ResolveStableId(HashCombine(AreaSeed, _areaCounter)), rect, options);
        }

        /// <summary>Area keyed by a precomputed identity hash (e.g. <see cref="NowControls.SiteId"/>).</summary>
        public static NowLayoutScope Area(
            int id,
            NowRect rect,
            float spacing = 0f,
            float padding = 0f,
            NowLayoutAlign alignItems = NowLayoutAlign.Start)
        {
            return Area(id, rect, GroupOptions(spacing, padding, alignItems, 0f, 0f, false, false));
        }

        /// <summary>Area keyed by a precomputed identity hash (e.g. <see cref="NowControls.SiteId"/>).</summary>
        public static NowLayoutScope Area(
            int id,
            NowRect rect,
            Vector4 padding,
            float spacing = 0f,
            NowLayoutAlign alignItems = NowLayoutAlign.Start)
        {
            return Area(id, rect, GroupOptions(spacing, padding, alignItems, 0f, 0f, false, false));
        }

        /// <summary>Area keyed by a precomputed identity hash (e.g. <see cref="NowControls.SiteId"/>).</summary>
        public static NowLayoutScope Area(int id, NowRect rect, in NowLayoutOptions options)
        {
            OnFrameBoundary();

            int areaId = id;
            _areaCounter++;

            bool hasCache = _cache.TryGetValue(areaId, out var cached);

            Push(new Group
            {
                id = areaId,
                horizontal = false,
                isArea = true,
                rect = rect,
                padding = options.Has(NowLayoutOptions.Field.Padding) ? options.padding : default,
                spacing = options.Has(NowLayoutOptions.Field.Spacing) ? options.spacing : 0f,
                alignItems = options.Has(NowLayoutOptions.Field.AlignItems) ? options.alignItems : NowLayoutAlign.Start,
                parentMainMax = float.MaxValue,
                hasCache = hasCache,
                cachedFixedMain = cached.fixedMain,
                cachedFlexTotal = cached.flexTotal
            });

            return new NowLayoutScope(NowLayoutScope.Kind.Area, rect);
        }

        /// <summary>
        /// Runs <paramref name="ui"/> inside an area in two passes, like Unity's
        /// IMGUI Layout and Repaint events: first a measure pass with draws
        /// suppressed and input passive, then the real pass using this frame's
        /// measurements — so flexible space, stretching and auto-sized groups are
        /// exact every frame, including the first and while animating. The callback
        /// must not mutate state unconditionally (input is inert during measuring,
        /// so reacting to clicks is always safe); check <see cref="isMeasurePass"/>
        /// when in doubt. A lambda that captures locals allocates a closure every
        /// rebuild — cache the delegate in a field, or pass the captured data
        /// through the <c>Area&lt;TState&gt;</c> overloads with a static lambda.
        /// </summary>
        public static void Area(
            NowRect rect,
            Action ui,
            float spacing = 0f,
            float padding = 0f,
            NowLayoutAlign alignItems = NowLayoutAlign.Start)
        {
            Area(default(NowId), rect, GroupOptions(spacing, padding, alignItems, 0f, 0f, false, false), ui);
        }

        public static void Area(
            NowRect rect,
            Action ui,
            Vector4 padding,
            float spacing = 0f,
            NowLayoutAlign alignItems = NowLayoutAlign.Start)
        {
            Area(default(NowId), rect, GroupOptions(spacing, padding, alignItems, 0f, 0f, false, false), ui);
        }

        public static void Area(NowRect rect, in NowLayoutOptions options, Action ui)
        {
            Area(default(NowId), rect, options, ui);
        }

        public static void Area(
            NowId id,
            NowRect rect,
            Action ui,
            float spacing = 0f,
            float padding = 0f,
            NowLayoutAlign alignItems = NowLayoutAlign.Start)
        {
            Area(id, rect, GroupOptions(spacing, padding, alignItems, 0f, 0f, false, false), ui);
        }

        public static void Area(
            NowId id,
            NowRect rect,
            Action ui,
            Vector4 padding,
            float spacing = 0f,
            NowLayoutAlign alignItems = NowLayoutAlign.Start)
        {
            Area(id, rect, GroupOptions(spacing, padding, alignItems, 0f, 0f, false, false), ui);
        }

        public static void Area(NowId id, NowRect rect, in NowLayoutOptions options, Action ui)
        {
            if (ui == null)
                throw new ArgumentNullException(nameof(ui));

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
        /// Two-pass callback area that threads <paramref name="state"/> into the
        /// callback, so a static lambda can be used and no closure is allocated
        /// per rebuild:
        /// <code>
        /// NowLayout.Area(rect, this, static self => self.DrawContent());
        /// </code>
        /// </summary>
        public static void Area<TState>(
            NowRect rect,
            TState state,
            Action<TState> ui,
            float spacing = 0f,
            float padding = 0f,
            NowLayoutAlign alignItems = NowLayoutAlign.Start)
        {
            Area(default(NowId), rect, GroupOptions(spacing, padding, alignItems, 0f, 0f, false, false), state, ui);
        }

        public static void Area<TState>(
            NowRect rect,
            TState state,
            Action<TState> ui,
            Vector4 padding,
            float spacing = 0f,
            NowLayoutAlign alignItems = NowLayoutAlign.Start)
        {
            Area(default(NowId), rect, GroupOptions(spacing, padding, alignItems, 0f, 0f, false, false), state, ui);
        }

        public static void Area<TState>(NowRect rect, in NowLayoutOptions options, TState state, Action<TState> ui)
        {
            Area(default(NowId), rect, options, state, ui);
        }

        public static void Area<TState>(
            NowId id,
            NowRect rect,
            TState state,
            Action<TState> ui,
            float spacing = 0f,
            float padding = 0f,
            NowLayoutAlign alignItems = NowLayoutAlign.Start)
        {
            Area(id, rect, GroupOptions(spacing, padding, alignItems, 0f, 0f, false, false), state, ui);
        }

        public static void Area<TState>(
            NowId id,
            NowRect rect,
            TState state,
            Action<TState> ui,
            Vector4 padding,
            float spacing = 0f,
            NowLayoutAlign alignItems = NowLayoutAlign.Start)
        {
            Area(id, rect, GroupOptions(spacing, padding, alignItems, 0f, 0f, false, false), state, ui);
        }

        public static void Area<TState>(NowId id, NowRect rect, in NowLayoutOptions options, TState state, Action<TState> ui)
        {
            if (ui == null)
                throw new ArgumentNullException(nameof(ui));

            if (_measurePass)
            {
                using (Area(id, rect, options))
                    ui(state);

                return;
            }

            int areaCounter = BeginMeasurePass();

            try
            {
                using (Area(id, rect, options))
                    ui(state);
            }
            finally
            {
                EndMeasurePass(areaCounter);
            }

            using (Area(id, rect, options))
                ui(state);
        }

        /// <summary>
        /// Enters measure mode: draws suppressed, input passive, layout calls record
        /// this frame's sizes. Run the UI once, call <see cref="EndMeasurePass"/> with
        /// the returned snapshot, then run the same UI again for real. Used by the
        /// callback Area overloads and by hosts that own a draw entry point (e.g.
        /// NowGraphic running DrawNowUI twice).
        /// </summary>
        /// <remarks>Resolves the frame boundary before snapshotting so the counter rewind
        /// hands the real pass the same anonymous area ids the measure pass used (the
        /// boundary would otherwise reset the counter mid-measure).</remarks>
        internal static int BeginMeasurePass()
        {
            OnFrameBoundary();

            int areaCounter = _areaCounter;
            _measurePass = true;
            Now.BeginSuppressDraw();
            NowInput.BeginPassive();
            return areaCounter;
        }

        /// <summary>Exits measure mode and rewinds the anonymous area counter: ids are
        /// sequential per frame, so the real pass resolves the same ids (and therefore
        /// the fresh measurements).</summary>
        internal static void EndMeasurePass(int areaCounterSnapshot)
        {
            NowInput.EndPassive();
            Now.EndSuppressDraw();
            _measurePass = false;

            _areaCounter = areaCounterSnapshot;
        }

        public static void EndArea()
        {
            if (_depth == 0)
                throw new InvalidOperationException("EndArea called without a matching Area.");

            ref var group = ref Top();

            if (!group.isArea)
                throw new InvalidOperationException("EndArea called while a layout group is still open.");

            StoreCache(ref group, out float contentWidth, out float contentHeight);

            if (_trackContent)
            {
                _trackedContent.x = Mathf.Max(_trackedContent.x, group.rect.x + contentWidth);
                _trackedContent.y = Mathf.Max(_trackedContent.y, group.rect.y + contentHeight);
            }

            _depth--;
            CleanupCache();
        }

        static bool _trackContent;

        static Vector2 _trackedContent;

        /// <summary>
        /// Starts accumulating the content extent of root areas (area origin +
        /// measured content, the union across all areas ended while tracking).
        /// Hosts use this to learn their preferred size — frame-late, like all
        /// layout measurement.
        /// </summary>
        internal static void BeginContentTracking()
        {
            _trackContent = true;
            _trackedContent = default;
        }

        internal static Vector2 EndContentTracking()
        {
            _trackContent = false;
            return _trackedContent;
        }

        /// <summary>
        /// Starts a horizontal group. The common settings are optional parameters
        /// (<c>Horizontal(spacing: 8, alignItems: NowLayoutAlign.Center)</c>);
        /// pass a <see cref="NowLayoutOptions"/> for anything beyond them.
        /// </summary>
        public static NowLayoutScope Horizontal(
            float spacing = 0f,
            float padding = 0f,
            NowLayoutAlign alignItems = NowLayoutAlign.Start,
            float width = 0f,
            float height = 0f,
            bool stretchWidth = false,
            bool stretchHeight = false)
        {
            return BeginGroup(true, default, GroupOptions(spacing, padding, alignItems, width, height, stretchWidth, stretchHeight));
        }

        public static NowLayoutScope Horizontal(
            Vector4 padding,
            float spacing = 0f,
            NowLayoutAlign alignItems = NowLayoutAlign.Start,
            float width = 0f,
            float height = 0f,
            bool stretchWidth = false,
            bool stretchHeight = false)
        {
            return BeginGroup(true, default, GroupOptions(spacing, padding, alignItems, width, height, stretchWidth, stretchHeight));
        }

        public static NowLayoutScope Horizontal(in NowLayoutOptions options)
        {
            return BeginGroup(true, default, options);
        }

        public static NowLayoutScope Horizontal(
            NowId id,
            float spacing = 0f,
            float padding = 0f,
            NowLayoutAlign alignItems = NowLayoutAlign.Start,
            float width = 0f,
            float height = 0f,
            bool stretchWidth = false,
            bool stretchHeight = false)
        {
            return BeginGroup(true, id, GroupOptions(spacing, padding, alignItems, width, height, stretchWidth, stretchHeight));
        }

        public static NowLayoutScope Horizontal(
            NowId id,
            Vector4 padding,
            float spacing = 0f,
            NowLayoutAlign alignItems = NowLayoutAlign.Start,
            float width = 0f,
            float height = 0f,
            bool stretchWidth = false,
            bool stretchHeight = false)
        {
            return BeginGroup(true, id, GroupOptions(spacing, padding, alignItems, width, height, stretchWidth, stretchHeight));
        }

        public static NowLayoutScope Horizontal(NowId id, in NowLayoutOptions options)
        {
            return BeginGroup(true, id, options);
        }

        static NowLayoutOptions GroupOptions(
            float spacing, float padding, NowLayoutAlign alignItems,
            float width, float height, bool stretchWidth, bool stretchHeight)
        {
            var options = default(NowLayoutOptions);

            if (spacing != 0f)
                options = options.SetSpacing(spacing);

            if (padding != 0f)
                options = options.SetPadding(padding);

            if (alignItems != NowLayoutAlign.Start)
                options = options.SetAlignItems(alignItems);

            if (width > 0f)
                options = options.SetWidth(width);

            if (height > 0f)
                options = options.SetHeight(height);

            if (stretchWidth)
                options = options.SetStretchWidth();

            if (stretchHeight)
                options = options.SetStretchHeight();

            return options;
        }

        static NowLayoutOptions GroupOptions(
            float spacing, Vector4 padding, NowLayoutAlign alignItems,
            float width, float height, bool stretchWidth, bool stretchHeight)
        {
            return GroupOptions(spacing, 0f, alignItems, width, height, stretchWidth, stretchHeight)
                .SetPadding(padding);
        }

        public static void EndHorizontal()
        {
            EndGroup(true);
        }

        /// <summary>
        /// Starts a vertical group. The common settings are optional parameters
        /// (<c>Vertical(spacing: 8, padding: 16)</c>); pass a
        /// <see cref="NowLayoutOptions"/> for anything beyond them.
        /// </summary>
        public static NowLayoutScope Vertical(
            float spacing = 0f,
            float padding = 0f,
            NowLayoutAlign alignItems = NowLayoutAlign.Start,
            float width = 0f,
            float height = 0f,
            bool stretchWidth = false,
            bool stretchHeight = false)
        {
            return BeginGroup(false, default, GroupOptions(spacing, padding, alignItems, width, height, stretchWidth, stretchHeight));
        }

        public static NowLayoutScope Vertical(
            Vector4 padding,
            float spacing = 0f,
            NowLayoutAlign alignItems = NowLayoutAlign.Start,
            float width = 0f,
            float height = 0f,
            bool stretchWidth = false,
            bool stretchHeight = false)
        {
            return BeginGroup(false, default, GroupOptions(spacing, padding, alignItems, width, height, stretchWidth, stretchHeight));
        }

        public static NowLayoutScope Vertical(in NowLayoutOptions options)
        {
            return BeginGroup(false, default, options);
        }

        public static NowLayoutScope Vertical(
            NowId id,
            float spacing = 0f,
            float padding = 0f,
            NowLayoutAlign alignItems = NowLayoutAlign.Start,
            float width = 0f,
            float height = 0f,
            bool stretchWidth = false,
            bool stretchHeight = false)
        {
            return BeginGroup(false, id, GroupOptions(spacing, padding, alignItems, width, height, stretchWidth, stretchHeight));
        }

        public static NowLayoutScope Vertical(
            NowId id,
            Vector4 padding,
            float spacing = 0f,
            NowLayoutAlign alignItems = NowLayoutAlign.Start,
            float width = 0f,
            float height = 0f,
            bool stretchWidth = false,
            bool stretchHeight = false)
        {
            return BeginGroup(false, id, GroupOptions(spacing, padding, alignItems, width, height, stretchWidth, stretchHeight));
        }

        public static NowLayoutScope Vertical(NowId id, in NowLayoutOptions options)
        {
            return BeginGroup(false, id, options);
        }

        public static void EndVertical()
        {
            EndGroup(false);
        }

        /// <summary>
        /// Reserves a rect at the current layout position. The common settings
        /// are optional parameters (<c>Rect(height: 22, stretchWidth: true)</c>);
        /// pass a <see cref="NowLayoutOptions"/> for anything beyond them.
        /// </summary>
        public static NowRect Rect(
            float width = 0f,
            float height = 0f,
            bool stretchWidth = false,
            bool stretchHeight = false,
            NowLayoutAlign align = NowLayoutAlign.Start)
        {
            var options = default(NowLayoutOptions);

            if (width > 0f)
                options = options.SetWidth(width);

            if (height > 0f)
                options = options.SetHeight(height);

            if (stretchWidth)
                options = options.SetStretchWidth();

            if (stretchHeight)
                options = options.SetStretchHeight();

            if (align != NowLayoutAlign.Start)
                options = options.SetAlign(align);

            return Rect(options);
        }

        /// <summary>Reserves a rect sized by <paramref name="options"/> at the current layout position.</summary>
        public static NowRect Rect(in NowLayoutOptions options)
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
        /// explicit style. Defaults to the active theme's body text at a 16px font size.
        /// The default is cached against the resolved theme instance, the ambient font
        /// and <see cref="NowThemeAsset.contentVersion"/>, so per-label calls skip the
        /// full preset resolution. Assigning the setter installs a process-wide
        /// default that stops tracking the theme until <see cref="ClearLabelStyle"/>
        /// restores it; for a temporary override use
        /// <see cref="OverrideLabelStyle(NowText)"/> with a using statement.
        /// </summary>
        public static NowText labelStyle
        {
            get
            {
                if (_hasLabelStyle)
                    return _labelStyle;

                var theme = NowTheme.themeAsset;
                var font = Now.font;

                if (!ReferenceEquals(theme, _defaultLabelStyleTheme) ||
                    !ReferenceEquals(font, _defaultLabelStyleFont) ||
                    _defaultLabelStyleVersion != NowThemeAsset.contentVersion)
                {
                    _defaultLabelStyle = theme.ResolveText(NowTextStyle.Body).SetFontSize(DefaultLabelFontSize);
                    _defaultLabelStyleTheme = theme;
                    _defaultLabelStyleFont = font;
                    _defaultLabelStyleVersion = NowThemeAsset.contentVersion;
                }

                return _defaultLabelStyle;
            }
            set
            {
                _labelStyle = value;
                _hasLabelStyle = true;
            }
        }

        /// <summary>
        /// Removes a <see cref="labelStyle"/> override so implicit labels resume
        /// tracking the active theme's body text style.
        /// </summary>
        public static void ClearLabelStyle()
        {
            _hasLabelStyle = false;
            _labelStyle = default;
        }

        /// <summary>
        /// Temporarily overrides <see cref="labelStyle"/>; dispose the returned
        /// scope (ideally with a using statement) to restore whatever was active
        /// before:
        /// <code>
        /// using (NowLayout.OverrideLabelStyle(heading))
        ///     NowLayout.Label("Section").Draw();
        /// </code>
        /// </summary>
        public static NowLabelStyleScope OverrideLabelStyle(NowText style)
        {
            var scope = new NowLabelStyleScope(_labelStyle, _hasLabelStyle);
            _labelStyle = style;
            _hasLabelStyle = true;
            return scope;
        }

        internal static void RestoreLabelStyle(NowText style, bool hasStyle)
        {
            _labelStyle = style;
            _hasLabelStyle = hasStyle;
        }

        /// <summary>
        /// Starts a text label builder using <see cref="labelStyle"/>. Like the other
        /// NowUI builders, nothing is measured, placed or drawn until
        /// <see cref="NowLabel.Draw"/> is called:
        /// <c>NowLayout.Label("Title").SetFontSize(24).Draw();</c>
        /// </summary>
        public static NowLabel Label(string value)
        {
            return new NowLabel(labelStyle, value, default);
        }

        public static NowLabel Label(string value, NowLayoutOptions options)
        {
            return new NowLabel(labelStyle, value, options);
        }

        public static NowLabel Label(string value, float fontSize, NowLayoutOptions options = default)
        {
            return new NowLabel(labelStyle.SetFontSize(fontSize), value, options);
        }

        public static NowLabel Label(string value, float fontSize, Color color, NowLayoutOptions options = default)
        {
            return new NowLabel(labelStyle.SetFontSize(fontSize).SetColor(color), value, options);
        }

        public static NowLabel Label(NowText style, string value)
        {
            return new NowLabel(style, value, default);
        }

        public static NowLabel Label(NowText style, string value, NowLayoutOptions options)
        {
            return new NowLabel(style, value, options);
        }

        /// <summary>
        /// Starts a Lottie builder sized to the animation's native dimensions;
        /// nothing is allocated or drawn until <see cref="NowLottieBuilder.Draw()"/>:
        /// <c>NowLayout.Lottie(spinner).SetHeight(32).Draw();</c>
        /// </summary>
        public static NowLottieBuilder Lottie(NowLottieAsset asset)
        {
            return new NowLottieBuilder(Now.Lottie(default, asset), default);
        }

        public static NowLottieBuilder Lottie(NowLottieAsset asset, NowLayoutOptions options)
        {
            return new NowLottieBuilder(Now.Lottie(default, asset), options);
        }

        /// <summary>
        /// Starts a Lottie builder backed by an http/https URL. The URL is downloaded
        /// once through <see cref="NowLottieCache"/> and reused on subsequent draws.
        /// </summary>
        public static NowLottieBuilder Lottie(ReadOnlySpan<char> url)
        {
            return Lottie(url, default);
        }

        public static NowLottieBuilder Lottie(ReadOnlySpan<char> url, NowLayoutOptions options)
        {
            string key = ResolveLottieUrl(url);
            var asset = NowLottieCache.GetAsset(key);
            return new NowLottieBuilder(Now.Lottie(default, asset), options, key);
        }

        static readonly Dictionary<int, string> _lottieUrlCache = new Dictionary<int, string>(16);

        /// <summary>
        /// Resolves a URL span to its cached string via a content hash, so repeated
        /// span draws of the same URL never materialize a new string.
        /// </summary>
        static string ResolveLottieUrl(ReadOnlySpan<char> url)
        {
            int hash = HashChars(url);

            if (_lottieUrlCache.TryGetValue(hash, out var cached) && url.SequenceEqual(cached))
                return cached;

            string key = url.ToString();
            _lottieUrlCache[hash] = key;
            return key;
        }

        static int HashChars(ReadOnlySpan<char> value)
        {
            unchecked
            {
                int hash = (int)2166136261;

                for (int i = 0; i < value.Length; ++i)
                    hash = (hash ^ value[i]) * 16777619;

                return hash;
            }
        }

        /// <summary>Allocates layout space for an animation without drawing it.</summary>
        internal static NowRect ReserveLottie(in NowLottie style, in NowLayoutOptions options)
        {
            ref var group = ref RequireGroup();
            return Allocate(ref group, options, LottieAutoSize(style, options), false, out _, out _);
        }

        /// <summary>Draws an animation into an already-reserved rect, consuming no layout space.</summary>
        internal static NowLottie DrawLottieAt(NowLottie style, NowRect rect)
        {
            return style
                .SetPosition(rect)
                .SetMask(rect)
                .Draw();
        }

        /// <summary>
        /// Native animation size; with exactly one fixed dimension the other is
        /// derived from the animation's aspect ratio.
        /// </summary>
        internal static Vector2 LottieAutoSize(in NowLottie style, in NowLayoutOptions options)
        {
            var asset = style.asset;

            if (asset == null || asset.width <= 0f || asset.height <= 0f)
                return default;

            bool hasWidth = options.Has(NowLayoutOptions.Field.Width);
            bool hasHeight = options.Has(NowLayoutOptions.Field.Height);

            if (hasWidth && !hasHeight)
                return new Vector2(asset.width, options.width * asset.height / asset.width);

            if (hasHeight && !hasWidth)
                return new Vector2(options.height * asset.width / asset.height, asset.height);

            return new Vector2(asset.width, asset.height);
        }

        /// <summary>Measures, allocates and draws a label at the current layout position.</summary>
        internal static NowText PlaceLabel(NowText style, string value, in NowLayoutOptions options)
        {
            var rect = ReserveLabel(style, value, options);
            return DrawLabelAt(style, value, rect);
        }

        /// <summary>Allocates layout space for a label without drawing it.</summary>
        internal static NowRect ReserveLabel(NowText style, string value, in NowLayoutOptions options)
        {
            ref var group = ref RequireGroup();
            var measured = style.Measure(value);
            return Allocate(ref group, options, measured, false, out _, out _);
        }

        /// <summary>Draws a label into an already-reserved rect, consuming no layout space.</summary>
        internal static NowText DrawLabelAt(NowText style, string value, NowRect rect)
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
            _defaultLabelStyle = default;
            _defaultLabelStyleTheme = null;
            _defaultLabelStyleFont = null;
            _defaultLabelStyleVersion = 0;
            _measurePass = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForRuntimeLoad()
        {
            Reset();
        }

        static NowLayoutScope BeginGroup(bool horizontal, NowId id, in NowLayoutOptions options)
        {
            ref var parent = ref RequireGroup("Layout groups require an open area. Call NowLayout.Area first.");

            int groupId = id.hasValue
                ? HashCombine(parent.id, id.ResolveStableId(1))
                : HashCombine(parent.id, parent.childIndex);
            parent.childIndex++;

            Vector2 autoSize = default;
            bool hasCache = _cache.TryGetValue(groupId, out var cached);

            if (hasCache)
                autoSize = new Vector2(cached.contentWidth, cached.contentHeight);

            bool mainIsWidth = parent.horizontal;
            bool crossAuto =
                !options.Has(mainIsWidth ? NowLayoutOptions.Field.Height : NowLayoutOptions.Field.Width) &&
                !options.Has(mainIsWidth ? NowLayoutOptions.Field.StretchHeight : NowLayoutOptions.Field.StretchWidth);
            float crossBefore = parent.maxCross;

            var rect = Allocate(ref parent, options, autoSize, true, out bool mainAuto, out float mainAllocated);

            Push(new Group
            {
                id = groupId,
                horizontal = horizontal,
                rect = rect,
                padding = options.Has(NowLayoutOptions.Field.Padding) ? options.padding : default,
                spacing = options.Has(NowLayoutOptions.Field.Spacing) ? options.spacing : 0f,
                alignItems = options.Has(NowLayoutOptions.Field.AlignItems) ? options.alignItems : NowLayoutAlign.Start,
                parentMainAuto = mainAuto,
                parentMainAllocated = mainAllocated,
                parentMainMin = options.Has(mainIsWidth ? NowLayoutOptions.Field.MinWidth : NowLayoutOptions.Field.MinHeight)
                    ? (mainIsWidth ? options.minWidth : options.minHeight)
                    : 0f,
                parentMainMax = options.Has(mainIsWidth ? NowLayoutOptions.Field.MaxWidth : NowLayoutOptions.Field.MaxHeight)
                    ? (mainIsWidth ? options.maxWidth : options.maxHeight)
                    : float.MaxValue,
                parentCrossAuto = crossAuto,
                parentCrossBefore = crossBefore,
                parentCrossMin = options.Has(mainIsWidth ? NowLayoutOptions.Field.MinHeight : NowLayoutOptions.Field.MinWidth)
                    ? (mainIsWidth ? options.minHeight : options.minWidth)
                    : 0f,
                parentCrossMax = options.Has(mainIsWidth ? NowLayoutOptions.Field.MaxHeight : NowLayoutOptions.Field.MaxWidth)
                    ? (mainIsWidth ? options.maxHeight : options.maxWidth)
                    : float.MaxValue,
                hasCache = hasCache,
                cachedFixedMain = cached.fixedMain,
                cachedFlexTotal = cached.flexTotal
            });

            return new NowLayoutScope(
                horizontal ? NowLayoutScope.Kind.Horizontal : NowLayoutScope.Kind.Vertical,
                rect);
        }

        /// <summary>Closes the current group. When the parent sized this group from stale
        /// cached content, retro-corrects the parent's cursor so siblings placed after the
        /// group stack correctly on the very first frame.</summary>
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

            if (_depth == 0)
                return;

            ref var parent = ref Top();

            if (ended.parentMainAuto)
            {
                float actualMain = parent.horizontal ? contentWidth : contentHeight;
                actualMain = Mathf.Clamp(actualMain, ended.parentMainMin, ended.parentMainMax);
                float delta = actualMain - ended.parentMainAllocated;

                if (delta != 0f)
                {
                    parent.cursor += delta;
                    parent.fixedMain += delta;
                }
            }

            // A default-stretched cross axis fills whatever the parent allocated, so
            // measuring that allocation back into the parent would make an auto-sized
            // parent's cached size a fixed point of itself — it could never grow toward
            // (or shrink back to) the real content. Measure the group's actual content
            // extent instead.
            if (ended.parentCrossAuto)
            {
                float actualCross = parent.horizontal ? contentHeight : contentWidth;
                actualCross = Mathf.Clamp(actualCross, ended.parentCrossMin, ended.parentCrossMax);
                parent.maxCross = Mathf.Max(ended.parentCrossBefore, actualCross);
            }
        }

        static void StoreCache(ref Group group)
        {
            StoreCache(ref group, out _, out _);
        }

        /// <summary>
        /// Last measured content size of an explicit-id group — how scroll views
        /// learn their content extent (one frame late, like all layout measures).
        /// </summary>
        /// <summary>
        /// Reserves a stretch-width rect for content whose height is only known
        /// after drawing — the standard frame-late pattern for expensive layout
        /// (markdown documents, wrapped text):
        /// <code>
        /// var content = NowLayout.ContentRect();
        /// float height = DrawMyContent(content.rect);
        /// content.End(height);
        /// </code>
        /// The last reported height is stored per call site (loops are salted by
        /// per-frame occurrence, like control identity), so the caller manages no
        /// state; <see cref="NowContentRect.End"/> requests a repaint while the
        /// measurement is still converging so retained hosts settle within a few
        /// frames.
        /// </summary>
        public static NowContentRect ContentRect(
            [System.Runtime.CompilerServices.CallerFilePath] string file = "",
            [System.Runtime.CompilerServices.CallerLineNumber] int line = 0)
        {
            return ContentRect(default, file, line);
        }

        public static NowContentRect ContentRect(
            NowLayoutOptions options,
            [System.Runtime.CompilerServices.CallerFilePath] string file = "",
            [System.Runtime.CompilerServices.CallerLineNumber] int line = 0)
        {
            int slot = NowControls.GetControlId(NowControls.SiteId(file, line));
            float lastHeight = NowControlState.Get<float>(slot);

            if (!options.Has(NowLayoutOptions.Field.Width) && !options.Has(NowLayoutOptions.Field.StretchWidth))
                options = options.SetStretchWidth();

            var rect = Rect(options.SetHeight(Mathf.Max(lastHeight, 1f)));
            return new NowContentRect(rect, slot);
        }

        internal static bool TryGetCachedContentSize(string id, out Vector2 size)
        {
            if (id != null)
                return TryGetCachedContentSize(NowInput.GetId(id), out size);

            size = default;
            return false;
        }

        internal static bool TryGetCachedContentSize(NowId id, out Vector2 size)
        {
            if (id.hasValue)
                return TryGetCachedContentSize(id.ResolveStableId(1), out size);

            size = default;
            return false;
        }

        internal static bool TryGetCachedContentSize(int id, out Vector2 size)
        {
            if (_cache.TryGetValue(id, out var cached))
            {
                size = new Vector2(cached.contentWidth, cached.contentHeight);
                return true;
            }

            size = default;
            return false;
        }

        /// <summary>Caches the group's content size, requesting repaints while measurements
        /// are still changing: deferred sizing settles over a few frames, and retained hosts
        /// (UGUI graphics) only rebuild when asked.</summary>
        static void StoreCache(ref Group group, out float contentWidth, out float contentHeight)
        {
            float contentMain = group.cursor;
            float contentCross = group.maxCross;

            contentWidth = (group.horizontal ? contentMain : contentCross) + group.padding.x + group.padding.z;
            contentHeight = (group.horizontal ? contentCross : contentMain) + group.padding.y + group.padding.w;

            if (!_cache.TryGetValue(group.id, out var previous) ||
                Mathf.Abs(previous.contentWidth - contentWidth) > 0.25f ||
                Mathf.Abs(previous.contentHeight - contentHeight) > 0.25f)
            {
                NowControlState.RequestRepaint();
            }

            _cache[group.id] = new CachedGroup
            {
                contentWidth = contentWidth,
                contentHeight = contentHeight,
                fixedMain = group.fixedMain,
                flexTotal = group.flexTotal,
                lastUsed = NowTime.realtimeSinceStartup
            };
        }

        static NowRect Allocate(
            ref Group group,
            in NowLayoutOptions options,
            Vector2 autoSize,
            bool stretchCrossByDefault,
            out bool mainAuto,
            out float mainAllocated)
        {
            float contentX = group.rect.x + group.padding.x;
            float contentY = group.rect.y + group.padding.y;
            float contentWidth = group.rect.width - group.padding.x - group.padding.z;
            float contentHeight = group.rect.height - group.padding.y - group.padding.w;

            bool mainIsWidth = group.horizontal;

            ResolveAxis(
                ref group,
                mainIsWidth,
                options.Has(NowLayoutOptions.Field.Width),
                options.width,
                options.Has(NowLayoutOptions.Field.StretchWidth),
                options.stretchWidth,
                options.Has(NowLayoutOptions.Field.MinWidth),
                options.minWidth,
                options.Has(NowLayoutOptions.Field.MaxWidth),
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
                options.Has(NowLayoutOptions.Field.Height),
                options.height,
                options.Has(NowLayoutOptions.Field.StretchHeight),
                options.stretchHeight,
                options.Has(NowLayoutOptions.Field.MinHeight),
                options.minHeight,
                options.Has(NowLayoutOptions.Field.MaxHeight),
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
            NowLayoutAlign align = options.Has(NowLayoutOptions.Field.Align) ? options.align : group.alignItems;
            float alignFactor = align switch
            {
                NowLayoutAlign.Center => 0.5f,
                NowLayoutAlign.End => 1f,
                _ => 0f
            };

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
        static NowRect LabelMask(in NowText style, string value, NowRect rect)
        {
            var mask = rect;

            if (style.font != null && !string.IsNullOrEmpty(value))
            {
                var bounds = style.MeasureBounds(value);

                if (bounds is { z: > 0f, w: > 0f })
                    mask = mask.Union(new NowRect(rect.x + bounds.x, rect.y + bounds.y, bounds.z, bounds.w));
            }

            return mask.Outset(4f);
        }

        static float FlexShare(ref Group group, float weight)
        {
            if (!group.hasCache || group.cachedFlexTotal <= 0f)
                return 0f;

            float avail = group.horizontal
                ? group.rect.width - group.padding.x - group.padding.z
                : group.rect.height - group.padding.y - group.padding.w;

            float remaining = avail - group.cachedFixedMain;
            return remaining > 0f ? remaining * weight / group.cachedFlexTotal : 0f;
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
                    message ?? "Layout calls require an open area. Call NowLayout.Area first.");
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
                Debug.LogError("NowLayout: unbalanced Begin/End calls detected from a previous frame. Stack cleared.");
                _depth = 0;
            }
        }

        static void CleanupCache()
        {
            double now = NowTime.realtimeSinceStartup;

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

        static int HashCombine(int a, int b)
        {
            unchecked
            {
                return (a * 397) ^ b;
            }
        }
    }

    /// <summary>
    /// Scope returned by <see cref="NowLayout.OverrideLabelStyle(NowText)"/>;
    /// disposing restores the label style that was active when the scope opened.
    /// </summary>
    [NowScope]
    public struct NowLabelStyleScope : IDisposable
    {
        readonly NowText _previous;

        readonly bool _hadPrevious;

        bool _active;

        internal NowLabelStyleScope(NowText previous, bool hadPrevious)
        {
            _previous = previous;
            _hadPrevious = hadPrevious;
            _active = true;
        }

        public void Dispose()
        {
            if (!_active)
                return;

            _active = false;
            NowLayout.RestoreLabelStyle(_previous, _hadPrevious);
        }
    }
}
