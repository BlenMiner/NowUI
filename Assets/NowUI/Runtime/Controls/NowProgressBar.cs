using UnityEngine;

namespace NowUI
{
    /// <summary>
    /// Progress bar. Determinate by default (pass a 0..1 value); call
    /// <see cref="SetIndeterminate"/> plus <see cref="SetTime"/> for a sweeping
    /// indicator. There is no hidden clock: the sweep phase comes entirely from
    /// the caller-passed time, so it is deterministic and test-friendly.
    /// <code>Now.ProgressBar(rect, downloaded / total).Draw();</code>
    /// <code>NowLayout.ProgressBar().SetIndeterminate().SetTime(Time.time).Draw();</code>
    /// </summary>
    [NowBuilder]
    public struct NowProgressBar
    {
        readonly float _value01;
        readonly int _site;
        NowId _id;
        NowLayoutOptions _options;
        readonly NowRect _rect;
        readonly bool _hasRect;
        bool _indeterminate;
        float _time;
        bool _hasTime;

        int ResolveControlId() => NowControls.GetControlId(_id, _site);

        internal NowProgressBar(float value01, int site)
        {
            _value01 = value01;
            _site = site;
            _id = default;
            _options = default;
            _rect = default;
            _hasRect = false;
            _indeterminate = false;
            _time = 0f;
            _hasTime = false;
        }

        internal NowProgressBar(NowRect rect, float value01, int site) : this(value01, site)
        {
            _rect = rect;
            _hasRect = true;
        }

        public NowProgressBar SetOptions(NowLayoutOptions options) { _options = options; return this; }

        public NowProgressBar SetWidth(float width) { _options = _options.SetWidth(width); return this; }

        public NowProgressBar SetStretchWidth(float weight = 1f) { _options = _options.SetStretchWidth(weight); return this; }

        /// <summary>Explicit control id, decoupling identity from the call site.</summary>
        public NowProgressBar SetId(NowId id) { _id = id; return this; }

        /// <summary>Sweeping activity indicator instead of a filled fraction. Pass time via <see cref="SetTime"/>.</summary>
        public NowProgressBar SetIndeterminate() { _indeterminate = true; return this; }

        /// <summary>Caller-owned clock driving the indeterminate sweep (e.g. <c>Time.time</c>). Without it the sweep is a static frame.</summary>
        public NowProgressBar SetTime(float time) { _time = time; _hasTime = true; return this; }

        public void Draw()
        {
            var theme = NowTheme.themeAsset;
            var renderer = theme.controlRenderer;

            NowRect rect = NowControls.ReserveRect(_hasRect, _rect, _options, renderer.MeasureProgressBar(theme));

            float phase01 = 0.35f;

            if (_indeterminate && _hasTime)
            {
                float period = theme.controlStyles.progressBarPeriod;
                phase01 = Mathf.Repeat(_time / period, 1f);

                int id = ResolveControlId();
                ref float lastTime = ref NowControlState.Get<float>(NowInput.CombineId(id, 0x50425474));

                if (!Mathf.Approximately(lastTime, _time))
                {
                    lastTime = _time;
                    NowControlState.RequestRepaint();
                }
            }

            renderer.DrawProgressBar(new NowProgressBarRenderContext(
                theme, rect, _value01, _indeterminate, phase01));
        }
    }
}
