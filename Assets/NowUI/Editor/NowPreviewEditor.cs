using UnityEditor;
using UnityEngine;

namespace NowUI.Editor
{
    /// <summary>
    /// Inspector for NowUI component hosts: the default component fields plus a
    /// preview of the host's own content, laid out at the host's size and scaled to
    /// fit the preview area. The preview is passive and edit-mode only. Derive from
    /// this editor to keep the preview in a host's own inspector.
    /// </summary>
    public class NowPreviewEditor : UnityEditor.Editor
    {
        static readonly Color PREVIEW_BACKGROUND_COLOR = new Color(0.22f, 0.22f, 0.22f, 1f);

        /// <summary>
        /// The host this inspector previews. Override it for a component that owns a
        /// host instead of being one, such as one holding a NowVisualElement.
        /// </summary>
        protected virtual INowPreviewHost previewHost => target as INowPreviewHost;

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            // A collapsed preview stops drawing, and nothing else in this window
            // would otherwise release its renderer and RenderTexture.
            if (Event.current.type == EventType.Repaint)
                NowEditorGUI.CleanupIdlePanels();
        }

        public override bool HasPreviewGUI()
        {
            // Play mode drives the same host every frame, and a control's
            // caller-owned state lives on the component rather than the host, so a
            // second live host would re-enter that state at another size.
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return false;

            if (target is Behaviour behaviour && !behaviour.isActiveAndEnabled)
                return false;

            return TryGetContentSize(out _);
        }

        public override GUIContent GetPreviewTitle()
        {
            return new GUIContent("NowUI Preview");
        }

        public override string GetInfoString()
        {
            if (!TryGetContentSize(out Vector2 content))
                return string.Empty;

            return $"Content Size: {content.x:0} × {content.y:0}";
        }

        public override void OnPreviewGUI(Rect rect, GUIStyle background)
        {
            if (rect.width < 1f || rect.height < 1f || !TryGetContentSize(out Vector2 content))
                return;

            // The host lays out at its own size and the rendered image is scaled to
            // fit, keeping its aspect ratio, like UGUI's Image and RawImage previews.
            // Scaling up raises the render density instead of magnifying pixels, so an
            // enlarged preview stays sharp. The scale is quantized so that dragging
            // the preview area does not resize the panel texture on every repaint.
            float scale = Mathf.Min(rect.width / content.x, rect.height / content.y);
            var margin = new Vector2(
                (rect.width - content.x * scale) * 0.5f,
                (rect.height - content.y * scale) * 0.5f);
            var panel = new Rect(rect.position + margin / scale, content);
            float renderScale = Mathf.Ceil(scale * 4f) * 0.25f;

            Matrix4x4 previousMatrix = GUI.matrix;
            GUIUtility.ScaleAroundPivot(new Vector2(scale, scale), rect.position);

            try
            {
                using var ui = NowEditorGUI.Auto(
                    panel,
                    PREVIEW_BACKGROUND_COLOR,
                    EditorGUIUtility.pixelsPerPoint * renderScale);

                // Passive, like the host's own edit-mode rebuild: nothing focuses,
                // types or transitions state.
                NowInput.BeginPassive();

                try
                {
                    previewHost.DrawPreviewContent(new NowRect(0f, 0f, ui.width, ui.height));
                }
                finally
                {
                    NowInput.EndPassive();
                }
            }
            finally
            {
                GUI.matrix = previousMatrix;
            }
        }

        bool TryGetContentSize(out Vector2 size)
        {
            var host = previewHost;
            size = host != null ? host.previewContentSize : Vector2.zero;
            return size.x >= 1f && size.y >= 1f;
        }
    }

#if NOWUI_UGUI
    [CustomEditor(typeof(NowGraphic), true)]
    [CanEditMultipleObjects]
    sealed class NowGraphicEditor : NowPreviewEditor
    {
    }
#endif

    [CustomEditor(typeof(NowWorldGraphic), true)]
    [CanEditMultipleObjects]
    sealed class NowWorldGraphicEditor : NowPreviewEditor
    {
    }

    [CustomEditor(typeof(NowPipelineGraphic), true)]
    [CanEditMultipleObjects]
    sealed class NowPipelineGraphicEditor : NowPreviewEditor
    {
    }
}
