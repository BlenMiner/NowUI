using Unity.Profiling;

namespace NowUI
{
    /// <summary>
    /// Profiler markers for NowUI's subsystems, visible as "Now.*" in the
    /// Unity Profiler (CPU module, and searchable in Timeline). Markers are
    /// compiled out of non-development builds, so they stay in shipping code.
    /// Custom controls can use these or declare their own.
    /// </summary>
    public static class NowProfiler
    {
        /// <summary>A full retained-host rebuild (NowGraphic.UpdateGeometry).</summary>
        public static readonly ProfilerMarker GraphicRebuild = new ProfilerMarker("Now.Graphic.Rebuild");

        /// <summary>The extra layout measure pass that precedes the real pass.</summary>
        public static readonly ProfilerMarker MeasurePass = new ProfilerMarker("Now.Layout.MeasurePass");

        /// <summary>The user's DrawNowUI / immediate-mode draw calls.</summary>
        public static readonly ProfilerMarker Draw = new ProfilerMarker("Now.Draw");

        /// <summary>Building and uploading the canvas meshes after a rebuild.</summary>
        public static readonly ProfilerMarker MeshUpload = new ProfilerMarker("Now.MeshUpload");

        /// <summary>Assigning meshes/materials to the CanvasRenderers.</summary>
        public static readonly ProfilerMarker ApplyCanvasPages = new ProfilerMarker("Now.Graphic.ApplyCanvasPages");

        /// <summary>One DrawString call: shaping lookup, glyph resolution, quad emission.</summary>
        public static readonly ProfilerMarker TextDraw = new ProfilerMarker("Now.Text.Draw");

        /// <summary>A HarfBuzz shaping call (cache misses only; shaped runs are cached).</summary>
        public static readonly ProfilerMarker TextShape = new ProfilerMarker("Now.Text.Shape");

        /// <summary>Baking missing glyphs into a dynamic atlas page (spiky, first-use cost).</summary>
        public static readonly ProfilerMarker FontBake = new ProfilerMarker("Now.Font.Bake");

        /// <summary>Tessellating a Lottie frame (cache misses only; frames are cached).</summary>
        public static readonly ProfilerMarker LottieRender = new ProfilerMarker("Now.Lottie.Render");

        /// <summary>Deferred overlay draws (dropdown popups, tooltips).</summary>
        public static readonly ProfilerMarker OverlayFlush = new ProfilerMarker("Now.Overlay.Flush");

        /// <summary>The screen path's end-of-frame GL submission.</summary>
        public static readonly ProfilerMarker FlushUI = new ProfilerMarker("Now.FlushUI");
    }
}
