#if NOWUI_UGUI
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using NowUI;
using Object = UnityEngine.Object;

/// <summary>
/// NowCapture: what a host drew, per completed frame. These tests drive the
/// UGUI NowGraphic family directly through Graphic.Rebuild, the same
/// CanvasUpdate.PreRender step Unity runs before rendering a canvas.
/// </summary>
public class NowCaptureTests
{
    const string Password = "hunter2-secret";

    sealed class CaptureGraphic : NowGraphic
    {
        public Action<NowRect> draw;

        public INowInputProvider provider;

        public int drawCount;

        protected override void DrawNowUI(NowRect rect)
        {
            ++drawCount;
            draw?.Invoke(rect);
        }

        protected override INowInputProvider GetInputProvider()
        {
            return provider ?? base.GetInputProvider();
        }
    }

    sealed class CaptureLayoutGraphic : NowLayoutGraphic
    {
        public Action<NowRect> draw;

        public int drawCount;

        protected override void DrawNowUI(NowRect rect)
        {
            ++drawCount;
            draw?.Invoke(rect);
        }
    }

    /// <summary>A deep copy of one published frame (frames are only valid in the callback).</summary>
    sealed class CapturedFrame
    {
        public object host;
        public NowRect bounds;
        public readonly List<NowCaptureItem> items = new List<NowCaptureItem>();
    }

    sealed class Recorder : IDisposable
    {
        public readonly List<CapturedFrame> frames = new List<CapturedFrame>();

        readonly Action<NowCaptureFrame> _handler;

        public Recorder()
        {
            _handler = OnFrame;
            NowCapture.onFrame += _handler;
        }

        void OnFrame(NowCaptureFrame frame)
        {
            var copy = new CapturedFrame { host = frame.host, bounds = frame.bounds };

            for (int i = 0; i < frame.count; ++i)
                copy.items.Add(frame[i]);

            frames.Add(copy);
        }

        public void Dispose()
        {
            NowCapture.onFrame -= _handler;
        }
    }

    readonly List<GameObject> _objects = new List<GameObject>();

    Texture2D _texture;

    string _password;

    [SetUp]
    public void SetUp()
    {
        Assert.NotNull(Resources.Load<Material>("NowUI/UIMaterial"));
        Assert.IsFalse(NowCapture.active, "a previous test leaked a NowCapture handler");
        _texture = new Texture2D(8, 8, TextureFormat.RGBA32, false) { name = "capture-avatar" };
        _password = Password;
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = _objects.Count - 1; i >= 0; --i)
        {
            if (_objects[i] != null)
                Object.DestroyImmediate(_objects[i]);
        }

        _objects.Clear();

        if (_texture != null)
            Object.DestroyImmediate(_texture);

        NowFocus.Clear();
        NowInput.Reset();
        NowControls.Reset();
        NowLayout.Reset();
    }

    T CreateGraphic<T>(Vector2 size) where T : Graphic
    {
        var go = new GameObject("Now Capture Graphic", typeof(RectTransform), typeof(CanvasRenderer));
        _objects.Add(go);
        var rectTransform = go.GetComponent<RectTransform>();
        rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x);
        rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y);
        var graphic = go.AddComponent<T>();
        graphic.raycastTarget = false;
        return graphic;
    }

    /// <summary>Edit-mode hosts draw passively (no input, no overlays) unless interaction is enabled.</summary>
    static void EnableInteraction(CaptureGraphic graphic, INowInputProvider provider)
    {
        typeof(NowGraphic)
            .GetField("_editModeInteraction", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(graphic, true);
        graphic.provider = provider;
    }

    static void Rebuild(Graphic graphic)
    {
        graphic.SetVerticesDirty();
        graphic.Rebuild(CanvasUpdate.PreRender);
    }

    void DrawLoginPanel(NowRect rect)
    {
        Now.Rectangle(rect).SetColor(new Color(0.1f, 0.1f, 0.12f, 1f)).Draw();
        Now.Button(new NowRect(10f, 10f, 120f, 32f), "Play").Draw();
        Now.Text(new NowRect(10f, 50f, 200f, 24f)).SetColor(Color.white).Draw("Score: 10");
        Now.Rectangle(new NowRect(10f, 80f, 32f, 32f)).SetTexture(_texture).Draw();
        Now.TextField(new NowRect(10f, 120f, 200f, 30f), "password").SetPassword().Draw(ref _password);
    }

    static int FindControl(CapturedFrame frame, string control)
    {
        for (int i = 0; i < frame.items.Count; ++i)
        {
            if (frame.items[i].kind == NowCaptureKind.Control && frame.items[i].control == control)
                return i;
        }

        return -1;
    }

    static int FindText(CapturedFrame frame, string text)
    {
        for (int i = 0; i < frame.items.Count; ++i)
        {
            if (frame.items[i].kind == NowCaptureKind.Text && frame.items[i].text == text)
                return i;
        }

        return -1;
    }

    static bool IsInside(CapturedFrame frame, int index, int ancestor)
    {
        for (int parent = frame.items[index].parent; parent >= 0; parent = frame.items[parent].parent)
        {
            if (parent == ancestor)
                return true;
        }

        return false;
    }

    [Test]
    public void UGUIHostReportsControlsPrimitivesParentsAndStableIds()
    {
        var graphic = CreateGraphic<CaptureGraphic>(new Vector2(320f, 200f));
        graphic.draw = DrawLoginPanel;

        using var recorder = new Recorder();
        Rebuild(graphic);
        Rebuild(graphic);

        Assert.AreEqual(2, recorder.frames.Count, "one frame per completed rebuild");
        var frame = recorder.frames[0];
        Assert.AreSame(graphic, frame.host);
        Assert.AreEqual(new NowRect(0f, 0f, 320f, 200f), frame.bounds);

        // Host background: a root Rect covering the bounds.
        Assert.AreEqual(NowCaptureKind.Rect, frame.items[0].kind);
        Assert.AreEqual(-1, frame.items[0].parent);
        Assert.AreEqual(0f, frame.items[0].rect.x, 0.01f);
        Assert.AreEqual(320f, frame.items[0].rect.width, 0.01f);

        int button = FindControl(frame, "Button");
        Assert.GreaterOrEqual(button, 0, "the button is reported as a Control");
        Assert.AreEqual("Play", frame.items[button].text);
        Assert.AreEqual(-1, frame.items[button].parent);
        Assert.AreEqual(new NowRect(10f, 10f, 120f, 32f), frame.items[button].rect);
        Assert.Greater(frame.items[button].color.a, 0f, "a control reports its background colour");

        int buttonLabel = FindText(frame, "Play");
        Assert.GreaterOrEqual(buttonLabel, 0);
        Assert.AreEqual(button, frame.items[buttonLabel].parent, "the button label is drawn inside the button");
        Assert.Greater(frame.items[buttonLabel].fontSize, 0f);

        int label = FindText(frame, "Score: 10");
        Assert.GreaterOrEqual(label, 0, "free text is reported as Text");
        Assert.AreEqual(-1, frame.items[label].parent);
        Assert.AreEqual(Color.white, frame.items[label].color);

        int image = frame.items.FindIndex(item => item.kind == NowCaptureKind.Image);
        Assert.GreaterOrEqual(image, 0, "the texture is reported as an Image");
        Assert.AreEqual("capture-avatar", frame.items[image].texture);
        Assert.AreEqual(-1, frame.items[image].parent);
        Assert.AreEqual(new NowRect(10f, 80f, 32f, 32f), frame.items[image].rect);

        int field = FindControl(frame, "TextField");
        Assert.GreaterOrEqual(field, 0, "the password field is reported as a Control");
        Assert.AreEqual(NowCaptureFlags.Editable | NowCaptureFlags.Secret,
            frame.items[field].flags & (NowCaptureFlags.Editable | NowCaptureFlags.Secret));
        Assert.IsNull(frame.items[field].text, "a secret field never reports its text");

        bool maskedTextDrawn = false;

        for (int i = 0; i < frame.items.Count; ++i)
        {
            var item = frame.items[i];
            Assert.AreNotEqual(Password, item.text, "the password must not appear in any item");

            if (item.text != null)
                StringAssert.DoesNotContain("hunter", item.text);

            if (IsInside(frame, i, field))
            {
                Assert.IsNull(item.text, "items inside a secret field carry no text");
                Assert.IsTrue((item.flags & NowCaptureFlags.Secret) != 0);
                maskedTextDrawn |= item.kind == NowCaptureKind.Text;
            }

            Assert.IsTrue(item.parent < i, "parents precede their children in draw order");
        }

        Assert.IsTrue(maskedTextDrawn, "the masked password run is still reported, without text");

        // Ids: unique within a frame and stable across frames.
        var ids = new HashSet<ulong>();

        foreach (var item in frame.items)
            Assert.IsTrue(ids.Add(item.id), "ids are unique within a frame");

        var second = recorder.frames[1];
        Assert.AreEqual(frame.items.Count, second.items.Count);

        for (int i = 0; i < frame.items.Count; ++i)
        {
            Assert.AreEqual(frame.items[i].id, second.items[i].id, $"item {i} keeps its id");
            Assert.AreEqual(frame.items[i].kind, second.items[i].kind);
            Assert.AreEqual(frame.items[i].parent, second.items[i].parent);
        }

        Assert.AreSame(frame.items[label].text, second.items[label].text, "unchanged text keeps its string instance");
    }

    [Test]
    public void ControlsReportHoverPressAndFocusFlags()
    {
        var provider = new NowInputReplay();
        var graphic = CreateGraphic<CaptureGraphic>(new Vector2(320f, 200f));
        EnableInteraction(graphic, provider);
        graphic.draw = DrawLoginPanel;

        using var recorder = new Recorder();

        provider.Move(new Vector2(40f, 20f));
        Rebuild(graphic);
        var hovered = recorder.frames[^1];
        int button = FindControl(hovered, "Button");
        Assert.IsTrue((hovered.items[button].flags & NowCaptureFlags.Hovered) != 0, "the pointer is over the button");
        Assert.IsFalse((hovered.items[FindControl(hovered, "TextField")].flags & NowCaptureFlags.Hovered) != 0);

        provider.Press(new Vector2(40f, 135f));
        Rebuild(graphic);
        var pressed = recorder.frames[^1];
        int field = FindControl(pressed, "TextField");
        Assert.IsTrue((pressed.items[field].flags & NowCaptureFlags.Pressed) != 0, "the field is pressed");
        Assert.IsTrue((pressed.items[field].flags & NowCaptureFlags.Focused) != 0, "pressing focuses the field");
        Assert.IsFalse((pressed.items[FindControl(pressed, "Button")].flags & NowCaptureFlags.Hovered) != 0);
        Assert.AreEqual(hovered.items[button].id, pressed.items[FindControl(pressed, "Button")].id,
            "state changes do not change control ids");
    }

    static void Card(NowRect rect, string key, int extraPrimitives)
    {
        var interaction = NowControls.Interact(new NowId(key), rect, out _, out _);
        NowCapture.Describe(interaction.id, "Card");

        for (int i = 0; i < extraPrimitives; ++i)
            Now.Rectangle(new NowRect(rect.x + 2f, rect.y + 2f, 4f, 4f)).SetColor(Color.red).Draw();

        Now.Rectangle(rect).SetColor(Color.gray).Draw();
        Now.Rectangle(rect.Inset(4f)).SetColor(Color.white).Draw();
    }

    [Test]
    public void InsertingAPrimitiveRenumbersOnlyItsSiblings()
    {
        int extra = 0;
        var graphic = CreateGraphic<CaptureGraphic>(new Vector2(320f, 200f));
        graphic.draw = _ =>
        {
            Now.Rectangle(new NowRect(0f, 0f, 320f, 4f)).SetColor(Color.black).Draw();
            Card(new NowRect(10f, 10f, 100f, 60f), "card-a", extra);
            Card(new NowRect(150f, 10f, 100f, 60f), "card-b", 0);
            Now.Rectangle(new NowRect(0f, 190f, 320f, 4f)).SetColor(Color.black).Draw();
        };

        using var recorder = new Recorder();
        Rebuild(graphic);
        extra = 1;
        Rebuild(graphic);

        var before = recorder.frames[0];
        var after = recorder.frames[1];
        Assert.AreEqual(before.items.Count + 1, after.items.Count);

        int cardA = FindControl(before, "Card");
        int cardB = before.items.FindIndex(cardA + 1, item => item.kind == NowCaptureKind.Control);
        Assert.GreaterOrEqual(cardB, 0);
        Assert.AreEqual(-1, before.items[cardB].parent, "the second card is a sibling, not a child");

        var beforeIds = new HashSet<ulong>();
        var afterById = new Dictionary<ulong, NowCaptureItem>();

        foreach (var item in after.items)
            afterById.Add(item.id, item);

        for (int i = 0; i < before.items.Count; ++i)
        {
            var item = before.items[i];
            beforeIds.Add(item.id);
            bool insideCardA = item.parent == cardA;

            if (insideCardA)
                continue;

            Assert.IsTrue(afterById.ContainsKey(item.id),
                $"item {i} ({item.kind}, parent {item.parent}) outside the edited card keeps its id");
        }

        Assert.AreEqual(before.items[cardA].id, after.items[FindControl(after, "Card")].id,
            "the edited card itself keeps its id");
        int changed = 0;

        foreach (var item in after.items)
        {
            if (!beforeIds.Contains(item.id))
                ++changed;
        }

        Assert.AreEqual(1, changed, "only the edited card's renumbered/added primitive is new");
    }

    [Test]
    public void NothingIsCollectedWithoutAHandler()
    {
        bool collectedDuringDraw = false;
        var graphic = CreateGraphic<CaptureGraphic>(new Vector2(320f, 200f));
        graphic.draw = rect =>
        {
            DrawLoginPanel(rect);
            collectedDuringDraw |= NowCaptureRecorder.collecting;
        };

        Assert.IsFalse(NowCapture.active);
        Rebuild(graphic);

        Assert.AreEqual(1, graphic.drawCount);
        Assert.IsFalse(collectedDuringDraw, "hosts collect nothing while no handler is attached");
        Assert.IsFalse(NowCaptureRecorder.hasFramesInFlight);

        // Attaching and detaching restores the idle state.
        using (new Recorder())
        {
            Assert.IsTrue(NowCapture.active);
            Rebuild(graphic);
            Assert.IsTrue(collectedDuringDraw);
        }

        collectedDuringDraw = false;
        Assert.IsFalse(NowCapture.active);
        Rebuild(graphic);
        Assert.IsFalse(collectedDuringDraw);
        Assert.IsFalse(NowCaptureRecorder.hasFramesInFlight);
    }

    float _sliderValue = 0.25f;

    bool _toggleValue = true;

    // A bare draw list has no host frame boundary to reset call-site
    // occurrences, so the controls use explicit ids to stay the same controls
    // from call to call.
    void DrawSteadyFrame()
    {
        Now.Rectangle(new NowRect(0f, 0f, 400f, 300f)).SetColor(Color.gray).Draw();
        Now.Button(new NowRect(10f, 10f, 120f, 32f), "Play").SetId("play").Draw();
        Now.Checkbox(new NowRect(10f, 50f, 160f, 24f), "Sound").SetId("sound").Draw(ref _toggleValue);
        Now.Slider(new NowRect(10f, 80f, 160f, 24f), 0f, 1f).SetId("volume").Draw(ref _sliderValue);
        Now.Text(new NowRect(10f, 110f, 200f, 24f)).Draw("Score: 10");
        Now.Text(new NowRect(10f, 140f, 200f, 24f)).Draw(1234);
        Now.Rectangle(new NowRect(10f, 170f, 32f, 32f)).SetTexture(_texture).Draw();
        Now.Circle(new Vector2(80f, 190f), 10f).SetColor(Color.cyan).Draw();
        Now.Line(new Vector2(100f, 190f), new Vector2(180f, 210f)).SetColor(Color.white).Draw();
    }

    static void AssertNoAllocAfterWarmup(Action draw, string message)
    {
        draw();
        draw();
        draw();

        using var allocations = new NowBenchmarkAllocations(reportAvailability: false);
        allocations.RequireAvailable();
        allocations.Begin();
        draw();
        long allocated = allocations.End();

        allocations.AssertZero(allocated, message);
    }

    [Test]
    public void CaptureOffDrawingDoesNotAllocate()
    {
        var drawList = new NowDrawList();

        try
        {
            void DrawFrame()
            {
                using (drawList.Begin(new Vector2(400f, 300f)))
                    DrawSteadyFrame();
            }

            Assert.IsFalse(NowCapture.active);
            AssertNoAllocAfterWarmup(DrawFrame, "capture hooks must not allocate while no handler is attached");
        }
        finally
        {
            drawList.Dispose();
        }
    }

    [Test]
    public void CaptureOnSteadyStateReusesBuffersAndStrings()
    {
        var drawList = new NowDrawList();
        var host = new object();
        int frames = 0;
        int items = 0;
        Action<NowCaptureFrame> handler = frame =>
        {
            ++frames;
            items = frame.count;

            for (int i = 0; i < frame.count; ++i)
                _ = frame[i].id;
        };

        NowCapture.onFrame += handler;

        try
        {
            void DrawFrame()
            {
                using (drawList.Begin(new Vector2(400f, 300f)))
                {
                    int token = NowCaptureRecorder.Begin(host, new NowRect(0f, 0f, 400f, 300f));
                    DrawSteadyFrame();
                    NowCaptureRecorder.End(token, publish: true);
                }
            }

            AssertNoAllocAfterWarmup(DrawFrame, "a steady captured frame must reuse its buffers and strings");
            Assert.AreEqual(4, frames);
            Assert.Greater(items, 8);
        }
        finally
        {
            NowCapture.onFrame -= handler;
            drawList.Dispose();
        }
    }

    [Test]
    public void MeasurePassesDoNotRaiseFrames()
    {
        var layout = CreateGraphic<CaptureLayoutGraphic>(new Vector2(320f, 200f));
        layout.draw = rect =>
        {
            using (NowLayout.Area(rect))
            using (NowLayout.VerticalScope(spacing: 4f))
            {
                NowLayout.Button("Play").Draw();
                NowLayout.Label("Score").Draw();
                NowLayout.FlexibleSpace();
                NowLayout.Button("Quit").Draw();
            }
        };

        using var recorder = new Recorder();
        Rebuild(layout);

        Assert.AreEqual(2, layout.drawCount, "the layout host runs a measure pass and a draw pass");
        Assert.AreEqual(1, recorder.frames.Count, "only the displayed draw pass raises onFrame");

        var frame = recorder.frames[0];
        int buttons = 0;

        foreach (var item in frame.items)
        {
            if (item.kind == NowCaptureKind.Control && item.control == "Button")
                ++buttons;
        }

        Assert.AreEqual(2, buttons, "the measure pass adds no duplicate items");
        Assert.GreaterOrEqual(FindText(frame, "Score"), 0);

        // Layout-input measurement (driveLayoutSize) is measure-only too.
        var explicitGraphic = CreateGraphic<CaptureGraphic>(new Vector2(120f, 40f));
        explicitGraphic.draw = DrawLoginPanel;
        explicitGraphic.driveLayoutSize = true;
        int before = recorder.frames.Count;
        explicitGraphic.CalculateLayoutInputHorizontal();
        explicitGraphic.CalculateLayoutInputVertical();
        Assert.Greater(explicitGraphic.drawCount, 0, "the layout query ran the content");
        Assert.AreEqual(before, recorder.frames.Count, "layout-input measurement does not raise onFrame");
    }

    [Test]
    public void RotationAndOpacityApplyToCapturedItems()
    {
        var graphic = CreateGraphic<CaptureGraphic>(new Vector2(200f, 200f));
        graphic.draw = _ =>
        {
            using (Now.Opacity(0.5f))
            using (Now.Rotate(90f, new Vector2(100f, 100f)))
                Now.Rectangle(new NowRect(140f, 90f, 40f, 20f)).SetColor(Color.white).Draw();

            using (Now.Opacity(0f))
                Now.Rectangle(new NowRect(0f, 0f, 20f, 20f)).SetColor(Color.white).Draw();

            Now.Rectangle(new NowRect(500f, 500f, 20f, 20f)).SetColor(Color.white).Draw();
        };

        using var recorder = new Recorder();
        Rebuild(graphic);

        var frame = recorder.frames[0];
        Assert.AreEqual(1, frame.items.Count, "transparent and clipped-out items are skipped");
        var item = frame.items[0];
        Assert.AreEqual(90f, item.rotation, 0.01f);
        Assert.AreEqual(100f, item.rect.center.x, 0.01f, "the centre turns clockwise about the pivot");
        Assert.AreEqual(160f, item.rect.center.y, 0.01f);
        Assert.AreEqual(40f, item.rect.width, 0.01f, "the rect keeps its unrotated size");
        Assert.AreEqual(0.5f, item.color.a, 0.001f);
    }

    [Test]
    public void DeferredOverlaysAreReportedAtTheRootOfTheirHost()
    {
        var graphic = CreateGraphic<CaptureGraphic>(new Vector2(300f, 200f));
        var provider = new NowInputReplay();
        provider.Idle();
        EnableInteraction(graphic, provider);
        graphic.draw = bounds =>
        {
            var interaction = NowControls.Interact(new NowId("anchor"), new NowRect(0f, 0f, 300f, 200f), out _, out _);
            NowCapture.Describe(interaction.id, "Panel");
            NowOverlay.Defer(new NowRect(100f, 50f, 80f, 40f), () =>
                Now.Rectangle(new NowRect(100f, 50f, 80f, 40f)).SetColor(Color.yellow).Draw());
            Now.Rectangle(new NowRect(10f, 10f, 20f, 20f)).SetColor(Color.red).Draw();
        };

        using var recorder = new Recorder();
        Rebuild(graphic);

        var frame = recorder.frames[0];
        int panel = FindControl(frame, "Panel");
        int popup = frame.items.FindIndex(item => item.color == Color.yellow);
        int content = frame.items.FindIndex(item => item.color == Color.red);
        Assert.AreEqual(panel, frame.items[content].parent);
        Assert.Greater(popup, content, "overlays paint after the content that queued them");
        Assert.AreEqual(-1, frame.items[popup].parent, "an overlay is not a child of the control open when it flushed");
    }

    [Test]
    public void PipelineGraphicsRouteOverlaysToTheirOwnFrames()
    {
        var cameraObject = new GameObject("Now Capture Camera", typeof(Camera));
        _objects.Add(cameraObject);
        var camera = cameraObject.GetComponent<Camera>();
        var target = new RenderTexture(320, 240, 0);
        camera.targetTexture = target;

        var firstObject = new GameObject("Now Capture Pipeline A");
        var secondObject = new GameObject("Now Capture Pipeline B");
        _objects.Add(firstObject);
        _objects.Add(secondObject);
        var first = firstObject.AddComponent<NowPipelineGraphic>();
        var second = secondObject.AddComponent<NowPipelineGraphic>();
        first.targetCamera = camera;
        second.targetCamera = camera;
        first.order = 0;
        second.order = 1;
        first.rebuildNowUI += (_, _, _) =>
        {
            Now.Rectangle(new NowRect(0f, 0f, 20f, 20f)).SetColor(Color.red).Draw();
            NowOverlay.Defer(new NowRect(40f, 40f, 20f, 20f), () =>
                Now.Rectangle(new NowRect(40f, 40f, 20f, 20f)).SetColor(Color.yellow).Draw());
        };
        second.rebuildNowUI += (_, _, _) =>
            Now.Rectangle(new NowRect(100f, 0f, 20f, 20f)).SetColor(Color.blue).Draw();

        var drawList = new NowDrawList();

        try
        {
            using var recorder = new Recorder();
            NowPipelineGraphic.BuildDrawList(camera, drawList);

            Assert.AreEqual(2, recorder.frames.Count, "one frame per pipeline graphic");
            var firstFrame = recorder.frames.Find(frame => ReferenceEquals(frame.host, first));
            var secondFrame = recorder.frames.Find(frame => ReferenceEquals(frame.host, second));
            Assert.NotNull(firstFrame);
            Assert.NotNull(secondFrame);
            Assert.AreEqual(2, firstFrame.items.Count, "the first graphic keeps its content and its overlay");
            Assert.AreEqual(Color.yellow, firstFrame.items[1].color);
            Assert.AreEqual(1, secondFrame.items.Count);
            Assert.AreEqual(Color.blue, secondFrame.items[0].color);
            Assert.IsFalse(NowCaptureRecorder.hasFramesInFlight);
        }
        finally
        {
            drawList.Dispose();
            camera.targetTexture = null;
            Object.DestroyImmediate(target);
        }
    }

    [Test]
    public void ExceptionsAndClearedHostsDoNotPublishPartialFrames()
    {
        var graphic = CreateGraphic<CaptureGraphic>(new Vector2(200f, 100f));
        graphic.draw = _ =>
        {
            Now.Rectangle(new NowRect(0f, 0f, 20f, 20f)).SetColor(Color.white).Draw();
            throw new InvalidOperationException("capture test failure");
        };

        using var recorder = new Recorder();
        UnityEngine.TestTools.LogAssert.Expect(LogType.Exception, new System.Text.RegularExpressions.Regex("capture test failure"));
        Rebuild(graphic);
        Assert.AreEqual(0, recorder.frames.Count, "a failed rebuild publishes nothing");
        Assert.IsFalse(NowCaptureRecorder.hasFramesInFlight);

        graphic.draw = null;
        graphic.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 0f);
        Rebuild(graphic);
        Assert.AreEqual(1, recorder.frames.Count, "a cleared host reports an empty frame");
        Assert.AreEqual(0, recorder.frames[0].items.Count);
    }
}
#endif
