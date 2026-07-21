using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NowUI.Internal;
using UnityEngine;

namespace NowUI
{
    /// <summary>The geometry used to turn local rectangle coordinates into a gradient ramp position.</summary>
    public enum NowGradientKind
    {
        Linear,
        Radial,
        Conic
    }

    /// <summary>Named CSS-style directions for linear gradients.</summary>
    public enum NowGradientDirection
    {
        ToTop,
        ToTopRight,
        ToRight,
        ToBottomRight,
        ToBottom,
        ToBottomLeft,
        ToLeft,
        ToTopLeft
    }

    /// <summary>Whether a radial gradient measures distance as a circle or an ellipse.</summary>
    public enum NowGradientShape
    {
        Ellipse,
        Circle
    }

    /// <summary>How ramp positions outside 0..1 are mapped back onto the color ramp.</summary>
    public enum NowGradientSpread
    {
        Clamp,
        Repeat,
        Mirror
    }

    /// <summary>
    /// CSS-inspired gradient paint. The ramp is independent from the geometry:
    /// choose linear, radial, or conic mapping, then supply two colors or a Unity
    /// <see cref="UnityEngine.Gradient"/> and finish with <see cref="Draw"/>.
    /// </summary>
    [NowBuilder]
    public struct NowGradient
    {
        public NowRect mask;

        public NowRect rect;

        public Vector4 radius;

        public Vector4 padding;

        public float blur;

        public float outline;

        public Vector4 outlineColor;

        public Vector4 tint;

        internal NowGradientKind kind;

        internal NowGradientShape shape;

        internal NowGradientSpread spread;

        internal Vector4 parameters;

        internal Vector4 colorFrom;

        internal Vector4 colorTo;

        internal UnityEngine.Gradient ramp;

        internal int rampRevision;

        internal float repetitions;

        internal NowGradient(NowRect rect)
        {
            mask = rect;
            this.rect = rect;
            radius = default;
            padding = default;
            blur = default;
            outline = default;
            outlineColor = default;
            tint = Vector4.one;
            kind = NowGradientKind.Linear;
            shape = NowGradientShape.Ellipse;
            spread = NowGradientSpread.Clamp;
            parameters = new Vector4(0f, 1f, 0f, 0f);
            colorFrom = Color.black;
            colorTo = Color.white;
            ramp = null;
            rampRevision = 0;
            repetitions = 1f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowGradient SetPosition(NowRect rect)
        {
            if (mask == this.rect)
                mask = rect;

            this.rect = rect;
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowGradient SetMask(NowRect mask)
        {
            this.mask = mask;
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowGradient SetRadius(float allRadius)
        {
            radius = new Vector4(allRadius, allRadius, allRadius, allRadius);
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowGradient SetRadius(float topLeft, float topRight, float bottomRight, float bottomLeft)
        {
            radius = new NowCornerRadius(topLeft, topRight, bottomRight, bottomLeft).packed;
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowGradient SetRadius(NowCornerRadius radius)
        {
            this.radius = radius.packed;
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowGradient SetRadius(Vector4 radius)
        {
            this.radius = radius;
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowGradient SetPadding(float all)
        {
            return SetPadding(new Vector4(all, all, all, all));
        }

        /// <summary>Expands the painted rect (x = left, y = top, z = right, w = bottom).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowGradient SetPadding(Vector4 padding)
        {
            this.padding = padding;
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowGradient SetBlur(float blur)
        {
            this.blur = blur;
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowGradient SetOutline(float outline)
        {
            this.outline = outline;
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowGradient SetOutline(float outline, Color color)
        {
            this.outline = outline;
            outlineColor = color;
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowGradient SetOutline(float outline, Vector4 color)
        {
            this.outline = outline;
            outlineColor = color;
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowGradient SetOutlineColor(Color color)
        {
            outlineColor = color;
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowGradient SetOutlineColor(Vector4 color)
        {
            outlineColor = color;
            return this;
        }

        /// <summary>Multiplies the sampled ramp color without creating a distinct cached ramp.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowGradient SetTint(Color tint)
        {
            this.tint = tint;
            return this;
        }

        /// <summary>Multiplies the sampled ramp color without creating a distinct cached ramp.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowGradient SetTint(Vector4 tint)
        {
            this.tint = tint;
            return this;
        }

        /// <summary>Uses a blended two-color ramp and clears any assigned Unity gradient.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowGradient SetColors(Color from, Color to)
        {
            colorFrom = from;
            colorTo = to;
            ramp = null;
            rampRevision = 0;
            return this;
        }

        /// <summary>Uses a blended two-color ramp and clears any assigned Unity gradient.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowGradient SetColors(Vector4 from, Vector4 to)
        {
            colorFrom = from;
            colorTo = to;
            ramp = null;
            rampRevision = 0;
            return this;
        }

        /// <summary>
        /// Uses all color and alpha keys from a Unity gradient. Cached ramps are
        /// keyed by object identity. Increment <paramref name="revision"/> when the
        /// same Gradient instance is mutated in place, or call
        /// <see cref="Now.InvalidateGradient(UnityEngine.Gradient)"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowGradient SetRamp(UnityEngine.Gradient gradient, int revision = 0)
        {
            ramp = gradient;
            rampRevision = revision;
            return this;
        }

        /// <summary>Maps the ramp along a named direction across the whole rectangle.</summary>
        public NowGradient SetLinear(NowGradientDirection direction = NowGradientDirection.ToBottom)
        {
            kind = NowGradientKind.Linear;
            parameters = Direction(direction);
            return this;
        }

        /// <summary>
        /// Maps the ramp along a CSS-style angle: 0 points up, 90 right, 180 down,
        /// and positive angles rotate clockwise.
        /// </summary>
        public NowGradient SetLinear(float angleDegrees)
        {
            float radians = angleDegrees * Mathf.Deg2Rad;
            return SetLinear(new Vector2(Mathf.Sin(radians), -Mathf.Cos(radians)));
        }

        /// <summary>Maps the ramp along a local UI-space direction (positive y points down).</summary>
        public NowGradient SetLinear(Vector2 direction)
        {
            kind = NowGradientKind.Linear;
            parameters = new Vector4(direction.x, direction.y, 0f, 0f);
            return this;
        }

        /// <summary>Uses a centered ellipse whose x/y radii are half the rectangle.</summary>
        public NowGradient SetRadial(NowGradientShape shape = NowGradientShape.Ellipse)
        {
            this.shape = shape;
            kind = NowGradientKind.Radial;
            parameters = new Vector4(0.5f, 0.5f, 0.5f, 0.5f);
            return this;
        }

        /// <summary>
        /// Uses an ellipse in normalized rectangle coordinates. A radius of 0.5
        /// reaches the corresponding pair of sides from a centered origin.
        /// </summary>
        public NowGradient SetRadial(Vector2 center, Vector2 radius)
        {
            shape = NowGradientShape.Ellipse;
            kind = NowGradientKind.Radial;
            parameters = new Vector4(center.x, center.y, radius.x, radius.y);
            return this;
        }

        /// <summary>
        /// Uses a screen-space circle. Radius is relative to the rectangle's
        /// smaller dimension, so 0.5 reaches the nearest side from the center.
        /// </summary>
        public NowGradient SetRadial(Vector2 center, float radius)
        {
            shape = NowGradientShape.Circle;
            kind = NowGradientKind.Radial;
            parameters = new Vector4(center.x, center.y, radius, radius);
            return this;
        }

        /// <summary>Uses a clockwise sweep around the rectangle center, starting at the top.</summary>
        public NowGradient SetConic()
        {
            return SetConic(new Vector2(0.5f, 0.5f), 0f);
        }

        /// <summary>
        /// Uses a clockwise sweep around <paramref name="center"/> in normalized
        /// rectangle coordinates. Angles follow CSS: 0 is up and 90 is right.
        /// </summary>
        public NowGradient SetConic(Vector2 center, float startAngle = 0f)
        {
            kind = NowGradientKind.Conic;
            parameters = new Vector4(center.x, center.y, startAngle / 360f, 0f);
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowGradient SetSpread(NowGradientSpread spread)
        {
            this.spread = spread;
            return this;
        }

        /// <summary>
        /// Scales the ramp position before applying its spread mode. Values above
        /// one produce repeating bands with Repeat or Mirror.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowGradient SetRepetitions(float repetitions)
        {
            this.repetitions = repetitions;
            return this;
        }

        [NowConsumer]
        public NowGradient Draw()
        {
            Now.DrawGradient(this);
            return this;
        }

        static Vector4 Direction(NowGradientDirection direction)
        {
            const float diagonal = 0.70710678118f;

            switch (direction)
            {
                case NowGradientDirection.ToTop: return new Vector4(0f, -1f, 0f, 0f);
                case NowGradientDirection.ToTopRight: return new Vector4(diagonal, -diagonal, 0f, 0f);
                case NowGradientDirection.ToRight: return new Vector4(1f, 0f, 0f, 0f);
                case NowGradientDirection.ToBottomRight: return new Vector4(diagonal, diagonal, 0f, 0f);
                case NowGradientDirection.ToBottomLeft: return new Vector4(-diagonal, diagonal, 0f, 0f);
                case NowGradientDirection.ToLeft: return new Vector4(-1f, 0f, 0f, 0f);
                case NowGradientDirection.ToTopLeft: return new Vector4(-diagonal, -diagonal, 0f, 0f);
                default: return new Vector4(0f, 1f, 0f, 0f);
            }
        }
    }

    internal readonly struct NowGradientRampHandle
    {
        public readonly int row;

        public readonly bool fixedMode;

        public NowGradientRampHandle(int row, bool fixedMode)
        {
            this.row = row;
            this.fixedMode = fixedMode;
        }
    }

    internal static class NowGradientRampCache
    {
        internal const int TextureWidth = 256;

        internal const int TextureHeight = 256;

        readonly struct ColorRampKey : IEquatable<ColorRampKey>
        {
            readonly uint _from;

            readonly uint _to;

            public ColorRampKey(Color32 from, Color32 to)
            {
                _from = Pack(from);
                _to = Pack(to);
            }

            public bool Equals(ColorRampKey other)
            {
                return _from == other._from && _to == other._to;
            }

            public override bool Equals(object obj)
            {
                return obj is ColorRampKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((int)_from * 397) ^ (int)_to;
                }
            }

            static uint Pack(Color32 color)
            {
                return (uint)(color.r | color.g << 8 | color.b << 16 | color.a << 24);
            }
        }

        struct GradientRampEntry
        {
            public int row;

            public int revision;

            public GradientMode mode;

            public bool dirty;
        }

        sealed class GradientReferenceComparer : IEqualityComparer<UnityEngine.Gradient>
        {
            public bool Equals(UnityEngine.Gradient x, UnityEngine.Gradient y)
            {
                return ReferenceEquals(x, y);
            }

            public int GetHashCode(UnityEngine.Gradient obj)
            {
                return obj != null ? RuntimeHelpers.GetHashCode(obj) : 0;
            }
        }

        static readonly Dictionary<ColorRampKey, NowGradientRampHandle> _colorRamps =
            new Dictionary<ColorRampKey, NowGradientRampHandle>(16);

        static readonly Dictionary<UnityEngine.Gradient, GradientRampEntry> _gradientRamps =
            new Dictionary<UnityEngine.Gradient, GradientRampEntry>(new GradientReferenceComparer());

        static Texture2D _texture;

        static Color32[] _rowPixels;

        static int _nextRow;

        static bool _reportedFull;

        internal static Texture2D texture
        {
            get
            {
                EnsureTexture();
                return _texture;
            }
        }

        internal static int rampCount => _nextRow;

        internal static NowGradientRampHandle Get(Color from, Color to)
        {
            EnsureTexture();
            Color32 from32 = from;
            Color32 to32 = to;
            var key = new ColorRampKey(from32, to32);

            if (_colorRamps.TryGetValue(key, out var handle))
                return handle;

            int row = AllocateRow();

            if (row <= 0)
                return new NowGradientRampHandle(0, false);

            for (int i = 0; i < TextureWidth; ++i)
            {
                float t = i / (TextureWidth - 1f);
                _rowPixels[i] = Color.Lerp((Color)from32, (Color)to32, t);
            }

            UploadRow(row);
            handle = new NowGradientRampHandle(row, false);
            _colorRamps.Add(key, handle);
            return handle;
        }

        internal static NowGradientRampHandle Get(UnityEngine.Gradient gradient, int revision)
        {
            if (gradient == null)
                return Get(Color.black, Color.white);

            EnsureTexture();
            GradientMode mode = gradient.mode;

            if (_gradientRamps.TryGetValue(gradient, out var entry))
            {
                if (!entry.dirty && entry.revision == revision && entry.mode == mode)
                    return new NowGradientRampHandle(entry.row, mode == GradientMode.Fixed);

                BakeGradientRow(gradient, entry.row);
                entry.revision = revision;
                entry.mode = mode;
                entry.dirty = false;
                _gradientRamps[gradient] = entry;
                return new NowGradientRampHandle(entry.row, mode == GradientMode.Fixed);
            }

            int row = AllocateRow();

            if (row <= 0)
                return new NowGradientRampHandle(0, false);

            BakeGradientRow(gradient, row);
            _gradientRamps.Add(gradient, new GradientRampEntry
            {
                row = row,
                revision = revision,
                mode = mode,
                dirty = false
            });
            return new NowGradientRampHandle(row, mode == GradientMode.Fixed);
        }

        internal static void Invalidate(UnityEngine.Gradient gradient)
        {
            if (gradient == null || !_gradientRamps.TryGetValue(gradient, out var entry))
                return;

            entry.dirty = true;
            _gradientRamps[gradient] = entry;
        }

        static void EnsureTexture()
        {
            if (_texture != null)
                return;

            _texture = new Texture2D(TextureWidth, TextureHeight, TextureFormat.RGBA32, false, true)
            {
                name = "Now Gradient Ramp Atlas",
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            _rowPixels = new Color32[TextureWidth];
            _nextRow = 1;

            for (int i = 0; i < TextureWidth; ++i)
                _rowPixels[i] = (i & 8) == 0 ? Color.magenta : Color.black;

            UploadRow(0);
        }

        static int AllocateRow()
        {
            if (_nextRow < TextureHeight)
                return _nextRow++;

            if (!_reportedFull)
            {
                _reportedFull = true;
                Debug.LogError(
                    $"NowUI gradient ramp atlas is full ({TextureHeight - 1} unique ramps). " +
                    "Reuse Color pairs or Gradient instances instead of creating unbounded ramp values.");
            }

            return 0;
        }

        static void BakeGradientRow(UnityEngine.Gradient gradient, int row)
        {
            for (int i = 0; i < TextureWidth; ++i)
                _rowPixels[i] = gradient.Evaluate(i / (TextureWidth - 1f));

            UploadRow(row);
        }

        static void UploadRow(int row)
        {
            _texture.SetPixels32(0, row, TextureWidth, 1, _rowPixels);
            _texture.Apply(false, false);
        }

        internal static void Reset()
        {
            _colorRamps.Clear();
            _gradientRamps.Clear();
            _rowPixels = null;
            _nextRow = 0;
            _reportedFull = false;

            if (_texture == null)
                return;

            if (Application.isPlaying)
                UnityEngine.Object.Destroy(_texture);
            else
                UnityEngine.Object.DestroyImmediate(_texture);

            _texture = null;
        }
    }

    internal static class NowGradientMaterials
    {
        static Material _material;

        static Material _canvasMaterial;

        internal static bool TryGet(out Material material, out Material canvasMaterial)
        {
            var atlas = NowGradientRampCache.texture;

            if (_material == null)
            {
                var template = Now.LoadRequiredResource<Material>("NowUI/GradientMaterial");

                if (template != null)
                {
                    _material = new Material(template)
                    {
                        name = "Now Gradient Material",
                        hideFlags = HideFlags.HideAndDontSave
                    };
                }
            }

            if (_canvasMaterial == null)
            {
                var template = Now.LoadRequiredResource<Material>("NowUI/GradientMaterialUGUI");

                if (template != null)
                {
                    _canvasMaterial = new Material(template)
                    {
                        name = "Now Gradient Material UGUI",
                        hideFlags = HideFlags.HideAndDontSave
                    };
                }
            }

            if (_material != null && !ReferenceEquals(_material.mainTexture, atlas))
                _material.mainTexture = atlas;

            if (_canvasMaterial != null && !ReferenceEquals(_canvasMaterial.mainTexture, atlas))
                _canvasMaterial.mainTexture = atlas;

            material = _material;
            canvasMaterial = _canvasMaterial;
            return material != null && canvasMaterial != null;
        }

        internal static void Reset()
        {
            Release(ref _material);
            Release(ref _canvasMaterial);
        }

        static void Release(ref Material material)
        {
            if (material == null)
                return;

            NowGraphic.ReleaseCachedMaterial(material);
            NowWorldGraphic.ReleaseCachedMaterial(material);

            if (Application.isPlaying)
                UnityEngine.Object.Destroy(material);
            else
                UnityEngine.Object.DestroyImmediate(material);

            material = null;
        }
    }

    public static partial class Now
    {
        const int GradientKindMask = 0x3;

        const int GradientSpreadShift = 2;

        const int GradientCircleFlag = 1 << 4;

        const int GradientFixedFlag = 1 << 5;

        /// <summary>Creates a top-to-bottom black-to-white gradient builder.</summary>
        public static NowGradient Gradient(NowRect rect)
        {
            return new NowGradient(rect);
        }

        /// <summary>Creates a top-to-bottom two-color gradient builder.</summary>
        public static NowGradient Gradient(NowRect rect, Color from, Color to)
        {
            return new NowGradient(rect).SetColors(from, to);
        }

        /// <summary>Creates a top-to-bottom builder backed by all keys in a Unity gradient.</summary>
        public static NowGradient Gradient(NowRect rect, UnityEngine.Gradient ramp, int revision = 0)
        {
            return new NowGradient(rect).SetRamp(ramp, revision);
        }

        /// <summary>
        /// Marks a cached Unity gradient ramp dirty after its keys were mutated in
        /// place. The next draw rebakes the existing atlas row.
        /// </summary>
        public static void InvalidateGradient(UnityEngine.Gradient gradient)
        {
            NowGradientRampCache.Invalidate(gradient);
        }

        internal static void DrawGradient(in NowGradient gradient)
        {
            if (_suppressDrawDepth > 0 || _defaultMaterial == null)
                return;

            var position = gradient.rect;
            var pad = gradient.padding;
            position.x -= pad.x;
            position.y -= pad.y;
            position.width += pad.x + pad.z;
            position.height += pad.y + pad.w;

            bool hasTransform = _transformStack.Count > 0;
            float x0, y0, rectWidth, rectHeight;

            if (hasTransform)
            {
                x0 = position.x;
                y0 = position.y;
                rectWidth = position.width;
                rectHeight = position.height;
            }
            else
            {
                x0 = Mathf.RoundToInt(position.x);
                y0 = Mathf.RoundToInt(position.y);
                rectWidth = Mathf.RoundToInt(position.x + position.width) - x0;
                rectHeight = Mathf.RoundToInt(position.y + position.height) - y0;
            }

            if (rectWidth <= 0f || rectHeight <= 0f)
                return;

            var rectMask = gradient.mask;
            bool defaultMask = !rectMask.isEmpty && rectMask == gradient.rect;

            if (!rectMask.isEmpty)
            {
                rectMask.x -= pad.x;
                rectMask.y -= pad.y;
                rectMask.width += pad.x + pad.z;
                rectMask.height += pad.y + pad.w;
            }

            float visualPadding = RectangleVisualPadding(gradient.blur, gradient.outline);
            float blur = gradient.blur;
            float outline = gradient.outline;
            Vector4 radius = gradient.radius;
            float geometryPadding = visualPadding;

            if (hasTransform)
            {
                float scalar = ApplyTransformScalar(1f);
                blur *= scalar;
                outline *= scalar;
                radius *= scalar;
                geometryPadding = RectangleVisualPadding(blur, outline);
            }

            if (defaultMask)
                rectMask = rectMask.Outset(visualPadding);

            if (hasTransform)
                rectMask = ApplyTransformRect(rectMask);

            _tmpVertex.mask = ApplyAmbientMask(rectMask);
            _tmpVertex.radius = radius;
            _tmpVertex.outlineColor = ApplyColorMultiplier(gradient.outlineColor);
            _tmpVertex.uvwh = default;

            if (hasTransform)
            {
                Vector2 transformedPos = ApplyTransform(new Vector2(x0, y0));
                Vector2 scaledSize = ApplyTransformSize(new Vector2(rectWidth, rectHeight));
                _tmpVertex.position = new Vector4(
                    transformedPos.x,
                    -transformedPos.y - scaledSize.y,
                    scaledSize.x,
                    scaledSize.y);
            }
            else
            {
                _tmpVertex.position = new Vector4(x0, -y0 - rectHeight, rectWidth, rectHeight);
            }

            Vector4 geometry = _tmpVertex.position;
            geometry.x -= geometryPadding;
            geometry.y -= geometryPadding;
            geometry.z += geometryPadding * 2f;
            geometry.w += geometryPadding * 2f;

            if (_tmpVertex.IsOutsideMask(geometry))
                return;

            NowGradientRampHandle ramp = gradient.ramp != null
                ? NowGradientRampCache.Get(gradient.ramp, gradient.rampRevision)
                : NowGradientRampCache.Get(gradient.colorFrom, gradient.colorTo);

            if (!NowGradientMaterials.TryGet(out var material, out var canvasMaterial))
                return;

            var mesh = UseMaterial(material, canvasMaterial, NowMeshKind.Gradient, default);

            if (mesh == null)
                return;

            mesh = EnsureMeshCapacity(mesh, material, NowMeshKind.Gradient, 4);

            float repetitions = gradient.repetitions;

            if (!IsFiniteGradientValue(repetitions) || Mathf.Abs(repetitions) < 0.0001f)
                repetitions = 1f;

            repetitions = Mathf.Abs(repetitions);
            Vector4 parameters = gradient.parameters;

            switch (gradient.kind)
            {
                case NowGradientKind.Linear:
                    parameters.z = repetitions;
                    break;
                case NowGradientKind.Radial:
                    parameters.z /= repetitions;
                    parameters.w /= repetitions;
                    break;
                case NowGradientKind.Conic:
                    parameters.w = repetitions;
                    break;
            }

            _tmpVertex.color = parameters;

            int flags = ((int)gradient.kind & GradientKindMask) |
                ((int)gradient.spread << GradientSpreadShift);

            if (gradient.kind == NowGradientKind.Radial && gradient.shape == NowGradientShape.Circle)
                flags |= GradientCircleFlag;

            if (ramp.fixedMode)
                flags |= GradientFixedFlag;

            float encodedRamp = ramp.row + (flags + 0.5f) / 256f;
            Vector4 extra = new Vector4(blur, outline, 0f, encodedRamp);
            Vector4 tint = ApplyColorMultiplier(gradient.tint);

            var packedTint = new Vector2(
                PackGradientPair(tint.x, tint.y),
                PackGradientPair(tint.z, tint.w));

            mesh.AddGradientRect(_tmpVertex, extra, geometryPadding, packedTint);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static float PackGradientPair(float first, float second)
        {
            const int max = 255;
            int a = Mathf.RoundToInt(Mathf.Clamp01(first) * max);
            int b = Mathf.RoundToInt(Mathf.Clamp01(second) * max);
            return a * 256 + b;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool IsFiniteGradientValue(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetGradientRuntime()
        {
            NowGradientMaterials.Reset();
            NowGradientRampCache.Reset();
        }
    }
}
