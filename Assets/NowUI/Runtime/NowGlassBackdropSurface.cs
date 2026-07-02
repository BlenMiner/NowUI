using UnityEngine;

namespace NowUI
{
    /// <summary>
    /// Shared lifecycle helpers for glass backdrop targets. The UGUI replay path
    /// and the world camera-backdrop path each manage persistent render textures
    /// and derived materials; the descriptor and play/edit destroy handling live
    /// here so the paths cannot drift apart. The capture pipelines themselves
    /// stay per-host — they exist for real render-order reasons.
    /// </summary>
    internal static class NowGlassBackdropSurface
    {
        /// <summary>Canonical backdrop target: ARGB32, no depth, no msaa/mips, bilinear, clamped, hidden.</summary>
        public static RenderTexture CreateTexture(int width, int height, string name)
        {
            var descriptor = new RenderTextureDescriptor(
                Mathf.Max(1, width),
                Mathf.Max(1, height),
                RenderTextureFormat.ARGB32,
                0)
            {
                msaaSamples = 1,
                useMipMap = false,
                autoGenerateMips = false
            };
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
    }
}
