using UnityEngine;

namespace NowUI
{
    /// <summary>
    /// Host hook used by editor previews to run the host's content in a frame the
    /// host does not own. The content runs a second time, so a preview must draw
    /// passively and never while the host itself is live.
    /// </summary>
    public interface INowPreviewHost
    {
        /// <summary>The size the host lays its content out at, in NowUI points.</summary>
        Vector2 previewContentSize { get; }

        /// <summary>Runs the host's content for the current frame at the given rect.</summary>
        void DrawPreviewContent(NowRect rect);
    }
}
