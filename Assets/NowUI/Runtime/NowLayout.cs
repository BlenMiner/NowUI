using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace NowUI
{
    public enum NowLayoutAlign
    {
        Start,
        Center,
        End
    }

    /// <summary>Main-axis placement of children inside a layout container.</summary>
    public enum NowLayoutJustify
    {
        Start,
        Center,
        End,
        SpaceBetween
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
            AlignItems = 1 << 11,
            Grow = 1 << 12,
            Justify = 1 << 13
        }

        float _width;

        float _height;

        float _minWidth;

        float _maxWidth;

        float _minHeight;

        float _maxHeight;

        float _stretchWidth;

        float _stretchHeight;

        float _spacing;

        Vector4 _padding;

        NowLayoutAlign _align;

        NowLayoutAlign _alignItems;

        float _grow;

        NowLayoutJustify _justify;

        /// <summary>Configured fixed width; meaningful only after <see cref="SetWidth"/>.</summary>
        public readonly float width => _width;

        /// <summary>Configured fixed height; meaningful only after <see cref="SetHeight"/>.</summary>
        public readonly float height => _height;

        public readonly float minWidth => _minWidth;

        public readonly float maxWidth => _maxWidth;

        public readonly float minHeight => _minHeight;

        public readonly float maxHeight => _maxHeight;

        public readonly float stretchWidth => _stretchWidth;

        public readonly float stretchHeight => _stretchHeight;

        public readonly float spacing => _spacing;

        public readonly Vector4 padding => _padding;

        public readonly NowLayoutAlign align => _align;

        public readonly NowLayoutAlign alignItems => _alignItems;

        public readonly float grow => _grow;

        public readonly NowLayoutJustify justify => _justify;

        internal Field fields;

        internal readonly bool Has(Field field)
        {
            return (fields & field) != 0;
        }

        public NowLayoutOptions SetWidth(float width)
        {
            RequireNonNegativeFinite(width, nameof(width));

            if (Has(Field.StretchWidth))
                throw new InvalidOperationException("A layout width cannot be both fixed and stretching. Choose SetWidth or SetStretchWidth.");

            _width = width;
            fields |= Field.Width;
            return this;
        }

        public NowLayoutOptions SetHeight(float height)
        {
            RequireNonNegativeFinite(height, nameof(height));

            if (Has(Field.StretchHeight))
                throw new InvalidOperationException("A layout height cannot be both fixed and stretching. Choose SetHeight or SetStretchHeight.");

            _height = height;
            fields |= Field.Height;
            return this;
        }

        public NowLayoutOptions SetSize(float width, float height)
        {
            return SetWidth(width).SetHeight(height);
        }

        public NowLayoutOptions SetMinWidth(float minWidth)
        {
            RequireNonNegativeFinite(minWidth, nameof(minWidth));

            if (Has(Field.MaxWidth) && minWidth > _maxWidth)
                throw new ArgumentException("minWidth cannot exceed the configured maxWidth.", nameof(minWidth));

            _minWidth = minWidth;
            fields |= Field.MinWidth;
            return this;
        }

        public NowLayoutOptions SetMaxWidth(float maxWidth)
        {
            RequireNonNegativeFinite(maxWidth, nameof(maxWidth));

            if (Has(Field.MinWidth) && maxWidth < _minWidth)
                throw new ArgumentException("maxWidth cannot be less than the configured minWidth.", nameof(maxWidth));

            _maxWidth = maxWidth;
            fields |= Field.MaxWidth;
            return this;
        }

        public NowLayoutOptions SetMinHeight(float minHeight)
        {
            RequireNonNegativeFinite(minHeight, nameof(minHeight));

            if (Has(Field.MaxHeight) && minHeight > _maxHeight)
                throw new ArgumentException("minHeight cannot exceed the configured maxHeight.", nameof(minHeight));

            _minHeight = minHeight;
            fields |= Field.MinHeight;
            return this;
        }

        public NowLayoutOptions SetMaxHeight(float maxHeight)
        {
            RequireNonNegativeFinite(maxHeight, nameof(maxHeight));

            if (Has(Field.MinHeight) && maxHeight < _minHeight)
                throw new ArgumentException("maxHeight cannot be less than the configured minHeight.", nameof(maxHeight));

            _maxHeight = maxHeight;
            fields |= Field.MaxHeight;
            return this;
        }

        /// <summary>
        /// Inside a horizontal group the element takes a weighted share of the remaining
        /// width; inside a vertical group it fills the available width.
        /// </summary>
        public NowLayoutOptions SetStretchWidth(float weight = 1f)
        {
            RequirePositiveFinite(weight, nameof(weight));

            if (Has(Field.Width))
                throw new InvalidOperationException("A layout width cannot be both fixed and stretching. Choose SetWidth or SetStretchWidth.");

            _stretchWidth = weight;
            fields |= Field.StretchWidth;
            return this;
        }

        /// <summary>
        /// Inside a vertical group the element takes a weighted share of the remaining
        /// height; inside a horizontal group it fills the available height.
        /// </summary>
        public NowLayoutOptions SetStretchHeight(float weight = 1f)
        {
            RequirePositiveFinite(weight, nameof(weight));

            if (Has(Field.Height))
                throw new InvalidOperationException("A layout height cannot be both fixed and stretching. Choose SetHeight or SetStretchHeight.");

            _stretchHeight = weight;
            fields |= Field.StretchHeight;
            return this;
        }

        /// <summary>
        /// Takes a weighted share of the parent's remaining main-axis space. Unlike
        /// SetStretchWidth/Height, this follows the parent direction, so the same
        /// declaration works when a container moves between a row and a column.
        /// </summary>
        public NowLayoutOptions SetGrow(float weight = 1f)
        {
            RequirePositiveFinite(weight, nameof(weight));
            _grow = weight;
            fields |= Field.Grow;
            return this;
        }

        /// <summary>Gap inserted between consecutive children. Only used by groups.</summary>
        public NowLayoutOptions SetSpacing(float spacing)
        {
            RequireFinite(spacing, nameof(spacing));
            _spacing = spacing;
            fields |= Field.Spacing;
            return this;
        }

        /// <summary>Inner padding as (left, top, right, bottom). Only used by groups.</summary>
        public NowLayoutOptions SetPadding(Vector4 padding)
        {
            RequireFinite(padding.x, nameof(padding));
            RequireFinite(padding.y, nameof(padding));
            RequireFinite(padding.z, nameof(padding));
            RequireFinite(padding.w, nameof(padding));
            _padding = padding;
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
            RequireAlign(align, nameof(align));
            _align = align;
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
            RequireAlign(align, nameof(align));
            _alignItems = align;
            fields |= Field.AlignItems;
            return this;
        }

        /// <summary>Places this container's children along its main axis.</summary>
        public NowLayoutOptions SetJustify(NowLayoutJustify justify)
        {
            RequireJustify(justify, nameof(justify));
            _justify = justify;
            fields |= Field.Justify;
            return this;
        }

        internal static void RequireNonNegativeFinite(float value, string paramName)
        {
            if (value < 0f || float.IsNaN(value) || float.IsInfinity(value))
                throw new ArgumentOutOfRangeException(paramName, "Layout sizes must be non-negative finite values.");
        }

        internal static void RequirePositiveFinite(float value, string paramName)
        {
            if (value <= 0f || float.IsNaN(value) || float.IsInfinity(value))
                throw new ArgumentOutOfRangeException(paramName, "Stretch weights must be positive finite values.");
        }

        internal static void RequireFinite(float value, string paramName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                throw new ArgumentOutOfRangeException(paramName, "Layout values must be finite.");
        }

        internal static void RequireAlign(NowLayoutAlign align, string paramName)
        {
            if ((uint)align > (uint)NowLayoutAlign.End)
                throw new ArgumentOutOfRangeException(paramName, align, "Unknown layout alignment.");
        }

        internal static void RequireJustify(NowLayoutJustify justify, string paramName)
        {
            if ((uint)justify > (uint)NowLayoutJustify.SpaceBetween)
                throw new ArgumentOutOfRangeException(paramName, justify, "Unknown main-axis justification.");
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
    [NowScope]
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

        int _token;

        internal NowLayoutScope(Kind kind, NowRect rect, int token)
        {
            _kind = kind;
            this.rect = rect;
            _token = token;
        }

        public float x => rect.x;

        public float y => rect.y;

        public float width => rect.width;

        public float height => rect.height;

        public void Dispose()
        {
            if (_token == 0)
                return;

            NowLayout.EndScope(_kind, _token);
            _token = 0;
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
    /// Declare a root <see cref="Column(NowRect, string, int)"/> or
    /// <see cref="Row(NowRect, string, int)"/>, nest fluent row/column containers,
    /// and reserve controls or rects that flow automatically with spacing, padding,
    /// growth and alignment. Containers are closed with a using statement:
    ///
    /// <code>
    /// using (NowLayout.Column(panelRect).Padding(16).Gap(8).Begin())
    /// using (NowLayout.Row().FillWidth().Begin())
    /// {
    ///     NowLayout.Label("Hello").Draw();
    /// }
    /// </code>
    ///
    /// Auto-sized groups, stretch shares, growth and flexible space need a
    /// measurement source. <see cref="NowLayoutGraphic"/>,
    /// <see cref="NowWorldLayoutGraphic"/>, <see cref="NowPipelineLayoutGraphic"/>,
    /// and <see cref="NowLayoutVisualElement"/> own an exact measure/draw cycle.
    /// Manual hosts can use
    /// <see cref="RunMeasured(NowRect, Action, float, float, NowLayoutAlign, string, int)"/>.
    /// Base one-pass hosts resolve from cached measurements instead, so deferred
    /// sizes settle on a later rebuild. Set explicit ids on data-backed containers
    /// whose order or existence can change.
    /// </summary>
    public static partial class NowLayout
    {
        struct FlexItem
        {
            public float weight;

            public float min;

            public float max;
        }

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

            public bool hasAlignItems;

            public NowLayoutJustify justify;

            public float justifyGap;

            public float fixedMain;

            public FlexItem[] cachedFlexItems;

            public int cachedFlexCount;

            public FlexItem[] measuredFlexItems;

            public float[] resolvedFlexSizes;

            public float resolvedFlexMain;

            public int flexCount;

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

            public int cachedChildCount;
        }

        struct CachedGroup
        {
            public bool horizontal;

            public float contentWidth;

            public float contentHeight;

            public float fixedMain;

            public FlexItem[] flexItems;

            public int flexCount;

            public int childCount;

            public double lastUsed;
        }

        const double CacheLifetimeSeconds = 10.0;

        const int AreaSeed = 0x4e6f774c;

        const float DefaultLabelFontSize = 16f;

        static NowText _labelStyle;

        static bool _hasLabelStyle;

        static readonly NowScopeGuard _labelStyleScopes = new NowScopeGuard("NowLayout.OverrideLabelStyle");

        static NowText _defaultLabelStyle;

        static NowThemeAsset _defaultLabelStyleTheme;

        static NowFontAsset _defaultLabelStyleFont;

        static int _defaultLabelStyleVersion;

        static Group[] _groups = new Group[16];

        static Dictionary<int, int>[] _groupSiteOccurrences = new Dictionary<int, int>[16];

        static int _depth;

        static readonly Dictionary<int, CachedGroup> _cache = new Dictionary<int, CachedGroup>(64);

        static readonly List<int> _removeIds = new List<int>(8);

        static int _frame = int.MinValue;

        static int _areaCounter;

        static double _lastCleanupTime;

        static bool _measurePass;

        static int _measureCycleDepth;

        static readonly NowScopeGuard _layoutScopes = new NowScopeGuard("NowLayout");

        static int _ambientStartedAt = int.MinValue;

        static bool hasActiveAmbientState =>
            _depth > 0 || _measurePass || _measureCycleDepth > 0 || _trackContent;

        static void MarkAmbientStart()
        {
            if (!hasActiveAmbientState)
                _ambientStartedAt = Time.frameCount;
        }

        internal static bool hasActiveScopesThisFrame =>
            hasActiveAmbientState && _ambientStartedAt == Time.frameCount;

        internal static void DiscardAbandonedScopes()
        {
            _depth = 0;
            _layoutScopes.Clear();
            _measurePass = false;
            _measureCycleDepth = 0;
            _trackContent = false;
            _trackedContent = default;
            _ambientStartedAt = int.MinValue;
        }

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

        /// <summary>True during the suppressed, input-passive half of an exact measure/draw cycle.</summary>
        public static bool isMeasurePass => _measurePass;

        internal static bool hasActiveMeasureCycle => _measureCycleDepth > 0;

        /// <summary>
        /// Opens a root layout area over an absolute rect, laying out children
        /// vertically. Dispose the returned scope (ideally with a using statement)
        /// to close the area. Deferred sizes (auto group extents, stretch shares,
        /// flexible space) resolve from the previous frame in a one-pass host.
        /// Layout-specific hosts resolve them in the same rebuild; manual hosts
        /// can use <see cref="RunMeasured(NowRect, Action, float, float, NowLayoutAlign, string, int)"/>.
        /// The common settings are
        /// optional parameters (<c>Area(rect, padding: 16, spacing: 8)</c>);
        /// pass a <see cref="NowLayoutOptions"/> for anything beyond them.
        /// </summary>
        public static NowLayoutScope Area(
            NowRect rect,
            float spacing = 0f,
            float padding = 0f,
            NowLayoutAlign alignItems = NowLayoutAlign.Start,
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0)
        {
            return BeginContainerArea(false, rect, default, GroupOptions(spacing, padding, alignItems, 0f, 0f, false, false), NowControls.SiteId(file, line));
        }

        public static NowLayoutScope Area(
            NowRect rect,
            Vector4 padding,
            float spacing = 0f,
            NowLayoutAlign alignItems = NowLayoutAlign.Start,
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0)
        {
            return BeginContainerArea(false, rect, default, GroupOptions(spacing, padding, alignItems, 0f, 0f, false, false), NowControls.SiteId(file, line));
        }

        public static NowLayoutScope Area(
            NowRect rect,
            in NowLayoutOptions options,
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0)
        {
            return BeginContainerArea(false, rect, default, options, NowControls.SiteId(file, line));
        }

        public static NowLayoutScope Area(
            NowId id,
            NowRect rect,
            float spacing = 0f,
            float padding = 0f,
            NowLayoutAlign alignItems = NowLayoutAlign.Start,
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0)
        {
            return BeginContainerArea(false, rect, id, GroupOptions(spacing, padding, alignItems, 0f, 0f, false, false), NowControls.SiteId(file, line));
        }

        public static NowLayoutScope Area(
            NowId id,
            NowRect rect,
            Vector4 padding,
            float spacing = 0f,
            NowLayoutAlign alignItems = NowLayoutAlign.Start,
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0)
        {
            return BeginContainerArea(false, rect, id, GroupOptions(spacing, padding, alignItems, 0f, 0f, false, false), NowControls.SiteId(file, line));
        }

        public static NowLayoutScope Area(
            NowId id,
            NowRect rect,
            in NowLayoutOptions options,
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0)
        {
            return BeginContainerArea(false, rect, id, options, NowControls.SiteId(file, line));
        }

        /// <summary>
        /// Area keyed by an ordinary integer identity local to the active retained
        /// host and <see cref="NowControls.IdScope(int)"/>. Wrap an already-composed
        /// key in <see cref="NowId.Resolved(int)"/> instead.
        /// </summary>
        public static NowLayoutScope Area(
            int id,
            NowRect rect,
            float spacing = 0f,
            float padding = 0f,
            NowLayoutAlign alignItems = NowLayoutAlign.Start)
        {
            return Area((NowId)id, rect, GroupOptions(spacing, padding, alignItems, 0f, 0f, false, false));
        }

        /// <summary>
        /// Area keyed by an ordinary integer identity local to the active retained
        /// host and <see cref="NowControls.IdScope(int)"/>. Wrap an already-composed
        /// key in <see cref="NowId.Resolved(int)"/> instead.
        /// </summary>
        public static NowLayoutScope Area(
            int id,
            NowRect rect,
            Vector4 padding,
            float spacing = 0f,
            NowLayoutAlign alignItems = NowLayoutAlign.Start)
        {
            return Area((NowId)id, rect, GroupOptions(spacing, padding, alignItems, 0f, 0f, false, false));
        }

        /// <summary>
        /// Area keyed by an ordinary integer identity local to the active retained
        /// host and <see cref="NowControls.IdScope(int)"/>. Wrap an already-composed
        /// key in <see cref="NowId.Resolved(int)"/> instead.
        /// </summary>
        public static NowLayoutScope Area(int id, NowRect rect, in NowLayoutOptions options)
        {
            return Area((NowId)id, rect, options);
        }

        internal static NowLayoutScope BeginArea(int id, NowRect rect, in NowLayoutOptions options, bool horizontal)
        {
            ValidateRootAreaOptions(options);
            OnFrameBoundary();

            int areaId = id;
            _areaCounter++;

            bool hasCache = _cache.TryGetValue(areaId, out var cached) &&
                cached.horizontal == horizontal;

            int token = Push(new Group
            {
                id = areaId,
                horizontal = horizontal,
                isArea = true,
                rect = rect,
                padding = options.Has(NowLayoutOptions.Field.Padding) ? options.padding : default,
                spacing = options.Has(NowLayoutOptions.Field.Spacing) ? options.spacing : 0f,
                alignItems = options.Has(NowLayoutOptions.Field.AlignItems) ? options.alignItems : NowLayoutAlign.Start,
                hasAlignItems = options.Has(NowLayoutOptions.Field.AlignItems),
                justify = options.Has(NowLayoutOptions.Field.Justify) ? options.justify : NowLayoutJustify.Start,
                parentMainMax = float.MaxValue,
                hasCache = hasCache,
                cachedFixedMain = hasCache ? cached.fixedMain : 0f,
                cachedChildCount = hasCache ? cached.childCount : 0,
                cachedFlexItems = hasCache ? cached.flexItems : null,
                cachedFlexCount = hasCache ? cached.flexCount : 0
            });

            ResolveFlexShares(ref _groups[_depth - 1]);
            ResolveJustification(ref _groups[_depth - 1]);

            return new NowLayoutScope(NowLayoutScope.Kind.Area, rect, token);
        }

        static void ValidateRootAreaOptions(in NowLayoutOptions options)
        {
            const NowLayoutOptions.Field placementFields =
                NowLayoutOptions.Field.Width |
                NowLayoutOptions.Field.Height |
                NowLayoutOptions.Field.MinWidth |
                NowLayoutOptions.Field.MaxWidth |
                NowLayoutOptions.Field.MinHeight |
                NowLayoutOptions.Field.MaxHeight |
                NowLayoutOptions.Field.StretchWidth |
                NowLayoutOptions.Field.StretchHeight |
                NowLayoutOptions.Field.Align |
                NowLayoutOptions.Field.Grow;

            if ((options.fields & placementFields) == 0)
                return;

            throw new InvalidOperationException(
                "Root layout areas already have explicit bounds. Their options may configure only spacing, padding, " +
                "child alignment, and justification; put sizing, stretching, Grow, or self-alignment on a nested element.");
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
        /// through the <c>RunMeasured&lt;TState&gt;</c> overloads with a static lambda.
        /// </summary>
        public static void RunMeasured(
            NowRect rect,
            Action ui,
            float spacing = 0f,
            float padding = 0f,
            NowLayoutAlign alignItems = NowLayoutAlign.Start,
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0)
        {
            RunMeasured(default, rect, GroupOptions(spacing, padding, alignItems, 0f, 0f, false, false), ui, file, line);
        }

        public static void RunMeasured(
            NowRect rect,
            Action ui,
            Vector4 padding,
            float spacing = 0f,
            NowLayoutAlign alignItems = NowLayoutAlign.Start,
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0)
        {
            RunMeasured(default, rect, GroupOptions(spacing, padding, alignItems, 0f, 0f, false, false), ui, file, line);
        }

        public static void RunMeasured(
            NowRect rect,
            in NowLayoutOptions options,
            Action ui,
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0)
        {
            RunMeasured(default, rect, options, ui, file, line);
        }

        public static void RunMeasured(
            NowId id,
            NowRect rect,
            Action ui,
            float spacing = 0f,
            float padding = 0f,
            NowLayoutAlign alignItems = NowLayoutAlign.Start,
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0)
        {
            RunMeasured(id, rect, GroupOptions(spacing, padding, alignItems, 0f, 0f, false, false), ui, file, line);
        }

        public static void RunMeasured(
            NowId id,
            NowRect rect,
            Action ui,
            Vector4 padding,
            float spacing = 0f,
            NowLayoutAlign alignItems = NowLayoutAlign.Start,
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0)
        {
            RunMeasured(id, rect, GroupOptions(spacing, padding, alignItems, 0f, 0f, false, false), ui, file, line);
        }

        public static void RunMeasured(
            NowId id,
            NowRect rect,
            in NowLayoutOptions options,
            Action ui,
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0)
        {
            if (ui == null)
                throw new ArgumentNullException(nameof(ui));

            int site = NowControls.SiteId(file, line);

            if (_measurePass || _measureCycleDepth > 0)
            {
                using (BeginContainerArea(false, rect, id, options, site))
                    ui();

                return;
            }

            BeginMeasureCycle();

            try
            {
                int areaCounter = BeginMeasurePass();

                try
                {
                    using (BeginContainerArea(false, rect, id, options, site))
                        ui();
                }
                finally
                {
                    EndMeasurePass(areaCounter);
                }

                using (BeginContainerArea(false, rect, id, options, site))
                    ui();
            }
            finally
            {
                EndMeasureCycle();
            }
        }

        /// <summary>
        /// Two-pass measured area that threads <paramref name="state"/> into the
        /// draw callback, so a static lambda can be used and no closure is
        /// allocated per rebuild:
        /// <code>
        /// NowLayout.RunMeasured(rect, this, static self => self.DrawContent());
        /// </code>
        /// </summary>
        public static void RunMeasured<TState>(
            NowRect rect,
            TState state,
            Action<TState> ui,
            float spacing = 0f,
            float padding = 0f,
            NowLayoutAlign alignItems = NowLayoutAlign.Start,
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0)
        {
            RunMeasured(default, rect, GroupOptions(spacing, padding, alignItems, 0f, 0f, false, false), state, ui, file, line);
        }

        public static void RunMeasured<TState>(
            NowRect rect,
            TState state,
            Action<TState> ui,
            Vector4 padding,
            float spacing = 0f,
            NowLayoutAlign alignItems = NowLayoutAlign.Start,
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0)
        {
            RunMeasured(default, rect, GroupOptions(spacing, padding, alignItems, 0f, 0f, false, false), state, ui, file, line);
        }

        public static void RunMeasured<TState>(
            NowRect rect,
            in NowLayoutOptions options,
            TState state,
            Action<TState> ui,
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0)
        {
            RunMeasured(default, rect, options, state, ui, file, line);
        }

        public static void RunMeasured<TState>(
            NowId id,
            NowRect rect,
            TState state,
            Action<TState> ui,
            float spacing = 0f,
            float padding = 0f,
            NowLayoutAlign alignItems = NowLayoutAlign.Start,
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0)
        {
            RunMeasured(id, rect, GroupOptions(spacing, padding, alignItems, 0f, 0f, false, false), state, ui, file, line);
        }

        public static void RunMeasured<TState>(
            NowId id,
            NowRect rect,
            TState state,
            Action<TState> ui,
            Vector4 padding,
            float spacing = 0f,
            NowLayoutAlign alignItems = NowLayoutAlign.Start,
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0)
        {
            RunMeasured(id, rect, GroupOptions(spacing, padding, alignItems, 0f, 0f, false, false), state, ui, file, line);
        }

        public static void RunMeasured<TState>(
            NowId id,
            NowRect rect,
            in NowLayoutOptions options,
            TState state,
            Action<TState> ui,
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0)
        {
            if (ui == null)
                throw new ArgumentNullException(nameof(ui));

            int site = NowControls.SiteId(file, line);

            if (_measurePass || _measureCycleDepth > 0)
            {
                using (BeginContainerArea(false, rect, id, options, site))
                    ui(state);

                return;
            }

            BeginMeasureCycle();

            try
            {
                int areaCounter = BeginMeasurePass();

                try
                {
                    using (BeginContainerArea(false, rect, id, options, site))
                        ui(state);
                }
                finally
                {
                    EndMeasurePass(areaCounter);
                }

                using (BeginContainerArea(false, rect, id, options, site))
                    ui(state);
            }
            finally
            {
                EndMeasureCycle();
            }
        }

        /// <summary>
        /// Enters measure mode: draws suppressed, input passive, layout calls record
        /// this frame's sizes. Run the UI once, call <see cref="EndMeasurePass"/> with
        /// the returned snapshot, then run the same UI again for real. Used by the
        /// RunMeasured and exact layout hosts that own a draw entry point (for
        /// example, NowLayoutGraphic running DrawNowUI twice).
        /// </summary>
        /// <remarks>Resolves the frame boundary before snapshotting so the counter rewind
        /// hands the real pass the same anonymous area ids the measure pass used (the
        /// boundary would otherwise reset the counter mid-measure).</remarks>
        internal static int BeginMeasurePass()
        {
            OnFrameBoundary();
            MarkAmbientStart();

            int areaCounter = _areaCounter;
            _measurePass = true;
            Now.BeginSuppressDraw();
            NowInput.BeginPassive();
            NowControls.CapturePassiveControlIdOccurrences();
            return areaCounter;
        }

        /// <summary>Exits measure mode and rewinds the anonymous area counter: ids are
        /// sequential per frame, so the real pass resolves the same ids (and therefore
        /// the fresh measurements).</summary>
        internal static void EndMeasurePass(int areaCounterSnapshot)
        {
            NowControls.RestorePassiveControlIdOccurrences();
            NowInput.EndPassive();
            Now.EndSuppressDraw();
            _measurePass = false;

            _areaCounter = areaCounterSnapshot;
        }

        /// <summary>
        /// Marks a complete measure/draw cycle owned by a host or RunMeasured.
        /// Nested measured regions reuse that owner instead of replaying again.
        /// </summary>
        internal static void BeginMeasureCycle()
        {
            MarkAmbientStart();
            ++_measureCycleDepth;
        }

        internal static void EndMeasureCycle()
        {
            if (_measureCycleDepth <= 0)
                throw new InvalidOperationException("NowLayout measure cycle ended without a matching begin.");

            --_measureCycleDepth;
        }

        internal static void EndArea()
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
            _layoutScopes.ExitCurrent();
            CleanupCache();
        }

        static bool _trackContent;

        static Vector2 _trackedContent;

        internal static bool isTrackingContent => _trackContent;

        /// <summary>
        /// Starts accumulating the content extent of root areas (area origin +
        /// measured content, the union across all areas ended while tracking).
        /// Hosts use the most recently completed pass to learn their preferred
        /// size. An exact layout host completes a measure pass before its real
        /// draw; a one-pass host makes the result available to later queries.
        /// </summary>
        internal static void BeginContentTracking()
        {
            if (_trackContent)
            {
                throw new InvalidOperationException(
                    "NowLayout content tracking cannot be nested. Let the outer host finish its preferred-size pass before starting another measurement.");
            }

            MarkAmbientStart();
            _trackContent = true;
            _trackedContent = default;
        }

        internal static Vector2 EndContentTracking()
        {
            if (!_trackContent)
                throw new InvalidOperationException("NowLayout content tracking ended without a matching begin.");

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
            bool stretchHeight = false,
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0)
        {
            return BeginGroup(true, default, GroupOptions(spacing, padding, alignItems, width, height, stretchWidth, stretchHeight), NowControls.SiteId(file, line));
        }

        public static NowLayoutScope Horizontal(
            Vector4 padding,
            float spacing = 0f,
            NowLayoutAlign alignItems = NowLayoutAlign.Start,
            float width = 0f,
            float height = 0f,
            bool stretchWidth = false,
            bool stretchHeight = false,
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0)
        {
            return BeginGroup(true, default, GroupOptions(spacing, padding, alignItems, width, height, stretchWidth, stretchHeight), NowControls.SiteId(file, line));
        }

        public static NowLayoutScope Horizontal(
            in NowLayoutOptions options,
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0)
        {
            return BeginGroup(true, default, options, NowControls.SiteId(file, line));
        }

        public static NowLayoutScope Horizontal(
            NowId id,
            float spacing = 0f,
            float padding = 0f,
            NowLayoutAlign alignItems = NowLayoutAlign.Start,
            float width = 0f,
            float height = 0f,
            bool stretchWidth = false,
            bool stretchHeight = false,
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0)
        {
            return BeginGroup(true, id, GroupOptions(spacing, padding, alignItems, width, height, stretchWidth, stretchHeight), NowControls.SiteId(file, line));
        }

        public static NowLayoutScope Horizontal(
            NowId id,
            Vector4 padding,
            float spacing = 0f,
            NowLayoutAlign alignItems = NowLayoutAlign.Start,
            float width = 0f,
            float height = 0f,
            bool stretchWidth = false,
            bool stretchHeight = false,
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0)
        {
            return BeginGroup(true, id, GroupOptions(spacing, padding, alignItems, width, height, stretchWidth, stretchHeight), NowControls.SiteId(file, line));
        }

        public static NowLayoutScope Horizontal(
            NowId id,
            in NowLayoutOptions options,
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0)
        {
            return BeginGroup(true, id, options, NowControls.SiteId(file, line));
        }

        static NowLayoutOptions GroupOptions(
            float spacing, float padding, NowLayoutAlign alignItems,
            float width, float height, bool stretchWidth, bool stretchHeight)
        {
            NowLayoutOptions.RequireFinite(spacing, nameof(spacing));
            NowLayoutOptions.RequireFinite(padding, nameof(padding));
            NowLayoutOptions.RequireAlign(alignItems, nameof(alignItems));
            NowLayoutOptions.RequireNonNegativeFinite(width, nameof(width));
            NowLayoutOptions.RequireNonNegativeFinite(height, nameof(height));

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

        internal static void EndHorizontal()
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
            bool stretchHeight = false,
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0)
        {
            return BeginGroup(false, default, GroupOptions(spacing, padding, alignItems, width, height, stretchWidth, stretchHeight), NowControls.SiteId(file, line));
        }

        public static NowLayoutScope Vertical(
            Vector4 padding,
            float spacing = 0f,
            NowLayoutAlign alignItems = NowLayoutAlign.Start,
            float width = 0f,
            float height = 0f,
            bool stretchWidth = false,
            bool stretchHeight = false,
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0)
        {
            return BeginGroup(false, default, GroupOptions(spacing, padding, alignItems, width, height, stretchWidth, stretchHeight), NowControls.SiteId(file, line));
        }

        public static NowLayoutScope Vertical(
            in NowLayoutOptions options,
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0)
        {
            return BeginGroup(false, default, options, NowControls.SiteId(file, line));
        }

        public static NowLayoutScope Vertical(
            NowId id,
            float spacing = 0f,
            float padding = 0f,
            NowLayoutAlign alignItems = NowLayoutAlign.Start,
            float width = 0f,
            float height = 0f,
            bool stretchWidth = false,
            bool stretchHeight = false,
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0)
        {
            return BeginGroup(false, id, GroupOptions(spacing, padding, alignItems, width, height, stretchWidth, stretchHeight), NowControls.SiteId(file, line));
        }

        public static NowLayoutScope Vertical(
            NowId id,
            Vector4 padding,
            float spacing = 0f,
            NowLayoutAlign alignItems = NowLayoutAlign.Start,
            float width = 0f,
            float height = 0f,
            bool stretchWidth = false,
            bool stretchHeight = false,
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0)
        {
            return BeginGroup(false, id, GroupOptions(spacing, padding, alignItems, width, height, stretchWidth, stretchHeight), NowControls.SiteId(file, line));
        }

        public static NowLayoutScope Vertical(
            NowId id,
            in NowLayoutOptions options,
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0)
        {
            return BeginGroup(false, id, options, NowControls.SiteId(file, line));
        }

        internal static void EndVertical()
        {
            EndGroup(false);
        }

        /// <summary>
        /// Reserves a rect at the current layout position. The common settings
        /// are optional parameters (<c>ReserveRect(height: 22, stretchWidth: true)</c>);
        /// pass a <see cref="NowLayoutOptions"/> for anything beyond them.
        /// </summary>
        public static NowRect ReserveRect(
            float width = 0f,
            float height = 0f,
            bool stretchWidth = false,
            bool stretchHeight = false,
            NowLayoutAlign align = NowLayoutAlign.Start)
        {
            NowLayoutOptions.RequireNonNegativeFinite(width, nameof(width));
            NowLayoutOptions.RequireNonNegativeFinite(height, nameof(height));
            NowLayoutOptions.RequireAlign(align, nameof(align));

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

            return ReserveRect(options);
        }

        /// <summary>Reserves a rect sized by <paramref name="options"/> at the current layout position.</summary>
        public static NowRect ReserveRect(in NowLayoutOptions options)
        {
            ref var group = ref RequireGroup();
            return Allocate(ref group, options, Vector2.zero, false, out _, out _);
        }

        /// <summary>Advances the current group's cursor by a fixed amount.</summary>
        public static void Space(float pixels)
        {
            NowLayoutOptions.RequireFinite(pixels, nameof(pixels));
            ref var group = ref RequireGroup();
            group.cursor += pixels;
            group.fixedMain += pixels;
        }

        /// <summary>
        /// Inserts a stretchable gap that absorbs a weighted share of the group's
        /// remaining main-axis space. An exact layout host resolves the share in
        /// its current measure/draw cycle. A one-pass host uses the previous
        /// measurement, so a newly appearing gap initially collapses.
        /// </summary>
        public static void FlexibleSpace(float weight = 1f)
        {
            NowLayoutOptions.RequirePositiveFinite(weight, nameof(weight));
            ref var group = ref RequireGroup();
            group.cursor += FlexShare(ref group, weight, 0f, float.MaxValue);
        }

        /// <summary>
        /// Style template used by <see cref="Label(string)"/> overloads that take no
        /// explicit style. Defaults to the active theme's body text at a 16px font size.
        /// The default is cached against the resolved theme instance, the ambient font
        /// and <see cref="NowThemeAsset.contentVersion"/>, so per-label calls skip the
        /// full preset resolution. Use <see cref="OverrideLabelStyle(NowText)"/>
        /// with a using statement for a temporary override; the process-wide
        /// setter is intentionally not public, so one host cannot leak its style
        /// into every other NowUI surface.
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
            internal set
            {
                _labelStyle = value;
                _hasLabelStyle = true;
            }
        }

        /// <summary>
        /// Removes a <see cref="labelStyle"/> override so implicit labels resume
        /// tracking the active theme's body text style.
        /// </summary>
        internal static void ClearLabelStyle()
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
            int token = _labelStyleScopes.Enter();
            var scope = new NowLabelStyleScope(_labelStyle, _hasLabelStyle, token);
            _labelStyle = style;
            _hasLabelStyle = true;
            return scope;
        }

        internal static void RestoreLabelStyle(NowText style, bool hasStyle, int token)
        {
            if (!_labelStyleScopes.Exit(token))
                return;

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
            _layoutScopes.Clear();
            _areaCounter = 0;
            _frame = int.MinValue;
            _cache.Clear();
            _lastCleanupTime = 0.0;
            _labelStyle = default;
            _hasLabelStyle = false;
            _labelStyleScopes.Clear();
            _defaultLabelStyle = default;
            _defaultLabelStyleTheme = null;
            _defaultLabelStyleFont = null;
            _defaultLabelStyleVersion = 0;
            _measurePass = false;
            _measureCycleDepth = 0;
            _trackContent = false;
            _trackedContent = default;
            _ambientStartedAt = int.MinValue;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForRuntimeLoad()
        {
            Reset();
        }

        static NowLayoutScope BeginGroup(bool horizontal, NowId id, in NowLayoutOptions options)
        {
            return BeginGroup(horizontal, id, options, 0);
        }

        internal static NowLayoutScope BeginGroup(
            bool horizontal,
            NowId id,
            in NowLayoutOptions options,
            int site)
        {
            ref var parent = ref RequireGroup("Nested layout containers require a root. Begin with NowLayout.Column(rect) or NowLayout.Row(rect), or open the lower-level NowLayout.Area(rect).");

            int groupId = id.hasValue
                ? HashCombine(parent.id, id.ResolveStableId(1))
                : HashCombine(
                    parent.id,
                    site != 0 ? ResolveGroupSiteOccurrence(_depth - 1, site) : parent.childIndex);
            parent.childIndex++;

            Vector2 autoSize = default;
            bool hasCache = _cache.TryGetValue(groupId, out var cached) &&
                cached.horizontal == horizontal;

            if (hasCache)
                autoSize = new Vector2(cached.contentWidth, cached.contentHeight);

            bool mainIsWidth = parent.horizontal;
            bool crossAuto =
                !options.Has(mainIsWidth ? NowLayoutOptions.Field.Height : NowLayoutOptions.Field.Width) &&
                !options.Has(mainIsWidth ? NowLayoutOptions.Field.StretchHeight : NowLayoutOptions.Field.StretchWidth);
            float crossBefore = parent.maxCross;

            var rect = Allocate(ref parent, options, autoSize, true, out bool mainAuto, out float mainAllocated);

            int token = Push(new Group
            {
                id = groupId,
                horizontal = horizontal,
                rect = rect,
                padding = options.Has(NowLayoutOptions.Field.Padding) ? options.padding : default,
                spacing = options.Has(NowLayoutOptions.Field.Spacing) ? options.spacing : 0f,
                alignItems = options.Has(NowLayoutOptions.Field.AlignItems) ? options.alignItems : NowLayoutAlign.Start,
                hasAlignItems = options.Has(NowLayoutOptions.Field.AlignItems),
                justify = options.Has(NowLayoutOptions.Field.Justify) ? options.justify : NowLayoutJustify.Start,
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
                cachedFixedMain = hasCache ? cached.fixedMain : 0f,
                cachedChildCount = hasCache ? cached.childCount : 0,
                cachedFlexItems = hasCache ? cached.flexItems : null,
                cachedFlexCount = hasCache ? cached.flexCount : 0
            });

            ResolveFlexShares(ref _groups[_depth - 1]);
            ResolveJustification(ref _groups[_depth - 1]);

            return new NowLayoutScope(
                horizontal ? NowLayoutScope.Kind.Horizontal : NowLayoutScope.Kind.Vertical,
                rect,
                token);
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
            _layoutScopes.ExitCurrent();

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
        /// Reserves a stretch-width rect for content whose height is only known
        /// after drawing, such as a markdown document or wrapped text:
        /// <code>
        /// var content = NowLayout.ContentRect();
        /// float height = DrawMyContent(content.rect);
        /// content.End(height);
        /// </code>
        /// The last reported height is stored per call site (loops are salted by
        /// per-frame occurrence, like control identity), so the caller manages no
        /// state. In an exact layout host, the measure pass reports the height and
        /// the real pass uses it in the same rebuild. A one-pass host uses the
        /// reported height on a later rebuild; <see cref="NowContentRect.End"/>
        /// requests repaint while that cached measurement is converging.
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

            var rect = ReserveRect(options.SetHeight(Mathf.Max(lastHeight, 1f)));
            return new NowContentRect(rect, slot);
        }

        /// <summary>
        /// Most recently completed content size of an explicit-id root area —
        /// how scroll views and container controls learn their content extent.
        /// The area stores the value when its using scope closes. Exact layout
        /// hosts therefore expose the measure-pass value during their real draw;
        /// one-pass hosts expose the previous completed draw until the current
        /// scope closes.
        /// </summary>
        public static bool TryGetCachedAreaContentSize(string id, out Vector2 size)
        {
            if (id != null)
                return TryGetCachedAreaContentSizeResolved(NowControls.GetControlId(id), out size);

            size = default;
            return false;
        }

        /// <summary>
        /// Last measured content size of an explicit-id root area — see
        /// <see cref="TryGetCachedAreaContentSize(string, out Vector2)"/>.
        /// </summary>
        public static bool TryGetCachedAreaContentSize(NowId id, out Vector2 size)
        {
            if (id.hasValue)
                return TryGetCachedAreaContentSizeResolved(id.ResolveStableId(1), out size);

            size = default;
            return false;
        }

        /// <summary>
        /// Last measured content size of an explicit-id root area. The integer
        /// is local to the active retained host and id scope; wrap an
        /// already-composed key in <see cref="NowId.Resolved(int)"/>.
        /// </summary>
        public static bool TryGetCachedAreaContentSize(int id, out Vector2 size)
        {
            return TryGetCachedAreaContentSize((NowId)id, out size);
        }

        static bool TryGetCachedAreaContentSizeResolved(int id, out Vector2 size)
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

            bool hasPrevious = _cache.TryGetValue(group.id, out var previous);
            bool changed = !hasPrevious ||
                previous.horizontal != group.horizontal ||
                Mathf.Abs(previous.contentWidth - contentWidth) > 0.25f ||
                Mathf.Abs(previous.contentHeight - contentHeight) > 0.25f ||
                Mathf.Abs(previous.fixedMain - group.fixedMain) > 0.25f ||
                previous.flexCount != group.flexCount ||
                previous.childCount != group.childCount;

            // Compare before copying: cachedFlexItems aliases the previous
            // snapshot, so overwriting it first would hide descriptor-only
            // changes such as Grow(1) -> Grow(3).
            if (!changed && group.flexCount > 0 &&
                (previous.flexItems == null || previous.flexItems.Length < group.flexCount))
            {
                changed = true;
            }

            if (!changed)
            {
                for (int i = 0; i < group.flexCount; ++i)
                {
                    FlexItem before = previous.flexItems[i];
                    FlexItem after = group.measuredFlexItems[i];

                    if (before.weight != after.weight ||
                        before.min != after.min ||
                        before.max != after.max)
                    {
                        changed = true;
                        break;
                    }
                }
            }

            if (changed)
                NowControlState.RequestRepaint();

            FlexItem[] flexItems = group.cachedFlexItems;
            EnsureCapacity(ref flexItems, group.flexCount);

            if (group.flexCount > 0)
                Array.Copy(group.measuredFlexItems, flexItems, group.flexCount);

            _cache[group.id] = new CachedGroup
            {
                horizontal = group.horizontal,
                contentWidth = contentWidth,
                contentHeight = contentHeight,
                fixedMain = group.fixedMain,
                flexItems = flexItems,
                flexCount = group.flexCount,
                childCount = group.childCount,
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
            bool grow = options.Has(NowLayoutOptions.Field.Grow);

            if (grow && options.Has(mainIsWidth ? NowLayoutOptions.Field.Width : NowLayoutOptions.Field.Height))
            {
                throw new InvalidOperationException(
                    "A growing element cannot also have a fixed size on its parent's main axis.");
            }

            if (grow && options.Has(mainIsWidth ? NowLayoutOptions.Field.StretchWidth : NowLayoutOptions.Field.StretchHeight))
            {
                throw new InvalidOperationException(
                    "Grow and stretch cannot both define the same parent main-axis weight.");
            }

            bool stretchWidth = options.Has(NowLayoutOptions.Field.StretchWidth) || (grow && mainIsWidth);
            bool stretchHeight = options.Has(NowLayoutOptions.Field.StretchHeight) || (grow && !mainIsWidth);
            float stretchWidthWeight = grow && mainIsWidth ? options.grow : options.stretchWidth;
            float stretchHeightWeight = grow && !mainIsWidth ? options.grow : options.stretchHeight;
            bool implicitCrossStretch = stretchCrossByDefault &&
                !options.Has(NowLayoutOptions.Field.Align) &&
                !group.hasAlignItems;

            ResolveAxis(
                ref group,
                mainIsWidth,
                options.Has(NowLayoutOptions.Field.Width),
                options.width,
                stretchWidth,
                stretchWidthWeight,
                options.Has(NowLayoutOptions.Field.MinWidth),
                options.minWidth,
                options.Has(NowLayoutOptions.Field.MaxWidth),
                options.maxWidth,
                autoSize.x,
                contentWidth,
                implicitCrossStretch,
                out float width,
                out bool widthFlex,
                out bool widthAuto);

            ResolveAxis(
                ref group,
                !mainIsWidth,
                options.Has(NowLayoutOptions.Field.Height),
                options.height,
                stretchHeight,
                stretchHeightWeight,
                options.Has(NowLayoutOptions.Field.MinHeight),
                options.minHeight,
                options.Has(NowLayoutOptions.Field.MaxHeight),
                options.maxHeight,
                autoSize.y,
                contentHeight,
                implicitCrossStretch,
                out float height,
                out bool heightFlex,
                out bool heightAuto);

            float main = mainIsWidth ? width : height;
            float cross = mainIsWidth ? height : width;
            bool mainFlex = mainIsWidth ? widthFlex : heightFlex;
            mainAuto = mainIsWidth ? widthAuto : heightAuto;
            mainAllocated = main;

            float gap = group.childCount > 0 ? group.spacing + group.justifyGap : 0f;
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
            group.fixedMain += (group.childCount > 0 ? group.spacing : 0f) + (mainFlex ? 0f : main);

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
            out bool isAuto)
        {
            isFlex = false;
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
                    size = FlexShare(
                        ref group,
                        stretchWeight,
                        hasMin ? min : 0f,
                        hasMax ? max : float.MaxValue);
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

        static float FlexShare(ref Group group, float weight, float min, float max)
        {
            int index = group.flexCount++;
            float size = group.hasCache && index < group.cachedFlexCount &&
                group.resolvedFlexSizes != null && index < group.resolvedFlexSizes.Length
                ? group.resolvedFlexSizes[index]
                : 0f;

            EnsureCapacity(ref group.measuredFlexItems, group.flexCount);
            group.measuredFlexItems[index] = new FlexItem
            {
                weight = weight,
                min = min,
                max = max
            };

            return size;
        }

        static void ResolveFlexShares(ref Group group)
        {
            if (!group.hasCache || group.cachedFlexCount == 0 || group.cachedFlexItems == null)
                return;

            EnsureCapacity(ref group.resolvedFlexSizes, group.cachedFlexCount);

            float available = group.horizontal
                ? group.rect.width - group.padding.x - group.padding.z
                : group.rect.height - group.padding.y - group.padding.w;
            float remaining = Mathf.Max(0f, available - group.cachedFixedMain);
            float activeWeight = 0f;

            for (int i = 0; i < group.cachedFlexCount; ++i)
            {
                group.resolvedFlexSizes[i] = float.NaN;
                activeWeight += group.cachedFlexItems[i].weight;
            }

            while (activeWeight > 0f)
            {
                float unit = remaining / activeWeight;
                float totalViolation = 0f;

                for (int i = 0; i < group.cachedFlexCount; ++i)
                {
                    if (!float.IsNaN(group.resolvedFlexSizes[i]))
                        continue;

                    FlexItem item = group.cachedFlexItems[i];
                    float proposed = unit * item.weight;
                    totalViolation += Mathf.Clamp(proposed, item.min, item.max) - proposed;
                }

                bool freezeMin = totalViolation > 0.0001f;
                bool freezeMax = totalViolation < -0.0001f;

                if (!freezeMin && !freezeMax)
                {
                    for (int i = 0; i < group.cachedFlexCount; ++i)
                    {
                        if (float.IsNaN(group.resolvedFlexSizes[i]))
                        {
                            FlexItem item = group.cachedFlexItems[i];
                            group.resolvedFlexSizes[i] = Mathf.Clamp(unit * item.weight, item.min, item.max);
                        }
                    }

                    break;
                }

                bool frozeItem = false;

                for (int i = 0; i < group.cachedFlexCount; ++i)
                {
                    if (!float.IsNaN(group.resolvedFlexSizes[i]))
                        continue;

                    FlexItem item = group.cachedFlexItems[i];
                    float proposed = unit * item.weight;
                    bool violatesSelectedBound = freezeMin
                        ? proposed < item.min
                        : proposed > item.max;

                    if (violatesSelectedBound)
                    {
                        float size = freezeMin ? item.min : item.max;
                        group.resolvedFlexSizes[i] = size;
                        remaining -= size;
                        activeWeight -= item.weight;
                        frozeItem = true;
                    }
                }

                if (!frozeItem)
                    break;
            }

            group.resolvedFlexMain = 0f;

            for (int i = 0; i < group.cachedFlexCount; ++i)
            {
                if (float.IsNaN(group.resolvedFlexSizes[i]))
                    group.resolvedFlexSizes[i] = 0f;

                group.resolvedFlexMain += group.resolvedFlexSizes[i];
            }
        }

        static void EnsureCapacity(ref FlexItem[] items, int count)
        {
            if (count == 0)
                return;

            if (items == null)
            {
                items = new FlexItem[Mathf.Max(4, Mathf.NextPowerOfTwo(count))];
                return;
            }

            if (items.Length < count)
                Array.Resize(ref items, Mathf.NextPowerOfTwo(count));
        }

        static void EnsureCapacity(ref float[] items, int count)
        {
            if (count == 0)
                return;

            if (items == null)
            {
                items = new float[Mathf.Max(4, Mathf.NextPowerOfTwo(count))];
                return;
            }

            if (items.Length < count)
                Array.Resize(ref items, Mathf.NextPowerOfTwo(count));
        }

        static void ResolveJustification(ref Group group)
        {
            if (!group.hasCache || group.cachedChildCount == 0 || group.justify == NowLayoutJustify.Start)
                return;

            float available = group.horizontal
                ? group.rect.width - group.padding.x - group.padding.z
                : group.rect.height - group.padding.y - group.padding.w;
            float remaining = Mathf.Max(0f, available - group.cachedFixedMain - group.resolvedFlexMain);

            switch (group.justify)
            {
                case NowLayoutJustify.Center:
                    group.cursor = remaining * 0.5f;
                    break;
                case NowLayoutJustify.End:
                    group.cursor = remaining;
                    break;
                case NowLayoutJustify.SpaceBetween:
                    if (group.cachedChildCount > 1)
                        group.justifyGap = remaining / (group.cachedChildCount - 1);
                    break;
            }
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
                    message ?? "Layout calls require a root. Begin with NowLayout.Column(rect) or NowLayout.Row(rect), or open the lower-level NowLayout.Area(rect).");
            }

            return ref Top();
        }

        static int Push(in Group group)
        {
            MarkAmbientStart();

            if (_depth == _groups.Length)
            {
                Array.Resize(ref _groups, _groups.Length * 2);
                Array.Resize(ref _groupSiteOccurrences, _groups.Length);
            }

            FlexItem[] measuredFlexItems = _groups[_depth].measuredFlexItems;
            float[] resolvedFlexSizes = _groups[_depth].resolvedFlexSizes;

            _groups[_depth] = group;
            _groups[_depth].measuredFlexItems = measuredFlexItems;
            _groups[_depth].resolvedFlexSizes = resolvedFlexSizes;

            var occurrences = _groupSiteOccurrences[_depth];

            if (occurrences != null)
                occurrences.Clear();

            _depth++;
            return _layoutScopes.Enter();
        }

        static int ResolveGroupSiteOccurrence(int parentDepth, int site)
        {
            var occurrences = _groupSiteOccurrences[parentDepth];

            if (occurrences == null)
            {
                occurrences = new Dictionary<int, int>(4);
                _groupSiteOccurrences[parentDepth] = occurrences;
            }

            if (!occurrences.TryGetValue(site, out int occurrence))
            {
                occurrences.Add(site, 1);
                return site;
            }

            occurrences[site] = occurrence + 1;
            return HashCombine(site, occurrence);
        }

        internal static void EndScope(NowLayoutScope.Kind kind, int token)
        {
            if (!_layoutScopes.IsCurrent(token))
                return;

            switch (kind)
            {
                case NowLayoutScope.Kind.Area:
                    EndArea();
                    break;
                case NowLayoutScope.Kind.Horizontal:
                    EndHorizontal();
                    break;
                case NowLayoutScope.Kind.Vertical:
                    EndVertical();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown layout scope kind.");
            }
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
                _layoutScopes.Clear();
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

        int _token;

        internal NowLabelStyleScope(NowText previous, bool hadPrevious, int token)
        {
            _previous = previous;
            _hadPrevious = hadPrevious;
            _token = token;
        }

        public void Dispose()
        {
            if (_token == 0)
                return;

            NowLayout.RestoreLabelStyle(_previous, _hadPrevious, _token);
            _token = 0;
        }
    }
}
