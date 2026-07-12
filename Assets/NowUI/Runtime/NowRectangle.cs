using System.Runtime.CompilerServices;
using UnityEngine;

namespace NowUI
{
    /// <summary>
    /// Human-readable rounded-rectangle corner radii. Use this instead of
    /// passing a raw Vector4 when corners differ; the renderer's packed order is
    /// an implementation detail.
    /// </summary>
    public readonly struct NowCornerRadius
    {
        public readonly float topLeft;
        public readonly float topRight;
        public readonly float bottomRight;
        public readonly float bottomLeft;

        public NowCornerRadius(float all)
            : this(all, all, all, all)
        {
        }

        public NowCornerRadius(float topLeft, float topRight, float bottomRight, float bottomLeft)
        {
            this.topLeft = topLeft;
            this.topRight = topRight;
            this.bottomRight = bottomRight;
            this.bottomLeft = bottomLeft;
        }

        /// <summary>
        /// The corner order the sdRoundedBox shaders decode: x/y select the
        /// right half (top, bottom), z/w the left half (top, bottom), with the
        /// quad's raw UV origin at the bottom.
        /// </summary>
        public Vector4 packed => new Vector4(topRight, bottomRight, topLeft, bottomLeft);

        public static NowCornerRadius All(float radius)
        {
            return new NowCornerRadius(radius);
        }

        public static NowCornerRadius Top(float radius)
        {
            return new NowCornerRadius(radius, radius, 0f, 0f);
        }

        public static NowCornerRadius Bottom(float radius)
        {
            return new NowCornerRadius(0f, 0f, radius, radius);
        }

        public static NowCornerRadius Left(float radius)
        {
            return new NowCornerRadius(radius, 0f, 0f, radius);
        }

        public static NowCornerRadius Right(float radius)
        {
            return new NowCornerRadius(0f, radius, radius, 0f);
        }

        public static NowCornerRadius FromPacked(Vector4 packed)
        {
            return new NowCornerRadius(packed.z, packed.x, packed.y, packed.w);
        }

        public static implicit operator Vector4(NowCornerRadius radius)
        {
            return radius.packed;
        }
    }

    [NowBuilder]
    public struct NowRectangle
    {
        public NowRect mask;

        public NowRect rect;

        public Vector4 radius;

        public Vector4 color;

        public Vector4 padding;

        public float blur;

        public float outline;

        public Vector4 outlineColor;

        /// <summary>Optional texture; sampled across <see cref="uvRect"/>.</summary>
        public Texture texture;

        /// <summary>Texture sub-region as (u, v, width, height), default full.</summary>
        public Vector4 uvRect;

        /// <summary>Sprite border in source pixels (left, bottom, right, top) for 9-slice.</summary>
        public Vector4 spriteBorder;

        /// <summary>Sprite source size in pixels; needed to map 9-slice borders to UVs.</summary>
        public Vector2 spritePixelSize;

        /// <summary>Draw as a 9-slice: corners stay fixed, edges and center stretch.</summary>
        public bool sliced;

        /// <summary>Letterbox the texture inside the rect instead of stretching.</summary>
        public bool preserveAspect;

        /// <summary>Optional material used by non-UGUI renderers instead of the built-in rectangle material.</summary>
        public Material material;

        /// <summary>Optional UGUI-specific material used by <see cref="NowGraphic"/>.</summary>
        public Material canvasMaterial;

        /// <summary>
        /// Skips the per-frame property re-sync of the cached textured copy of
        /// <see cref="material"/>. Set when the source material's properties never
        /// change after assignment; property (and shader) edits on the source are
        /// then no longer picked up automatically.
        /// </summary>
        public bool staticMaterial;

        public NowRectangle(NowRect rect)
        {
            mask = rect;
            this.rect = rect;
            radius = default;
            padding = default;
            blur = default;
            outline = default;
            color = new Vector4(1, 1, 1, 1);
            outlineColor = default;
            texture = null;
            uvRect = new Vector4(0f, 0f, 1f, 1f);
            spriteBorder = default;
            spritePixelSize = default;
            sliced = false;
            preserveAspect = false;
            material = null;
            canvasMaterial = null;
            staticMaterial = false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowRectangle SetBlur(float blur)
        {
            this.blur = blur;
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowRectangle SetRadius(float allRadius)
        {
            radius = new Vector4(allRadius, allRadius, allRadius, allRadius);
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowRectangle SetRadius(float topLeft, float topRight, float bottomRight, float bottomLeft)
        {
            radius = new NowCornerRadius(topLeft, topRight, bottomRight, bottomLeft).packed;
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowRectangle SetRadius(NowCornerRadius radius)
        {
            this.radius = radius.packed;
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowRectangle SetRadius(Vector4 radius)
        {
            this.radius = radius;
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowRectangle SetPadding(float all)
        {
            return SetPadding(new Vector4(all, all, all, all));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowRectangle SetPadding(Vector4 padding)
        {
            padding = new Vector4(-padding.x, -padding.y, -padding.z, -padding.w);
            this.padding = padding;
            mask = mask.Inset(padding.x, padding.y, padding.z, padding.w);
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowRectangle SetOutline(float outline)
        {
            this.outline = outline;
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowRectangle SetPosition(NowRect rect)
        {
            this.rect = rect;
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowRectangle SetMask(NowRect mask)
        {
            this.mask = mask;
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowRectangle SetColor(Color color)
        {
            this.color = color;
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowRectangle SetColor(Color color, float alpha)
        {
            this.color = new Color(color.r, color.g, color.b, alpha);
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowRectangle SetColor(Vector4 color)
        {
            this.color = color;
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowRectangle SetOutlineColor(Color color)
        {
            outlineColor = color;
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowRectangle SetOutlineColor(Vector4 color)
        {
            outlineColor = color;
            return this;
        }

        /// <summary>
        /// Draws the texture inside the rect (tinted by the color, clipped by
        /// radius and masks like any rectangle):
        /// <code>Now.Rectangle(rect).SetTexture(photo).SetRadius(8).Draw();</code>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowRectangle SetTexture(Texture texture)
        {
            this.texture = texture;
            return this;
        }

        /// <summary>Restricts sampling to a texture sub-region (u, v, width, height in 0..1).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowRectangle SetUV(Vector4 uvRect)
        {
            this.uvRect = uvRect;
            return this;
        }

        /// <summary>
        /// Draws a sprite — atlas sub-rect resolved automatically. With
        /// <paramref name="sliced"/> and a sprite border, corners keep their pixel
        /// size while edges and the center stretch (9-slice); radius, outline and
        /// blur do not apply to sliced draws.
        /// <code>Now.Rectangle(rect).SetSprite(panelSprite, sliced: true).Draw();</code>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowRectangle SetSprite(Sprite sprite, bool sliced = false)
        {
            if (sprite == null || sprite.texture == null)
                return this;

            texture = sprite.texture;
            var textureRect = sprite.textureRect;
            float textureWidth = sprite.texture.width;
            float textureHeight = sprite.texture.height;
            uvRect = new Vector4(
                textureRect.x / textureWidth,
                textureRect.y / textureHeight,
                textureRect.width / textureWidth,
                textureRect.height / textureHeight);
            spriteBorder = sprite.border;
            spritePixelSize = new Vector2(textureRect.width, textureRect.height);
            this.sliced = sliced && sprite.border != Vector4.zero;
            return this;
        }

        /// <summary>Letterboxes the texture inside the rect instead of stretching it.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowRectangle SetPreserveAspect(bool preserve = true)
        {
            preserveAspect = preserve;
            return this;
        }

        /// <summary>
        /// Uses a caller-provided material for this rectangle. The material shader
        /// receives the same quad geometry and vertex streams as NowUI's built-in
        /// rectangle shader; if a texture is also set, NowUI draws with a cached
        /// material instance whose main texture is assigned to that texture.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowRectangle SetMaterial(Material material)
        {
            this.material = material;
            return this;
        }

        /// <summary>
        /// Like <see cref="SetMaterial(Material)"/>; pass
        /// <paramref name="syncPerFrame"/> false when the material's properties never
        /// change, so textured draws skip the per-frame property copy into the cached
        /// textured material instance.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowRectangle SetMaterial(Material material, bool syncPerFrame)
        {
            this.material = material;
            staticMaterial = !syncPerFrame;
            return this;
        }

        /// <summary>
        /// Uses one material for normal render paths and a separate UGUI-compatible
        /// material when the rectangle is drawn inside <see cref="NowGraphic"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowRectangle SetMaterial(Material material, Material canvasMaterial)
        {
            this.material = material;
            this.canvasMaterial = canvasMaterial;
            return this;
        }

        /// <summary>
        /// Overrides only the UGUI material. Non-UGUI hosts keep using the normal
        /// built-in or custom rectangle material.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowRectangle SetCanvasMaterial(Material canvasMaterial)
        {
            this.canvasMaterial = canvasMaterial;
            return this;
        }

        [NowConsumer]
        public NowRectangle Draw()
        {
            Now.DrawRect(this);
            return this;
        }
    }
}
