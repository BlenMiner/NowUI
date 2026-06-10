using System;
using System.Collections.Generic;
using UnityEngine;

namespace NowUI
{
    [CreateAssetMenu(menuName = "NowUI/Theme", fileName = "NowUITheme")]
    public sealed class NowUITheme : ScriptableObject
    {
        [SerializeField] string _defaultRectanglePreset = "surface";

        [SerializeField] string _defaultTextPreset = "body";

        [SerializeField] NowUIColorToken[] _palette =
        {
            new NowUIColorToken("background", new Color(0.945f, 0.961f, 0.976f, 1f)),
            new NowUIColorToken("surface", Color.white),
            new NowUIColorToken("surface-muted", new Color(0.973f, 0.980f, 0.988f, 1f)),
            new NowUIColorToken("text", new Color(0.067f, 0.094f, 0.153f, 1f)),
            new NowUIColorToken("text-muted", new Color(0.420f, 0.447f, 0.502f, 1f)),
            new NowUIColorToken("border", new Color(0.886f, 0.910f, 0.941f, 1f)),
            new NowUIColorToken("accent", new Color(0.102f, 0.451f, 0.910f, 1f)),
            new NowUIColorToken("accent-text", Color.white)
        };

        [SerializeField] NowUISpacingToken[] _spacings =
        {
            new NowUISpacingToken("none", default),
            new NowUISpacingToken("xs", new Vector4(4f, 4f, 4f, 4f)),
            new NowUISpacingToken("sm", new Vector4(8f, 8f, 8f, 8f)),
            new NowUISpacingToken("md", new Vector4(12f, 12f, 12f, 12f)),
            new NowUISpacingToken("lg", new Vector4(20f, 20f, 20f, 20f)),
            new NowUISpacingToken("panel", new Vector4(24f, 20f, 24f, 20f))
        };

        [SerializeField] NowUIRadiusToken[] _radii =
        {
            new NowUIRadiusToken("none", default),
            new NowUIRadiusToken("sm", new Vector4(4f, 4f, 4f, 4f)),
            new NowUIRadiusToken("md", new Vector4(8f, 8f, 8f, 8f)),
            new NowUIRadiusToken("lg", new Vector4(14f, 14f, 14f, 14f)),
            new NowUIRadiusToken("pill", new Vector4(999f, 999f, 999f, 999f))
        };

        [SerializeField] NowUIRectanglePreset[] _rectanglePresets =
        {
            new NowUIRectanglePreset(
                "surface",
                new NowUIColorReference("surface", Color.white),
                new NowUIRadiusReference("md", new Vector4(8f, 8f, 8f, 8f)),
                default,
                0f,
                0f,
                new NowUIColorReference("border", Color.clear)),
            new NowUIRectanglePreset(
                "muted",
                new NowUIColorReference("surface-muted", new Color(0.973f, 0.980f, 0.988f, 1f)),
                new NowUIRadiusReference("md", new Vector4(8f, 8f, 8f, 8f)),
                default,
                0f,
                0f,
                new NowUIColorReference("border", Color.clear)),
            new NowUIRectanglePreset(
                "outline",
                new NowUIColorReference(string.Empty, Color.clear),
                new NowUIRadiusReference("md", new Vector4(8f, 8f, 8f, 8f)),
                default,
                0f,
                1f,
                new NowUIColorReference("border", new Color(0.886f, 0.910f, 0.941f, 1f))),
            new NowUIRectanglePreset(
                "accent",
                new NowUIColorReference("accent", new Color(0.102f, 0.451f, 0.910f, 1f)),
                new NowUIRadiusReference("md", new Vector4(8f, 8f, 8f, 8f)),
                default,
                0f,
                0f,
                new NowUIColorReference("accent", Color.clear))
        };

        [SerializeField] NowUITextPreset[] _textPresets =
        {
            new NowUITextPreset(
                "title",
                null,
                28f,
                new NowUIColorReference("text", new Color(0.067f, 0.094f, 0.153f, 1f)),
                0f,
                new NowUIColorReference(string.Empty, Color.clear),
                default),
            new NowUITextPreset(
                "body",
                null,
                16f,
                new NowUIColorReference("text", new Color(0.067f, 0.094f, 0.153f, 1f)),
                0f,
                new NowUIColorReference(string.Empty, Color.clear),
                default),
            new NowUITextPreset(
                "muted",
                null,
                14f,
                new NowUIColorReference("text-muted", new Color(0.420f, 0.447f, 0.502f, 1f)),
                0f,
                new NowUIColorReference(string.Empty, Color.clear),
                default),
            new NowUITextPreset(
                "button",
                null,
                14f,
                new NowUIColorReference("accent-text", Color.white),
                0f,
                new NowUIColorReference(string.Empty, Color.clear),
                default)
        };

        public string defaultRectanglePreset => _defaultRectanglePreset;

        public string defaultTextPreset => _defaultTextPreset;

        public IReadOnlyList<NowUIColorToken> palette => _palette;

        public IReadOnlyList<NowUISpacingToken> spacings => _spacings;

        public IReadOnlyList<NowUIRadiusToken> radii => _radii;

        public IReadOnlyList<NowUIRectanglePreset> rectanglePresets => _rectanglePresets;

        public IReadOnlyList<NowUITextPreset> textPresets => _textPresets;

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

        public bool TryGetRectanglePreset(string id, out NowUIRectanglePreset preset)
        {
            return TryFindToken(_rectanglePresets, id, out preset);
        }

        public bool TryGetTextPreset(string id, out NowUITextPreset preset)
        {
            return TryFindToken(_textPresets, id, out preset);
        }

        static bool TryFindToken<T>(T[] tokens, string id, out T token) where T : INowUIThemeToken
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

        public NowUIRectangle Rectangle(NowRect rect, string presetId = null)
        {
            return ApplyRectanglePreset(Now.Rectangle(rect), ResolveRectanglePresetId(presetId));
        }

        public NowUIText Text(NowRect rect, NowFontAsset font, string presetId = null)
        {
            return ApplyTextPreset(Now.Text(rect, font), ResolveTextPresetId(presetId));
        }

        public NowUIText Text(NowRect rect, string presetId = null)
        {
            var text = ApplyTextPreset(Now.Text(rect, null), ResolveTextPresetId(presetId));

            // Preset fonts win over the ambient font, but a preset without one
            // still resolves to the active font stack.
            if (text.font == null)
                text = text.SetFont(Now.font);

            return text;
        }

        public NowUIRectangle ApplyRectanglePreset(NowUIRectangle rectangle, string presetId = null)
        {
            if (!TryGetRectanglePreset(ResolveRectanglePresetId(presetId), out var preset))
                return rectangle;

            return preset.Apply(this, rectangle);
        }

        public NowUIText ApplyTextPreset(NowUIText text, string presetId = null)
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

    interface INowUIThemeToken
    {
        string id { get; }
    }

    [Serializable]
    public struct NowUIColorToken : INowUIThemeToken
    {
        [SerializeField] string _id;

        [SerializeField] Color _color;

        public NowUIColorToken(string id, Color color)
        {
            _id = id;
            _color = color;
        }

        public string id => _id;

        public Color color => _color;
    }

    [Serializable]
    public struct NowUISpacingToken : INowUIThemeToken
    {
        [SerializeField] string _id;

        [SerializeField] Vector4 _insets;

        public NowUISpacingToken(string id, Vector4 insets)
        {
            _id = id;
            _insets = insets;
        }

        public string id => _id;

        public Vector4 insets => _insets;
    }

    [Serializable]
    public struct NowUIRadiusToken : INowUIThemeToken
    {
        [SerializeField] string _id;

        [SerializeField] Vector4 _radius;

        public NowUIRadiusToken(string id, Vector4 radius)
        {
            _id = id;
            _radius = radius;
        }

        public string id => _id;

        public Vector4 radius => _radius;
    }

    [Serializable]
    public struct NowUIColorReference
    {
        [SerializeField] string _token;

        [SerializeField] Color _fallback;

        public NowUIColorReference(string token, Color fallback)
        {
            _token = token;
            _fallback = fallback;
        }

        public string token => _token;

        public Color fallback => _fallback;

        public Color Resolve(NowUITheme theme)
        {
            if (theme != null && !string.IsNullOrEmpty(_token) && theme.TryGetColor(_token, out var color))
                return color;

            return _fallback;
        }
    }

    [Serializable]
    public struct NowUISpacingReference
    {
        [SerializeField] string _token;

        [SerializeField] Vector4 _fallback;

        public NowUISpacingReference(string token, Vector4 fallback)
        {
            _token = token;
            _fallback = fallback;
        }

        public string token => _token;

        public Vector4 fallback => _fallback;

        public Vector4 Resolve(NowUITheme theme)
        {
            if (theme != null && !string.IsNullOrEmpty(_token) && theme.TryGetSpacing(_token, out var spacing))
                return spacing;

            return _fallback;
        }
    }

    [Serializable]
    public struct NowUIRadiusReference
    {
        [SerializeField] string _token;

        [SerializeField] Vector4 _fallback;

        public NowUIRadiusReference(string token, Vector4 fallback)
        {
            _token = token;
            _fallback = fallback;
        }

        public string token => _token;

        public Vector4 fallback => _fallback;

        public Vector4 Resolve(NowUITheme theme)
        {
            if (theme != null && !string.IsNullOrEmpty(_token) && theme.TryGetRadius(_token, out var radius))
                return radius;

            return _fallback;
        }
    }

    [Serializable]
    public struct NowUIRectanglePreset : INowUIThemeToken
    {
        [SerializeField] string _id;

        [SerializeField] NowUIColorReference _fill;

        [SerializeField] NowUIRadiusReference _radius;

        [SerializeField] NowUISpacingReference _padding;

        [SerializeField] float _blur;

        [SerializeField] float _outline;

        [SerializeField] NowUIColorReference _outlineColor;

        public NowUIRectanglePreset(
            string id,
            NowUIColorReference fill,
            NowUIRadiusReference radius,
            NowUISpacingReference padding,
            float blur,
            float outline,
            NowUIColorReference outlineColor)
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

        public NowUIRectangle Apply(NowUITheme theme, NowUIRectangle rectangle)
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
    public struct NowUITextPreset : INowUIThemeToken
    {
        [SerializeField] string _id;

        [SerializeField] NowFontAsset _font;

        [SerializeField] float _fontSize;

        [SerializeField] NowUIColorReference _color;

        [SerializeField] float _outline;

        [SerializeField] NowUIColorReference _outlineColor;

        [SerializeField] NowUISpacingReference _padding;

        public NowUITextPreset(
            string id,
            NowFontAsset font,
            float fontSize,
            NowUIColorReference color,
            float outline,
            NowUIColorReference outlineColor,
            NowUISpacingReference padding)
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

        public NowUIText Apply(NowUITheme theme, NowUIText text)
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

    public static class NowUIThemeExtensions
    {
        public static NowUIRectangle SetStyle(this NowUIRectangle rectangle, NowUITheme theme, string presetId = null)
        {
            return theme != null ? theme.ApplyRectanglePreset(rectangle, presetId) : rectangle;
        }

        public static NowUIText SetStyle(this NowUIText text, NowUITheme theme, string presetId = null)
        {
            return theme != null ? theme.ApplyTextPreset(text, presetId) : text;
        }
    }
}
