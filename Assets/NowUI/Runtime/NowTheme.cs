using System;
using System.Collections.Generic;
using UnityEngine;

namespace NowUI
{
    [CreateAssetMenu(menuName = "NowUI/Theme", fileName = "NowTheme")]
    public sealed class NowTheme : ScriptableObject
    {
        [SerializeField] string _defaultRectanglePreset = "surface";

        [SerializeField] string _defaultTextPreset = "body";

        [SerializeField] NowThemeColorToken[] _palette =
        {
            new NowThemeColorToken("background", new Color(0.945f, 0.961f, 0.976f, 1f)),
            new NowThemeColorToken("surface", Color.white),
            new NowThemeColorToken("surface-muted", new Color(0.973f, 0.980f, 0.988f, 1f)),
            new NowThemeColorToken("text", new Color(0.067f, 0.094f, 0.153f, 1f)),
            new NowThemeColorToken("text-muted", new Color(0.420f, 0.447f, 0.502f, 1f)),
            new NowThemeColorToken("border", new Color(0.886f, 0.910f, 0.941f, 1f)),
            new NowThemeColorToken("accent", new Color(0.102f, 0.451f, 0.910f, 1f)),
            new NowThemeColorToken("accent-text", Color.white)
        };

        [SerializeField] NowThemeSpacingToken[] _spacings =
        {
            new NowThemeSpacingToken("none", default),
            new NowThemeSpacingToken("xs", new Vector4(4f, 4f, 4f, 4f)),
            new NowThemeSpacingToken("sm", new Vector4(8f, 8f, 8f, 8f)),
            new NowThemeSpacingToken("md", new Vector4(12f, 12f, 12f, 12f)),
            new NowThemeSpacingToken("lg", new Vector4(20f, 20f, 20f, 20f)),
            new NowThemeSpacingToken("panel", new Vector4(24f, 20f, 24f, 20f))
        };

        [SerializeField] NowThemeRadiusToken[] _radii =
        {
            new NowThemeRadiusToken("none", default),
            new NowThemeRadiusToken("sm", new Vector4(4f, 4f, 4f, 4f)),
            new NowThemeRadiusToken("md", new Vector4(8f, 8f, 8f, 8f)),
            new NowThemeRadiusToken("lg", new Vector4(14f, 14f, 14f, 14f)),
            new NowThemeRadiusToken("pill", new Vector4(999f, 999f, 999f, 999f))
        };

        [SerializeField] NowRectanglePreset[] _rectanglePresets =
        {
            new NowRectanglePreset(
                "surface",
                new NowThemeColorReference("surface", Color.white),
                new NowThemeRadiusReference("md", new Vector4(8f, 8f, 8f, 8f)),
                default,
                0f,
                0f,
                new NowThemeColorReference("border", Color.clear)),
            new NowRectanglePreset(
                "muted",
                new NowThemeColorReference("surface-muted", new Color(0.973f, 0.980f, 0.988f, 1f)),
                new NowThemeRadiusReference("md", new Vector4(8f, 8f, 8f, 8f)),
                default,
                0f,
                0f,
                new NowThemeColorReference("border", Color.clear)),
            new NowRectanglePreset(
                "outline",
                new NowThemeColorReference(string.Empty, Color.clear),
                new NowThemeRadiusReference("md", new Vector4(8f, 8f, 8f, 8f)),
                default,
                0f,
                1f,
                new NowThemeColorReference("border", new Color(0.886f, 0.910f, 0.941f, 1f))),
            new NowRectanglePreset(
                "accent",
                new NowThemeColorReference("accent", new Color(0.102f, 0.451f, 0.910f, 1f)),
                new NowThemeRadiusReference("md", new Vector4(8f, 8f, 8f, 8f)),
                default,
                0f,
                0f,
                new NowThemeColorReference("accent", Color.clear))
        };

        [SerializeField] NowTextPreset[] _textPresets =
        {
            new NowTextPreset(
                "title",
                null,
                28f,
                new NowThemeColorReference("text", new Color(0.067f, 0.094f, 0.153f, 1f)),
                0f,
                new NowThemeColorReference(string.Empty, Color.clear),
                default),
            new NowTextPreset(
                "body",
                null,
                16f,
                new NowThemeColorReference("text", new Color(0.067f, 0.094f, 0.153f, 1f)),
                0f,
                new NowThemeColorReference(string.Empty, Color.clear),
                default),
            new NowTextPreset(
                "muted",
                null,
                14f,
                new NowThemeColorReference("text-muted", new Color(0.420f, 0.447f, 0.502f, 1f)),
                0f,
                new NowThemeColorReference(string.Empty, Color.clear),
                default),
            new NowTextPreset(
                "button",
                null,
                14f,
                new NowThemeColorReference("accent-text", Color.white),
                0f,
                new NowThemeColorReference(string.Empty, Color.clear),
                default)
        };

        public string defaultRectanglePreset => _defaultRectanglePreset;

        public string defaultTextPreset => _defaultTextPreset;

        public IReadOnlyList<NowThemeColorToken> palette => _palette;

        public IReadOnlyList<NowThemeSpacingToken> spacings => _spacings;

        public IReadOnlyList<NowThemeRadiusToken> radii => _radii;

        public IReadOnlyList<NowRectanglePreset> rectanglePresets => _rectanglePresets;

        public IReadOnlyList<NowTextPreset> textPresets => _textPresets;

        public bool TryGetColor(string id, out Color color)
        {
            bool found = TryFindToken(_palette, id, out var token);
            color = found ? token.color : default;
            return found;
        }

        public Color GetColor(string id, Color fallback)
        {
            return TryGetColor(id, out var color) ? color : fallback;
        }

        public bool TryGetSpacing(string id, out Vector4 spacing)
        {
            bool found = TryFindToken(_spacings, id, out var token);
            spacing = found ? token.insets : default;
            return found;
        }

        public Vector4 GetSpacing(string id, Vector4 fallback)
        {
            return TryGetSpacing(id, out var spacing) ? spacing : fallback;
        }

        public bool TryGetRadius(string id, out Vector4 radius)
        {
            bool found = TryFindToken(_radii, id, out var token);
            radius = found ? token.radius : default;
            return found;
        }

        public Vector4 GetRadius(string id, Vector4 fallback)
        {
            return TryGetRadius(id, out var radius) ? radius : fallback;
        }

        public bool TryGetRectanglePreset(string id, out NowRectanglePreset preset)
        {
            return TryFindToken(_rectanglePresets, id, out preset);
        }

        public bool TryGetTextPreset(string id, out NowTextPreset preset)
        {
            return TryFindToken(_textPresets, id, out preset);
        }

        static bool TryFindToken<T>(T[] tokens, string id, out T token) where T : INowThemeToken
        {
            token = default;

            if (string.IsNullOrEmpty(id) || tokens == null)
                return false;

            for (int i = 0; i < tokens.Length; ++i)
            {
                if (!IdEquals(tokens[i].id, id))
                    continue;

                token = tokens[i];
                return true;
            }

            return false;
        }

        public NowRect Inset(NowRect rect, string spacingId)
        {
            return Inset(rect, GetSpacing(spacingId, default));
        }

        public NowRect Outset(NowRect rect, string spacingId)
        {
            return Outset(rect, GetSpacing(spacingId, default));
        }

        public static NowRect Inset(NowRect rect, Vector4 spacing)
        {
            return rect.Inset(spacing.x, spacing.y, spacing.z, spacing.w);
        }

        public static NowRect Outset(NowRect rect, Vector4 spacing)
        {
            return rect.Outset(spacing.x, spacing.y, spacing.z, spacing.w);
        }

        public NowRectangle Rectangle(NowRect rect, string presetId = null)
        {
            return ApplyRectanglePreset(Now.Rectangle(rect), ResolveRectanglePresetId(presetId));
        }

        public NowText Text(NowRect rect, NowFontAsset font, string presetId = null)
        {
            return ApplyTextPreset(Now.Text(rect, font), ResolveTextPresetId(presetId));
        }

        /// <summary>Creates preset-styled text. Preset fonts win over the ambient font,
        /// but a preset without one still resolves to the active font stack.</summary>
        public NowText Text(NowRect rect, string presetId = null)
        {
            var text = ApplyTextPreset(Now.Text(rect, null), ResolveTextPresetId(presetId));

            if (text.font == null)
                text = text.SetFont(Now.font);

            return text;
        }

        public NowRectangle ApplyRectanglePreset(NowRectangle rectangle, string presetId = null)
        {
            if (!TryGetRectanglePreset(ResolveRectanglePresetId(presetId), out var preset))
                return rectangle;

            return preset.Apply(this, rectangle);
        }

        public NowText ApplyTextPreset(NowText text, string presetId = null)
        {
            if (!TryGetTextPreset(ResolveTextPresetId(presetId), out var preset))
                return text;

            return preset.Apply(this, text);
        }

        string ResolveRectanglePresetId(string presetId)
        {
            return string.IsNullOrEmpty(presetId) ? _defaultRectanglePreset : presetId;
        }

        string ResolveTextPresetId(string presetId)
        {
            return string.IsNullOrEmpty(presetId) ? _defaultTextPreset : presetId;
        }

        static bool IdEquals(string lhs, string rhs)
        {
            return string.Equals(lhs, rhs, StringComparison.OrdinalIgnoreCase);
        }
    }

    interface INowThemeToken
    {
        string id { get; }
    }

    [Serializable]
    public struct NowThemeColorToken : INowThemeToken
    {
        [SerializeField] string _id;

        [SerializeField] Color _color;

        public NowThemeColorToken(string id, Color color)
        {
            _id = id;
            _color = color;
        }

        public string id => _id;

        public Color color => _color;
    }

    [Serializable]
    public struct NowThemeSpacingToken : INowThemeToken
    {
        [SerializeField] string _id;

        [SerializeField] Vector4 _insets;

        public NowThemeSpacingToken(string id, Vector4 insets)
        {
            _id = id;
            _insets = insets;
        }

        public string id => _id;

        public Vector4 insets => _insets;
    }

    [Serializable]
    public struct NowThemeRadiusToken : INowThemeToken
    {
        [SerializeField] string _id;

        [SerializeField] Vector4 _radius;

        public NowThemeRadiusToken(string id, Vector4 radius)
        {
            _id = id;
            _radius = radius;
        }

        public string id => _id;

        public Vector4 radius => _radius;
    }

    [Serializable]
    public struct NowThemeColorReference
    {
        [SerializeField] string _token;

        [SerializeField] Color _fallback;

        public NowThemeColorReference(string token, Color fallback)
        {
            _token = token;
            _fallback = fallback;
        }

        public string token => _token;

        public Color fallback => _fallback;

        public Color Resolve(NowTheme theme)
        {
            if (theme != null && !string.IsNullOrEmpty(_token) && theme.TryGetColor(_token, out var color))
                return color;

            return _fallback;
        }
    }

    [Serializable]
    public struct NowThemeSpacingReference
    {
        [SerializeField] string _token;

        [SerializeField] Vector4 _fallback;

        public NowThemeSpacingReference(string token, Vector4 fallback)
        {
            _token = token;
            _fallback = fallback;
        }

        public string token => _token;

        public Vector4 fallback => _fallback;

        public Vector4 Resolve(NowTheme theme)
        {
            if (theme != null && !string.IsNullOrEmpty(_token) && theme.TryGetSpacing(_token, out var spacing))
                return spacing;

            return _fallback;
        }
    }

    [Serializable]
    public struct NowThemeRadiusReference
    {
        [SerializeField] string _token;

        [SerializeField] Vector4 _fallback;

        public NowThemeRadiusReference(string token, Vector4 fallback)
        {
            _token = token;
            _fallback = fallback;
        }

        public string token => _token;

        public Vector4 fallback => _fallback;

        public Vector4 Resolve(NowTheme theme)
        {
            if (theme != null && !string.IsNullOrEmpty(_token) && theme.TryGetRadius(_token, out var radius))
                return radius;

            return _fallback;
        }
    }

    [Serializable]
    public struct NowRectanglePreset : INowThemeToken
    {
        [SerializeField] string _id;

        [SerializeField] NowThemeColorReference _fill;

        [SerializeField] NowThemeRadiusReference _radius;

        [SerializeField] NowThemeSpacingReference _padding;

        [SerializeField] float _blur;

        [SerializeField] float _outline;

        [SerializeField] NowThemeColorReference _outlineColor;

        public NowRectanglePreset(
            string id,
            NowThemeColorReference fill,
            NowThemeRadiusReference radius,
            NowThemeSpacingReference padding,
            float blur,
            float outline,
            NowThemeColorReference outlineColor)
        {
            _id = id;
            _fill = fill;
            _radius = radius;
            _padding = padding;
            _blur = blur;
            _outline = outline;
            _outlineColor = outlineColor;
        }

        public string id => _id;

        public float blur => _blur;

        public float outline => _outline;

        public NowRectangle Apply(NowTheme theme, NowRectangle rectangle)
        {
            rectangle.color = _fill.Resolve(theme);
            rectangle.radius = _radius.Resolve(theme);
            rectangle = rectangle.SetPadding(_padding.Resolve(theme));
            rectangle.blur = _blur;
            rectangle.outline = _outline;
            rectangle.outlineColor = _outlineColor.Resolve(theme);
            return rectangle;
        }
    }

    [Serializable]
    public struct NowTextPreset : INowThemeToken
    {
        [SerializeField] string _id;

        [SerializeField] NowFontAsset _font;

        [SerializeField] float _fontSize;

        [SerializeField] NowThemeColorReference _color;

        [SerializeField] float _outline;

        [SerializeField] NowThemeColorReference _outlineColor;

        [SerializeField] NowThemeSpacingReference _padding;

        public NowTextPreset(
            string id,
            NowFontAsset font,
            float fontSize,
            NowThemeColorReference color,
            float outline,
            NowThemeColorReference outlineColor,
            NowThemeSpacingReference padding)
        {
            _id = id;
            _font = font;
            _fontSize = fontSize;
            _color = color;
            _outline = outline;
            _outlineColor = outlineColor;
            _padding = padding;
        }

        public string id => _id;

        public float fontSize => _fontSize;

        public NowText Apply(NowTheme theme, NowText text)
        {
            if (_font != null)
                text.font = _font;

            if (_fontSize > 0f)
                text.fontSize = _fontSize;

            text.color = _color.Resolve(theme);
            text.outline = _outline;
            text.outlineColor = _outlineColor.Resolve(theme);
            text.padding = _padding.Resolve(theme);
            return text;
        }
    }

    public static class NowThemeExtensions
    {
        public static NowRectangle SetStyle(this NowRectangle rectangle, NowTheme theme, string presetId = null)
        {
            return theme != null ? theme.ApplyRectanglePreset(rectangle, presetId) : rectangle;
        }

        public static NowText SetStyle(this NowText text, NowTheme theme, string presetId = null)
        {
            return theme != null ? theme.ApplyTextPreset(text, presetId) : text;
        }
    }
}
