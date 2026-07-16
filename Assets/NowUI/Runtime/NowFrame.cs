using UnityEngine;

namespace NowUI
{
    internal interface INowDynamicTextureHost
    {
        int dynamicTextureBuildVersion { get; }

        bool isDynamicTextureHostValid { get; }

        void BeginDynamicTextureBuild();

        void RequestDynamicTextureRebuild();
    }

    internal interface INowFrameContent
    {
        void Draw(NowRect rect);
    }

    internal static class NowFrame
    {
        static readonly NowScopeGuard _scopes = new NowScopeGuard("NowFrame.Begin");

        static int _scopeStartedAt = int.MinValue;
        static INowDynamicTextureHost _dynamicTextureHost;

        internal static INowDynamicTextureHost dynamicTextureHost => _dynamicTextureHost;

        internal static bool hasActiveScopesThisFrame =>
            _scopes.count > 0 && _scopeStartedAt == Time.frameCount;

        internal static void DiscardAbandonedScopes()
        {
            _scopes.Clear();
            _scopeStartedAt = int.MinValue;
            _dynamicTextureHost = null;
        }

        internal static NowFrameScope Begin(
            float uiScale,
            bool trackRepaint = false,
            INowDynamicTextureHost dynamicTextureHost = null)
        {
            if (_scopes.count > 0)
            {
                throw new System.InvalidOperationException(
                    "NowUI retained hosts cannot rebuild recursively. Let Unity rebuild the nested host separately, " +
                    "or call a shared draw method directly from the outer host instead of invoking its rebuild entry point.");
            }

            _scopeStartedAt = Time.frameCount;
            int token = _scopes.Enter();

            try
            {
                dynamicTextureHost?.BeginDynamicTextureBuild();
                _dynamicTextureHost = dynamicTextureHost;
                return new NowFrameScope(uiScale, trackRepaint, token);
            }
            catch
            {
                _dynamicTextureHost = null;
                _scopes.Exit(token);
                throw;
            }
        }

        internal static bool IsCurrent(int token) => _scopes.IsCurrent(token);

        internal static void End(int token)
        {
            _dynamicTextureHost = null;
            _scopes.Exit(token);
        }

        internal static Vector2 DrawContent<TContent>(
            ref TContent content,
            NowRect rect,
            bool measurePass,
            bool trackContent,
            bool flushOverlays = true)
            where TContent : struct, INowFrameContent
        {
            Vector2 measured;

            if (measurePass)
                RequireStandaloneMeasurement();

            if (measurePass)
                NowLayout.BeginMeasureCycle();

            try
            {
                if (measurePass)
                    RunMeasurePass(ref content, rect);

                measured = RunDrawPass(ref content, rect, trackContent);
            }
            finally
            {
                if (measurePass)
                    NowLayout.EndMeasureCycle();
            }

            if (flushOverlays)
                NowOverlay.Flush();

            return measured;
        }

        internal static Vector2 MeasureContent<TContent>(ref TContent content, NowRect rect)
            where TContent : struct, INowFrameContent
        {
            RequireStandaloneMeasurement();

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

        static void RequireStandaloneMeasurement()
        {
            if (!NowLayout.isMeasurePass &&
                !NowLayout.hasActiveMeasureCycle &&
                !NowLayout.isTrackingContent)
                return;

            throw new System.InvalidOperationException(
                "An exact NowLayout measurement cannot rebuild recursively inside another measurement. " +
                "Call the nested draw method from the outer callback so it participates in the existing measure/draw cycle " +
                "and preferred-size tracking.");
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

        readonly int _token;

        bool _active;

        bool _repaintEnded;

        internal NowFrameScope(float uiScale, bool trackRepaint, int token)
        {
            _previousScale = Now.uiScale;
            _trackRepaint = trackRepaint;
            _token = token;
            _active = true;
            _repaintEnded = false;

            Now.SetUIScale(uiScale);

            if (trackRepaint)
                NowControlState.BeginRepaintTracking();
        }

        internal bool EndRepaintTracking()
        {
            if (!_active || !NowFrame.IsCurrent(_token) || !_trackRepaint || _repaintEnded)
                return false;

            _repaintEnded = true;
            return NowControlState.EndRepaintTracking();
        }

        public void Dispose()
        {
            if (!_active)
                return;

            if (!NowFrame.IsCurrent(_token))
            {
                _active = false;
                return;
            }

            try
            {
                if (_trackRepaint && !_repaintEnded)
                    NowControlState.EndRepaintTracking();

                Now.SetUIScale(_previousScale);
            }
            finally
            {
                NowFrame.End(_token);
                _active = false;
            }
        }
    }
}
