using NowUI.Internal;
using UnityEngine;

namespace NowUI
{
    [NowBuilder]
    public struct NowRipple
    {
        public NowRect rect;
        public NowRect mask;
        public Vector4 radius;
        public Vector4 color;
        public Vector2 origin;
        public float circleRadius;

        public NowRipple(NowRect rect)
        {
            this.rect = rect;
            mask = rect;
            radius = default;
            color = new Vector4(1f, 1f, 1f, 1f);
            origin = rect.center;
            circleRadius = 0f;
        }

        public NowRipple SetMask(NowRect mask)
        {
            this.mask = mask;
            return this;
        }

        public NowRipple SetRadius(float allRadius)
        {
            radius = new Vector4(allRadius, allRadius, allRadius, allRadius);
            return this;
        }

        public NowRipple SetRadius(Vector4 radius)
        {
            this.radius = radius;
            return this;
        }

        public NowRipple SetColor(Color color)
        {
            this.color = color;
            return this;
        }

        public NowRipple SetColor(Vector4 color)
        {
            this.color = color;
            return this;
        }

        public NowRipple SetOrigin(Vector2 origin)
        {
            this.origin = origin;
            return this;
        }

        public NowRipple SetCircleRadius(float radius)
        {
            circleRadius = radius;
            return this;
        }

        [NowConsumer]
        public NowRipple Draw()
        {
            Now.DrawRipple(this);
            return this;
        }
    }

    public static partial class Now
    {
        static Material _rippleMaterial;
        static Material _rippleCanvasMaterial;
        static int _rippleMesh = -1;

        public static NowRipple Ripple(NowRect rect)
        {
            return new NowRipple(rect);
        }

        internal static void DrawRipple(NowRipple ripple)
        {
            if (_suppressDrawDepth > 0 || ripple.circleRadius <= 0f || ripple.color.w <= 0f)
                return;

            var material = GetRippleMaterial();
            if (material == null)
                return;

            var position = ripple.rect;
            int x0 = Mathf.RoundToInt(position.x);
            int y0 = Mathf.RoundToInt(position.y);
            int rectWidth = Mathf.RoundToInt(position.x + position.width) - x0;
            int rectHeight = Mathf.RoundToInt(position.y + position.height) - y0;

            if (rectWidth <= 0 || rectHeight <= 0)
                return;

            _tmpVertex.mask = ApplyAmbientMask(ripple.mask);
            _tmpVertex.radius = ripple.radius;
            _tmpVertex.color = ApplyColorMultiplier(ripple.color);
            _tmpVertex.outlineColor = default;
            _tmpVertex.uvwh = _defaultUV;
            _tmpVertex.position.x = x0;
            _tmpVertex.position.y = -y0 - rectHeight;
            _tmpVertex.position.z = rectWidth;
            _tmpVertex.position.w = rectHeight;

            var mesh = UseMaterial(material, GetRippleCanvasMaterial(), ref _rippleMesh, NowMeshKind.Ripple);
            mesh = EnsureMeshCapacity(mesh, material, NowMeshKind.Ripple, 4);

            if (mesh == null)
                return;

            mesh.AddRect(_tmpVertex, new Vector4(ripple.origin.x, ripple.origin.y, 0f, ripple.circleRadius));
        }

        static Material GetRippleMaterial()
        {
            if (_rippleMaterial == null)
                _rippleMaterial = Resources.Load<Material>("NowUI/RippleMaterial");

            if (_rippleMaterial == null)
            {
                var shader = Shader.Find("NowUI/UI Ripple");
                if (shader != null)
                    _rippleMaterial = new Material(shader) { name = "Now Ripple Material", hideFlags = HideFlags.HideAndDontSave };
            }

            return _rippleMaterial;
        }

        static Material GetRippleCanvasMaterial()
        {
            if (_rippleCanvasMaterial == null)
                _rippleCanvasMaterial = Resources.Load<Material>("NowUI/RippleMaterialUGUI");

            if (_rippleCanvasMaterial == null)
            {
                var shader = Shader.Find("NowUI/UI Ripple UGUI");
                if (shader != null)
                    _rippleCanvasMaterial = new Material(shader) { name = "Now Ripple Material UGUI", hideFlags = HideFlags.HideAndDontSave };
            }

            return _rippleCanvasMaterial;
        }
    }
}
