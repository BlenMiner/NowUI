using UnityEngine;

namespace NowUI
{
    internal interface INowFrameContent
    {
        void Draw(NowRect rect);
    }

    internal static class NowFrame
    {
        internal static NowFrameScope Begin(float uiScale, bool trackRepaint = false)
        {
            return new NowFrameScope(uiScale, trackRepaint);
        }

        internal static Vector2 DrawContent<TContent>(
            ref TContent content,
            NowRect rect,
            bool measurePass,
            bool trackContent,
            bool flushOverlays = true)
            where TContent : struct, INowFrameContent
        {
            if (measurePass)
                RunMeasurePass(ref content, rect);

            Vector2 measured = RunDrawPass(ref content, rect, trackContent);

            if (flushOverlays)
                NowOverlay.Flush();

            return measured;
        }

        internal static Vector2 MeasureContent<TContent>(ref TContent content, NowRect rect)
            where TContent : struct, INowFrameContent
        {
            using var profile = NowProfiler.MeasurePass.Auto();
            int layoutCounter = NowLayout.BeginMeasurePass();
            bool tracking = false;

            try
            {
                NowLayout.BeginContentTracking();
                tracking = true;

                content.Draw(rect);

                Vector2 measured = NowLayout.EndContentTracking();
                tracking = false;
                return measured;
            }
            finally
            {
                if (tracking)
                    NowLayout.EndContentTracking();

                NowLayout.EndMeasurePass(layoutCounter);
            }
        }

        static void RunMeasurePass<TContent>(ref TContent content, NowRect rect)
            where TContent : struct, INowFrameContent
        {
            using var profile = NowProfiler.MeasurePass.Auto();
            int layoutCounter = NowLayout.BeginMeasurePass();

            try
            {
                content.Draw(rect);
            }
            finally
            {
                NowLayout.EndMeasurePass(layoutCounter);
            }
        }

        static Vector2 RunDrawPass<TContent>(ref TContent content, NowRect rect, bool trackContent)
            where TContent : struct, INowFrameContent
        {
            using var profile = NowProfiler.Draw.Auto();

            if (!trackContent)
            {
                content.Draw(rect);
                return default;
            }

            bool tracking = false;

            try
            {
                NowLayout.BeginContentTracking();
                tracking = true;

                content.Draw(rect);

                Vector2 measured = NowLayout.EndContentTracking();
                tracking = false;
                return measured;
            }
            finally
            {
                if (tracking)
                    NowLayout.EndContentTracking();
            }
        }
    }

    internal struct NowFrameScope : System.IDisposable
    {
        readonly float _previousScale;

        readonly bool _trackRepaint;

        bool _active;

        bool _repaintEnded;

        internal NowFrameScope(float uiScale, bool trackRepaint)
        {
            _previousScale = Now.uiScale;
            _trackRepaint = trackRepaint;
            _active = true;
            _repaintEnded = false;

            Now.SetUIScale(uiScale);

            if (trackRepaint)
                NowControlState.BeginRepaintTracking();
        }

        internal bool EndRepaintTracking()
        {
            if (!_active || !_trackRepaint || _repaintEnded)
                return false;

            _repaintEnded = true;
            return NowControlState.EndRepaintTracking();
        }

        public void Dispose()
        {
            if (!_active)
                return;

            if (_trackRepaint && !_repaintEnded)
                NowControlState.EndRepaintTracking();

            Now.SetUIScale(_previousScale);
            _active = false;
        }
    }
}
