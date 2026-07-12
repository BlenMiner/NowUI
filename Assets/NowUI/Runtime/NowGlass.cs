using System;
using System.Collections.Generic;
using NowUI.Internal;
using UnityEngine;

namespace NowUI
{
    [NowBuilder]
    public struct NowGlass
    {
        public NowRect rect;

        public NowRect mask;

        public Vector4 radius;

        public Vector4 tint;

        public Vector4 outlineColor;

        public float blurRadius;

        public float outline;

        public float saturation;

        public float brightness;

        public NowGlassBlurQuality blurQuality;

        public NowGlass(NowRect rect)
        {
            this.rect = rect;
            mask = rect;
            radius = default;
            tint = new Vector4(1f, 1f, 1f, 0.22f);
            outlineColor = default;
            blurRadius = 16f;
            outline = 0f;
            saturation = 1.15f;
            brightness = 1f;
            blurQuality = NowGlassBlurQuality.Auto;
        }

        /// <summary>
        /// Moves the glass rect. The default mask (which the constructor sets to
        /// the rect) follows the move; a mask pinned with
        /// <see cref="SetMask(NowRect)"/> stays where it was put.
        /// </summary>
        public NowGlass SetPosition(NowRect rect)
        {
            if (mask == this.rect)
                mask = rect;

            this.rect = rect;
            return this;
        }

        /// <summary>
        /// Pins the clip mask independently of the rect: later
        /// <see cref="SetPosition(NowRect)"/> calls no longer move it.
        /// </summary>
        public NowGlass SetMask(NowRect mask)
        {
            this.mask = mask;
            return this;
        }

        public NowGlass SetRadius(float allRadius)
        {
            radius = new Vector4(allRadius, allRadius, allRadius, allRadius);
            return this;
        }

        public NowGlass SetRadius(float topLeft, float topRight, float bottomRight, float bottomLeft)
        {
            radius = new NowCornerRadius(topLeft, topRight, bottomRight, bottomLeft).packed;
            return this;
        }

        public NowGlass SetRadius(NowCornerRadius radius)
        {
            this.radius = radius.packed;
            return this;
        }

        public NowGlass SetRadius(Vector4 radius)
        {
            this.radius = radius;
            return this;
        }

        public NowGlass SetBlurRadius(float radius)
        {
            blurRadius = Mathf.Max(0f, radius);
            return this;
        }

        public NowGlass SetTint(Color tint)
        {
            this.tint = tint;
            return this;
        }

        public NowGlass SetTint(Vector4 tint)
        {
            this.tint = tint;
            return this;
        }

        /// <summary>Sets the outline width in UI units. The outline stays invisible
        /// until an outline color with alpha above zero is supplied — use
        /// <see cref="SetOutline(float, Color)"/> to set both in one call.</summary>
        public NowGlass SetOutline(float outline)
        {
            this.outline = Mathf.Max(0f, outline);
            return this;
        }

        /// <summary>Sets the outline width in UI units and its color together.</summary>
        public NowGlass SetOutline(float outline, Color color)
        {
            this.outline = Mathf.Max(0f, outline);
            outlineColor = color;
            return this;
        }

        /// <summary>Sets the outline width in UI units and its color together.</summary>
        public NowGlass SetOutline(float outline, Vector4 color)
        {
            this.outline = Mathf.Max(0f, outline);
            outlineColor = color;
            return this;
        }

        public NowGlass SetOutlineColor(Color color)
        {
            outlineColor = color;
            return this;
        }

        public NowGlass SetOutlineColor(Vector4 color)
        {
            outlineColor = color;
            return this;
        }

        public NowGlass SetVibrancy(float saturation, float brightness)
        {
            this.saturation = Mathf.Max(0f, saturation);
            this.brightness = Mathf.Max(0f, brightness);
            return this;
        }

        public NowGlass SetBlurQuality(NowGlassBlurQuality quality)
        {
            blurQuality = quality;
            return this;
        }

        [NowConsumer]
        public NowGlass Draw()
        {
            Now.DrawGlass(this);
            return this;
        }
    }

    public static partial class Now
    {
        readonly struct GlassBatchKey : IEquatable<GlassBatchKey>
        {
            readonly float _blurRadius;

            readonly float _saturation;

            readonly float _brightness;

            readonly NowGlassBlurQuality _quality;

            public GlassBatchKey(float blurRadius, float saturation, float brightness, NowGlassBlurQuality quality)
            {
                _blurRadius = blurRadius;
                _saturation = saturation;
                _brightness = brightness;
                _quality = quality;
            }

            public Vector4 data => new Vector4(_blurRadius, _saturation, _brightness, (float)_quality);

            public bool Equals(GlassBatchKey other)
            {
                return _blurRadius.Equals(other._blurRadius) &&
                    _saturation.Equals(other._saturation) &&
                    _brightness.Equals(other._brightness) &&
                    _quality == other._quality;
            }

            public override bool Equals(object obj)
            {
                return obj is GlassBatchKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = 17;
                    hash = hash * 31 + _blurRadius.GetHashCode();
                    hash = hash * 31 + _saturation.GetHashCode();
                    hash = hash * 31 + _brightness.GetHashCode();
                    hash = hash * 31 + (int)_quality;
                    return hash;
                }
            }
        }

        sealed class GlassMeshEntry
        {
            public int meshId = -1;

            public int lastUsedFrame;
        }

        const int GlassEntryLifetimeFrames = 300;

        static readonly Dictionary<GlassBatchKey, GlassMeshEntry> _glassMaterialMeshes =
            new Dictionary<GlassBatchKey, GlassMeshEntry>(8);

        static readonly List<GlassBatchKey> _staleGlassKeys = new List<GlassBatchKey>(8);

        static Material _glassMaterial;

        static Material _glassCanvasMaterial;

        public static NowGlass Glass(NowRect rect)
        {
            return new NowGlass(rect);
        }

        internal static void DrawGlass(NowGlass glass)
        {
            if (_suppressDrawDepth > 0)
                return;

            if (glass.rect.width <= 0f || glass.rect.height <= 0f)
                return;

            var material = GetGlassMaterial();

            if (material == null)
                return;

            bool hasTransform = _transformStack.Count > 0;
            var rect = hasTransform ? ApplyTransformRect(glass.rect) : glass.rect;
            float x0, y0, rectWidth, rectHeight;

            if (hasTransform)
            {
                x0 = rect.x;
                y0 = rect.y;
                rectWidth = rect.width;
                rectHeight = rect.height;
            }
            else
            {
                x0 = Mathf.RoundToInt(rect.x);
                y0 = Mathf.RoundToInt(rect.y);
                rectWidth = Mathf.RoundToInt(rect.x + rect.width) - x0;
                rectHeight = Mathf.RoundToInt(rect.y + rect.height) - y0;
            }

            if (rectWidth <= 0 || rectHeight <= 0)
                return;

            var rectMask = glass.mask;
            float visualPadding = RectangleVisualPadding(0f, glass.outline);
            float scalar = hasTransform ? ApplyTransformScalar(1f) : 1f;
            Vector4 radius = glass.radius * scalar;
            float outline = glass.outline * scalar;
            float blurRadius = glass.blurRadius * scalar;
            float geometryPadding = RectangleVisualPadding(0f, outline);

            if (!rectMask.isEmpty && rectMask == glass.rect)
                rectMask = rectMask.Outset(visualPadding);

            if (hasTransform && !rectMask.isEmpty)
                rectMask = ApplyTransformRect(rectMask);

            _tmpVertex.mask = ApplyAmbientMask(rectMask);
            _tmpVertex.radius = radius;
            _tmpVertex.color = ApplyColorMultiplier(glass.tint);
            _tmpVertex.outlineColor = ApplyColorMultiplier(glass.outlineColor);
            _tmpVertex.uvwh = _defaultUV;
            _tmpVertex.position.x = x0;
            _tmpVertex.position.y = -y0 - rectHeight;
            _tmpVertex.position.z = rectWidth;
            _tmpVertex.position.w = rectHeight;

            var key = new GlassBatchKey(
                QuantizeGlassBlurRadius(Mathf.Max(0f, blurRadius)),
                QuantizeGlassVibrancy(Mathf.Max(0f, glass.saturation)),
                QuantizeGlassVibrancy(Mathf.Max(0f, glass.brightness)),
                glass.blurQuality == NowGlassBlurQuality.Auto
                    ? NowGlassSettings.currentBlurQuality
                    : NowGlassSettings.Resolve(glass.blurQuality));
            var keyData = key.data;
            var mesh = UseGlassMaterial(material, GetGlassCanvasMaterial(), key);

            if (mesh == null)
                return;

            mesh = EnsureMeshCapacity(mesh, material, NowMeshKind.Glass, 4);

            Vector4 extra = default;
            extra.x = keyData.x;
            extra.y = outline;
            extra.z = keyData.y;
            extra.w = keyData.z;
            mesh.AddRect(_tmpVertex, extra, geometryPadding);
        }

        static Material GetGlassMaterial()
        {
            if (_glassMaterial == null)
                _glassMaterial = Now.LoadRequiredResource<Material>("NowUI/GlassMaterial");

            return _glassMaterial;
        }

        static Material GetGlassCanvasMaterial()
        {
            if (_glassCanvasMaterial == null)
                _glassCanvasMaterial = Now.LoadRequiredResource<Material>("NowUI/GlassMaterialUGUI");

            return _glassCanvasMaterial;
        }

        static NowMesh UseGlassMaterial(Material material, Material canvasMaterial, GlassBatchKey key)
        {
            int frame = Time.frameCount;

            if (!_glassMaterialMeshes.TryGetValue(key, out var entry))
            {
                EvictStaleGlassEntries(frame);
                entry = new GlassMeshEntry();
                _glassMaterialMeshes[key] = entry;
            }

            entry.lastUsedFrame = frame;
            return UseMaterial(material, canvasMaterial, ref entry.meshId, NowMeshKind.Glass, key.data);
        }

        /// <summary>
        /// Quantizes blur radii to quarter-pixel steps so animated blur or scaled
        /// transforms reuse a batch key instead of minting one per unique float.
        /// </summary>
        static float QuantizeGlassBlurRadius(float value)
        {
            return Mathf.Round(value * 4f) * 0.25f;
        }

        /// <summary>Quantizes saturation and brightness to 0.01 steps for stable batch keys.</summary>
        static float QuantizeGlassVibrancy(float value)
        {
            return Mathf.Round(value * 100f) * 0.01f;
        }

        /// <summary>Drops batch keys that have not drawn recently so animated values cannot grow the cache without bound.</summary>
        static void EvictStaleGlassEntries(int frame)
        {
            _staleGlassKeys.Clear();

            foreach (var pair in _glassMaterialMeshes)
            {
                if (frame - pair.Value.lastUsedFrame > GlassEntryLifetimeFrames)
                    _staleGlassKeys.Add(pair.Key);
            }

            for (int i = 0; i < _staleGlassKeys.Count; ++i)
                _glassMaterialMeshes.Remove(_staleGlassKeys[i]);

            _staleGlassKeys.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForRuntimeLoad()
        {
            _glassMaterialMeshes.Clear();
            _staleGlassKeys.Clear();
            _glassMaterial = null;
            _glassCanvasMaterial = null;
        }
    }
}
