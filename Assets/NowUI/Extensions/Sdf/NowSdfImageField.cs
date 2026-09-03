using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace NowUI.Sdf
{
    /// <summary>
    /// One baked signed distance field for an image's alpha silhouette. The
    /// texture stores the signed distance in source texels (negative inside)
    /// in its red channel, padded by <see cref="padding"/> texels on every side
    /// of the source rect so exterior effects have an exact field to sample.
    /// </summary>
    sealed class NowSdfImageField
    {
        public NowSdfImageFieldKey key;
        public RenderTexture texture;
        public uint sourceUpdateCount;
        public int bakeFrame = -1;
        public int lastUsedFrame;
        public int version;

        public int padding => key.padding;

        public bool isValid => texture != null && texture.IsCreated();
    }

    readonly struct NowSdfImageFieldKey : IEquatable<NowSdfImageFieldKey>
    {
        public readonly Texture texture;
        public readonly RectInt texelRect;
        public readonly int padding;
        public readonly float threshold;

        public NowSdfImageFieldKey(Texture texture, RectInt texelRect, int padding, float threshold)
        {
            this.texture = texture;
            this.texelRect = texelRect;
            this.padding = padding;
            this.threshold = threshold;
        }

        public bool Equals(NowSdfImageFieldKey other)
        {
            return ReferenceEquals(texture, other.texture) &&
                texelRect.Equals(other.texelRect) &&
                padding == other.padding &&
                threshold.Equals(other.threshold);
        }

        public override bool Equals(object obj)
        {
            return obj is NowSdfImageFieldKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = !ReferenceEquals(texture, null) ? RuntimeHelpers.GetHashCode(texture) : 0;
                hash = hash * 397 ^ texelRect.x;
                hash = hash * 397 ^ texelRect.y;
                hash = hash * 397 ^ texelRect.width;
                hash = hash * 397 ^ texelRect.height;
                hash = hash * 397 ^ padding;
                hash = hash * 397 ^ threshold.GetHashCode();
                return hash;
            }
        }
    }

    /// <summary>
    /// Shared cache of GPU-baked image distance fields. Fields are produced with
    /// jump flooding through <c>Graphics.Blit</c>, so any texture the GPU can
    /// sample works: import-time read/write access is not required.
    /// </summary>
    static class NowSdfImageFields
    {
        /// <summary>Padding is quantized to this many texels so animated effect reach does not rebake every frame.</summary>
        public const int PaddingStep = 8;

        /// <summary>Largest padding baked around a source rect, in texels.</summary>
        public const int MaxPadding = 256;

        /// <summary>Fields unused for this many frames are released on the next sweep.</summary>
        const int StaleFrames = 600;

        const int SweepInterval = 128;
        const string MaterialResource = "NowUI/SdfImageField";
        const string ShaderName = "Hidden/NowUI/SDF Image Field";

        static readonly int _sourceTexProp = Shader.PropertyToID("_SourceTex");
        static readonly int _sourceUvProp = Shader.PropertyToID("_SourceUv");
        static readonly int _fieldParamsProp = Shader.PropertyToID("_FieldParams");
        static readonly int _fieldTexelsProp = Shader.PropertyToID("_FieldTexels");
        static readonly int _stepProp = Shader.PropertyToID("_Step");

        static readonly Dictionary<NowSdfImageFieldKey, NowSdfImageField> _fields =
            new Dictionary<NowSdfImageFieldKey, NowSdfImageField>(8);
        static readonly List<NowSdfImageFieldKey> _staleKeys = new List<NowSdfImageFieldKey>(8);

        static Material _material;
        static int _acquireCount;
        static int _bakeCount;

        internal static int fieldCount => _fields.Count;

        internal static int bakeCount => _bakeCount;

        /// <summary>
        /// Padding in texels that keeps an exact field for effects reaching
        /// <paramref name="reachTexels"/> beyond the silhouette, plus one texel of
        /// filtering guard, quantized to <see cref="PaddingStep"/>.
        /// </summary>
        public static int PaddingForReach(float reachTexels)
        {
            if (float.IsNaN(reachTexels) || reachTexels < 0f)
                reachTexels = 0f;

            double steps = Math.Ceiling((reachTexels + 1d) / PaddingStep);
            if (double.IsInfinity(steps) || steps * PaddingStep >= MaxPadding)
                return MaxPadding;

            return Mathf.Clamp((int)steps * PaddingStep, PaddingStep, MaxPadding);
        }

        /// <summary>
        /// Scene-local reach the field answers exactly. One texel is reserved
        /// for bilinear filtering at the padded border.
        /// </summary>
        public static float SafeEffectReach(int padding, float sceneUnitsPerTexel)
        {
            if (float.IsNaN(sceneUnitsPerTexel) || sceneUnitsPerTexel <= 0f)
                return 0f;

            return Mathf.Max(0f, (padding - 1) * sceneUnitsPerTexel);
        }

        public static bool IsCurrent(NowSdfImageField field)
        {
            if (field == null || !field.isValid || field.key.texture == null)
                return false;

            if (field.key.texture is RenderTexture)
                return field.bakeFrame == Time.frameCount;

            return field.sourceUpdateCount == field.key.texture.updateCount;
        }

        /// <summary>
        /// Returns the cached field for the request, baking it when missing,
        /// lost, or stale. Returns null when the bake material is unavailable.
        /// </summary>
        public static NowSdfImageField Acquire(Texture source, RectInt texelRect, int padding, float threshold)
        {
            if (source == null)
                return null;

            var key = new NowSdfImageFieldKey(source, texelRect, padding, threshold);

            if (!_fields.TryGetValue(key, out var field))
            {
                field = new NowSdfImageField { key = key };
                _fields.Add(key, field);
            }

            field.lastUsedFrame = Time.frameCount;

            if (!IsCurrent(field) && !Bake(field))
            {
                Release(field);
                _fields.Remove(key);
                return null;
            }

            if (++_acquireCount % SweepInterval == 0)
                Sweep();

            return field;
        }

        public static void Reset()
        {
            foreach (var field in _fields.Values)
                Release(field);

            _fields.Clear();
            _acquireCount = 0;
            _bakeCount = 0;
        }

        static void Sweep()
        {
            int frame = Time.frameCount;
            _staleKeys.Clear();

            foreach (var pair in _fields)
            {
                if (frame - pair.Value.lastUsedFrame > StaleFrames || pair.Key.texture == null)
                    _staleKeys.Add(pair.Key);
            }

            for (int i = 0; i < _staleKeys.Count; ++i)
            {
                if (_fields.TryGetValue(_staleKeys[i], out var field))
                {
                    Release(field);
                    _fields.Remove(_staleKeys[i]);
                }
            }

            _staleKeys.Clear();
        }

        static void Release(NowSdfImageField field)
        {
            if (field?.texture == null)
                return;

            field.texture.Release();
            DestroyTarget(field.texture);
            field.texture = null;
        }

        static void DestroyTarget(UnityEngine.Object target)
        {
            if (target == null)
                return;

            if (Application.isPlaying)
                UnityEngine.Object.Destroy(target);
            else
                UnityEngine.Object.DestroyImmediate(target);
        }

        static Material GetMaterial()
        {
            if (_material != null)
                return _material;

            var template = Resources.Load<Material>(MaterialResource);

            if (template != null)
            {
                _material = new Material(template);
            }
            else
            {
                var shader = Shader.Find(ShaderName);
                if (shader == null)
                    return null;

                _material = new Material(shader);
            }

            _material.name = "Now SDF Image Field";
            _material.hideFlags = HideFlags.HideAndDontSave;
            return _material;
        }

        static RenderTextureFormat FieldFormat()
        {
            if (SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RHalf))
                return RenderTextureFormat.RHalf;

            if (SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RFloat))
                return RenderTextureFormat.RFloat;

            return RenderTextureFormat.ARGBHalf;
        }

        static RenderTextureFormat FloodFormat()
        {
            // Seeds are texel-center coordinates. Half floats represent integers
            // exactly to 2048 and the +0.5 offset to 1024, which covers the sizes
            // the padding cap allows; prefer full floats where they exist.
            return SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBFloat)
                ? RenderTextureFormat.ARGBFloat
                : RenderTextureFormat.ARGBHalf;
        }

        static bool Bake(NowSdfImageField field)
        {
            var material = GetMaterial();
            Texture source = field.key.texture;

            if (material == null || source == null)
                return false;

            RectInt texelRect = field.key.texelRect;
            int padding = field.key.padding;
            int maximum = Mathf.Max(1, SystemInfo.maxTextureSize);
            int width = Mathf.Clamp(texelRect.width + padding * 2, 1, maximum);
            int height = Mathf.Clamp(texelRect.height + padding * 2, 1, maximum);

            if (field.texture != null &&
                (field.texture.width != width || field.texture.height != height))
            {
                Release(field);
            }

            if (field.texture == null)
            {
                field.texture = new RenderTexture(width, height, 0, FieldFormat(), RenderTextureReadWrite.Linear)
                {
                    name = "Now SDF Image Field",
                    hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    useMipMap = false,
                    autoGenerateMips = false
                };
            }

            if (!field.texture.IsCreated() && !field.texture.Create())
                return false;

            float sourceWidth = Mathf.Max(1, source.width);
            float sourceHeight = Mathf.Max(1, source.height);
            material.SetTexture(_sourceTexProp, source);
            material.SetVector(_sourceUvProp, new Vector4(
                texelRect.x / sourceWidth,
                texelRect.y / sourceHeight,
                texelRect.width / sourceWidth,
                texelRect.height / sourceHeight));
            material.SetVector(_fieldParamsProp, new Vector4(
                texelRect.width,
                texelRect.height,
                padding,
                field.key.threshold));
            material.SetVector(_fieldTexelsProp, new Vector4(width, height, 1f / width, 1f / height));

            var floodFormat = FloodFormat();
            var ping = RenderTexture.GetTemporary(width, height, 0, floodFormat, RenderTextureReadWrite.Linear);
            var pong = RenderTexture.GetTemporary(width, height, 0, floodFormat, RenderTextureReadWrite.Linear);
            ping.filterMode = FilterMode.Point;
            pong.filterMode = FilterMode.Point;
            ping.wrapMode = TextureWrapMode.Clamp;
            pong.wrapMode = TextureWrapMode.Clamp;
            var previousActive = RenderTexture.active;

            try
            {
                Graphics.Blit(source, ping, material, 0);

                int step = Mathf.NextPowerOfTwo(Mathf.Max(width, height)) / 2;
                while (step >= 1)
                {
                    material.SetFloat(_stepProp, step);
                    Graphics.Blit(ping, pong, material, 1);
                    (ping, pong) = (pong, ping);
                    step /= 2;
                }

                // One extra unit pass repairs the rare seeds standard jump
                // flooding misses along diagonals.
                material.SetFloat(_stepProp, 1f);
                Graphics.Blit(ping, pong, material, 1);
                (ping, pong) = (pong, ping);

                Graphics.Blit(ping, field.texture, material, 2);
            }
            finally
            {
                RenderTexture.active = previousActive;
                RenderTexture.ReleaseTemporary(ping);
                RenderTexture.ReleaseTemporary(pong);
            }

            field.sourceUpdateCount = source.updateCount;
            field.bakeFrame = Time.frameCount;
            unchecked
            {
                ++field.version;
                ++_bakeCount;
            }

            return true;
        }
    }
}
