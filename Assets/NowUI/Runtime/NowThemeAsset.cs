using System;
using UnityEngine;

namespace NowUI
{
    [CreateAssetMenu(menuName = "NowUI/Theme", fileName = "NowTheme")]
    public sealed class NowThemeAsset : ScriptableObject
    {
        [SerializeField] NowRectangleStyle _defaultRectangleStyle = NowRectangleStyle.Surface;

        [SerializeField] NowTextStyle _defaultTextStyle = NowTextStyle.Body;

        [SerializeField] NowThemeColorSet _palette = NowThemeColorSet.Default;

        [SerializeField] NowThemeSpacingSet _spacings = NowThemeSpacingSet.Default;

        [SerializeField] NowThemeRadiusSet _radii = NowThemeRadiusSet.Default;

        [SerializeField] NowRectangleStyleSet _rectanglePresets = NowRectangleStyleSet.Default;

        [SerializeField] NowTextStyleSet _textPresets = NowTextStyleSet.Default;

        [SerializeField] NowControlStyleSet _controlStyles = NowControlStyleSet.Default;

        [SerializeField] NowControlRenderer _controlRenderer;

        #pragma warning disable 0414
        [HideInInspector, SerializeField] bool _generatorDark;

        [HideInInspector, SerializeField] Color _generatorKeyColor = new Color(0.18f, 0.28f, 0.40f, 1f);

        [HideInInspector, SerializeField] Color _generatorAccentColor = new Color(0.10f, 0.45f, 0.91f, 1f);

        [HideInInspector, SerializeField] int _generatorSeed = 42069;
        #pragma warning restore 0414

        public NowRectangleStyle defaultRectangleStyle => _defaultRectangleStyle;

        public NowTextStyle defaultTextStyle => _defaultTextStyle;

        public NowThemeColorSet palette => _palette;

        public NowThemeSpacingSet spacings => _spacings;

        public NowThemeRadiusSet radii => _radii;

        public NowRectangleStyleSet rectanglePresets => _rectanglePresets;

        public NowTextStyleSet textPresets => _textPresets;

        public NowControlStyleSet controlStyles => _controlStyles;

        public NowControlRenderer controlRenderer => _controlRenderer != null
            ? _controlRenderer
            : NowControlRenderer.defaultRenderer;

        public bool TryGetColor(NowColorToken token, out Color color)
        {
            return _palette.TryGet(token, out color);
        }

        public Color GetColor(NowColorToken token, Color fallback)
        {
            return TryGetColor(token, out var color) ? color : fallback;
        }

        public bool TryGetSpacing(NowSpacingToken token, out Vector4 spacing)
        {
            return _spacings.TryGet(token, out spacing);
        }

        public Vector4 GetSpacing(NowSpacingToken token, Vector4 fallback)
        {
            return TryGetSpacing(token, out var spacing) ? spacing : fallback;
        }

        public bool TryGetRadius(NowRadiusToken token, out Vector4 radius)
        {
            return _radii.TryGet(token, out radius);
        }

        public Vector4 GetRadius(NowRadiusToken token, Vector4 fallback)
        {
            return TryGetRadius(token, out var radius) ? radius : fallback;
        }

        public bool TryGetRectanglePreset(NowRectangleStyle style, out NowRectanglePreset preset)
        {
            return _rectanglePresets.TryGet(style, out preset);
        }

        public bool TryGetTextPreset(NowTextStyle style, out NowTextPreset preset)
        {
            return _textPresets.TryGet(style, out preset);
        }

        public NowRect Inset(NowRect rect, NowSpacingToken spacing)
        {
            return Inset(rect, GetSpacing(spacing, default));
        }

        public NowRect Outset(NowRect rect, NowSpacingToken spacing)
        {
            return Outset(rect, GetSpacing(spacing, default));
        }

        public static NowRect Inset(NowRect rect, Vector4 spacing)
        {
            return rect.Inset(spacing.x, spacing.y, spacing.z, spacing.w);
        }

        public static NowRect Outset(NowRect rect, Vector4 spacing)
        {
            return rect.Outset(spacing.x, spacing.y, spacing.z, spacing.w);
        }

        public NowRectangle Rectangle(NowRect rect)
        {
            return ApplyRectanglePreset(Now.Rectangle(rect), _defaultRectangleStyle);
        }

        public NowRectangle Rectangle(NowRect rect, NowRectangleStyle style)
        {
            return ApplyRectanglePreset(Now.Rectangle(rect), style);
        }

        public NowText Text(NowRect rect, NowFontAsset font)
        {
            return ApplyTextPreset(Now.Text(rect, font), _defaultTextStyle);
        }

        public NowText Text(NowRect rect, NowFontAsset font, NowTextStyle style)
        {
            return ApplyTextPreset(Now.Text(rect, font), style);
        }

        /// <summary>Creates preset-styled text. Preset fonts win over the ambient font,
        /// but a preset without one still resolves to the active font stack.</summary>
        public NowText Text(NowRect rect)
        {
            return Text(rect, _defaultTextStyle);
        }

        public NowText Text(NowRect rect, NowTextStyle style)
        {
            var text = ApplyTextPreset(Now.Text(rect, null), style);

            if (text.font == null)
                text = text.SetFont(Now.font);

            return text;
        }

        public NowText ResolveText(NowTextStyle style = NowTextStyle.Body)
        {
            var text = Text(default(NowRect), style);
            text.mask = default;
            return text;
        }

        public NowRectangle ApplyRectanglePreset(NowRectangle rectangle)
        {
            return ApplyRectanglePreset(rectangle, _defaultRectangleStyle);
        }

        public NowRectangle ApplyRectanglePreset(NowRectangle rectangle, NowRectangleStyle style)
        {
            if (!TryGetRectanglePreset(style, out var preset))
                return rectangle;

            return preset.Apply(this, rectangle);
        }

        public NowText ApplyTextPreset(NowText text)
        {
            return ApplyTextPreset(text, _defaultTextStyle);
        }

        public NowText ApplyTextPreset(NowText text, NowTextStyle style)
        {
            if (!TryGetTextPreset(style, out var preset))
                return text;

            return preset.Apply(this, text);
        }
    }

    [Serializable]
    public struct NowThemeColorSet
    {
        [SerializeField] Color _background;
        [SerializeField] Color _surface;
        [SerializeField] Color _surfaceMuted;
        [SerializeField] Color _text;
        [SerializeField] Color _textMuted;
        [SerializeField] Color _border;
        [SerializeField] Color _accent;
        [SerializeField] Color _accentText;

        public static NowThemeColorSet Default => new NowThemeColorSet
        {
            _background = new Color(0.945f, 0.961f, 0.976f, 1f),
            _surface = Color.white,
            _surfaceMuted = new Color(0.973f, 0.980f, 0.988f, 1f),
            _text = new Color(0.067f, 0.094f, 0.153f, 1f),
            _textMuted = new Color(0.420f, 0.447f, 0.502f, 1f),
            _border = new Color(0.886f, 0.910f, 0.941f, 1f),
            _accent = new Color(0.102f, 0.451f, 0.910f, 1f),
            _accentText = Color.white
        };

        public Color background => _background;
        public Color surface => _surface;
        public Color surfaceMuted => _surfaceMuted;
        public Color text => _text;
        public Color textMuted => _textMuted;
        public Color border => _border;
        public Color accent => _accent;
        public Color accentText => _accentText;

        public bool TryGet(NowColorToken token, out Color color)
        {
            switch (token)
            {
                case NowColorToken.Background:
                    color = _background;
                    return true;
                case NowColorToken.Surface:
                    color = _surface;
                    return true;
                case NowColorToken.SurfaceMuted:
                    color = _surfaceMuted;
                    return true;
                case NowColorToken.Text:
                    color = _text;
                    return true;
                case NowColorToken.TextMuted:
                    color = _textMuted;
                    return true;
                case NowColorToken.Border:
                    color = _border;
                    return true;
                case NowColorToken.Accent:
                    color = _accent;
                    return true;
                case NowColorToken.AccentText:
                    color = _accentText;
                    return true;
                default:
                    color = default;
                    return false;
            }
        }
    }

    [Serializable]
    public struct NowThemeSpacingSet
    {
        [SerializeField] Vector4 _none;
        [SerializeField] Vector4 _xs;
        [SerializeField] Vector4 _sm;
        [SerializeField] Vector4 _md;
        [SerializeField] Vector4 _lg;
        [SerializeField] Vector4 _panel;

        public static NowThemeSpacingSet Default => new NowThemeSpacingSet
        {
            _none = default,
            _xs = new Vector4(4f, 4f, 4f, 4f),
            _sm = new Vector4(8f, 8f, 8f, 8f),
            _md = new Vector4(12f, 12f, 12f, 12f),
            _lg = new Vector4(20f, 20f, 20f, 20f),
            _panel = new Vector4(24f, 20f, 24f, 20f)
        };

        public Vector4 none => _none;
        public Vector4 xs => _xs;
        public Vector4 sm => _sm;
        public Vector4 md => _md;
        public Vector4 lg => _lg;
        public Vector4 panel => _panel;

        public bool TryGet(NowSpacingToken token, out Vector4 insets)
        {
            switch (token)
            {
                case NowSpacingToken.None:
                    insets = _none;
                    return true;
                case NowSpacingToken.Xs:
                    insets = _xs;
                    return true;
                case NowSpacingToken.Sm:
                    insets = _sm;
                    return true;
                case NowSpacingToken.Md:
                    insets = _md;
                    return true;
                case NowSpacingToken.Lg:
                    insets = _lg;
                    return true;
                case NowSpacingToken.Panel:
                    insets = _panel;
                    return true;
                default:
                    insets = default;
                    return false;
            }
        }
    }

    [Serializable]
    public struct NowThemeRadiusSet
    {
        [SerializeField] Vector4 _none;
        [SerializeField] Vector4 _sm;
        [SerializeField] Vector4 _md;
        [SerializeField] Vector4 _lg;
        [SerializeField] Vector4 _pill;

        public static NowThemeRadiusSet Default => new NowThemeRadiusSet
        {
            _none = default,
            _sm = new Vector4(4f, 4f, 4f, 4f),
            _md = new Vector4(8f, 8f, 8f, 8f),
            _lg = new Vector4(14f, 14f, 14f, 14f),
            _pill = new Vector4(999f, 999f, 999f, 999f)
        };

        public Vector4 none => _none;
        public Vector4 sm => _sm;
        public Vector4 md => _md;
        public Vector4 lg => _lg;
        public Vector4 pill => _pill;

        public bool TryGet(NowRadiusToken token, out Vector4 radius)
        {
            switch (token)
            {
                case NowRadiusToken.None:
                    radius = _none;
                    return true;
                case NowRadiusToken.Sm:
                    radius = _sm;
                    return true;
                case NowRadiusToken.Md:
                    radius = _md;
                    return true;
                case NowRadiusToken.Lg:
                    radius = _lg;
                    return true;
                case NowRadiusToken.Pill:
                    radius = _pill;
                    return true;
                default:
                    radius = default;
                    return false;
            }
        }
    }

    [Serializable]
    public struct NowThemeColorReference
    {
        [SerializeField] bool _useToken;

        [SerializeField] NowColorToken _token;

        [SerializeField] Color _fallback;

        public NowThemeColorReference(NowColorToken token, Color fallback)
        {
            _useToken = true;
            _token = token;
            _fallback = fallback;
        }

        public static NowThemeColorReference Fallback(Color fallback)
        {
            return new NowThemeColorReference
            {
                _useToken = false,
                _token = default,
                _fallback = fallback
            };
        }

        public bool useToken => _useToken;

        public NowColorToken token => _token;

        public Color fallback => _fallback;

        public Color Resolve(NowThemeAsset themeAsset)
        {
            if (_useToken && themeAsset != null && themeAsset.TryGetColor(_token, out var color))
                return color;

            return _fallback;
        }
    }

    [Serializable]
    public struct NowThemeSpacingReference
    {
        [SerializeField] bool _useToken;

        [SerializeField] NowSpacingToken _token;

        [SerializeField] Vector4 _fallback;

        public NowThemeSpacingReference(NowSpacingToken token, Vector4 fallback)
        {
            _useToken = true;
            _token = token;
            _fallback = fallback;
        }

        public static NowThemeSpacingReference Fallback(Vector4 fallback)
        {
            return new NowThemeSpacingReference
            {
                _useToken = false,
                _token = default,
                _fallback = fallback
            };
        }

        public bool useToken => _useToken;

        public NowSpacingToken token => _token;

        public Vector4 fallback => _fallback;

        public Vector4 Resolve(NowThemeAsset themeAsset)
        {
            if (_useToken && themeAsset != null && themeAsset.TryGetSpacing(_token, out var spacing))
                return spacing;

            return _fallback;
        }
    }

    [Serializable]
    public struct NowThemeRadiusReference
    {
        [SerializeField] bool _useToken;

        [SerializeField] NowRadiusToken _token;

        [SerializeField] Vector4 _fallback;

        public NowThemeRadiusReference(NowRadiusToken token, Vector4 fallback)
        {
            _useToken = true;
            _token = token;
            _fallback = fallback;
        }

        public static NowThemeRadiusReference Fallback(Vector4 fallback)
        {
            return new NowThemeRadiusReference
            {
                _useToken = false,
                _token = default,
                _fallback = fallback
            };
        }

        public bool useToken => _useToken;

        public NowRadiusToken token => _token;

        public Vector4 fallback => _fallback;

        public Vector4 Resolve(NowThemeAsset themeAsset)
        {
            if (_useToken && themeAsset != null && themeAsset.TryGetRadius(_token, out var radius))
                return radius;

            return _fallback;
        }
    }

    [Serializable]
    public struct NowRectangleStyleSet
    {
        [SerializeField] NowRectanglePreset _surface;
        [SerializeField] NowRectanglePreset _muted;
        [SerializeField] NowRectanglePreset _outline;
        [SerializeField] NowRectanglePreset _accent;

        public static NowRectangleStyleSet Default => new NowRectangleStyleSet
        {
            _surface = new NowRectanglePreset(
                new NowThemeColorReference(NowColorToken.Surface, Color.white),
                new NowThemeRadiusReference(NowRadiusToken.Md, new Vector4(8f, 8f, 8f, 8f)),
                default,
                0f,
                0f,
                new NowThemeColorReference(NowColorToken.Border, Color.clear)),
            _muted = new NowRectanglePreset(
                new NowThemeColorReference(NowColorToken.SurfaceMuted, new Color(0.973f, 0.980f, 0.988f, 1f)),
                new NowThemeRadiusReference(NowRadiusToken.Md, new Vector4(8f, 8f, 8f, 8f)),
                default,
                0f,
                0f,
                new NowThemeColorReference(NowColorToken.Border, Color.clear)),
            _outline = new NowRectanglePreset(
                NowThemeColorReference.Fallback(Color.clear),
                new NowThemeRadiusReference(NowRadiusToken.Md, new Vector4(8f, 8f, 8f, 8f)),
                default,
                0f,
                1f,
                new NowThemeColorReference(NowColorToken.Border, new Color(0.886f, 0.910f, 0.941f, 1f))),
            _accent = new NowRectanglePreset(
                new NowThemeColorReference(NowColorToken.Accent, new Color(0.102f, 0.451f, 0.910f, 1f)),
                new NowThemeRadiusReference(NowRadiusToken.Md, new Vector4(8f, 8f, 8f, 8f)),
                default,
                0f,
                0f,
                new NowThemeColorReference(NowColorToken.Accent, Color.clear))
        };

        public NowRectanglePreset surface => _surface;
        public NowRectanglePreset muted => _muted;
        public NowRectanglePreset outline => _outline;
        public NowRectanglePreset accent => _accent;

        public bool TryGet(NowRectangleStyle style, out NowRectanglePreset preset)
        {
            switch (style)
            {
                case NowRectangleStyle.Surface:
                    preset = _surface;
                    return true;
                case NowRectangleStyle.Muted:
                    preset = _muted;
                    return true;
                case NowRectangleStyle.Outline:
                    preset = _outline;
                    return true;
                case NowRectangleStyle.Accent:
                    preset = _accent;
                    return true;
                default:
                    preset = default;
                    return false;
            }
        }
    }

    [Serializable]
    public struct NowRectanglePreset
    {
        [SerializeField] NowThemeColorReference _fill;

        [SerializeField] NowThemeRadiusReference _radius;

        [SerializeField] NowThemeSpacingReference _padding;

        [SerializeField] float _blur;

        [SerializeField] float _outline;

        [SerializeField] NowThemeColorReference _outlineColor;

        public NowRectanglePreset(
            NowThemeColorReference fill,
            NowThemeRadiusReference radius,
            NowThemeSpacingReference padding,
            float blur,
            float outline,
            NowThemeColorReference outlineColor)
        {
            _fill = fill;
            _radius = radius;
            _padding = padding;
            _blur = blur;
            _outline = outline;
            _outlineColor = outlineColor;
        }

        public float blur => _blur;

        public float outline => _outline;

        public NowRectangle Apply(NowThemeAsset themeAsset, NowRectangle rectangle)
        {
            rectangle.color = _fill.Resolve(themeAsset);
            rectangle.radius = _radius.Resolve(themeAsset);
            rectangle = rectangle.SetPadding(_padding.Resolve(themeAsset));
            rectangle.blur = _blur;
            rectangle.outline = _outline;
            rectangle.outlineColor = _outlineColor.Resolve(themeAsset);
            return rectangle;
        }
    }

    [Serializable]
    public struct NowTextStyleSet
    {
        [SerializeField] NowTextPreset _title;
        [SerializeField] NowTextPreset _body;
        [SerializeField] NowTextPreset _muted;
        [SerializeField] NowTextPreset _button;

        public static NowTextStyleSet Default => new NowTextStyleSet
        {
            _title = new NowTextPreset(
                null,
                28f,
                new NowThemeColorReference(NowColorToken.Text, new Color(0.067f, 0.094f, 0.153f, 1f)),
                0f,
                NowThemeColorReference.Fallback(Color.clear),
                default),
            _body = new NowTextPreset(
                null,
                16f,
                new NowThemeColorReference(NowColorToken.Text, new Color(0.067f, 0.094f, 0.153f, 1f)),
                0f,
                NowThemeColorReference.Fallback(Color.clear),
                default),
            _muted = new NowTextPreset(
                null,
                14f,
                new NowThemeColorReference(NowColorToken.TextMuted, new Color(0.420f, 0.447f, 0.502f, 1f)),
                0f,
                NowThemeColorReference.Fallback(Color.clear),
                default),
            _button = new NowTextPreset(
                null,
                14f,
                new NowThemeColorReference(NowColorToken.AccentText, Color.white),
                0f,
                NowThemeColorReference.Fallback(Color.clear),
                default)
        };

        public NowTextPreset title => _title;
        public NowTextPreset body => _body;
        public NowTextPreset muted => _muted;
        public NowTextPreset button => _button;

        public bool TryGet(NowTextStyle style, out NowTextPreset preset)
        {
            switch (style)
            {
                case NowTextStyle.Title:
                    preset = _title;
                    return true;
                case NowTextStyle.Body:
                    preset = _body;
                    return true;
                case NowTextStyle.Muted:
                    preset = _muted;
                    return true;
                case NowTextStyle.Button:
                    preset = _button;
                    return true;
                default:
                    preset = default;
                    return false;
            }
        }
    }

    [Serializable]
    public struct NowTextPreset
    {
        [SerializeField] NowFontAsset _font;

        [SerializeField] float _fontSize;

        [SerializeField] NowThemeColorReference _color;

        [SerializeField] float _outline;

        [SerializeField] NowThemeColorReference _outlineColor;

        [SerializeField] NowThemeSpacingReference _padding;

        public NowTextPreset(
            NowFontAsset font,
            float fontSize,
            NowThemeColorReference color,
            float outline,
            NowThemeColorReference outlineColor,
            NowThemeSpacingReference padding)
        {
            _font = font;
            _fontSize = fontSize;
            _color = color;
            _outline = outline;
            _outlineColor = outlineColor;
            _padding = padding;
        }

        public float fontSize => _fontSize;

        public NowText Apply(NowThemeAsset themeAsset, NowText text)
        {
            if (_font != null)
                text.font = _font;

            if (_fontSize > 0f)
                text.fontSize = _fontSize;

            text.color = _color.Resolve(themeAsset);
            text.outline = _outline;
            text.outlineColor = _outlineColor.Resolve(themeAsset);
            text.padding = _padding.Resolve(themeAsset);
            return text;
        }
    }

    [Serializable]
    public struct NowControlStyleSet
    {
        [SerializeField] Vector4 _buttonPadding;
        [SerializeField] float _buttonContentGap;
        [SerializeField] float _buttonFallbackContentWidth;
        [SerializeField] float _buttonFallbackContentHeight;
        [SerializeField] float _buttonMinHeight;
        [SerializeField] NowThemeRadiusReference _buttonRadius;
        [SerializeField] float _focusOutline;
        [SerializeField] NowThemeColorReference _focusColor;
        [SerializeField] NowThemeColorReference _fieldFocusColor;
        [SerializeField] float _hoverBrightness;
        [SerializeField] float _pressedBrightness;
        [SerializeField] float _hoverStateOpacity;
        [SerializeField] float _pressedStateOpacity;
        [SerializeField] float _rippleDuration;
        [SerializeField] float _rippleOpacity;
        [SerializeField] float _toggleSize;
        [SerializeField] float _toggleGap;
        [SerializeField] float _toggleStateLayerSize;
        [SerializeField] float _checkboxMarkInsetRatio;
        [SerializeField] float _checkboxMarkRadius;
        [SerializeField] float _radioDotInsetRatio;
        [SerializeField] float _sliderHeight;
        [SerializeField] float _sliderKnobSize;
        [SerializeField] float _sliderTrackThickness;
        [SerializeField] float _sliderNavigationStep;
        [SerializeField] float _sliderStateLayerSize;
        [SerializeField] Vector4 _textFieldPadding;
        [SerializeField] float _textFieldMinHeight;
        [SerializeField] NowThemeRadiusReference _fieldRadius;
        [SerializeField] Vector4 _textAreaPadding;
        [SerializeField] float _selectionAlpha;
        [SerializeField] float _caretWidth;
        [SerializeField] float _compositionUnderlineHeight;
        [SerializeField] Vector4 _dropdownFieldPadding;
        [SerializeField] float _dropdownFieldMinHeight;
        [SerializeField] float _dropdownItemHeight;
        [SerializeField] float _dropdownMaxPopupHeight;
        [SerializeField] float _dropdownPopupGap;
        [SerializeField] float _popupPadding;
        [SerializeField] float _popupItemRadius;
        [SerializeField] NowThemeRadiusReference _popupRadius;
        [SerializeField] float _contextMenuItemHeight;
        [SerializeField] float _contextMenuPaddingX;
        [SerializeField] float _contextMenuMinWidth;
        [SerializeField] float _contextMenuRadius;
        [SerializeField] float _scrollbarWidth;
        [SerializeField] float _scrollbarPadding;
        [SerializeField] float _scrollbarMinThumbSize;
        [SerializeField] float _scrollWheelStep;

        public static NowControlStyleSet Default
        {
            get
            {
                var style = new NowControlStyleSet
                {
                    _buttonPadding = new Vector4(12f, 12f, 12f, 12f),
                    _buttonContentGap = 6f,
                    _buttonFallbackContentWidth = 40f,
                    _buttonFallbackContentHeight = 20f,
                    _buttonMinHeight = 0f,
                    _buttonRadius = NowThemeRadiusReference.Fallback(default),
                    _focusOutline = 2f,
                    _focusColor = new NowThemeColorReference(NowColorToken.Text, Color.black),
                    _fieldFocusColor = new NowThemeColorReference(NowColorToken.Accent, Color.blue),
                    _hoverBrightness = 1.10f,
                    _pressedBrightness = 0.86f,
                    _hoverStateOpacity = 0.08f,
                    _pressedStateOpacity = 0.12f,
                    _rippleDuration = 0f,
                    _rippleOpacity = 0f,
                    _toggleSize = 18f,
                    _toggleGap = 8f,
                    _toggleStateLayerSize = 0f,
                    _checkboxMarkInsetRatio = 0.30f,
                    _checkboxMarkRadius = 2f,
                    _radioDotInsetRatio = 0.32f,
                    _sliderHeight = 20f,
                    _sliderKnobSize = 16f,
                    _sliderTrackThickness = 6f,
                    _sliderNavigationStep = 0.05f,
                    _sliderStateLayerSize = 0f,
                    _textFieldPadding = new Vector4(8f, 6f, 8f, 6f),
                    _textFieldMinHeight = 0f,
                    _fieldRadius = NowThemeRadiusReference.Fallback(default),
                    _textAreaPadding = new Vector4(8f, 6f, 8f, 6f),
                    _selectionAlpha = 0.35f,
                    _caretWidth = 2f,
                    _compositionUnderlineHeight = 1f,
                    _dropdownFieldPadding = new Vector4(8f, 6f, 8f, 6f),
                    _dropdownFieldMinHeight = 0f,
                    _dropdownItemHeight = 30f,
                    _dropdownMaxPopupHeight = 240f,
                    _dropdownPopupGap = 4f,
                    _popupPadding = 4f,
                    _popupItemRadius = 4f,
                    _popupRadius = NowThemeRadiusReference.Fallback(default),
                    _contextMenuItemHeight = 26f,
                    _contextMenuPaddingX = 14f,
                    _contextMenuMinWidth = 120f,
                    _contextMenuRadius = 6f,
                    _scrollbarWidth = 8f,
                    _scrollbarPadding = 4f,
                    _scrollbarMinThumbSize = 24f,
                    _scrollWheelStep = 40f
                };

                return style;
            }
        }

        public Vector4 buttonPadding => _buttonPadding;
        public float buttonContentGap => _buttonContentGap;
        public float buttonFallbackContentWidth => _buttonFallbackContentWidth;
        public float buttonFallbackContentHeight => _buttonFallbackContentHeight;
        public float buttonMinHeight => _buttonMinHeight;
        public NowThemeRadiusReference buttonRadius => _buttonRadius;
        public float focusOutline => _focusOutline;
        public NowThemeColorReference focusColor => _focusColor;
        public NowThemeColorReference fieldFocusColor => _fieldFocusColor;
        public float hoverBrightness => _hoverBrightness;
        public float pressedBrightness => _pressedBrightness;
        public float hoverStateOpacity => _hoverStateOpacity;
        public float pressedStateOpacity => _pressedStateOpacity;
        public float rippleDuration => _rippleDuration;
        public float rippleOpacity => _rippleOpacity;
        public float toggleSize => _toggleSize;
        public float toggleGap => _toggleGap;
        public float toggleStateLayerSize => _toggleStateLayerSize;
        public float checkboxMarkInsetRatio => _checkboxMarkInsetRatio;
        public float checkboxMarkRadius => _checkboxMarkRadius;
        public float radioDotInsetRatio => _radioDotInsetRatio;
        public float sliderHeight => _sliderHeight;
        public float sliderKnobSize => _sliderKnobSize;
        public float sliderTrackThickness => _sliderTrackThickness;
        public float sliderNavigationStep => _sliderNavigationStep;
        public float sliderStateLayerSize => _sliderStateLayerSize;
        public Vector4 textFieldPadding => _textFieldPadding;
        public float textFieldMinHeight => _textFieldMinHeight;
        public NowThemeRadiusReference fieldRadius => _fieldRadius;
        public Vector4 textAreaPadding => _textAreaPadding;
        public float selectionAlpha => _selectionAlpha;
        public float caretWidth => _caretWidth;
        public float compositionUnderlineHeight => _compositionUnderlineHeight;
        public Vector4 dropdownFieldPadding => _dropdownFieldPadding == default ? _textFieldPadding : _dropdownFieldPadding;
        public float dropdownFieldMinHeight => _dropdownFieldMinHeight;
        public float dropdownItemHeight => _dropdownItemHeight;
        public float dropdownMaxPopupHeight => _dropdownMaxPopupHeight;
        public float dropdownPopupGap => _dropdownPopupGap;
        public float popupPadding => _popupPadding;
        public float popupItemRadius => _popupItemRadius;
        public NowThemeRadiusReference popupRadius => _popupRadius;
        public float contextMenuItemHeight => _contextMenuItemHeight;
        public float contextMenuPaddingX => _contextMenuPaddingX;
        public float contextMenuMinWidth => _contextMenuMinWidth;
        public float contextMenuRadius => _contextMenuRadius;
        public float scrollbarWidth => _scrollbarWidth;
        public float scrollbarPadding => _scrollbarPadding;
        public float scrollbarMinThumbSize => _scrollbarMinThumbSize;
        public float scrollWheelStep => _scrollWheelStep;
    }

    public readonly struct NowSliderVisualMetrics
    {
        public readonly NowRect track;
        public readonly NowRect fill;
        public readonly NowRect knob;

        public NowSliderVisualMetrics(NowRect track, NowRect fill, NowRect knob)
        {
            this.track = track;
            this.fill = fill;
            this.knob = knob;
        }
    }

    public readonly struct NowButtonRenderContext
    {
        public readonly NowThemeAsset themeAsset;
        public readonly NowRect rect;
        public readonly string label;
        public readonly NowRectangleStyle rectangleStyle;
        public readonly NowTextStyle textStyle;
        public readonly NowInteraction interaction;
        public readonly bool focused;
        public readonly bool submitted;
        public readonly float hoverT;

        public NowButtonRenderContext(NowThemeAsset themeAsset, NowRect rect, string label, NowRectangleStyle rectangleStyle, NowTextStyle textStyle, NowInteraction interaction, bool focused, float hoverT)
            : this(themeAsset, rect, label, rectangleStyle, textStyle, interaction, focused, false, hoverT)
        {
        }

        public NowButtonRenderContext(NowThemeAsset themeAsset, NowRect rect, string label, NowRectangleStyle rectangleStyle, NowTextStyle textStyle, NowInteraction interaction, bool focused, bool submitted, float hoverT)
        {
            this.themeAsset = themeAsset;
            this.rect = rect;
            this.label = label;
            this.rectangleStyle = rectangleStyle;
            this.textStyle = textStyle;
            this.interaction = interaction;
            this.focused = focused;
            this.submitted = submitted;
            this.hoverT = hoverT;
        }
    }

    public readonly struct NowToggleRenderContext
    {
        public readonly NowThemeAsset themeAsset;
        public readonly NowRect rect;
        public readonly NowRect glyphRect;
        public readonly bool value;
        public readonly NowInteraction interaction;
        public readonly bool focused;
        public readonly float hoverT;

        public NowToggleRenderContext(NowThemeAsset themeAsset, NowRect rect, NowRect glyphRect, bool value, NowInteraction interaction, bool focused, float hoverT)
        {
            this.themeAsset = themeAsset;
            this.rect = rect;
            this.glyphRect = glyphRect;
            this.value = value;
            this.interaction = interaction;
            this.focused = focused;
            this.hoverT = hoverT;
        }
    }

    public readonly struct NowSliderRenderContext
    {
        public readonly NowThemeAsset themeAsset;
        public readonly NowRect rect;
        public readonly NowSliderVisualMetrics metrics;
        public readonly NowInteraction interaction;
        public readonly bool focused;
        public readonly float hoverT;

        public NowSliderRenderContext(NowThemeAsset themeAsset, NowRect rect, NowSliderVisualMetrics metrics, NowInteraction interaction, bool focused, float hoverT)
        {
            this.themeAsset = themeAsset;
            this.rect = rect;
            this.metrics = metrics;
            this.interaction = interaction;
            this.focused = focused;
            this.hoverT = hoverT;
        }
    }

    public readonly struct NowControlFrameRenderContext
    {
        public readonly NowThemeAsset themeAsset;
        public readonly NowRect rect;
        public readonly bool focused;

        public NowControlFrameRenderContext(NowThemeAsset themeAsset, NowRect rect, bool focused)
        {
            this.themeAsset = themeAsset;
            this.rect = rect;
            this.focused = focused;
        }
    }

    public readonly struct NowDropdownFieldRenderContext
    {
        public readonly NowThemeAsset themeAsset;
        public readonly NowRect rect;
        public readonly string current;
        public readonly bool open;
        public readonly NowInteraction interaction;
        public readonly bool focused;
        public readonly float hoverT;

        public NowDropdownFieldRenderContext(NowThemeAsset themeAsset, NowRect rect, string current, bool open, NowInteraction interaction, bool focused, float hoverT)
        {
            this.themeAsset = themeAsset;
            this.rect = rect;
            this.current = current;
            this.open = open;
            this.interaction = interaction;
            this.focused = focused;
            this.hoverT = hoverT;
        }
    }

    public readonly struct NowPopupItemRenderContext
    {
        public readonly NowThemeAsset themeAsset;
        public readonly NowRect rect;
        public readonly string label;
        public readonly bool selected;
        public readonly NowInteraction interaction;

        public NowPopupItemRenderContext(NowThemeAsset themeAsset, NowRect rect, string label, bool selected, NowInteraction interaction)
        {
            this.themeAsset = themeAsset;
            this.rect = rect;
            this.label = label;
            this.selected = selected;
            this.interaction = interaction;
        }
    }

    public readonly struct NowScrollbarRenderContext
    {
        public readonly NowThemeAsset themeAsset;
        public readonly NowScrollbarAxis axis;
        public readonly NowScrollbarMetrics metrics;
        public readonly bool dragging;
        public readonly float hoverT;

        public NowScrollbarRenderContext(NowThemeAsset themeAsset, NowScrollbarAxis axis, in NowScrollbarMetrics metrics, bool dragging, float hoverT)
        {
            this.themeAsset = themeAsset;
            this.axis = axis;
            this.metrics = metrics;
            this.dragging = dragging;
            this.hoverT = hoverT;
        }
    }

    public class NowControlRenderer : ScriptableObject
    {
        static NowControlRenderer _defaultRenderer;

        public static NowControlRenderer defaultRenderer
        {
            get
            {
                if (_defaultRenderer == null)
                {
                    _defaultRenderer = CreateInstance<NowControlRenderer>();
                    _defaultRenderer.name = "Now Default Control Renderer";
                    _defaultRenderer.hideFlags = HideFlags.HideAndDontSave;
                }

                return _defaultRenderer;
            }
        }

        public virtual Vector2 MeasureButton(NowThemeAsset themeAsset, string label, NowTextStyle textStyle)
        {
            var text = themeAsset.Text(default, textStyle);
            Vector2 labelSize = text.Measure(label ?? string.Empty);
            Vector4 padding = themeAsset.controlStyles.buttonPadding;
            return new Vector2(
                labelSize.x + padding.x + padding.z,
                Mathf.Max(themeAsset.controlStyles.buttonMinHeight, labelSize.y + (padding.y + padding.w) * 0.5f));
        }

        public virtual Vector2 MeasureButtonContent(NowThemeAsset themeAsset, Vector2 cachedContentSize)
        {
            Vector4 padding = themeAsset.controlStyles.buttonPadding;
            var contentSize = cachedContentSize.x > 0f
                ? cachedContentSize
                : new Vector2(themeAsset.controlStyles.buttonFallbackContentWidth, themeAsset.controlStyles.buttonFallbackContentHeight);
            return new Vector2(
                contentSize.x + padding.x + padding.z,
                Mathf.Max(themeAsset.controlStyles.buttonMinHeight, contentSize.y + padding.y + padding.w));
        }

        public virtual Vector2 MeasureToggle(NowThemeAsset themeAsset, string label, NowTextStyle textStyle)
        {
            var text = themeAsset.Text(default, textStyle);
            Vector2 labelSize = text.Measure(label ?? string.Empty);
            float glyphSize = ToggleGlyphSize(themeAsset, labelSize.y);
            return new Vector2(
                glyphSize + themeAsset.controlStyles.toggleGap + labelSize.x,
                Mathf.Max(glyphSize, labelSize.y));
        }

        public virtual Vector2 MeasureToggleContent(NowThemeAsset themeAsset, Vector2 cachedContentSize)
        {
            float glyphSize = themeAsset.controlStyles.toggleSize;
            return new Vector2(
                glyphSize + themeAsset.controlStyles.toggleGap + Mathf.Max(cachedContentSize.x, themeAsset.controlStyles.buttonFallbackContentWidth),
                Mathf.Max(glyphSize, cachedContentSize.y));
        }

        public virtual float ToggleGlyphSize(NowThemeAsset themeAsset, float labelHeight)
        {
            return Mathf.Max(themeAsset.controlStyles.toggleSize, labelHeight);
        }

        public virtual NowRect ToggleGlyphRect(NowThemeAsset themeAsset, NowRect rect, float glyphSize)
        {
            return new NowRect(rect.x, rect.y + (rect.height - glyphSize) * 0.5f, glyphSize, glyphSize);
        }

        public virtual NowRect ToggleContentRect(NowThemeAsset themeAsset, NowRect rect, float glyphSize)
        {
            float offset = glyphSize + themeAsset.controlStyles.toggleGap;
            return new NowRect(rect.x + offset, rect.y, rect.width - offset, rect.height);
        }

        public virtual Vector2 MeasureSlider(NowThemeAsset themeAsset)
        {
            return new Vector2(160f, themeAsset.controlStyles.sliderHeight);
        }

        public virtual NowSliderVisualMetrics CalculateSliderMetrics(NowThemeAsset themeAsset, NowRect rect, float normalized)
        {
            float knob = themeAsset.controlStyles.sliderKnobSize;
            float trackThickness = themeAsset.controlStyles.sliderTrackThickness;
            float knobX = rect.x + normalized * (rect.width - knob);
            float trackY = rect.y + (rect.height - trackThickness) * 0.5f;
            var track = new NowRect(rect.x, trackY, rect.width, trackThickness);
            var fill = new NowRect(rect.x, trackY, knobX - rect.x + knob * 0.5f, trackThickness);
            var knobRect = new NowRect(knobX, rect.y + (rect.height - knob) * 0.5f, knob, knob);
            return new NowSliderVisualMetrics(track, fill, knobRect);
        }

        public virtual Vector2 MeasureTextField(NowThemeAsset themeAsset, float lineHeight)
        {
            Vector4 padding = themeAsset.controlStyles.textFieldPadding;
            return new Vector2(200f, Mathf.Max(themeAsset.controlStyles.textFieldMinHeight, lineHeight + padding.y + padding.w));
        }

        public virtual Vector2 MeasureDropdownField(NowThemeAsset themeAsset, float lineHeight)
        {
            Vector4 padding = themeAsset.controlStyles.dropdownFieldPadding;
            return new Vector2(200f, Mathf.Max(themeAsset.controlStyles.dropdownFieldMinHeight, lineHeight + padding.y + padding.w));
        }

        public virtual Vector2 MeasureTextArea(NowThemeAsset themeAsset, float lineHeight, int visualLines)
        {
            Vector4 padding = themeAsset.controlStyles.textAreaPadding;
            return new Vector2(200f, visualLines * lineHeight + padding.y + padding.w);
        }

        public virtual NowRect TextFieldInnerRect(NowThemeAsset themeAsset, NowRect rect, float lineHeight)
        {
            Vector4 padding = themeAsset.controlStyles.textFieldPadding;
            float top = (rect.height - lineHeight) * 0.5f;
            return rect.Inset(padding.x, top, padding.z, top);
        }

        public virtual NowRect DropdownFieldInnerRect(NowThemeAsset themeAsset, NowRect rect, float lineHeight)
        {
            Vector4 padding = themeAsset.controlStyles.dropdownFieldPadding;
            float top = (rect.height - lineHeight) * 0.5f;
            return rect.Inset(padding.x, top, Mathf.Max(padding.z, 24f), top);
        }

        public virtual NowRect TextAreaInnerRect(NowThemeAsset themeAsset, NowRect rect)
        {
            Vector4 padding = themeAsset.controlStyles.textAreaPadding;
            return rect.Inset(padding.x, padding.y, padding.z, padding.w);
        }

        public virtual void DrawButton(in NowButtonRenderContext context)
        {
            var rectangle = context.themeAsset.Rectangle(context.rect, context.rectangleStyle);
            rectangle.color = NowControls.StateTint(context.themeAsset, rectangle.color, context.hoverT, context.interaction.held);

            if (context.focused)
                ApplyFocus(context.themeAsset, ref rectangle, field: false);

            rectangle.Draw();

            if (!string.IsNullOrEmpty(context.label))
                DrawButtonLabel(context);
        }

        protected virtual void DrawButtonLabel(in NowButtonRenderContext context)
        {
            NowControls.DrawCenteredLabel(
                context.themeAsset,
                context.rect,
                context.label,
                context.textStyle,
                context.rect,
                ResolveDefaultButtonTextColor(context.themeAsset, context.rectangleStyle));
        }

        protected virtual Color ResolveDefaultButtonTextColor(NowThemeAsset themeAsset, NowRectangleStyle rectangleStyle)
        {
            switch (rectangleStyle)
            {
                case NowRectangleStyle.Accent:
                    return themeAsset.GetColor(NowColorToken.AccentText, Color.white);
                case NowRectangleStyle.Outline:
                    return themeAsset.GetColor(NowColorToken.Accent, Color.blue);
                default:
                    return themeAsset.GetColor(NowColorToken.Text, Color.black);
            }
        }

        public virtual void DrawCheckbox(in NowToggleRenderContext context)
        {
            var frame = context.themeAsset.Rectangle(context.glyphRect, context.value ? NowRectangleStyle.Accent : NowRectangleStyle.Outline);
            frame.color = NowControls.StateTint(context.themeAsset, frame.color, context.hoverT, context.interaction.held);

            if (context.focused)
                ApplyFocus(context.themeAsset, ref frame, field: false);

            frame.Draw();

            if (!context.value)
                return;

            float inset = context.glyphRect.width * context.themeAsset.controlStyles.checkboxMarkInsetRatio;
            Now.Rectangle(new NowRect(
                    context.glyphRect.x + inset,
                    context.glyphRect.y + inset,
                    context.glyphRect.width - inset * 2f,
                    context.glyphRect.height - inset * 2f))
                .SetColor(context.themeAsset.GetColor(NowColorToken.AccentText, Color.white))
                .SetRadius(context.themeAsset.controlStyles.checkboxMarkRadius)
                .Draw();
        }

        public virtual void DrawRadio(in NowToggleRenderContext context)
        {
            var frame = context.themeAsset.Rectangle(context.glyphRect, context.value ? NowRectangleStyle.Accent : NowRectangleStyle.Outline);
            frame.radius = new Vector4(context.glyphRect.width, context.glyphRect.height, context.glyphRect.width, context.glyphRect.height) * 0.5f;
            frame.color = NowControls.StateTint(context.themeAsset, frame.color, context.hoverT, context.interaction.held);

            if (context.focused)
                ApplyFocus(context.themeAsset, ref frame, field: false);

            frame.Draw();

            if (!context.value)
                return;

            float inset = context.glyphRect.width * context.themeAsset.controlStyles.radioDotInsetRatio;
            float dot = context.glyphRect.width - inset * 2f;
            Now.Rectangle(new NowRect(context.glyphRect.x + inset, context.glyphRect.y + inset, dot, dot))
                .SetColor(context.themeAsset.GetColor(NowColorToken.AccentText, Color.white))
                .SetRadius(dot * 0.5f)
                .Draw();
        }

        public virtual void DrawSlider(in NowSliderRenderContext context)
        {
            float trackRadius = context.metrics.track.height * 0.5f;
            var track = context.themeAsset.Rectangle(context.metrics.track, NowRectangleStyle.Muted);
            track.radius = new Vector4(trackRadius, trackRadius, trackRadius, trackRadius);
            track.Draw();

            var fill = context.themeAsset.Rectangle(context.metrics.fill, NowRectangleStyle.Accent);
            fill.radius = track.radius;
            fill.Draw();

            float knobRadius = context.metrics.knob.width * 0.5f;
            var knob = context.themeAsset.Rectangle(context.metrics.knob, NowRectangleStyle.Accent);
            knob.radius = new Vector4(knobRadius, knobRadius, knobRadius, knobRadius);
            knob.color = NowControls.StateTint(context.themeAsset, knob.color, context.hoverT, context.interaction.held);

            if (context.focused)
                ApplyFocus(context.themeAsset, ref knob, field: false);

            knob.Draw();
        }

        public virtual void DrawTextInputFrame(in NowControlFrameRenderContext context)
        {
            var box = context.themeAsset.Rectangle(context.rect, NowRectangleStyle.Outline);

            if (context.focused)
                ApplyFocus(context.themeAsset, ref box, field: true);

            box.Draw();
        }

        public virtual void DrawSelection(NowThemeAsset themeAsset, NowRect rect)
        {
            Color selectionColor = themeAsset.GetColor(NowColorToken.Accent, Color.blue);
            selectionColor.a = themeAsset.controlStyles.selectionAlpha;
            Now.Rectangle(rect).SetColor(selectionColor).Draw();
        }

        public virtual void DrawCaret(NowThemeAsset themeAsset, NowRect rect)
        {
            Now.Rectangle(rect).SetColor(themeAsset.GetColor(NowColorToken.Text, Color.black)).Draw();
        }

        public virtual void DrawCompositionUnderline(NowThemeAsset themeAsset, NowRect rect)
        {
            Now.Rectangle(rect).SetColor(themeAsset.GetColor(NowColorToken.Text, Color.black)).Draw();
        }

        public virtual void DrawDropdownField(in NowDropdownFieldRenderContext context)
        {
            var box = context.themeAsset.Rectangle(context.rect, NowRectangleStyle.Outline);
            box.color = NowControls.StateTint(context.themeAsset, box.color, context.hoverT, context.interaction.held);

            if (context.focused || context.open)
                ApplyFocus(context.themeAsset, ref box, field: true);

            box.Draw();

            var inner = DropdownFieldInnerRect(context.themeAsset, context.rect, LabelHeight(context.themeAsset));
            NowControls.DrawLeftLabel(context.themeAsset, inner, context.current, NowTextStyle.Body);
            NowControls.DrawLeftLabel(
                context.themeAsset,
                new NowRect(context.rect.xMax - 20f, context.rect.y, 16f, context.rect.height),
                context.open ? "^" : "v",
                NowTextStyle.Muted);
        }

        protected static float LabelHeight(NowThemeAsset themeAsset)
        {
            var text = themeAsset.Text(default, NowTextStyle.Body);
            float height = text.Measure("Ag").y;
            if (height > 0f)
                return height;

            return text.font != null ? text.font.GetLineHeight() * text.fontSize : 20f;
        }

        public virtual void DrawPopupBackground(NowThemeAsset themeAsset, NowRect rect, bool menu)
        {
            var background = themeAsset.Rectangle(rect, NowRectangleStyle.Surface);
            background.outline = 1f;
            background.outlineColor = themeAsset.GetColor(NowColorToken.Border, Color.gray);

            if (menu)
                background.radius = new Vector4(
                    themeAsset.controlStyles.contextMenuRadius,
                    themeAsset.controlStyles.contextMenuRadius,
                    themeAsset.controlStyles.contextMenuRadius,
                    themeAsset.controlStyles.contextMenuRadius);

            background.Draw();
        }

        public virtual void DrawPopupItem(in NowPopupItemRenderContext context)
        {
            if (context.interaction.hovered || context.selected)
            {
                var highlight = context.themeAsset.Rectangle(
                    context.rect,
                    context.selected ? NowRectangleStyle.Accent : NowRectangleStyle.Muted);

                if (context.interaction.hovered && !context.selected)
                    highlight.color = NowControls.StateTint(context.themeAsset, highlight.color, 1f, context.interaction.held);

                float radius = context.themeAsset.controlStyles.popupItemRadius;
                highlight.radius = new Vector4(radius, radius, radius, radius);
                highlight.Draw();
            }

            Color textColor = context.selected
                ? context.themeAsset.GetColor(NowColorToken.AccentText, Color.white)
                : context.themeAsset.GetColor(NowColorToken.Text, Color.black);
            NowControls.DrawLeftLabel(context.themeAsset, context.rect.Inset(8f, 0f, 4f, 0f), context.label, NowTextStyle.Body, textColor);
        }

        public virtual void DrawContextMenuItem(in NowPopupItemRenderContext context)
        {
            if (context.interaction.hovered)
            {
                var highlight = context.themeAsset.Rectangle(context.rect, NowRectangleStyle.Muted);
                float radius = context.themeAsset.controlStyles.popupItemRadius;
                highlight.radius = new Vector4(radius, radius, radius, radius);
                highlight.color = NowControls.StateTint(context.themeAsset, highlight.color, 1f, context.interaction.held);
                highlight.Draw();
            }

            float left = context.themeAsset.controlStyles.contextMenuPaddingX * 0.7f;
            NowControls.DrawLeftLabel(context.themeAsset, context.rect.Inset(left, 0f, 4f, 0f), context.label, NowTextStyle.Body);
        }

        public virtual void DrawScrollbar(in NowScrollbarRenderContext context)
        {
            if (!context.metrics.visible)
                return;

            float radius = context.axis == NowScrollbarAxis.Vertical
                ? context.metrics.track.width * 0.5f
                : context.metrics.track.height * 0.5f;
            var track = context.themeAsset.Rectangle(context.metrics.track, NowRectangleStyle.Muted);
            track.radius = new Vector4(radius, radius, radius, radius);
            track.Draw();

            var thumb = context.themeAsset.Rectangle(context.metrics.thumb, NowRectangleStyle.Accent);
            thumb.radius = track.radius;
            thumb.color = NowControls.StateTint(context.themeAsset, thumb.color, context.hoverT, context.dragging);
            thumb.Draw();
        }

        static void ApplyFocus(NowThemeAsset themeAsset, ref NowRectangle rectangle, bool field)
        {
            var styles = themeAsset.controlStyles;
            rectangle.outline = Mathf.Max(rectangle.outline, styles.focusOutline);
            rectangle.outlineColor = field
                ? styles.fieldFocusColor.Resolve(themeAsset)
                : styles.focusColor.Resolve(themeAsset);
        }
    }

    public static class NowThemeExtensions
    {
        public static NowRectangle SetStyle(this NowRectangle rectangle, NowThemeAsset themeAsset, NowRectangleStyle style)
        {
            return themeAsset != null ? themeAsset.ApplyRectanglePreset(rectangle, style) : rectangle;
        }

        public static NowText SetStyle(this NowText text, NowThemeAsset themeAsset, NowTextStyle style)
        {
            return themeAsset != null ? themeAsset.ApplyTextPreset(text, style) : text;
        }
    }
}
