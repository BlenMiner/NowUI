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

        public NowGlass SetPosition(NowRect rect)
        {
            this.rect = rect;
            return this;
        }

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

        public NowGlass SetOutline(float outline)
        {
            this.outline = Mathf.Max(0f, outline);
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

        static readonly Dictionary<GlassBatchKey, MaterialMeshEntry> _glassMaterialMeshes =
            new Dictionary<GlassBatchKey, MaterialMeshEntry>(8);

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

            int x0 = Mathf.RoundToInt(glass.rect.x);
            int y0 = Mathf.RoundToInt(glass.rect.y);
            int rectWidth = Mathf.RoundToInt(glass.rect.x + glass.rect.width) - x0;
            int rectHeight = Mathf.RoundToInt(glass.rect.y + glass.rect.height) - y0;

            if (rectWidth <= 0 || rectHeight <= 0)
                return;

            var rectMask = glass.mask;

            if (!rectMask.isEmpty && rectMask == glass.rect)
                rectMask = rectMask.Outset(2f + glass.outline);

            _tmpVertex.mask = ApplyAmbientMask(rectMask);
            _tmpVertex.radius = glass.radius;
            _tmpVertex.color = ApplyColorMultiplier(glass.tint);
            _tmpVertex.outlineColor = ApplyColorMultiplier(glass.outlineColor);
            _tmpVertex.uvwh = _defaultUV;
            _tmpVertex.position.x = x0;
            _tmpVertex.position.y = -y0 - rectHeight;
            _tmpVertex.position.z = rectWidth;
            _tmpVertex.position.w = rectHeight;

            var key = new GlassBatchKey(
                Mathf.Max(0f, glass.blurRadius),
                Mathf.Max(0f, glass.saturation),
                Mathf.Max(0f, glass.brightness),
                glass.blurQuality == NowGlassBlurQuality.Auto
                    ? NowGlassSettings.currentBlurQuality
                    : NowGlassSettings.Resolve(glass.blurQuality));
            var mesh = UseGlassMaterial(material, GetGlassCanvasMaterial(), key);

            if (mesh == null)
                return;

            mesh = EnsureMeshCapacity(mesh, material, NowMeshKind.Glass, 4);

            Vector4 extra = default;
            extra.x = key.data.x;
            extra.y = glass.outline;
            extra.z = key.data.y;
            extra.w = key.data.z;
            mesh.AddRect(_tmpVertex, extra);
        }

        static Material GetGlassMaterial()
        {
            if (_glassMaterial == null)
                _glassMaterial = Resources.Load<Material>("NowUI/GlassMaterial");

            return _glassMaterial;
        }

        static Material GetGlassCanvasMaterial()
        {
            if (_glassCanvasMaterial == null)
                _glassCanvasMaterial = Resources.Load<Material>("NowUI/GlassMaterialUGUI");

            return _glassCanvasMaterial;
        }

        static NowMesh UseGlassMaterial(Material material, Material canvasMaterial, GlassBatchKey key)
        {
            if (!_glassMaterialMeshes.TryGetValue(key, out var entry))
            {
                entry = new MaterialMeshEntry();
                _glassMaterialMeshes[key] = entry;
            }

            return UseMaterial(material, canvasMaterial, ref entry.meshId, NowMeshKind.Glass, key.data);
        }
    }
}
