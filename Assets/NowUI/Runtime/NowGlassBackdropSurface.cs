using UnityEngine;
using UnityEngine.Rendering;

namespace NowUI
{
    internal readonly struct NowGlassTextureLayout
    {
        public readonly TextureDimension dimension;

        public readonly int volumeDepth;

        public readonly VRTextureUsage vrUsage;

        public readonly int sourceMsaaSamples;

        public readonly bool sourceBindMS;

        public bool isArray => dimension == TextureDimension.Tex2DArray;

        public int sliceCount => isArray ? Mathf.Max(1, volumeDepth) : 1;

        public bool sourceIsMultisampled => sourceMsaaSamples > 1;

        public bool sourceRequiresExplicitResolve => sourceIsMultisampled && sourceBindMS;

        public NowGlassTextureLayout(
            TextureDimension dimension,
            int volumeDepth,
            VRTextureUsage vrUsage,
            int sourceMsaaSamples,
            bool sourceBindMS = false)
        {
            bool useArray = dimension == TextureDimension.Tex2DArray;
            this.dimension = useArray ? TextureDimension.Tex2DArray : TextureDimension.Tex2D;
            this.volumeDepth = useArray ? Mathf.Max(1, volumeDepth) : 1;
            this.vrUsage = useArray ? vrUsage : VRTextureUsage.None;
            this.sourceMsaaSamples = Mathf.Max(1, sourceMsaaSamples);
            this.sourceBindMS = sourceBindMS && this.sourceMsaaSamples > 1;
        }

        public static NowGlassTextureLayout FromDescriptor(in RenderTextureDescriptor descriptor)
        {
            return new NowGlassTextureLayout(
                descriptor.dimension,
                descriptor.volumeDepth,
                descriptor.vrUsage,
                descriptor.msaaSamples,
                descriptor.bindMS);
        }

        public NowGlassTextureLayout AsSingleSampled()
        {
            return new NowGlassTextureLayout(dimension, volumeDepth, vrUsage, 1, false);
        }
    }

    /// <summary>
    /// Shared lifecycle helpers for glass backdrop targets. The UGUI replay path
    /// and the world camera-backdrop path each manage persistent render textures
    /// and derived materials; the descriptor and play/edit destroy handling live
    /// here so the paths cannot drift apart. The capture pipelines themselves
    /// stay per-host — they exist for real render-order reasons.
    /// </summary>
    internal static class NowGlassBackdropSurface
    {
        /// <summary>Canonical flat backdrop target: ARGB32, no depth, no msaa/mips, bilinear, clamped, hidden.</summary>
        public static RenderTexture CreateTexture(int width, int height, string name)
        {
            return CreateTexture(width, height, name, default);
        }

        /// <summary>
        /// Creates a single-sampled backdrop while retaining an XR source's
        /// texture-array shape. MSAA is resolved before this texture is sampled.
        /// </summary>
        public static RenderTexture CreateTexture(
            int width,
            int height,
            string name,
            in NowGlassTextureLayout layout)
        {
            var descriptor = CreateDescriptor(width, height, layout);
            var texture = new RenderTexture(descriptor)
            {
                name = name,
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            texture.Create();
            return texture;
        }

        public static RenderTextureDescriptor CreateDescriptor(
            int width,
            int height,
            in NowGlassTextureLayout layout)
        {
            var normalizedLayout = Normalize(layout);
            return new RenderTextureDescriptor(
                Mathf.Max(1, width),
                Mathf.Max(1, height),
                RenderTextureFormat.ARGB32,
                0)
            {
                msaaSamples = 1,
                bindMS = false,
                useMipMap = false,
                autoGenerateMips = false,
                dimension = normalizedLayout.dimension,
                volumeDepth = normalizedLayout.volumeDepth,
                vrUsage = normalizedLayout.vrUsage
            };
        }

        public static bool Matches(
            RenderTexture texture,
            int width,
            int height,
            in NowGlassTextureLayout layout)
        {
            if (texture == null)
                return false;

            var normalizedLayout = Normalize(layout);
            return texture.width == Mathf.Max(1, width) &&
                texture.height == Mathf.Max(1, height) &&
                texture.dimension == normalizedLayout.dimension &&
                texture.volumeDepth == normalizedLayout.volumeDepth &&
                texture.vrUsage == normalizedLayout.vrUsage &&
                texture.antiAliasing == 1;
        }

        public static void ReleaseTexture(ref RenderTexture texture)
        {
            if (texture == null)
                return;

            texture.Release();
            DestroyTarget(texture);
            texture = null;
        }

        /// <summary>Keeps a derived material cloned from <paramref name="baseMaterial"/>, recreating it when the base changes.</summary>
        public static void EnsureDerivedMaterial(ref Material material, ref Material sourceMaterial, Material baseMaterial, string nameSuffix)
        {
            if (material != null && sourceMaterial == baseMaterial)
                return;

            ReleaseMaterial(ref material);
            sourceMaterial = baseMaterial;
            material = new Material(baseMaterial)
            {
                name = $"{baseMaterial.name}{nameSuffix}",
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        public static void ReleaseMaterial(ref Material material)
        {
            if (material == null)
                return;

            DestroyTarget(material);
            material = null;
        }

        static void DestroyTarget(Object target)
        {
            if (Application.isPlaying)
                Object.Destroy(target);
            else
                Object.DestroyImmediate(target);
        }

        static NowGlassTextureLayout Normalize(in NowGlassTextureLayout layout)
        {
            return layout.isArray
                ? layout.AsSingleSampled()
                : new NowGlassTextureLayout(TextureDimension.Tex2D, 1, VRTextureUsage.None, 1);
        }
    }
}
