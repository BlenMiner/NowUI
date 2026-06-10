using NowUIInternal;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Inspector for imported Lottie animations: shows document info and an animated,
/// anti-aliased vector preview with play/pause and scrubbing.
/// </summary>
[CustomEditor(typeof(NowLottieAsset))]
sealed class NowLottieAssetEditor : Editor
{
    static readonly Color PREVIEW_BACKGROUND_COLOR = new Color(0.22f, 0.22f, 0.22f, 1f);

    NowLottiePreviewRenderer _preview;

    bool _playing = true;

    float _time;

    double _lastEditorTime;

    NowLottieAsset asset => target as NowLottieAsset;

    void OnEnable()
    {
        _lastEditorTime = EditorApplication.timeSinceStartup;
    }

    void OnDisable()
    {
        _preview?.Dispose();
        _preview = null;
    }

    public override void OnInspectorGUI()
    {
        var lottie = asset;

        if (lottie == null)
            return;

        if (!lottie.hasJson)
        {
            EditorGUILayout.HelpBox("No animation data.", MessageType.Warning);
            return;
        }

        EditorGUILayout.LabelField("Size", $"{lottie.width:0} × {lottie.height:0}");
        EditorGUILayout.LabelField("Frame Rate", $"{lottie.frameRate:0.##} fps");
        EditorGUILayout.LabelField("Frames", $"{lottie.durationFrames:0} ({lottie.inPoint:0} – {lottie.outPoint:0})");
        EditorGUILayout.LabelField("Duration", $"{lottie.duration:0.##} s");

        var composition = lottie.composition;

        if (composition != null)
        {
            EditorGUILayout.LabelField("Layers", composition.layers.Count.ToString());

            if (composition.precomps.Count > 0)
                EditorGUILayout.LabelField("Precomps", composition.precomps.Count.ToString());
        }
        else
        {
            EditorGUILayout.HelpBox("The animation JSON could not be parsed.", MessageType.Error);
        }
    }

    public override bool HasPreviewGUI()
    {
        return asset != null && asset.hasJson;
    }

    public override bool RequiresConstantRepaint()
    {
        return _playing && HasPreviewGUI();
    }

    public override GUIContent GetPreviewTitle()
    {
        return new GUIContent("Lottie Animation");
    }

    public override void OnPreviewSettings()
    {
        var lottie = asset;

        if (lottie == null || lottie.duration <= 0f)
            return;

        var icon = _playing
            ? EditorGUIUtility.IconContent("PauseButton")
            : EditorGUIUtility.IconContent("PlayButton");

        if (GUILayout.Button(icon, "preButton", GUILayout.Width(28f)))
        {
            _playing = !_playing;
            _lastEditorTime = EditorApplication.timeSinceStartup;
        }

        float duration = lottie.duration;
        float normalized = Mathf.Repeat(_time, duration) / duration;

        EditorGUI.BeginChangeCheck();
        normalized = GUILayout.HorizontalSlider(normalized, 0f, 1f, GUILayout.Width(110f));

        if (EditorGUI.EndChangeCheck())
        {
            _time = normalized * duration;
            _playing = false;
        }

        GUILayout.Label($"{Mathf.Repeat(_time, duration):0.00}s", EditorStyles.miniLabel, GUILayout.Width(44f));
    }

    public override void OnInteractivePreviewGUI(Rect rect, GUIStyle background)
    {
        var lottie = asset;

        if (lottie == null || rect.width < 8f || rect.height < 8f)
            return;

        double now = EditorApplication.timeSinceStartup;
        float delta = Mathf.Clamp((float)(now - _lastEditorTime), 0f, 0.25f);
        _lastEditorTime = now;

        if (_playing)
            _time += delta;

        if (Event.current.type != EventType.Repaint)
            return;

        _preview ??= new NowLottiePreviewRenderer();

        var composition = lottie.composition;

        if (composition == null)
            return;

        float frame = NowLottieRenderer.TimeToFrame(composition, _time, true);
        float pixelsPerPoint = EditorGUIUtility.pixelsPerPoint;

        var texture = _preview.Render(
            lottie,
            frame,
            Mathf.RoundToInt(rect.width * pixelsPerPoint),
            Mathf.RoundToInt(rect.height * pixelsPerPoint),
            PREVIEW_BACKGROUND_COLOR);

        if (texture != null)
            GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, false);
    }

    public override Texture2D RenderStaticPreview(string assetPath, Object[] subAssets, int width, int height)
    {
        var lottie = asset;

        if (lottie == null || !lottie.hasJson)
            return null;

        var composition = lottie.composition;

        if (composition == null)
            return null;

        // A frame a third of the way in is usually more representative than frame 0.
        float frame = composition.inPoint + composition.durationFrames * 0.33f;

        using var preview = new NowLottiePreviewRenderer();
        return preview.RenderToTexture(lottie, frame, width, height, Color.clear);
    }
}
