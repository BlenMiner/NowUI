using UnityEngine;

namespace NowUI
{
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
        }

        public NowRectangle SetBlur(float blur)
        {
            this.blur = blur;
            return this;
        }

        public NowRectangle SetRadius(float allRadius)
        {
            radius = new Vector4(allRadius, allRadius, allRadius, allRadius);
            return this;
        }

        public NowRectangle SetRadius(Vector4 radius)
        {
            this.radius = radius;
            return this;
        }

        public NowRectangle SetPadding(float all)
        {
            return SetPadding(new Vector4(all, all, all, all));
        }

        public NowRectangle SetPadding(Vector4 padding)
        {
            padding = new Vector4(-padding.x, -padding.y, -padding.z, -padding.w);
            this.padding = padding;
            mask = mask.Inset(padding.x, padding.y, padding.z, padding.w);
            return this;
        }

        public NowRectangle SetOutline(float outline)
        {
            this.outline = outline;
            return this;
        }

        public NowRectangle SetPosition(NowRect rect)
        {
            this.rect = rect;
            return this;
        }

        public NowRectangle SetMask(NowRect mask)
        {
            this.mask = mask;
            return this;
        }

        public NowRectangle SetColor(Color color)
        {
            this.color = color;
            return this;
        }

        public NowRectangle SetColor(Vector4 color)
        {
            this.color = color;
            return this;
        }

        public NowRectangle SetOutlineColor(Color color)
        {
            outlineColor = color;
            return this;
        }

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
        public NowRectangle SetTexture(Texture texture)
        {
            this.texture = texture;
            return this;
        }

        /// <summary>Restricts sampling to a texture sub-region (u, v, width, height in 0..1).</summary>
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
        public NowRectangle SetPreserveAspect(bool preserve = true)
        {
            preserveAspect = preserve;
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
