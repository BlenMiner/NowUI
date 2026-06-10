using UnityEngine;

namespace NowUI
{
    public struct NowUIRectangle
    {
        public Vector4 mask;

        public Vector4 rect;

        public Vector4 radius;

        public Vector4 color;

        public Vector4 padding;

        public float blur;

        public float outline;

        public Vector4 outlineColor;

        public NowUIRectangle(Vector4 rect)
        {
            mask = rect;
            this.rect = rect;
            radius = default;
            padding = default;
            blur = default;
            outline = default;
            color = new Vector4(1, 1, 1, 1);
            outlineColor = default;
        }

        public NowUIRectangle SetBlur(float blur)
        {
            this.blur = blur;
            return this;
        }

        public NowUIRectangle SetRadius(float allRadius)
        {
            radius = new Vector4(allRadius, allRadius, allRadius, allRadius);
            return this;
        }

        public NowUIRectangle SetRadius(Vector4 radius)
        {
            this.radius = radius;
            return this;
        }

        public NowUIRectangle SetPadding(float all)
        {
            return SetPadding(new Vector4(all, all, all, all));
        }

        public NowUIRectangle SetPadding(Vector4 padding)
        {
            padding = new Vector4(-padding.x, -padding.y, -padding.z, -padding.w);
            this.padding = padding;
            mask.x += padding.x;
            mask.y += padding.y;
            mask.z = mask.z - padding.x - padding.z;
            mask.w = mask.w - padding.y - padding.w;
            return this;
        }

        public NowUIRectangle SetOutline(float outline)
        {
            this.outline = outline;
            return this;
        }

        public NowUIRectangle SetPosition(Vector4 rect)
        {
            this.rect = rect;
            return this;
        }

        public NowUIRectangle SetMask(Vector4 mask)
        {
            this.mask = mask;
            return this;
        }

        public NowUIRectangle SetColor(Color color)
        {
            this.color = color;
            return this;
        }

        public NowUIRectangle SetColor(Vector4 color)
        {
            this.color = color;
            return this;
        }

        public NowUIRectangle SetOutlineColor(Color color)
        {
            outlineColor = color;
            return this;
        }

        public NowUIRectangle SetOutlineColor(Vector4 color)
        {
            outlineColor = color;
            return this;
        }

        public NowUIRectangle Draw()
        {
            Now.DrawRect(this);
            return this;
        }
    }
}
