using System;
using UnityEngine;

namespace NowUI
{
    [CreateAssetMenu(menuName = "NowUI/Theme", fileName = "NowTheme")]
    public sealed class NowThemeAsset : ScriptableObject, ISerializationCallbackReceiver
    {
        [SerializeField] NowRectangleStyle _defaultRectangleStyle = NowRectangleStyle.Surface;

        [SerializeField] NowTextStyle _defaultTextStyle = NowTextStyle.Body;

        [SerializeField] NowThemeColorSet _palette = NowThemeColorSet.DefaultLight;

        [SerializeField] NowThemeSpacingSet _spacings = NowThemeSpacingSet.Default;

        [SerializeField] NowThemeRadiusSet _radii = NowThemeRadiusSet.Default;

        [SerializeField] NowThemeShadowSet _shadows = NowThemeShadowSet.Default;

        [SerializeField] NowRectangleStyleSet _rectanglePresets = NowRectangleStyleSet.Default;

        [SerializeField] NowTextStyleSet _textPresets = NowTextStyleSet.Default;

        [SerializeField] NowControlStyleSet _controlStyles = NowControlStyleSet.Default;

        [SerializeField] NowControlRenderer _controlRenderer;

        [SerializeField, Tooltip("The opposite-mode twin of this theme. NowTheme.preferDark resolves to the counterpart whose mode matches, so light/dark switching is one static flag.")]
        NowThemeAsset _counterpart;

        [SerializeField, HideInInspector] bool _generatorDark;

        [SerializeField, HideInInspector] Color _generatorKeyColor = new Color(0.58f, 0.64f, 0.72f, 1f);

        [SerializeField, HideInInspector] Color _generatorAccentColor = new Color(0.145f, 0.388f, 0.922f, 1f);

        [SerializeField, HideInInspector] int _generatorSeed;

        public NowRectangleStyle defaultRectangleStyle => _defaultRectangleStyle;

        public NowTextStyle defaultTextStyle => _defaultTextStyle;

        public NowThemeColorSet palette => _palette;

        public NowThemeSpacingSet spacings => _spacings;

        public NowThemeRadiusSet radii => _radii;

        public NowThemeShadowSet shadows => _shadows;

        public NowRectangleStyleSet rectanglePresets => _rectanglePresets;

        public NowTextStyleSet textPresets => _textPresets;

        public NowControlStyleSet controlStyles => _controlStyles;

        /// <summary>The opposite-mode twin used by <see cref="NowTheme.preferDark"/>.</summary>
        public NowThemeAsset counterpart => _counterpart;

        /// <summary>True when the palette background reads as dark.</summary>
        public bool isDark => _palette.isDark;

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

        /// <summary>Resolves a palette color; unknown tokens resolve to the built-in default palette.</summary>
        public Color GetColor(NowColorToken token)
        {
            return TryGetColor(token, out var color) ? color : NowThemeColorSet.DefaultLight.GetOrDefault(token);
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

        public bool TryGetShadow(NowElevationToken token, out NowShadowPreset preset)
        {
            return _shadows.TryGet(token, out preset);
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

        /// <summary>
        /// Fills any still-empty extended palette roles (state, status, elevation
        /// colors) derived from the legacy eight-color palette, then bakes the
        /// result into the serialized fields when saved. Explicitly set roles are
        /// never overwritten.
        /// </summary>
        public void MigrateDerivedRoles()
        {
            _palette.MigrateDerivedRoles();
        }

        /// <summary>Resets every set to the built-in light or dark defaults.</summary>
        public void ResetToDefaults(bool dark)
        {
            _palette = dark ? NowThemeColorSet.DefaultDark : NowThemeColorSet.DefaultLight;
            _spacings = NowThemeSpacingSet.Default;
            _radii = NowThemeRadiusSet.Default;
            _shadows = NowThemeShadowSet.Default;
            _rectanglePresets = NowRectangleStyleSet.Default;
            _textPresets = NowTextStyleSet.Default;
            _controlStyles = NowControlStyleSet.Default;
            _generatorDark = dark;
            _palette.InvalidateCache();
        }

        /// <summary>Links the opposite-mode twin used by <see cref="NowTheme.preferDark"/>.</summary>
        public void SetCounterpart(NowThemeAsset value)
        {
            _counterpart = value;
        }

        /// <summary>
        /// Clears every extended role and re-derives it from the current base
        /// eight colors — what the theme generator calls after writing a new
        /// base palette.
        /// </summary>
        public void RegenerateDerivedRoles()
        {
            _palette.ClearDerivedRoles();
            _palette.MigrateDerivedRoles();
            _palette.InvalidateCache();
        }

        void OnEnable()
        {
            _palette.InvalidateCache();
        }

        void OnValidate()
        {
            _palette.InvalidateCache();
        }

        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
        }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            _palette.MigrateDerivedRoles();
            _palette.InvalidateCache();
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
        [SerializeField] Color _surfaceElevated;
        [SerializeField] Color _surfaceHover;
        [SerializeField] Color _surfacePressed;
        [SerializeField] Color _accentHover;
        [SerializeField] Color _accentPressed;
        [SerializeField] Color _accentMuted;
        [SerializeField] Color _borderStrong;
        [SerializeField] Color _focusRing;
        [SerializeField] Color _success;
        [SerializeField] Color _successText;
        [SerializeField] Color _successMuted;
        [SerializeField] Color _warning;
        [SerializeField] Color _warningText;
        [SerializeField] Color _warningMuted;
        [SerializeField] Color _danger;
        [SerializeField] Color _dangerText;
        [SerializeField] Color _dangerMuted;
        [SerializeField] Color _shadow;
        [SerializeField] Color _scrim;

        [NonSerialized] Color[] _cache;

        [NonSerialized] bool _cachedIsDark;

        public const int TokenCount = 27;

        /// <summary>Slate-neutral light palette with a blue accent — the out-of-box look.</summary>
        public static NowThemeColorSet DefaultLight => new NowThemeColorSet
        {
            _background = new Color(0.973f, 0.980f, 0.988f, 1f),
            _surface = Color.white,
            _surfaceMuted = new Color(0.945f, 0.961f, 0.976f, 1f),
            _text = new Color(0.059f, 0.090f, 0.165f, 1f),
            _textMuted = new Color(0.392f, 0.455f, 0.545f, 1f),
            _border = new Color(0.886f, 0.910f, 0.941f, 1f),
            _accent = new Color(0.145f, 0.388f, 0.922f, 1f),
            _accentText = Color.white,
            _surfaceElevated = Color.white,
            _surfaceHover = new Color(0.945f, 0.961f, 0.976f, 1f),
            _surfacePressed = new Color(0.886f, 0.910f, 0.941f, 1f),
            _accentHover = new Color(0.114f, 0.306f, 0.847f, 1f),
            _accentPressed = new Color(0.118f, 0.251f, 0.686f, 1f),
            _accentMuted = new Color(0.859f, 0.918f, 0.996f, 1f),
            _borderStrong = new Color(0.796f, 0.835f, 0.882f, 1f),
            _focusRing = new Color(0.145f, 0.388f, 0.922f, 0.55f),
            _success = new Color(0.082f, 0.502f, 0.239f, 1f),
            _successText = Color.white,
            _successMuted = new Color(0.863f, 0.988f, 0.906f, 1f),
            _warning = new Color(0.961f, 0.620f, 0.043f, 1f),
            _warningText = new Color(0.271f, 0.102f, 0.012f, 1f),
            _warningMuted = new Color(0.996f, 0.953f, 0.780f, 1f),
            _danger = new Color(0.863f, 0.149f, 0.149f, 1f),
            _dangerText = Color.white,
            _dangerMuted = new Color(0.996f, 0.886f, 0.886f, 1f),
            _shadow = new Color(0.059f, 0.090f, 0.165f, 1f),
            _scrim = new Color(0.059f, 0.090f, 0.165f, 0.50f)
        };

        /// <summary>Slate-neutral dark palette; lighter surfaces read as closer.</summary>
        public static NowThemeColorSet DefaultDark => new NowThemeColorSet
        {
            _background = new Color(0.059f, 0.090f, 0.165f, 1f),
            _surface = new Color(0.118f, 0.161f, 0.231f, 1f),
            _surfaceMuted = new Color(0.161f, 0.208f, 0.282f, 1f),
            _text = new Color(0.973f, 0.980f, 0.988f, 1f),
            _textMuted = new Color(0.580f, 0.639f, 0.722f, 1f),
            _border = new Color(0.200f, 0.255f, 0.333f, 1f),
            _accent = new Color(0.376f, 0.647f, 0.980f, 1f),
            _accentText = new Color(0.043f, 0.071f, 0.125f, 1f),
            _surfaceElevated = new Color(0.173f, 0.227f, 0.314f, 1f),
            _surfaceHover = new Color(0.200f, 0.255f, 0.333f, 1f),
            _surfacePressed = new Color(0.243f, 0.298f, 0.388f, 1f),
            _accentHover = new Color(0.576f, 0.773f, 0.992f, 1f),
            _accentPressed = new Color(0.231f, 0.510f, 0.965f, 1f),
            _accentMuted = new Color(0.118f, 0.227f, 0.373f, 1f),
            _borderStrong = new Color(0.278f, 0.333f, 0.412f, 1f),
            _focusRing = new Color(0.376f, 0.647f, 0.980f, 0.60f),
            _success = new Color(0.290f, 0.871f, 0.502f, 1f),
            _successText = new Color(0.020f, 0.180f, 0.086f, 1f),
            _successMuted = new Color(0.090f, 0.204f, 0.145f, 1f),
            _warning = new Color(0.984f, 0.749f, 0.141f, 1f),
            _warningText = new Color(0.271f, 0.102f, 0.012f, 1f),
            _warningMuted = new Color(0.227f, 0.180f, 0.078f, 1f),
            _danger = new Color(0.973f, 0.443f, 0.443f, 1f),
            _dangerText = new Color(0.271f, 0.039f, 0.039f, 1f),
            _dangerMuted = new Color(0.231f, 0.114f, 0.122f, 1f),
            _shadow = Color.black,
            _scrim = new Color(0f, 0f, 0f, 0.60f)
        };

        /// <summary>Kept as an alias of <see cref="DefaultLight"/> for compatibility.</summary>
        public static NowThemeColorSet Default => DefaultLight;

        public Color background => _background;
        public Color surface => _surface;
        public Color surfaceMuted => _surfaceMuted;
        public Color text => _text;
        public Color textMuted => _textMuted;
        public Color border => _border;
        public Color accent => _accent;
        public Color accentText => _accentText;
        public Color surfaceElevated => _surfaceElevated;
        public Color surfaceHover => _surfaceHover;
        public Color surfacePressed => _surfacePressed;
        public Color accentHover => _accentHover;
        public Color accentPressed => _accentPressed;
        public Color accentMuted => _accentMuted;
        public Color borderStrong => _borderStrong;
        public Color focusRing => _focusRing;
        public Color success => _success;
        public Color successText => _successText;
        public Color successMuted => _successMuted;
        public Color warning => _warning;
        public Color warningText => _warningText;
        public Color warningMuted => _warningMuted;
        public Color danger => _danger;
        public Color dangerText => _dangerText;
        public Color dangerMuted => _dangerMuted;
        public Color shadow => _shadow;
        public Color scrim => _scrim;

        /// <summary>True when the background reads as dark (cached with the color cache).</summary>
        public bool isDark
        {
            get
            {
                if (_cache == null)
                    BuildCache();

                return _cachedIsDark;
            }
        }

        public bool TryGet(NowColorToken token, out Color color)
        {
            var cache = _cache;

            if (cache == null)
                cache = BuildCache();

            int index = (int)token;

            if ((uint)index >= TokenCount)
            {
                color = default;
                return false;
            }

            color = cache[index];
            return true;
        }

        internal Color GetOrDefault(NowColorToken token)
        {
            return TryGet(token, out var color) ? color : default;
        }

        internal void InvalidateCache()
        {
            _cache = null;
        }

        Color[] BuildCache()
        {
            var cache = new Color[TokenCount];
            cache[(int)NowColorToken.Background] = _background;
            cache[(int)NowColorToken.Surface] = _surface;
            cache[(int)NowColorToken.SurfaceMuted] = _surfaceMuted;
            cache[(int)NowColorToken.Text] = _text;
            cache[(int)NowColorToken.TextMuted] = _textMuted;
            cache[(int)NowColorToken.Border] = _border;
            cache[(int)NowColorToken.Accent] = _accent;
            cache[(int)NowColorToken.AccentText] = _accentText;
            cache[(int)NowColorToken.SurfaceElevated] = _surfaceElevated;
            cache[(int)NowColorToken.SurfaceHover] = _surfaceHover;
            cache[(int)NowColorToken.SurfacePressed] = _surfacePressed;
            cache[(int)NowColorToken.AccentHover] = _accentHover;
            cache[(int)NowColorToken.AccentPressed] = _accentPressed;
            cache[(int)NowColorToken.AccentMuted] = _accentMuted;
            cache[(int)NowColorToken.BorderStrong] = _borderStrong;
            cache[(int)NowColorToken.FocusRing] = _focusRing;
            cache[(int)NowColorToken.Success] = _success;
            cache[(int)NowColorToken.SuccessText] = _successText;
            cache[(int)NowColorToken.SuccessMuted] = _successMuted;
            cache[(int)NowColorToken.Warning] = _warning;
            cache[(int)NowColorToken.WarningText] = _warningText;
            cache[(int)NowColorToken.WarningMuted] = _warningMuted;
            cache[(int)NowColorToken.Danger] = _danger;
            cache[(int)NowColorToken.DangerText] = _dangerText;
            cache[(int)NowColorToken.DangerMuted] = _dangerMuted;
            cache[(int)NowColorToken.Shadow] = _shadow;
            cache[(int)NowColorToken.Scrim] = _scrim;
            _cachedIsDark = Luminance(_background) < 0.35f;
            _cache = cache;
            return cache;
        }

        /// <summary>
        /// Derives any still-empty extended roles from the legacy eight-color
        /// palette, so themes authored before the extended token set keep working
        /// unmodified. A role is considered unset while its alpha is zero.
        /// </summary>
        public void MigrateDerivedRoles()
        {
            if (_background.a <= 0f && _surface.a <= 0f && _text.a <= 0f)
                return;

            bool dark = Luminance(_background) < 0.35f;

            DeriveIfEmpty(ref _surfaceElevated, dark ? Lighten(_surface, 0.05f) : _surface);
            DeriveIfEmpty(ref _surfaceHover, Mix(_surface, _text, 0.06f));
            DeriveIfEmpty(ref _surfacePressed, Mix(_surface, _text, 0.12f));
            DeriveIfEmpty(ref _accentHover, dark ? Lighten(_accent, 0.10f) : Darken(_accent, 0.10f));
            DeriveIfEmpty(ref _accentPressed, dark ? Darken(_accent, 0.08f) : Darken(_accent, 0.20f));
            DeriveIfEmpty(ref _accentMuted, Mix(_accent, _background, dark ? 0.72f : 0.85f));
            DeriveIfEmpty(ref _borderStrong, Mix(_border, _text, 0.18f));
            DeriveIfEmpty(ref _focusRing, WithAlpha(_accent, dark ? 0.60f : 0.55f));
            DeriveIfEmpty(ref _success, StatusColor(0.39f, dark));
            DeriveIfEmpty(ref _successText, StatusTextColor(_success));
            DeriveIfEmpty(ref _successMuted, Mix(_success, _background, dark ? 0.78f : 0.86f));
            DeriveIfEmpty(ref _warning, StatusColor(0.11f, dark));
            DeriveIfEmpty(ref _warningText, StatusTextColor(_warning));
            DeriveIfEmpty(ref _warningMuted, Mix(_warning, _background, dark ? 0.78f : 0.86f));
            DeriveIfEmpty(ref _danger, StatusColor(0.0f, dark));
            DeriveIfEmpty(ref _dangerText, StatusTextColor(_danger));
            DeriveIfEmpty(ref _dangerMuted, Mix(_danger, _background, dark ? 0.78f : 0.86f));
            DeriveIfEmpty(ref _shadow, dark ? Color.black : Mix(_text, Color.black, 0.2f));
            DeriveIfEmpty(ref _scrim, WithAlpha(dark ? Color.black : _text, dark ? 0.60f : 0.50f));
        }

        internal void ClearDerivedRoles()
        {
            _surfaceElevated = default;
            _surfaceHover = default;
            _surfacePressed = default;
            _accentHover = default;
            _accentPressed = default;
            _accentMuted = default;
            _borderStrong = default;
            _focusRing = default;
            _success = default;
            _successText = default;
            _successMuted = default;
            _warning = default;
            _warningText = default;
            _warningMuted = default;
            _danger = default;
            _dangerText = default;
            _dangerMuted = default;
            _shadow = default;
            _scrim = default;
        }

        static void DeriveIfEmpty(ref Color field, Color derived)
        {
            if (field.a <= 0f)
                field = derived;
        }

        static Color StatusColor(float hue, bool dark)
        {
            Color color = Color.HSVToRGB(hue, dark ? 0.62f : 0.78f, dark ? 0.92f : 0.70f);
            color.a = 1f;
            return color;
        }

        static Color StatusTextColor(Color status)
        {
            return Luminance(status) < 0.45f
                ? Color.white
                : new Color(0.10f, 0.07f, 0.02f, 1f);
        }

        static Color Mix(Color a, Color b, float t)
        {
            var mixed = Color.LerpUnclamped(a, b, t);
            mixed.a = a.a;
            return mixed;
        }

        static Color Lighten(Color color, float amount)
        {
            return Mix(color, Color.white, amount);
        }

        static Color Darken(Color color, float amount)
        {
            return Mix(color, Color.black, amount);
        }

        static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        static float Luminance(Color color)
        {
            return color.r * 0.2126f + color.g * 0.7152f + color.b * 0.0722f;
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
        [SerializeField] Vector4 _xl;
        [SerializeField] Vector4 _xxl;

        public static NowThemeSpacingSet Default => new NowThemeSpacingSet
        {
            _none = default,
            _xs = new Vector4(4f, 4f, 4f, 4f),
            _sm = new Vector4(8f, 8f, 8f, 8f),
            _md = new Vector4(12f, 12f, 12f, 12f),
            _lg = new Vector4(16f, 16f, 16f, 16f),
            _panel = new Vector4(20f, 16f, 20f, 16f),
            _xl = new Vector4(24f, 24f, 24f, 24f),
            _xxl = new Vector4(32f, 32f, 32f, 32f)
        };

        public Vector4 none => _none;
        public Vector4 xs => _xs;
        public Vector4 sm => _sm;
        public Vector4 md => _md;
        public Vector4 lg => _lg;
        public Vector4 panel => _panel;
        public Vector4 xl => _xl == default ? Default.xl : _xl;
        public Vector4 xxl => _xxl == default ? Default.xxl : _xxl;

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
                case NowSpacingToken.Xl:
                    insets = xl;
                    return true;
                case NowSpacingToken.Xxl:
                    insets = xxl;
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
        [SerializeField] Vector4 _xl;

        public static NowThemeRadiusSet Default => new NowThemeRadiusSet
        {
            _none = default,
            _sm = new Vector4(6f, 6f, 6f, 6f),
            _md = new Vector4(10f, 10f, 10f, 10f),
            _lg = new Vector4(16f, 16f, 16f, 16f),
            _pill = new Vector4(999f, 999f, 999f, 999f),
            _xl = new Vector4(24f, 24f, 24f, 24f)
        };

        public Vector4 none => _none;
        public Vector4 sm => _sm;
        public Vector4 md => _md;
        public Vector4 lg => _lg;
        public Vector4 pill => _pill;
        public Vector4 xl => _xl == default ? Default.xl : _xl;

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
                case NowRadiusToken.Xl:
                    radius = xl;
                    return true;
                default:
                    radius = default;
                    return false;
            }
        }
    }

    /// <summary>One layer of a drop shadow: vertical offset, blur, outset and opacity.</summary>
    [Serializable]
    public struct NowShadowLayer
    {
        [SerializeField] float _offsetY;
        [SerializeField] float _blur;
        [SerializeField] float _spread;
        [SerializeField] float _alpha;

        public NowShadowLayer(float offsetY, float blur, float spread, float alpha)
        {
            _offsetY = offsetY;
            _blur = blur;
            _spread = spread;
            _alpha = alpha;
        }

        public float offsetY => _offsetY;
        public float blur => _blur;
        public float spread => _spread;
        public float alpha => _alpha;
    }

    /// <summary>A two-layer drop shadow: a directional key layer plus a soft ambient layer.</summary>
    [Serializable]
    public struct NowShadowPreset
    {
        [SerializeField] NowShadowLayer _key;
        [SerializeField] NowShadowLayer _ambient;

        public NowShadowPreset(NowShadowLayer key, NowShadowLayer ambient)
        {
            _key = key;
            _ambient = ambient;
        }

        public NowShadowLayer key => _key;
        public NowShadowLayer ambient => _ambient;
    }

    [Serializable]
    public struct NowThemeShadowSet
    {
        [SerializeField] NowShadowPreset _raised;
        [SerializeField] NowShadowPreset _overlay;
        [SerializeField] NowShadowPreset _modal;
        [SerializeField] float _darkModeAlphaScale;

        public static NowThemeShadowSet Default => new NowThemeShadowSet
        {
            _raised = new NowShadowPreset(
                new NowShadowLayer(2f, 4f, 0f, 0.06f),
                new NowShadowLayer(1f, 2f, -1f, 0.05f)),
            _overlay = new NowShadowPreset(
                new NowShadowLayer(6f, 12f, -2f, 0.10f),
                new NowShadowLayer(2f, 5f, -1f, 0.06f)),
            _modal = new NowShadowPreset(
                new NowShadowLayer(16f, 28f, -4f, 0.16f),
                new NowShadowLayer(4f, 10f, -2f, 0.08f)),
            _darkModeAlphaScale = 1.6f
        };

        public NowShadowPreset raised => _raised;
        public NowShadowPreset overlay => _overlay;
        public NowShadowPreset modal => _modal;

        /// <summary>Dark themes need stronger shadow alpha since tint carries less contrast.</summary>
        public float darkModeAlphaScale => _darkModeAlphaScale <= 0f ? Default.darkModeAlphaScale : _darkModeAlphaScale;

        public bool TryGet(NowElevationToken token, out NowShadowPreset preset)
        {
            switch (token)
            {
                case NowElevationToken.Raised:
                    preset = _raised;
                    return true;
                case NowElevationToken.Overlay:
                    preset = _overlay;
                    return true;
                case NowElevationToken.Modal:
                    preset = _modal;
                    return true;
                default:
                    preset = default;
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
        [SerializeField] NowRectanglePreset _elevated;
        [SerializeField] NowRectanglePreset _accentSoft;
        [SerializeField] NowRectanglePreset _danger;
        [SerializeField] NowRectanglePreset _ghost;
        [SerializeField] bool _hasExtendedPresets;

        public static NowRectangleStyleSet Default => new NowRectangleStyleSet
        {
            _surface = new NowRectanglePreset(
                new NowThemeColorReference(NowColorToken.Surface, Color.white),
                new NowThemeRadiusReference(NowRadiusToken.Md, new Vector4(10f, 10f, 10f, 10f)),
                default,
                0f,
                1f,
                new NowThemeColorReference(NowColorToken.Border, new Color(0.886f, 0.910f, 0.941f, 1f))),
            _muted = new NowRectanglePreset(
                new NowThemeColorReference(NowColorToken.SurfaceMuted, new Color(0.945f, 0.961f, 0.976f, 1f)),
                new NowThemeRadiusReference(NowRadiusToken.Md, new Vector4(10f, 10f, 10f, 10f)),
                default,
                0f,
                0f,
                new NowThemeColorReference(NowColorToken.Border, Color.clear)),
            _outline = new NowRectanglePreset(
                NowThemeColorReference.Fallback(Color.clear),
                new NowThemeRadiusReference(NowRadiusToken.Md, new Vector4(10f, 10f, 10f, 10f)),
                default,
                0f,
                1f,
                new NowThemeColorReference(NowColorToken.BorderStrong, new Color(0.796f, 0.835f, 0.882f, 1f))),
            _accent = new NowRectanglePreset(
                new NowThemeColorReference(NowColorToken.Accent, new Color(0.145f, 0.388f, 0.922f, 1f)),
                new NowThemeRadiusReference(NowRadiusToken.Md, new Vector4(10f, 10f, 10f, 10f)),
                default,
                0f,
                0f,
                new NowThemeColorReference(NowColorToken.Accent, Color.clear)),
            _elevated = new NowRectanglePreset(
                new NowThemeColorReference(NowColorToken.SurfaceElevated, Color.white),
                new NowThemeRadiusReference(NowRadiusToken.Md, new Vector4(10f, 10f, 10f, 10f)),
                default,
                0f,
                1f,
                new NowThemeColorReference(NowColorToken.Border, new Color(0.886f, 0.910f, 0.941f, 1f)),
                NowElevationToken.Raised),
            _accentSoft = new NowRectanglePreset(
                new NowThemeColorReference(NowColorToken.AccentMuted, new Color(0.859f, 0.918f, 0.996f, 1f)),
                new NowThemeRadiusReference(NowRadiusToken.Md, new Vector4(10f, 10f, 10f, 10f)),
                default,
                0f,
                0f,
                new NowThemeColorReference(NowColorToken.AccentMuted, Color.clear)),
            _danger = new NowRectanglePreset(
                new NowThemeColorReference(NowColorToken.Danger, new Color(0.863f, 0.149f, 0.149f, 1f)),
                new NowThemeRadiusReference(NowRadiusToken.Md, new Vector4(10f, 10f, 10f, 10f)),
                default,
                0f,
                0f,
                new NowThemeColorReference(NowColorToken.Danger, Color.clear)),
            _ghost = new NowRectanglePreset(
                NowThemeColorReference.Fallback(Color.clear),
                new NowThemeRadiusReference(NowRadiusToken.Md, new Vector4(10f, 10f, 10f, 10f)),
                default,
                0f,
                0f,
                NowThemeColorReference.Fallback(Color.clear)),
            _hasExtendedPresets = true
        };

        public NowRectanglePreset surface => _surface;
        public NowRectanglePreset muted => _muted;
        public NowRectanglePreset outline => _outline;
        public NowRectanglePreset accent => _accent;
        public NowRectanglePreset elevated => _hasExtendedPresets ? _elevated : Default.elevated;
        public NowRectanglePreset accentSoft => _hasExtendedPresets ? _accentSoft : Default.accentSoft;
        public NowRectanglePreset danger => _hasExtendedPresets ? _danger : Default.danger;
        public NowRectanglePreset ghost => _hasExtendedPresets ? _ghost : Default.ghost;

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
                case NowRectangleStyle.Elevated:
                    preset = elevated;
                    return true;
                case NowRectangleStyle.AccentSoft:
                    preset = accentSoft;
                    return true;
                case NowRectangleStyle.Danger:
                    preset = danger;
                    return true;
                case NowRectangleStyle.Ghost:
                    preset = ghost;
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

        [SerializeField] NowElevationToken _elevation;

        public NowRectanglePreset(
            NowThemeColorReference fill,
            NowThemeRadiusReference radius,
            NowThemeSpacingReference padding,
            float blur,
            float outline,
            NowThemeColorReference outlineColor)
            : this(fill, radius, padding, blur, outline, outlineColor, NowElevationToken.None)
        {
        }

        public NowRectanglePreset(
            NowThemeColorReference fill,
            NowThemeRadiusReference radius,
            NowThemeSpacingReference padding,
            float blur,
            float outline,
            NowThemeColorReference outlineColor,
            NowElevationToken elevation)
        {
            _fill = fill;
            _radius = radius;
            _padding = padding;
            _blur = blur;
            _outline = outline;
            _outlineColor = outlineColor;
            _elevation = elevation;
        }

        public float blur => _blur;

        public float outline => _outline;

        /// <summary>Optional drop-shadow level applied by <see cref="NowRectangle.Draw(NowThemeAsset)"/>-style helpers and the control renderer.</summary>
        public NowElevationToken elevation => _elevation;

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
        [SerializeField] NowTextPreset _display;
        [SerializeField] NowTextPreset _heading;
        [SerializeField] NowTextPreset _subheading;
        [SerializeField] NowTextPreset _bodyStrong;
        [SerializeField] NowTextPreset _label;
        [SerializeField] NowTextPreset _caption;

        public static NowTextStyleSet Default => new NowTextStyleSet
        {
            _title = Preset(26f, NowColorToken.Text, NowFontStyle.Bold),
            _body = Preset(15f, NowColorToken.Text, NowFontStyle.Regular),
            _muted = Preset(13f, NowColorToken.TextMuted, NowFontStyle.Regular),
            _button = Preset(15f, NowColorToken.AccentText, NowFontStyle.Bold),
            _display = Preset(34f, NowColorToken.Text, NowFontStyle.Bold),
            _heading = Preset(20f, NowColorToken.Text, NowFontStyle.Bold),
            _subheading = Preset(17f, NowColorToken.Text, NowFontStyle.Bold),
            _bodyStrong = Preset(15f, NowColorToken.Text, NowFontStyle.Bold),
            _label = Preset(13f, NowColorToken.Text, NowFontStyle.Regular),
            _caption = Preset(12f, NowColorToken.TextMuted, NowFontStyle.Regular)
        };

        static NowTextPreset Preset(float size, NowColorToken color, NowFontStyle fontStyle)
        {
            return new NowTextPreset(
                null,
                size,
                new NowThemeColorReference(color, new Color(0.059f, 0.090f, 0.165f, 1f)),
                0f,
                NowThemeColorReference.Fallback(Color.clear),
                default,
                fontStyle);
        }

        public NowTextPreset title => _title;
        public NowTextPreset body => _body;
        public NowTextPreset muted => _muted;
        public NowTextPreset button => _button;
        public NowTextPreset display => Fallback(_display, NowTextStyle.Display);
        public NowTextPreset heading => Fallback(_heading, NowTextStyle.Heading);
        public NowTextPreset subheading => Fallback(_subheading, NowTextStyle.Subheading);
        public NowTextPreset bodyStrong => Fallback(_bodyStrong, NowTextStyle.BodyStrong);
        public NowTextPreset label => Fallback(_label, NowTextStyle.Label);
        public NowTextPreset caption => Fallback(_caption, NowTextStyle.Caption);

        static NowTextPreset Fallback(NowTextPreset preset, NowTextStyle style)
        {
            if (preset.fontSize > 0f)
                return preset;

            Default.TryGet(style, out var fallback);
            return fallback;
        }

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
                case NowTextStyle.Display:
                    preset = display;
                    return true;
                case NowTextStyle.Heading:
                    preset = heading;
                    return true;
                case NowTextStyle.Subheading:
                    preset = subheading;
                    return true;
                case NowTextStyle.BodyStrong:
                    preset = bodyStrong;
                    return true;
                case NowTextStyle.Label:
                    preset = label;
                    return true;
                case NowTextStyle.Caption:
                    preset = caption;
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

        [SerializeField] NowFontStyle _fontStyle;

        public NowTextPreset(
            NowFontAsset font,
            float fontSize,
            NowThemeColorReference color,
            float outline,
            NowThemeColorReference outlineColor,
            NowThemeSpacingReference padding)
            : this(font, fontSize, color, outline, outlineColor, padding, NowFontStyle.Regular)
        {
        }

        public NowTextPreset(
            NowFontAsset font,
            float fontSize,
            NowThemeColorReference color,
            float outline,
            NowThemeColorReference outlineColor,
            NowThemeSpacingReference padding,
            NowFontStyle fontStyle)
        {
            _font = font;
            _fontSize = fontSize;
            _color = color;
            _outline = outline;
            _outlineColor = outlineColor;
            _padding = padding;
            _fontStyle = fontStyle;
        }

        public float fontSize => _fontSize;

        public NowFontStyle fontStyle => _fontStyle;

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
            text.fontStyle = _fontStyle;
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
        [SerializeField] float _focusRingOffset;
        [SerializeField] float _hoverStateOpacity;
        [SerializeField] float _pressedStateOpacity;
        [SerializeField] float _disabledOpacity;
        [SerializeField] float _rippleDuration;
        [SerializeField] float _rippleOpacity;
        [SerializeField] float _controlMinTouchTarget;
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
        [SerializeField] float _dropdownArrowInset;
        [SerializeField] float _fieldChevronSize;
        [SerializeField] float _popupPadding;
        [SerializeField] float _popupItemRadius;
        [SerializeField] NowThemeRadiusReference _popupRadius;
        [SerializeField] float _contextMenuItemHeight;
        [SerializeField] float _contextMenuPaddingX;
        [SerializeField] float _contextMenuMinWidth;
        [SerializeField] float _contextMenuRadius;
        [SerializeField] float _submenuIndicatorInset;
        [SerializeField] float _submenuIndicatorSize;
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
                    _buttonPadding = new Vector4(14f, 10f, 14f, 10f),
                    _buttonContentGap = 6f,
                    _buttonFallbackContentWidth = 40f,
                    _buttonFallbackContentHeight = 20f,
                    _buttonMinHeight = 36f,
                    _buttonRadius = NowThemeRadiusReference.Fallback(default),
                    _focusOutline = 2f,
                    _focusColor = new NowThemeColorReference(NowColorToken.FocusRing, new Color(0.145f, 0.388f, 0.922f, 0.55f)),
                    _fieldFocusColor = new NowThemeColorReference(NowColorToken.FocusRing, new Color(0.145f, 0.388f, 0.922f, 0.55f)),
                    _focusRingOffset = 2f,
                    _hoverStateOpacity = 0.08f,
                    _pressedStateOpacity = 0.12f,
                    _disabledOpacity = 0.45f,
                    _rippleDuration = 0f,
                    _rippleOpacity = 0f,
                    _controlMinTouchTarget = 44f,
                    _toggleSize = 18f,
                    _toggleGap = 8f,
                    _toggleStateLayerSize = 0f,
                    _checkboxMarkInsetRatio = 0.30f,
                    _checkboxMarkRadius = 4f,
                    _radioDotInsetRatio = 0.32f,
                    _sliderHeight = 20f,
                    _sliderKnobSize = 18f,
                    _sliderTrackThickness = 6f,
                    _sliderNavigationStep = 0.05f,
                    _sliderStateLayerSize = 0f,
                    _textFieldPadding = new Vector4(10f, 7f, 10f, 7f),
                    _textFieldMinHeight = 36f,
                    _fieldRadius = NowThemeRadiusReference.Fallback(default),
                    _textAreaPadding = new Vector4(10f, 7f, 10f, 7f),
                    _selectionAlpha = 0.35f,
                    _caretWidth = 2f,
                    _compositionUnderlineHeight = 1f,
                    _dropdownFieldPadding = new Vector4(10f, 7f, 10f, 7f),
                    _dropdownFieldMinHeight = 36f,
                    _dropdownItemHeight = 32f,
                    _dropdownMaxPopupHeight = 240f,
                    _dropdownPopupGap = 4f,
                    _dropdownArrowInset = 28f,
                    _fieldChevronSize = 16f,
                    _popupPadding = 4f,
                    _popupItemRadius = 6f,
                    _popupRadius = NowThemeRadiusReference.Fallback(default),
                    _contextMenuItemHeight = 28f,
                    _contextMenuPaddingX = 14f,
                    _contextMenuMinWidth = 160f,
                    _contextMenuRadius = 10f,
                    _submenuIndicatorInset = 18f,
                    _submenuIndicatorSize = 14f,
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
        public float focusRingOffset => _focusRingOffset <= 0f ? 2f : _focusRingOffset;
        public float hoverStateOpacity => _hoverStateOpacity;
        public float pressedStateOpacity => _pressedStateOpacity;
        public float disabledOpacity => _disabledOpacity <= 0f ? 0.45f : _disabledOpacity;
        public float rippleDuration => _rippleDuration;
        public float rippleOpacity => _rippleOpacity;
        public float controlMinTouchTarget => _controlMinTouchTarget <= 0f ? 44f : _controlMinTouchTarget;
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
        public float dropdownArrowInset => _dropdownArrowInset <= 0f ? 28f : _dropdownArrowInset;
        public float fieldChevronSize => _fieldChevronSize <= 0f ? 16f : _fieldChevronSize;
        public float popupPadding => _popupPadding;
        public float popupItemRadius => _popupItemRadius;
        public NowThemeRadiusReference popupRadius => _popupRadius;
        public float contextMenuItemHeight => _contextMenuItemHeight;
        public float contextMenuPaddingX => _contextMenuPaddingX;
        public float contextMenuMinWidth => _contextMenuMinWidth;
        public float contextMenuRadius => _contextMenuRadius;
        public float submenuIndicatorInset => _submenuIndicatorInset <= 0f ? 18f : _submenuIndicatorInset;
        public float submenuIndicatorSize => _submenuIndicatorSize <= 0f ? 14f : _submenuIndicatorSize;
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
        public readonly bool hasSubmenu;

        public NowPopupItemRenderContext(
            NowThemeAsset themeAsset,
            NowRect rect,
            string label,
            bool selected,
            NowInteraction interaction,
            bool hasSubmenu = false)
        {
            this.themeAsset = themeAsset;
            this.rect = rect;
            this.label = label;
            this.selected = selected;
            this.interaction = interaction;
            this.hasSubmenu = hasSubmenu;
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
            var text = NowControls.Text(themeAsset, textStyle);
            Vector2 labelSize = text.Measure(label ?? string.Empty);
            Vector4 padding = themeAsset.controlStyles.buttonPadding;
            return new Vector2(
                labelSize.x + padding.x + padding.z,
                Mathf.Max(themeAsset.controlStyles.buttonMinHeight, labelSize.y + padding.y + padding.w));
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
            var text = NowControls.Text(themeAsset, textStyle);
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
            return rect.Inset(padding.x, top, Mathf.Max(padding.z, themeAsset.controlStyles.dropdownArrowInset), top);
        }

        public virtual NowRect TextAreaInnerRect(NowThemeAsset themeAsset, NowRect rect)
        {
            Vector4 padding = themeAsset.controlStyles.textAreaPadding;
            return rect.Inset(padding.x, padding.y, padding.z, padding.w);
        }

        /// <summary>
        /// Draws a themed two-layer drop shadow behind <paramref name="rect"/>.
        /// One or two blurred quads; batches like any other rectangle.
        /// </summary>
        public virtual void DrawElevationShadow(NowThemeAsset themeAsset, NowRect rect, Vector4 radius, NowElevationToken level)
        {
            if (level == NowElevationToken.None || themeAsset == null)
                return;

            if (!themeAsset.TryGetShadow(level, out var preset))
                return;

            Color tint = themeAsset.GetColor(NowColorToken.Shadow, Color.black);
            float alphaScale = themeAsset.isDark ? themeAsset.shadows.darkModeAlphaScale : 1f;

            DrawShadowLayer(rect, radius, preset.key, tint, alphaScale);
            DrawShadowLayer(rect, radius, preset.ambient, tint, alphaScale);
        }

        static void DrawShadowLayer(NowRect rect, Vector4 radius, in NowShadowLayer layer, Color tint, float alphaScale)
        {
            float alpha = layer.alpha * alphaScale;

            if (alpha <= 0f)
                return;

            tint.a = Mathf.Clamp01(alpha);
            var shadowRect = new NowRect(rect.x, rect.y + layer.offsetY, rect.width, rect.height).Outset(layer.spread);
            float radiusSpread = Mathf.Max(0f, layer.spread);

            Now.Rectangle(shadowRect)
                .SetRadius(radius + new Vector4(radiusSpread, radiusSpread, radiusSpread, radiusSpread))
                .SetBlur(Mathf.Max(0.01f, layer.blur))
                .SetColor(tint)
                .Draw();
        }

        public virtual void DrawButton(in NowButtonRenderContext context)
        {
            NowRect visualRect = context.interaction.held ? ScaleRect(context.rect, 0.98f) : context.rect;
            Vector4 radius = ResolveRadius(context.themeAsset, context.themeAsset.controlStyles.buttonRadius, visualRect, NowRadiusToken.Md);
            var rectangle = ButtonRectangle(context.themeAsset, visualRect, context.rectangleStyle, context.hoverT, context.interaction.held);
            rectangle.radius = radius;

            if (context.rectangleStyle == NowRectangleStyle.Elevated && !context.interaction.held)
                DrawElevationShadow(context.themeAsset, visualRect, radius, NowElevationToken.Raised);

            rectangle.Draw();

            if (context.focused)
                DrawFocusRing(context.themeAsset, visualRect, radius);

            if (!string.IsNullOrEmpty(context.label))
                DrawButtonLabel(context, visualRect);
        }

        /// <summary>Resolves a button surface with its hover/pressed state baked into the fill.</summary>
        protected virtual NowRectangle ButtonRectangle(NowThemeAsset themeAsset, NowRect rect, NowRectangleStyle rectangleStyle, float hoverT, bool held)
        {
            var rectangle = themeAsset.Rectangle(rect, rectangleStyle);

            switch (rectangleStyle)
            {
                case NowRectangleStyle.Accent:
                    rectangle.color = StateToken(themeAsset, NowColorToken.Accent, NowColorToken.AccentHover, NowColorToken.AccentPressed, hoverT, held);
                    break;
                case NowRectangleStyle.Surface:
                case NowRectangleStyle.Elevated:
                    rectangle.color = StateToken(themeAsset, rectangleStyle == NowRectangleStyle.Elevated ? NowColorToken.SurfaceElevated : NowColorToken.Surface, NowColorToken.SurfaceHover, NowColorToken.SurfacePressed, hoverT, held);
                    break;
                case NowRectangleStyle.Ghost:
                case NowRectangleStyle.Outline:
                    Color hoverFill = themeAsset.GetColor(NowColorToken.SurfaceHover);
                    hoverFill.a *= held ? 1f : Mathf.Clamp01(hoverT);
                    if (held)
                        hoverFill = themeAsset.GetColor(NowColorToken.SurfacePressed);
                    rectangle.color = hoverFill;
                    break;
                default:
                    rectangle.color = NowControls.StateColor(themeAsset, rectangle.color, hoverT, held);
                    break;
            }

            return rectangle;
        }

        protected static Color StateToken(NowThemeAsset themeAsset, NowColorToken baseToken, NowColorToken hoverToken, NowColorToken pressedToken, float hoverT, bool held)
        {
            if (held)
                return themeAsset.GetColor(pressedToken);

            Color baseColor = themeAsset.GetColor(baseToken);

            if (hoverT <= 0f)
                return baseColor;

            return Color.LerpUnclamped(baseColor, themeAsset.GetColor(hoverToken), Mathf.Clamp01(hoverT));
        }

        protected virtual void DrawButtonLabel(in NowButtonRenderContext context, NowRect visualRect)
        {
            NowControls.DrawCenteredLabel(
                context.themeAsset,
                visualRect,
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
                    return themeAsset.GetColor(NowColorToken.AccentText);
                case NowRectangleStyle.AccentSoft:
                    return themeAsset.GetColor(NowColorToken.Accent);
                case NowRectangleStyle.Danger:
                    return themeAsset.GetColor(NowColorToken.DangerText);
                case NowRectangleStyle.Outline:
                    return themeAsset.GetColor(NowColorToken.Accent);
                default:
                    return themeAsset.GetColor(NowColorToken.Text);
            }
        }

        public virtual void DrawCheckbox(in NowToggleRenderContext context)
        {
            float radius = context.themeAsset.controlStyles.checkboxMarkRadius;
            Color fill = context.value
                ? StateToken(context.themeAsset, NowColorToken.Accent, NowColorToken.AccentHover, NowColorToken.AccentPressed, context.hoverT, context.interaction.held)
                : StateToken(context.themeAsset, NowColorToken.Surface, NowColorToken.SurfaceHover, NowColorToken.SurfacePressed, context.hoverT, context.interaction.held);

            Now.Rectangle(context.glyphRect)
                .SetRadius(radius)
                .SetColor(fill)
                .SetOutline(context.value ? 0f : 1.5f)
                .SetOutlineColor(context.themeAsset.GetColor(NowColorToken.BorderStrong))
                .Draw();

            if (context.focused)
                DrawFocusRing(context.themeAsset, context.glyphRect, new Vector4(radius, radius, radius, radius));

            if (!context.value)
                return;

            DrawCheckMark(context.themeAsset, context.glyphRect, context.themeAsset.GetColor(NowColorToken.AccentText));
        }

        public virtual void DrawRadio(in NowToggleRenderContext context)
        {
            float radius = Mathf.Min(context.glyphRect.width, context.glyphRect.height) * 0.5f;
            Color accent = StateToken(context.themeAsset, NowColorToken.Accent, NowColorToken.AccentHover, NowColorToken.AccentPressed, context.hoverT, context.interaction.held);
            Color surface = StateToken(context.themeAsset, NowColorToken.Surface, NowColorToken.SurfaceHover, NowColorToken.SurfacePressed, context.hoverT, context.interaction.held);

            Now.Rectangle(context.glyphRect)
                .SetRadius(radius)
                .SetColor(surface)
                .SetOutline(context.value ? 2f : 1.5f)
                .SetOutlineColor(context.value ? accent : context.themeAsset.GetColor(NowColorToken.BorderStrong))
                .Draw();

            if (context.focused)
                DrawFocusRing(context.themeAsset, context.glyphRect, Circle(context.glyphRect));

            if (!context.value)
                return;

            float dot = context.glyphRect.width * 0.5f;
            Now.Rectangle(new NowRect(
                    context.glyphRect.x + (context.glyphRect.width - dot) * 0.5f,
                    context.glyphRect.y + (context.glyphRect.height - dot) * 0.5f,
                    dot,
                    dot))
                .SetRadius(dot * 0.5f)
                .SetColor(accent)
                .Draw();
        }

        public virtual void DrawSlider(in NowSliderRenderContext context)
        {
            float trackRadius = context.metrics.track.height * 0.5f;
            Now.Rectangle(context.metrics.track)
                .SetRadius(trackRadius)
                .SetColor(context.themeAsset.GetColor(NowColorToken.SurfaceMuted))
                .SetOutline(1f)
                .SetOutlineColor(context.themeAsset.GetColor(NowColorToken.Border))
                .Draw();

            Now.Rectangle(context.metrics.fill)
                .SetRadius(trackRadius)
                .SetColor(context.themeAsset.GetColor(NowColorToken.Accent))
                .Draw();

            float knobRadius = context.metrics.knob.width * 0.5f;
            DrawElevationShadow(context.themeAsset, context.metrics.knob, Circle(context.metrics.knob), NowElevationToken.Raised);

            Now.Rectangle(context.metrics.knob)
                .SetRadius(knobRadius)
                .SetColor(StateToken(context.themeAsset, NowColorToken.Surface, NowColorToken.SurfaceHover, NowColorToken.SurfacePressed, context.hoverT, context.interaction.held))
                .SetOutline(2f)
                .SetOutlineColor(context.themeAsset.GetColor(NowColorToken.Accent))
                .Draw();

            if (context.focused)
                DrawFocusRing(context.themeAsset, context.metrics.knob, Circle(context.metrics.knob));
        }

        public virtual void DrawTextInputFrame(in NowControlFrameRenderContext context)
        {
            Vector4 radius = ResolveRadius(context.themeAsset, context.themeAsset.controlStyles.fieldRadius, context.rect, NowRadiusToken.Md);

            Now.Rectangle(context.rect)
                .SetRadius(radius)
                .SetColor(context.themeAsset.GetColor(NowColorToken.Surface))
                .SetOutline(context.focused ? 1.5f : 1f)
                .SetOutlineColor(context.focused
                    ? context.themeAsset.GetColor(NowColorToken.Accent)
                    : context.themeAsset.GetColor(NowColorToken.BorderStrong))
                .Draw();

            if (context.focused)
                DrawFocusRing(context.themeAsset, context.rect, radius);
        }

        public virtual void DrawSelection(NowThemeAsset themeAsset, NowRect rect)
        {
            Color selectionColor = themeAsset.GetColor(NowColorToken.Accent);
            selectionColor.a = themeAsset.controlStyles.selectionAlpha;
            Now.Rectangle(rect).SetColor(selectionColor).Draw();
        }

        public virtual void DrawCaret(NowThemeAsset themeAsset, NowRect rect)
        {
            Now.Rectangle(rect).SetColor(themeAsset.GetColor(NowColorToken.Accent)).Draw();
        }

        public virtual void DrawCompositionUnderline(NowThemeAsset themeAsset, NowRect rect)
        {
            Now.Rectangle(rect).SetColor(themeAsset.GetColor(NowColorToken.Text)).Draw();
        }

        public virtual void DrawDropdownField(in NowDropdownFieldRenderContext context)
        {
            Vector4 radius = ResolveRadius(context.themeAsset, context.themeAsset.controlStyles.fieldRadius, context.rect, NowRadiusToken.Md);
            bool active = context.focused || context.open;

            Now.Rectangle(context.rect)
                .SetRadius(radius)
                .SetColor(StateToken(context.themeAsset, NowColorToken.Surface, NowColorToken.SurfaceHover, NowColorToken.SurfacePressed, context.hoverT, context.interaction.held))
                .SetOutline(active ? 1.5f : 1f)
                .SetOutlineColor(active
                    ? context.themeAsset.GetColor(NowColorToken.Accent)
                    : context.themeAsset.GetColor(NowColorToken.BorderStrong))
                .Draw();

            if (active)
                DrawFocusRing(context.themeAsset, context.rect, radius);

            var inner = DropdownFieldInnerRect(context.themeAsset, context.rect, LabelHeight(context.themeAsset));
            NowControls.DrawLeftLabel(context.themeAsset, inner, context.current, NowTextStyle.Body);

            float chevron = context.themeAsset.controlStyles.fieldChevronSize;
            DrawFieldChevron(
                context.themeAsset,
                new NowRect(context.rect.xMax - chevron - 8f, context.rect.y, chevron, context.rect.height),
                context.open);
        }

        protected static float LabelHeight(NowThemeAsset themeAsset)
        {
            var text = NowControls.Text(themeAsset, NowTextStyle.Body);
            float height = text.Measure("Ag").y;
            if (height > 0f)
                return height;

            return text.font != null ? text.font.GetLineHeight() * text.fontSize : 20f;
        }

        public virtual void DrawPopupBackground(NowThemeAsset themeAsset, NowRect rect, bool menu)
        {
            Vector4 radius = menu
                ? new Vector4(
                    themeAsset.controlStyles.contextMenuRadius,
                    themeAsset.controlStyles.contextMenuRadius,
                    themeAsset.controlStyles.contextMenuRadius,
                    themeAsset.controlStyles.contextMenuRadius)
                : ResolveRadius(themeAsset, themeAsset.controlStyles.popupRadius, rect, NowRadiusToken.Md);

            DrawElevationShadow(themeAsset, rect, radius, NowElevationToken.Overlay);

            Now.Rectangle(rect)
                .SetRadius(radius)
                .SetColor(themeAsset.GetColor(NowColorToken.SurfaceElevated))
                .SetOutline(1f)
                .SetOutlineColor(themeAsset.GetColor(NowColorToken.Border))
                .Draw();
        }

        public virtual void DrawPopupItem(in NowPopupItemRenderContext context)
        {
            if (context.interaction.hovered || context.selected)
            {
                Color color = context.selected
                    ? context.themeAsset.GetColor(NowColorToken.AccentMuted)
                    : context.themeAsset.GetColor(NowColorToken.SurfaceHover);

                if (context.interaction.held)
                    color = context.themeAsset.GetColor(NowColorToken.SurfacePressed);

                float radius = context.themeAsset.controlStyles.popupItemRadius;
                Now.Rectangle(context.rect)
                    .SetRadius(radius)
                    .SetColor(color)
                    .Draw();
            }

            Color textColor = context.selected
                ? context.themeAsset.GetColor(NowColorToken.Accent)
                : context.themeAsset.GetColor(NowColorToken.Text);
            NowControls.DrawLeftLabel(context.themeAsset, context.rect.Inset(8f, 0f, 4f, 0f), context.label, NowTextStyle.Body, textColor);
        }

        public virtual void DrawContextMenuItem(in NowPopupItemRenderContext context)
        {
            if (context.interaction.hovered || context.selected)
            {
                Color color = context.interaction.held
                    ? context.themeAsset.GetColor(NowColorToken.SurfacePressed)
                    : context.themeAsset.GetColor(NowColorToken.SurfaceHover);

                float radius = context.themeAsset.controlStyles.popupItemRadius;
                Now.Rectangle(context.rect)
                    .SetRadius(radius)
                    .SetColor(color)
                    .Draw();
            }

            float left = context.themeAsset.controlStyles.contextMenuPaddingX * 0.7f;
            float right = context.hasSubmenu ? context.themeAsset.controlStyles.submenuIndicatorInset + 4f : 4f;
            NowControls.DrawLeftLabel(context.themeAsset, context.rect.Inset(left, 0f, right, 0f), context.label, NowTextStyle.Body);
        }

        public virtual void DrawContextMenuSubmenuIndicator(NowThemeAsset themeAsset, NowRect rect, bool enabled, bool open)
        {
            Color color = themeAsset.GetColor(NowColorToken.TextMuted);

            if (!enabled)
                color.a *= 0.62f;

            float inset = themeAsset.controlStyles.submenuIndicatorInset;
            float size = themeAsset.controlStyles.submenuIndicatorSize;
            var chevronRect = new NowRect(rect.xMax - inset, rect.y, size, rect.height);
            DrawChevron(chevronRect, color, NowChevronDirection.Right);
        }

        public virtual void DrawScrollbar(in NowScrollbarRenderContext context)
        {
            if (!context.metrics.visible)
                return;

            float radius = context.axis == NowScrollbarAxis.Vertical
                ? context.metrics.thumb.width * 0.5f
                : context.metrics.thumb.height * 0.5f;

            Color thumb = context.themeAsset.GetColor(NowColorToken.TextMuted);
            thumb.a = context.themeAsset.isDark ? 0.42f : 0.32f;

            if (context.dragging || context.hoverT > 0f)
                thumb.a = Mathf.Lerp(thumb.a, context.themeAsset.isDark ? 0.62f : 0.5f, context.dragging ? 1f : context.hoverT);

            Now.Rectangle(context.metrics.thumb)
                .SetRadius(radius)
                .SetColor(thumb)
                .Draw();
        }

        /// <summary>Offset focus ring drawn outside the control at the themed focus color.</summary>
        protected virtual void DrawFocusRing(NowThemeAsset themeAsset, NowRect rect, Vector4 radius)
        {
            var styles = themeAsset.controlStyles;
            Color color = styles.focusColor.Resolve(themeAsset);
            Vector2 transformScale = Now.currentTransform.scale;
            float scale = Mathf.Max(Mathf.Abs(transformScale.x), Mathf.Abs(transformScale.y));
            float offset = styles.focusRingOffset * scale;

            Now.Rectangle(rect.Outset(offset))
                .SetRadius(radius + new Vector4(offset, offset, offset, offset))
                .SetColor(Color.clear)
                .SetOutline(styles.focusOutline)
                .SetOutlineColor(color)
                .Draw();
        }

        protected enum NowChevronDirection
        {
            Down,
            Up,
            Right,
            Left
        }

        /// <summary>Line-drawn chevron glyph used by dropdown fields and submenu indicators.</summary>
        protected static void DrawChevron(NowRect rect, Color color, NowChevronDirection direction)
        {
            float cx = rect.center.x;
            float cy = rect.center.y;
            float w = Mathf.Min(rect.width, rect.height) * 0.32f;
            float h = w * 0.7f;

            Vector2 a;
            Vector2 mid;
            Vector2 b;

            switch (direction)
            {
                case NowChevronDirection.Up:
                    a = new Vector2(cx - w, cy + h * 0.5f);
                    mid = new Vector2(cx, cy - h * 0.5f);
                    b = new Vector2(cx + w, cy + h * 0.5f);
                    break;
                case NowChevronDirection.Right:
                    a = new Vector2(cx - h * 0.5f, cy - w);
                    mid = new Vector2(cx + h * 0.5f, cy);
                    b = new Vector2(cx - h * 0.5f, cy + w);
                    break;
                case NowChevronDirection.Left:
                    a = new Vector2(cx + h * 0.5f, cy - w);
                    mid = new Vector2(cx - h * 0.5f, cy);
                    b = new Vector2(cx + h * 0.5f, cy + w);
                    break;
                default:
                    a = new Vector2(cx - w, cy - h * 0.5f);
                    mid = new Vector2(cx, cy + h * 0.5f);
                    b = new Vector2(cx + w, cy - h * 0.5f);
                    break;
            }

            Now.Line(a, mid).SetColor(color).SetWidth(1.6f).SetCap(NowLineCap.Round).Draw();
            Now.Line(mid, b).SetColor(color).SetWidth(1.6f).SetCap(NowLineCap.Round).Draw();
        }

        /// <summary>Chevron in the muted text color, pointing up when open.</summary>
        protected static void DrawFieldChevron(NowThemeAsset themeAsset, NowRect rect, bool open)
        {
            DrawChevron(rect, themeAsset.GetColor(NowColorToken.TextMuted), open ? NowChevronDirection.Up : NowChevronDirection.Down);
        }

        /// <summary>Line-drawn check mark sized to the glyph rect.</summary>
        protected static void DrawCheckMark(NowThemeAsset themeAsset, NowRect rect, Color color)
        {
            float x = rect.x;
            float y = rect.y;
            float w = rect.width;
            float h = rect.height;
            Vector2 a = new Vector2(x + w * 0.28f, y + h * 0.53f);
            Vector2 b = new Vector2(x + w * 0.43f, y + h * 0.68f);
            Vector2 c = new Vector2(x + w * 0.74f, y + h * 0.32f);

            Now.Line(a, b).SetColor(color).SetWidth(2f).SetCap(NowLineCap.Round).Draw();
            Now.Line(b, c).SetColor(color).SetWidth(2f).SetCap(NowLineCap.Round).Draw();
        }

        protected static Vector4 ResolveRadius(NowThemeAsset themeAsset, NowThemeRadiusReference reference, NowRect rect, NowRadiusToken fallbackToken)
        {
            Vector4 radius = reference.Resolve(themeAsset);

            if (radius == default)
                radius = themeAsset.GetRadius(fallbackToken, new Vector4(10f, 10f, 10f, 10f));

            if (radius.x >= 999f || radius.y >= 999f || radius.z >= 999f || radius.w >= 999f)
                return Circle(rect);

            return radius;
        }

        protected static Vector4 Circle(NowRect rect)
        {
            float radius = Mathf.Min(rect.width, rect.height) * 0.5f;
            return new Vector4(radius, radius, radius, radius);
        }

        protected static NowRect ScaleRect(NowRect rect, float scale)
        {
            float width = rect.width * scale;
            float height = rect.height * scale;
            return new NowRect(
                rect.x + (rect.width - width) * 0.5f,
                rect.y + (rect.height - height) * 0.5f,
                width,
                height);
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

        /// <summary>
        /// Draws the rectangle with a themed elevation shadow behind it.
        /// <code>theme.Rectangle(rect, NowRectangleStyle.Elevated).DrawElevated(theme, NowElevationToken.Raised);</code>
        /// </summary>
        public static void DrawElevated(this NowRectangle rectangle, NowThemeAsset themeAsset, NowElevationToken elevation)
        {
            if (themeAsset != null)
                themeAsset.controlRenderer.DrawElevationShadow(themeAsset, rectangle.rect, rectangle.radius, elevation);

            rectangle.Draw();
        }
    }
}
