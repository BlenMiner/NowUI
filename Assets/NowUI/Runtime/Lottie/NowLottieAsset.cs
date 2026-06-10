using NowUIInternal;
using System;
using UnityEngine;

/// <summary>
/// A Lottie vector animation. The source JSON is kept verbatim and parsed into a
/// runtime model on first use — the animation is never rasterized to textures, so it
/// scales losslessly at any size.
/// </summary>
public sealed class NowLottieAsset : ScriptableObject
{
    [SerializeField, HideInInspector] string _json;

    [SerializeField, HideInInspector] float _width;

    [SerializeField, HideInInspector] float _height;

    [SerializeField, HideInInspector] float _frameRate;

    [SerializeField, HideInInspector] float _inPoint;

    [SerializeField, HideInInspector] float _outPoint;

    [NonSerialized] NowLottieComposition _composition;

    [NonSerialized] bool _parseFailed;

    public float width => _width;

    public float height => _height;

    public float frameRate => _frameRate;

    public float inPoint => _inPoint;

    public float outPoint => _outPoint;

    public float durationFrames => Mathf.Max(0f, _outPoint - _inPoint);

    public float duration => _frameRate > 0f ? durationFrames / _frameRate : 0f;

    public bool hasJson => !string.IsNullOrEmpty(_json);

    /// <summary>Parsed animation model; null when the asset is empty or invalid.</summary>
    public NowLottieComposition composition
    {
        get
        {
            if (_composition == null && !_parseFailed && !string.IsNullOrEmpty(_json))
            {
                try
                {
                    _composition = NowLottieComposition.Parse(_json);
                }
                catch (Exception exception)
                {
                    _parseFailed = true;
                    Debug.LogError($"Failed to parse Lottie animation '{name}': {exception.Message}", this);
                }
            }

            return _composition;
        }
    }

    /// <summary>
    /// Assigns the animation JSON. Throws on invalid documents so importers can
    /// surface the error. The parsed model is cached.
    /// </summary>
    public void SetSource(string json)
    {
        var parsed = NowLottieComposition.Parse(json);

        _json = json;
        _composition = parsed;
        _parseFailed = false;
        _width = parsed.width;
        _height = parsed.height;
        _frameRate = parsed.frameRate;
        _inPoint = parsed.inPoint;
        _outPoint = parsed.outPoint;
    }
}
