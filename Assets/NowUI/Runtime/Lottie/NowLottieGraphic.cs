using UnityEngine;

namespace NowUI
{
    /// <summary>
    /// Canvas component that plays a <see cref="NowLottieAsset"/> through the NowUI
    /// vector pipeline. The animation is re-tessellated at display resolution every
    /// frame, so it stays sharp at any scale.
    /// </summary>
    [AddComponentMenu("NowUI/NowUI Lottie")]
    public class NowLottieGraphic : NowGraphic
    {
        [SerializeField] NowLottieAsset _animation;

        [SerializeField] bool _playOnEnable = true;

        [SerializeField] bool _loop = true;

        [SerializeField] float _speed = 1f;

        [SerializeField] bool _preserveAspect = true;

        [Tooltip("Caps how often the animation advances; 0 plays at the animation's native rate. Lower rates (15-20) are ideal for small decorative emoji.")]
        [SerializeField, Min(0f)] float _playbackFrameRate;

        float _time;

        bool _playing;

        int _lastFrameIndex = int.MinValue;

        // 'new' because the obsolete Component.animation property still exists.
        public new NowLottieAsset animation
        {
            get => _animation;
            set
            {
                if (_animation == value)
                    return;

                _animation = value;
                _time = 0f;
                MarkDirty();
            }
        }

        public bool loop
        {
            get => _loop;
            set => _loop = value;
        }

        public float speed
        {
            get => _speed;
            set => _speed = value;
        }

        public bool preserveAspect
        {
            get => _preserveAspect;
            set
            {
                if (_preserveAspect == value)
                    return;

                _preserveAspect = value;
                MarkDirty();
            }
        }

        /// <summary>Playback frame rate cap; 0 plays at the animation's native rate.</summary>
        public float playbackFrameRate
        {
            get => _playbackFrameRate;
            set => _playbackFrameRate = Mathf.Max(0f, value);
        }

        public bool isPlaying => _playing;

        /// <summary>Playback position in seconds.</summary>
        public float time
        {
            get => _time;
            set
            {
                _time = Mathf.Max(0f, value);
                MarkDirty();
            }
        }

        /// <summary>Playback position as a 0..1 fraction of the animation duration.</summary>
        public float normalizedTime
        {
            get
            {
                float duration = _animation != null ? _animation.duration : 0f;

                if (duration <= 0f)
                    return 0f;

                return _loop ? Mathf.Repeat(_time, duration) / duration : Mathf.Clamp01(_time / duration);
            }
            set => time = (_animation != null ? _animation.duration : 0f) * value;
        }

        public void Play()
        {
            _playing = true;
            MarkDirty();
        }

        public void Pause()
        {
            _playing = false;
        }

        public void Stop()
        {
            _playing = false;
            _time = 0f;
            MarkDirty();
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            if (_playOnEnable && Application.isPlaying)
            {
                _time = 0f;
                _playing = true;
            }
        }

        /// <summary>
        /// Advances playback but marks dirty only when the displayed frame index
        /// changes: the draw call quantizes to whole frames (optionally capped by the
        /// playback rate), so the rebuild gate must match that quantization.
        /// </summary>
        protected override void LateUpdate()
        {
            base.LateUpdate();

            if (!_playing || !Application.isPlaying || _animation == null)
                return;

            _time += Time.deltaTime * _speed;

            if (!_loop && _animation.duration > 0f && _time >= _animation.duration)
            {
                _time = _animation.duration;
                _playing = false;
            }

            float effectiveRate = Mathf.Max(1f, _animation.frameRate);

            if (_playbackFrameRate > 0f)
                effectiveRate = Mathf.Min(effectiveRate, _playbackFrameRate);

            int frameIndex = Mathf.FloorToInt(_time * effectiveRate);

            if (frameIndex == _lastFrameIndex)
                return;

            _lastFrameIndex = frameIndex;
            MarkDirty();
        }

        protected override void DrawNowUI(NowRect rect)
        {
            base.DrawNowUI(rect);

            if (_animation == null)
                return;

            Now.Lottie(new Vector4(rect.x, rect.y, rect.width, rect.height), _animation)
                .SetTime(_time)
                .SetLoop(_loop)
                .SetPreserveAspect(_preserveAspect)
                .SetPlaybackFrameRate(_playbackFrameRate)
                .Draw();
        }
    }
}
