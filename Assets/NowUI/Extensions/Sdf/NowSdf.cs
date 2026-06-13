using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace NowUI.Sdf
{
    public enum NowSdfOperation
    {
        Union = 0,
        Subtract = 1,
        Intersect = 2,
        SmoothUnion = 3,
        SmoothSubtract = 4,
        SmoothIntersect = 5
    }

    enum NowSdfShapeType
    {
        Circle = 0,
        Box = 1,
        RoundedBox = 2,
        Ellipse = 3,
        Capsule = 4
    }

    enum NowSdfLayerKind
    {
        Graph = 0,
        Morph = 1
    }

    struct NowSdfNode
    {
        public NowSdfShapeType type;
        public NowSdfOperation operation;
        public float smoothing;
        public Vector4 data1;
        public Vector4 data2;
        public Vector4 color;
        public Vector4 uv;
        public bool useTexture;
        public NowRect bounds;
    }

    struct NowSdfLayer
    {
        public NowSdfLayerKind kind;
        public NowSdfOperation operation;
        public float smoothing;
        public NowSdfGraph graph;
        public NowSdfGraph targetGraph;
        public float morph;
    }

    /// <summary>
    /// Reusable signed-distance-field primitive graph. Graphs are pure shape
    /// content; compose them at draw time with <see cref="NowSdfBuilder.Graph"/>
    /// and <see cref="NowSdfBuilder.Morph"/>.
    /// </summary>
    public sealed class NowSdfGraph
    {
        readonly List<NowSdfNode> _nodes = new List<NowSdfNode>(8);

        Vector4 _color = Vector4.one;
        Vector4 _textureUv = new Vector4(0f, 0f, 1f, 1f);
        Texture _texture;
        bool _useTexture;
        NowSdfOperation _operation = NowSdfOperation.Union;
        float _smoothing;
        NowRect _bounds;
        bool _hasBounds;

        internal IReadOnlyList<NowSdfNode> nodes => _nodes;

        internal Texture texture => _texture;

        internal bool hasNodes => _nodes.Count > 0;

        public Vector2 measureSize => _hasBounds ? new Vector2(_bounds.xMax, _bounds.yMax) : Vector2.zero;

        public NowSdfGraph Clear()
        {
            _nodes.Clear();
            _operation = NowSdfOperation.Union;
            _smoothing = 0f;
            _bounds = default;
            _hasBounds = false;
            return this;
        }

        internal NowSdfGraph ResetForReuse()
        {
            Clear();
            _color = Vector4.one;
            _textureUv = new Vector4(0f, 0f, 1f, 1f);
            _texture = null;
            _useTexture = false;
            return this;
        }

        public NowSdfGraph SetColor(Color color)
        {
            _color = color;
            return this;
        }

        public NowSdfGraph SetColor(Vector4 color)
        {
            _color = color;
            return this;
        }

        public NowSdfGraph UseColor()
        {
            _useTexture = false;
            return this;
        }

        public NowSdfGraph SetTexture(Texture texture)
        {
            _texture = texture;
            _useTexture = texture != null;
            return this;
        }

        public NowSdfGraph SetTexture(Texture texture, Vector4 uvRect)
        {
            SetTexture(texture);
            SetTextureUV(uvRect);
            return this;
        }

        public NowSdfGraph UseTexture()
        {
            _useTexture = _texture != null;
            return this;
        }

        public NowSdfGraph UseTexture(Vector4 uvRect)
        {
            SetTextureUV(uvRect);
            return UseTexture();
        }

        public NowSdfGraph SetTextureUV(Vector4 uvRect)
        {
            if (Mathf.Approximately(uvRect.z, 0f) && Mathf.Approximately(uvRect.w, 0f))
                uvRect = new Vector4(0f, 0f, 1f, 1f);

            _textureUv = uvRect;
            return this;
        }

        public NowSdfGraph SetOperation(NowSdfOperation operation, float smoothing = 0f)
        {
            _operation = operation;
            _smoothing = Mathf.Max(0f, smoothing);
            return this;
        }

        public NowSdfGraph Union(float smoothing = 0f)
        {
            return SetOperation(smoothing > 0f ? NowSdfOperation.SmoothUnion : NowSdfOperation.Union, smoothing);
        }

        public NowSdfGraph Subtract(float smoothing = 0f)
        {
            return SetOperation(smoothing > 0f ? NowSdfOperation.SmoothSubtract : NowSdfOperation.Subtract, smoothing);
        }

        public NowSdfGraph Intersect(float smoothing = 0f)
        {
            return SetOperation(smoothing > 0f ? NowSdfOperation.SmoothIntersect : NowSdfOperation.Intersect, smoothing);
        }

        public NowSdfGraph SmoothUnion(float smoothing)
        {
            return SetOperation(NowSdfOperation.SmoothUnion, smoothing);
        }

        public NowSdfGraph SmoothSubtract(float smoothing)
        {
            return SetOperation(NowSdfOperation.SmoothSubtract, smoothing);
        }

        public NowSdfGraph SmoothIntersect(float smoothing)
        {
            return SetOperation(NowSdfOperation.SmoothIntersect, smoothing);
        }

        public NowSdfGraph Circle(Vector2 center, float radius)
        {
            radius = Mathf.Max(0f, radius);
            Add(
                NowSdfShapeType.Circle,
                new Vector4(center.x, center.y, radius, 0f),
                default,
                new NowRect(center.x - radius, center.y - radius, radius * 2f, radius * 2f));
            return this;
        }

        public NowSdfGraph Circle(Vector2 center, float radius, Color color)
        {
            SetColor(color);
            UseColor();
            return Circle(center, radius);
        }

        public NowSdfGraph Box(NowRect rect)
        {
            Add(NowSdfShapeType.Box, RectData(rect), default, rect);
            return this;
        }

        public NowSdfGraph Box(NowRect rect, Color color)
        {
            SetColor(color);
            UseColor();
            return Box(rect);
        }

        public NowSdfGraph Rectangle(NowRect rect)
        {
            return Box(rect);
        }

        public NowSdfGraph RoundedBox(NowRect rect, float radius)
        {
            return RoundedBox(rect, new Vector4(radius, radius, radius, radius));
        }

        public NowSdfGraph RoundedBox(NowRect rect, Vector4 radius)
        {
            radius.x = Mathf.Max(0f, radius.x);
            radius.y = Mathf.Max(0f, radius.y);
            radius.z = Mathf.Max(0f, radius.z);
            radius.w = Mathf.Max(0f, radius.w);
            Add(NowSdfShapeType.RoundedBox, RectData(rect), radius, rect);
            return this;
        }

        public NowSdfGraph RoundedBox(NowRect rect, float radius, Color color)
        {
            SetColor(color);
            UseColor();
            return RoundedBox(rect, radius);
        }

        public NowSdfGraph RoundRect(NowRect rect, float radius)
        {
            return RoundedBox(rect, radius);
        }

        public NowSdfGraph Ellipse(NowRect rect)
        {
            Add(NowSdfShapeType.Ellipse, RectData(rect), default, rect);
            return this;
        }

        public NowSdfGraph Ellipse(NowRect rect, Color color)
        {
            SetColor(color);
            UseColor();
            return Ellipse(rect);
        }

        public NowSdfGraph Capsule(Vector2 from, Vector2 to, float radius)
        {
            radius = Mathf.Max(0f, radius);
            var min = Vector2.Min(from, to) - new Vector2(radius, radius);
            var max = Vector2.Max(from, to) + new Vector2(radius, radius);
            Add(
                NowSdfShapeType.Capsule,
                new Vector4(from.x, from.y, to.x, to.y),
                new Vector4(radius, 0f, 0f, 0f),
                new NowRect(min.x, min.y, max.x - min.x, max.y - min.y));
            return this;
        }

        public NowSdfGraph Capsule(NowRect rect)
        {
            float radius = Mathf.Min(rect.width, rect.height) * 0.5f;
            Vector2 from;
            Vector2 to;

            if (rect.width >= rect.height)
            {
                from = new Vector2(rect.x + radius, rect.y + rect.height * 0.5f);
                to = new Vector2(rect.xMax - radius, rect.y + rect.height * 0.5f);
            }
            else
            {
                from = new Vector2(rect.x + rect.width * 0.5f, rect.y + radius);
                to = new Vector2(rect.x + rect.width * 0.5f, rect.yMax - radius);
            }

            return Capsule(from, to, radius);
        }

        internal void CopyStyleFrom(NowSdfGraph source)
        {
            _color = source._color;
            _textureUv = source._textureUv;
            _texture = source._texture;
            _useTexture = source._useTexture;
        }

        void Add(NowSdfShapeType type, Vector4 data1, Vector4 data2, NowRect bounds)
        {
            var operation = _nodes.Count == 0 ? NowSdfOperation.Union : _operation;
            _nodes.Add(new NowSdfNode
            {
                type = type,
                operation = operation,
                smoothing = _smoothing,
                data1 = data1,
                data2 = data2,
                color = _color,
                uv = _textureUv,
                useTexture = _useTexture,
                bounds = bounds
            });

            Encapsulate(bounds);
            _operation = NowSdfOperation.Union;
            _smoothing = 0f;
        }

        static Vector4 RectData(NowRect rect)
        {
            return new Vector4(rect.x + rect.width * 0.5f, rect.y + rect.height * 0.5f, rect.width, rect.height);
        }

        void Encapsulate(NowRect rect)
        {
            if (rect.isEmpty)
                return;

            _bounds = _hasBounds ? _bounds.Union(rect) : rect;
            _hasBounds = true;
        }
    }

    /// <summary>
    /// Entry points for signed-distance-field shape composition.
    /// <code>
    /// var cutout = NowSdf.Graph().Circle(new Vector2(92, 52), 18);
    ///
    /// NowSdf.Scene(rect)
    ///     .SetColor(Color.red).Circle(new Vector2(44, 44), 36)
    ///     .SetColor(Color.cyan).SmoothUnion(10).RoundedBox(new NowRect(38, 20, 120, 70), 16)
    ///     .Subtract().Graph(cutout)
    ///     .Draw();
    /// </code>
    /// Shape coordinates are local to the scene rect. Operations apply to the
    /// next primitive or graph, then reset to Union.
    /// </summary>
    public static class NowSdf
    {
        public const int MaxShapes = 64;
        public const int MaxLayers = 16;

        static readonly Dictionary<int, NowSdfCache> _caches = new Dictionary<int, NowSdfCache>(16);

        public static NowSdfGraph Graph()
        {
            return new NowSdfGraph();
        }

        public static NowSdfBuilder Scene(
            NowRect rect,
            string id = null,
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0)
        {
            return new NowSdfBuilder(GetCache(ControlId(id, file, line)), rect, true, default);
        }

        public static NowSdfBuilder Scene(
            string id = null,
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0)
        {
            return new NowSdfBuilder(GetCache(ControlId(id, file, line)), default, false, default);
        }

        public static NowSdfBuilder Scene(
            float width,
            float height,
            string id = null,
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0)
        {
            var options = new NowLayoutOptions().SetSize(width, height);
            return new NowSdfBuilder(GetCache(ControlId(id, file, line)), default, false, options);
        }

        public static NowSdfBuilder Scene(
            NowLayoutOptions options,
            string id = null,
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0)
        {
            return new NowSdfBuilder(GetCache(ControlId(id, file, line)), default, false, options);
        }

        public static void Reset()
        {
            foreach (var cache in _caches.Values)
                cache.Release();

            _caches.Clear();
        }

        static int ControlId(string id, string file, int line)
        {
            return id != null
                ? NowControls.GetControlId(id)
                : NowControls.GetControlId(NowControls.SiteId(file, line));
        }

        static NowSdfCache GetCache(int id)
        {
            if (!_caches.TryGetValue(id, out var cache))
            {
                cache = new NowSdfCache();
                _caches[id] = cache;
            }

            cache.Begin();
            return cache;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForRuntimeLoad()
        {
            Reset();
        }
    }

    [NowBuilder]
    public struct NowSdfBuilder
    {
        readonly NowSdfCache _cache;
        readonly bool _hasRect;
        readonly NowRect _rect;
        NowLayoutOptions _options;
        NowRect _mask;
        bool _hasMask;
        Vector4 _tint;

        internal NowSdfBuilder(NowSdfCache cache, NowRect rect, bool hasRect, NowLayoutOptions options)
        {
            _cache = cache;
            _rect = rect;
            _hasRect = hasRect;
            _options = options;
            _mask = default;
            _hasMask = false;
            _tint = Vector4.one;
        }

        public NowSdfBuilder SetOptions(NowLayoutOptions options) { _options = options; return this; }

        public NowSdfBuilder SetWidth(float width) { _options = _options.SetWidth(width); return this; }

        public NowSdfBuilder SetHeight(float height) { _options = _options.SetHeight(height); return this; }

        public NowSdfBuilder SetLayoutSize(float width, float height) { _options = _options.SetSize(width, height); return this; }

        public NowSdfBuilder SetMinWidth(float minWidth) { _options = _options.SetMinWidth(minWidth); return this; }

        public NowSdfBuilder SetMaxWidth(float maxWidth) { _options = _options.SetMaxWidth(maxWidth); return this; }

        public NowSdfBuilder SetMinHeight(float minHeight) { _options = _options.SetMinHeight(minHeight); return this; }

        public NowSdfBuilder SetMaxHeight(float maxHeight) { _options = _options.SetMaxHeight(maxHeight); return this; }

        public NowSdfBuilder SetStretchWidth(float weight = 1f) { _options = _options.SetStretchWidth(weight); return this; }

        public NowSdfBuilder SetStretchHeight(float weight = 1f) { _options = _options.SetStretchHeight(weight); return this; }

        public NowSdfBuilder SetAlign(NowLayoutAlign align) { _options = _options.SetAlign(align); return this; }

        public NowSdfBuilder SetMask(NowRect mask)
        {
            _mask = mask;
            _hasMask = true;
            return this;
        }

        public NowSdfBuilder SetTint(Color color)
        {
            _tint = color;
            return this;
        }

        public NowSdfBuilder SetTint(Vector4 color)
        {
            _tint = color;
            return this;
        }

        public NowSdfBuilder SetColor(Color color)
        {
            _cache.SetColor(color);
            return this;
        }

        public NowSdfBuilder SetColor(Vector4 color)
        {
            _cache.SetColor(color);
            return this;
        }

        public NowSdfBuilder UseColor()
        {
            _cache.UseColor();
            return this;
        }

        public NowSdfBuilder SetTexture(Texture texture)
        {
            _cache.SetTexture(texture);
            return this;
        }

        public NowSdfBuilder SetTexture(Texture texture, Vector4 uvRect)
        {
            _cache.SetTexture(texture);
            _cache.SetTextureUV(uvRect);
            return this;
        }

        public NowSdfBuilder UseTexture()
        {
            _cache.UseTexture();
            return this;
        }

        public NowSdfBuilder UseTexture(Vector4 uvRect)
        {
            _cache.SetTextureUV(uvRect);
            _cache.UseTexture();
            return this;
        }

        public NowSdfBuilder SetTextureUV(Vector4 uvRect)
        {
            _cache.SetTextureUV(uvRect);
            return this;
        }

        public NowSdfBuilder SetFeather(float feather)
        {
            _cache.SetFeather(feather);
            return this;
        }

        public NowSdfBuilder SetOperation(NowSdfOperation operation, float smoothing = 0f)
        {
            _cache.SetOperation(operation, smoothing);
            return this;
        }

        public NowSdfBuilder Union(float smoothing = 0f)
        {
            _cache.SetOperation(smoothing > 0f ? NowSdfOperation.SmoothUnion : NowSdfOperation.Union, smoothing);
            return this;
        }

        public NowSdfBuilder Subtract(float smoothing = 0f)
        {
            _cache.SetOperation(smoothing > 0f ? NowSdfOperation.SmoothSubtract : NowSdfOperation.Subtract, smoothing);
            return this;
        }

        public NowSdfBuilder Intersect(float smoothing = 0f)
        {
            _cache.SetOperation(smoothing > 0f ? NowSdfOperation.SmoothIntersect : NowSdfOperation.Intersect, smoothing);
            return this;
        }

        public NowSdfBuilder SmoothUnion(float smoothing)
        {
            _cache.SetOperation(NowSdfOperation.SmoothUnion, smoothing);
            return this;
        }

        public NowSdfBuilder SmoothSubtract(float smoothing)
        {
            _cache.SetOperation(NowSdfOperation.SmoothSubtract, smoothing);
            return this;
        }

        public NowSdfBuilder SmoothIntersect(float smoothing)
        {
            _cache.SetOperation(NowSdfOperation.SmoothIntersect, smoothing);
            return this;
        }

        public NowSdfBuilder Graph(NowSdfGraph graph)
        {
            _cache.Graph(graph);
            return this;
        }

        public NowSdfBuilder Morph(NowSdfGraph from, NowSdfGraph to, float t)
        {
            _cache.Morph(from, to, t);
            return this;
        }

        public NowSdfBuilder Lerp(NowSdfGraph from, NowSdfGraph to, float t)
        {
            return Morph(from, to, t);
        }

        public NowSdfBuilder Circle(Vector2 center, float radius)
        {
            _cache.Circle(center, radius);
            return this;
        }

        public NowSdfBuilder Circle(Vector2 center, float radius, Color color)
        {
            _cache.SetColor(color);
            _cache.UseColor();
            _cache.Circle(center, radius);
            return this;
        }

        public NowSdfBuilder Box(NowRect rect)
        {
            _cache.Box(rect);
            return this;
        }

        public NowSdfBuilder Box(NowRect rect, Color color)
        {
            _cache.SetColor(color);
            _cache.UseColor();
            _cache.Box(rect);
            return this;
        }

        public NowSdfBuilder Rectangle(NowRect rect)
        {
            return Box(rect);
        }

        public NowSdfBuilder RoundedBox(NowRect rect, float radius)
        {
            _cache.RoundedBox(rect, radius);
            return this;
        }

        public NowSdfBuilder RoundedBox(NowRect rect, Vector4 radius)
        {
            _cache.RoundedBox(rect, radius);
            return this;
        }

        public NowSdfBuilder RoundedBox(NowRect rect, float radius, Color color)
        {
            _cache.SetColor(color);
            _cache.UseColor();
            _cache.RoundedBox(rect, radius);
            return this;
        }

        public NowSdfBuilder RoundRect(NowRect rect, float radius)
        {
            return RoundedBox(rect, radius);
        }

        public NowSdfBuilder Ellipse(NowRect rect)
        {
            _cache.Ellipse(rect);
            return this;
        }

        public NowSdfBuilder Ellipse(NowRect rect, Color color)
        {
            _cache.SetColor(color);
            _cache.UseColor();
            _cache.Ellipse(rect);
            return this;
        }

        public NowSdfBuilder Capsule(Vector2 from, Vector2 to, float radius)
        {
            _cache.Capsule(from, to, radius);
            return this;
        }

        public NowSdfBuilder Capsule(NowRect rect)
        {
            _cache.Capsule(rect);
            return this;
        }

        public Vector2 Measure()
        {
            return _cache.measureSize;
        }

        [NowConsumer]
        public NowSdfBuilder Draw()
        {
            return Draw(_hasRect ? _rect : ReserveLayoutRect());
        }

        [NowConsumer]
        public NowSdfBuilder Draw(NowRect rect)
        {
            _cache.Draw(rect, _hasMask ? _mask : rect, _tint);
            return this;
        }

        NowRect ReserveLayoutRect()
        {
            var options = _options;
            Vector2 size = _cache.measureSize;

            if (!options.Has(NowLayoutOptions.Field.Width) && size.x > 0f)
                options = options.SetWidth(size.x);

            if (!options.Has(NowLayoutOptions.Field.Height) && size.y > 0f)
                options = options.SetHeight(size.y);

            return NowLayout.Rect(options);
        }
    }

    sealed class NowSdfCache
    {
        static readonly int _mainTexProp = Shader.PropertyToID("_MainTex");
        static readonly int _shapeCountProp = Shader.PropertyToID("_SdfShapeCount");
        static readonly int _layerCountProp = Shader.PropertyToID("_SdfLayerCount");
        static readonly int _featherProp = Shader.PropertyToID("_SdfFeather");
        static readonly int _canvasLayoutProp = Shader.PropertyToID("_NowCanvasLayout");
        static readonly int _data0Prop = Shader.PropertyToID("_SdfData0");
        static readonly int _data1Prop = Shader.PropertyToID("_SdfData1");
        static readonly int _data2Prop = Shader.PropertyToID("_SdfData2");
        static readonly int _shapeMetaProp = Shader.PropertyToID("_SdfShapeMeta");
        static readonly int _colorsProp = Shader.PropertyToID("_SdfColors");
        static readonly int _uvsProp = Shader.PropertyToID("_SdfUvs");
        static readonly int _layerData0Prop = Shader.PropertyToID("_SdfLayerData0");
        static readonly int _layerData1Prop = Shader.PropertyToID("_SdfLayerData1");

        readonly Vector4[] _data0 = new Vector4[NowSdf.MaxShapes];
        readonly Vector4[] _data1 = new Vector4[NowSdf.MaxShapes];
        readonly Vector4[] _data2 = new Vector4[NowSdf.MaxShapes];
        readonly Vector4[] _shapeMeta = new Vector4[NowSdf.MaxShapes];
        readonly Vector4[] _colors = new Vector4[NowSdf.MaxShapes];
        readonly Vector4[] _uvs = new Vector4[NowSdf.MaxShapes];
        readonly Vector4[] _layerData0 = new Vector4[NowSdf.MaxLayers];
        readonly Vector4[] _layerData1 = new Vector4[NowSdf.MaxLayers];

        readonly List<NowSdfLayer> _layers = new List<NowSdfLayer>(4);
        readonly List<NowSdfGraph> _inlineGraphs = new List<NowSdfGraph>(4);
        readonly Dictionary<NowSdfGraph, int> _graphIds = new Dictionary<NowSdfGraph, int>(8);

        Material _material;
        NowSdfGraph _activeGraph;
        int _inlineGraphCursor;
        NowSdfOperation _pendingOperation;
        float _pendingSmoothing;
        NowSdfOperation _activeLayerOperation;
        float _activeLayerSmoothing;
        float _feather;
        Texture _texture;
        NowRect _bounds;
        bool _hasBounds;

        public Vector2 measureSize => _hasBounds
            ? new Vector2(_bounds.xMax, _bounds.yMax)
            : _activeGraph != null
                ? _activeGraph.measureSize
                : Vector2.zero;

        public void Begin()
        {
            _layers.Clear();
            _graphIds.Clear();
            _inlineGraphCursor = 0;
            _activeGraph = RentInlineGraph();
            _pendingOperation = NowSdfOperation.Union;
            _pendingSmoothing = 0f;
            _activeLayerOperation = NowSdfOperation.Union;
            _activeLayerSmoothing = 0f;
            _feather = 0f;
            _texture = null;
            _bounds = default;
            _hasBounds = false;
        }

        NowSdfGraph RentInlineGraph()
        {
            if (_inlineGraphCursor == _inlineGraphs.Count)
                _inlineGraphs.Add(new NowSdfGraph());

            return _inlineGraphs[_inlineGraphCursor++].ResetForReuse();
        }

        public void Release()
        {
            if (_material == null)
                return;

            if (Application.isPlaying)
                UnityEngine.Object.Destroy(_material);
            else
                UnityEngine.Object.DestroyImmediate(_material);

            _material = null;
        }

        public void SetColor(Vector4 color)
        {
            _activeGraph.SetColor(color);
        }

        public void UseColor()
        {
            _activeGraph.UseColor();
        }

        public void SetTexture(Texture texture)
        {
            _texture = _texture != null ? _texture : texture;
            _activeGraph.SetTexture(texture);
        }

        public void UseTexture()
        {
            _activeGraph.UseTexture();
        }

        public void SetTextureUV(Vector4 uvRect)
        {
            _activeGraph.SetTextureUV(uvRect);
        }

        public void SetFeather(float feather)
        {
            _feather = Mathf.Max(0f, feather);
        }

        public void SetOperation(NowSdfOperation operation, float smoothing)
        {
            _pendingOperation = operation;
            _pendingSmoothing = Mathf.Max(0f, smoothing);
        }

        public void Graph(NowSdfGraph graph)
        {
            if (graph == null || !graph.hasNodes)
                return;

            FlushActiveGraph();
            AddLayer(new NowSdfLayer
            {
                kind = NowSdfLayerKind.Graph,
                operation = ConsumePendingOperation(),
                smoothing = ConsumePendingSmoothing(),
                graph = graph
            });
        }

        public void Morph(NowSdfGraph from, NowSdfGraph to, float t)
        {
            if (from == null || to == null || !from.hasNodes || !to.hasNodes)
                return;

            FlushActiveGraph();
            AddLayer(new NowSdfLayer
            {
                kind = NowSdfLayerKind.Morph,
                operation = ConsumePendingOperation(),
                smoothing = ConsumePendingSmoothing(),
                graph = from,
                targetGraph = to,
                morph = Mathf.Clamp01(t)
            });
        }

        public void Circle(Vector2 center, float radius)
        {
            PrepareActivePrimitive();
            _activeGraph.SetOperation(_pendingOperation, _pendingSmoothing).Circle(center, radius);
            ResetPendingOperation();
            Encapsulate(_activeGraph.measureSize);
        }

        public void Box(NowRect rect)
        {
            PrepareActivePrimitive();
            _activeGraph.SetOperation(_pendingOperation, _pendingSmoothing).Box(rect);
            ResetPendingOperation();
            Encapsulate(rect);
        }

        public void RoundedBox(NowRect rect, float radius)
        {
            PrepareActivePrimitive();
            _activeGraph.SetOperation(_pendingOperation, _pendingSmoothing).RoundedBox(rect, radius);
            ResetPendingOperation();
            Encapsulate(rect);
        }

        public void RoundedBox(NowRect rect, Vector4 radius)
        {
            PrepareActivePrimitive();
            _activeGraph.SetOperation(_pendingOperation, _pendingSmoothing).RoundedBox(rect, radius);
            ResetPendingOperation();
            Encapsulate(rect);
        }

        public void Ellipse(NowRect rect)
        {
            PrepareActivePrimitive();
            _activeGraph.SetOperation(_pendingOperation, _pendingSmoothing).Ellipse(rect);
            ResetPendingOperation();
            Encapsulate(rect);
        }

        public void Capsule(Vector2 from, Vector2 to, float radius)
        {
            PrepareActivePrimitive();
            _activeGraph.SetOperation(_pendingOperation, _pendingSmoothing).Capsule(from, to, radius);
            ResetPendingOperation();
            Encapsulate(_activeGraph.measureSize);
        }

        public void Capsule(NowRect rect)
        {
            PrepareActivePrimitive();
            _activeGraph.SetOperation(_pendingOperation, _pendingSmoothing).Capsule(rect);
            ResetPendingOperation();
            Encapsulate(rect);
        }

        public void Draw(NowRect rect, NowRect mask, Vector4 tint)
        {
            FlushActiveGraph();

            if (_layers.Count == 0)
                return;

            var material = GetMaterial();

            if (material == null)
                return;

            Upload(material);
            Now.DrawSdf(rect, mask, material, tint);
        }

        void PrepareActivePrimitive()
        {
            if (_activeGraph.hasNodes || _layers.Count == 0)
                return;

            _activeLayerOperation = ConsumePendingOperation();
            _activeLayerSmoothing = ConsumePendingSmoothing();
        }

        void FlushActiveGraph()
        {
            if (!_activeGraph.hasNodes)
                return;

            AddLayer(new NowSdfLayer
            {
                kind = NowSdfLayerKind.Graph,
                operation = _layers.Count == 0 ? NowSdfOperation.Union : _activeLayerOperation,
                smoothing = _layers.Count == 0 ? 0f : _activeLayerSmoothing,
                graph = _activeGraph
            });

            var next = RentInlineGraph();
            next.CopyStyleFrom(_activeGraph);
            _activeGraph = next;
            _activeLayerOperation = NowSdfOperation.Union;
            _activeLayerSmoothing = 0f;
        }

        void AddLayer(NowSdfLayer layer)
        {
            if (_layers.Count >= NowSdf.MaxLayers)
                return;

            _layers.Add(layer);
            Encapsulate(layer.graph.measureSize);

            if (layer.targetGraph != null)
                Encapsulate(layer.targetGraph.measureSize);

            _texture ??= layer.graph.texture;

            if (layer.targetGraph != null)
                _texture ??= layer.targetGraph.texture;

            ResetPendingOperation();
        }

        NowSdfOperation ConsumePendingOperation()
        {
            var operation = _layers.Count == 0 ? NowSdfOperation.Union : _pendingOperation;
            return operation;
        }

        float ConsumePendingSmoothing()
        {
            return _layers.Count == 0 ? 0f : _pendingSmoothing;
        }

        void ResetPendingOperation()
        {
            _pendingOperation = NowSdfOperation.Union;
            _pendingSmoothing = 0f;
        }

        void Encapsulate(Vector2 size)
        {
            if (size.x <= 0f || size.y <= 0f)
                return;

            Encapsulate(new NowRect(0f, 0f, size.x, size.y));
        }

        void Encapsulate(NowRect rect)
        {
            if (rect.isEmpty)
                return;

            _bounds = _hasBounds ? _bounds.Union(rect) : rect;
            _hasBounds = true;
        }

        Material GetMaterial()
        {
            if (_material != null)
                return _material;

            var template = Resources.Load<Material>("NowUI/SdfMaterial");

            if (template != null)
            {
                _material = new Material(template);
            }
            else
            {
                var shader = Shader.Find("NowUI/SDF Scene");

                if (shader == null)
                    return null;

                _material = new Material(shader);
            }

            _material.name = "Now SDF Scene";
            _material.hideFlags = HideFlags.HideAndDontSave;
            return _material;
        }

        void Upload(Material material)
        {
            int shapeCount = 0;
            int layerCount = Mathf.Min(_layers.Count, NowSdf.MaxLayers);

            for (int i = 0; i < layerCount; ++i)
            {
                var layer = _layers[i];
                int graphId = GetGraphId(layer.graph, ref shapeCount);
                int targetId = layer.targetGraph != null ? GetGraphId(layer.targetGraph, ref shapeCount) : -1;

                _layerData0[i] = new Vector4(
                    graphId,
                    i == 0 ? (float)NowSdfOperation.Union : (float)layer.operation,
                    i == 0 ? 0f : layer.smoothing,
                    (float)layer.kind);
                _layerData1[i] = new Vector4(targetId, layer.morph, 0f, 0f);
            }

            material.SetFloat(_shapeCountProp, shapeCount);
            material.SetFloat(_layerCountProp, layerCount);
            material.SetFloat(_featherProp, _feather);
            material.SetFloat(_canvasLayoutProp, 0f);
            material.SetTexture(_mainTexProp, _texture != null ? _texture : Texture2D.whiteTexture);
            material.SetVectorArray(_data0Prop, _data0);
            material.SetVectorArray(_data1Prop, _data1);
            material.SetVectorArray(_data2Prop, _data2);
            material.SetVectorArray(_shapeMetaProp, _shapeMeta);
            material.SetVectorArray(_colorsProp, _colors);
            material.SetVectorArray(_uvsProp, _uvs);
            material.SetVectorArray(_layerData0Prop, _layerData0);
            material.SetVectorArray(_layerData1Prop, _layerData1);
        }

        int GetGraphId(NowSdfGraph graph, ref int shapeCount)
        {
            if (_graphIds.TryGetValue(graph, out int graphId))
                return graphId;

            graphId = _graphIds.Count;
            _graphIds[graph] = graphId;
            AppendGraph(graph, graphId, ref shapeCount);
            return graphId;
        }

        void AppendGraph(NowSdfGraph graph, int graphId, ref int shapeCount)
        {
            var nodes = graph.nodes;
            _texture ??= graph.texture;

            for (int i = 0; i < nodes.Count && shapeCount < NowSdf.MaxShapes; ++i)
            {
                var node = nodes[i];
                _data0[shapeCount] = new Vector4((float)node.type, (float)node.operation, node.smoothing, 0f);
                _data1[shapeCount] = node.data1;
                _data2[shapeCount] = node.data2;
                _shapeMeta[shapeCount] = new Vector4(graphId, node.useTexture ? 1f : 0f, 0f, 0f);
                _colors[shapeCount] = node.color;
                _uvs[shapeCount] = node.uv;
                ++shapeCount;
            }
        }
    }
}
