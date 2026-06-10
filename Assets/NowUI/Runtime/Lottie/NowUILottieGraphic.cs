using UnityEngine;

/// <summary>
/// Canvas component that plays a <see cref="NowLottieAsset"/> through the NowUI
/// vector pipeline. The animation is re-tessellated at display resolution every
/// frame, so it stays sharp at any scale.
/// </summary>
[AddComponentMenu("NowUI/NowUI Lottie")]
public class NowUILottieGraphic : NowUIGraphic
{
    [SerializeField] NowLottieAsset _animation;

    [SerializeField] bool _playOnEnable = true;

    [SerializeField] bool _loop = true;

    [SerializeField] float _speed = 1f;

    [SerializeField] bool _preserveAspect = true;

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

        // Only rebuild when the displayed frame changes; the draw call quantizes to
        // 1/8th frame, so use the same granularity (matters on high refresh displays).
        int frameIndex = Mathf.RoundToInt(_time * Mathf.Max(1f, _animation.frameRate) * 8f);

        if (frameIndex == _lastFrameIndex)
            return;

        _lastFrameIndex = frameIndex;
        MarkDirty();
    }

    protected override void DrawNowUI(Rect rect)
    {
        base.DrawNowUI(rect);

        if (_animation == null)
            return;

        NowUI.Lottie(new Vector4(rect.x, rect.y, rect.width, rect.height), _animation)
            .SetTime(_time)
            .SetLoop(_loop)
            .SetPreserveAspect(_preserveAspect)
            .Draw();
    }
}
