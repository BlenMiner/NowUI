using System;
using System.Collections.Generic;
using NowUIInternal;
using UnityEngine;

/// <summary>
/// Renders a frame of a <see cref="NowLottieAsset"/> into a render texture for
/// editor previews using the same vector tessellation as the runtime, drawn with a
/// simple vertex-color shader so previews stay independent of the NowUI draw state.
/// </summary>
sealed class NowLottiePreviewRenderer : IDisposable
{
    const float PREVIEW_MARGIN = 8f;

    readonly NowLottieDrawBuffer _buffer = new NowLottieDrawBuffer();

    readonly List<Vector3> _vertices = new List<Vector3>(1024);

    readonly List<Color> _colors = new List<Color>(1024);

    readonly List<int> _indices = new List<int>(2048);

    Mesh _mesh;

    Material _material;

    RenderTexture _texture;

    public RenderTexture texture => _texture;

    public RenderTexture Render(NowLottieAsset asset, float frame, int width, int height, Color background)
    {
        width = Mathf.Max(8, width);
        height = Mathf.Max(8, height);

        EnsureResources(width, height);

        var previous = RenderTexture.active;
        RenderTexture.active = _texture;

        GL.Clear(true, true, background);

        var composition = asset != null ? asset.composition : null;

        if (composition != null && _material != null)
        {
            float margin = Mathf.Min(PREVIEW_MARGIN, Mathf.Min(width, height) * 0.1f);
            var rect = new Vector4(margin, margin, width - margin * 2f, height - margin * 2f);

            if (NowLottieRenderer.Render(composition, frame, rect, true, Vector4.one, _buffer))
            {
                UploadMesh();

                GL.PushMatrix();
                GL.LoadPixelMatrix(0f, width, height, 0f);
                _material.SetPass(0);
                Graphics.DrawMeshNow(_mesh, Matrix4x4.identity);
                GL.PopMatrix();
            }
        }

        RenderTexture.active = previous;
        return _texture;
    }

    /// <summary>Renders into a readable Texture2D (for static asset thumbnails).</summary>
    public Texture2D RenderToTexture(NowLottieAsset asset, float frame, int width, int height, Color background)
    {
        Render(asset, frame, width, height, background);

        var previous = RenderTexture.active;
        RenderTexture.active = _texture;

        var result = new Texture2D(width, height, TextureFormat.RGBA32, false);
        result.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        result.Apply();

        RenderTexture.active = previous;
        return result;
    }

    void EnsureResources(int width, int height)
    {
        if (_texture != null && (_texture.width != width || _texture.height != height))
        {
            _texture.Release();
            UnityEngine.Object.DestroyImmediate(_texture);
            _texture = null;
        }

        if (_texture == null)
        {
            _texture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 8,
                hideFlags = HideFlags.HideAndDontSave
            };
            _texture.Create();
        }

        if (_mesh == null)
        {
            _mesh = new Mesh
            {
                indexFormat = UnityEngine.Rendering.IndexFormat.UInt32,
                hideFlags = HideFlags.HideAndDontSave
            };
            _mesh.MarkDynamic();
        }

        if (_material == null)
        {
            var shader = Shader.Find("Hidden/NowUI/Lottie Preview");

            if (shader != null)
            {
                _material = new Material(shader)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
            }
        }
    }

    void UploadMesh()
    {
        _vertices.Clear();
        _colors.Clear();
        _indices.Clear();

        for (int i = 0; i < _buffer.positions.count; ++i)
        {
            var position = _buffer.positions.array[i];
            _vertices.Add(new Vector3(position.x, position.y, 0f));

            var color = _buffer.colors.array[i];
            _colors.Add(new Color(color.x, color.y, color.z, color.w));
        }

        for (int i = 0; i < _buffer.indices.count; ++i)
            _indices.Add(_buffer.indices.array[i]);

        _mesh.Clear(true);
        _mesh.SetVertices(_vertices);
        _mesh.SetColors(_colors);
        _mesh.SetTriangles(_indices, 0, false);
        _mesh.RecalculateBounds();
    }

    public void Dispose()
    {
        if (_texture != null)
        {
            _texture.Release();
            UnityEngine.Object.DestroyImmediate(_texture);
            _texture = null;
        }

        if (_mesh != null)
        {
            UnityEngine.Object.DestroyImmediate(_mesh);
            _mesh = null;
        }

        if (_material != null)
        {
            UnityEngine.Object.DestroyImmediate(_material);
            _material = null;
        }
    }
}
