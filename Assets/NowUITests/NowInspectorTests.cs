using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using NowUI;

#pragma warning disable 169, 414, 649

public class NowInspectorTests
{
    enum Quality
    {
        Low,
        Medium,
        High
    }

    [Flags]
    enum Channels
    {
        None = 0,
        A = 1 << 0,
        B = 1 << 1,
        C = 1 << 2
    }

    enum SignedByteEnum : sbyte
    {
        Negative = -1
    }

    [Serializable]
    class NestedSection
    {
        public float inner = 1f;
    }

    class SerializationRulesTarget
    {
        public int visiblePublic;
        [SerializeField] int visiblePrivate;
        [NonSerialized] public int hiddenNonSerialized;
        [HideInInspector] public int hiddenInInspector;
        public readonly int hiddenReadonly = 1;
        int hiddenPrivate;
        public static int hiddenStatic;
        public int HiddenAutoProperty { get; set; }
    }

    class RulesBase
    {
        public int baseField;
    }

    class RulesDerived : RulesBase
    {
        public int derivedField;
    }

    class RichTarget
    {
        [Header("Basics")]
        public bool flag = true;
        [UnityEngine.Range(0f, 10f)] public float ranged = 5f;
        public int count = 3;
        public double precise = 1.25;
        public long wide = 123456789L;
        public byte small = 4;
        public string name = "Ready";
        [TextArea(2, 3)] public string notes = "Line";

        [Space(8f)]
        public Quality quality = Quality.Medium;
        public Channels channels = Channels.A | Channels.C;
        public LayerMask layers = 1;

        public Color tint = Color.red;
        public Vector3 offset = Vector3.one;
        public Quaternion rotation = Quaternion.Euler(0f, 90f, 0f);
        public Rect viewport = new Rect(0f, 0f, 1f, 1f);
        public Bounds bounds = new Bounds(Vector3.zero, Vector3.one);
        public DateTime due = new DateTime(2026, 7, 2);
        public TimeSpan alarm = new TimeSpan(7, 30, 0);

        public NestedSection nested = new NestedSection();
        public List<int> numbers = new List<int> { 1, 2, 3 };
    }

    class NullFieldsTarget
    {
        public NestedSection nested;
        public List<int> numbers;
        public string text;
    }

    class BoolTarget
    {
        public bool flag;
    }

    struct Meters
    {
        public float value;
    }

    class DrawerTarget
    {
        public Meters distance;
    }

    sealed class FakePointer : INowInputProvider
    {
        public NowInputSnapshot snapshot;

        public bool TryGetSnapshot(NowInputSurface surface, out NowInputSnapshot result)
        {
            result = snapshot;
            return true;
        }
    }

    static readonly Vector2 Surface = new Vector2(512, 640);

    FakePointer _pointer;
    NowDrawList _drawList;

    [SetUp]
    public void SetUp()
    {
        NowInput.Reset();
        NowFocus.Reset();
        NowControlState.Reset();
        NowControls.Reset();
        NowOverlay.Reset();
        NowLayout.Reset();
        NowInspectorGui.ClearCachesForTests();

        _pointer = new FakePointer();
        _drawList = new NowDrawList();
    }

    [TearDown]
    public void TearDown()
    {
        _drawList.Dispose();
        NowInspectorGui.ClearCachesForTests();
        NowOverlay.Reset();
        NowInput.Reset();
        NowFocus.Reset();
        NowControlState.Reset();
        NowControls.Reset();
        NowLayout.Reset();
    }

    bool DrawInspectorFrame<T>(ref T target, Vector2 pointer = default, bool down = false, bool pressed = false, bool released = false)
    {
        _pointer.snapshot = new NowInputSnapshot(pointer, down, pressed, released);

        using (NowInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
        {
            bool changed;

            using (NowLayout.Area(new NowRect(0, 0, 480, 600)))
                changed = NowLayout.Inspector("inspector").Draw(ref target);

            NowOverlay.Flush();
            NowOverlay.ForceNewFrame();
            return changed;
        }
    }

    [Test]
    public void MembersFollowUnitySerializationRules()
    {
        var members = NowInspectorGui.GetMembers(typeof(SerializationRulesTarget));

        Assert.AreEqual(2, members.Length);
        Assert.AreEqual("visiblePublic", members[0].name);
        Assert.AreEqual("visiblePrivate", members[1].name);
    }

    [Test]
    public void BaseClassFieldsComeFirst()
    {
        var members = NowInspectorGui.GetMembers(typeof(RulesDerived));

        Assert.AreEqual(2, members.Length);
        Assert.AreEqual("baseField", members[0].name);
        Assert.AreEqual("derivedField", members[1].name);
    }

    [Test]
    public void NicifyNameFormatsLikeUnity()
    {
        Assert.AreEqual("Player Name", NowInspectorGui.NicifyName("m_PlayerName"));
        Assert.AreEqual("Health", NowInspectorGui.NicifyName("_health"));
        Assert.AreEqual("Max HP", NowInspectorGui.NicifyName("maxHP"));
        Assert.AreEqual("Player Name 2", NowInspectorGui.NicifyName("playerName2"));
        Assert.AreEqual("Constant", NowInspectorGui.NicifyName("kConstant"));
        Assert.AreEqual("Simple", NowInspectorGui.NicifyName("simple"));
    }

    [Test]
    public void DrawWithoutEditsReturnsFalse()
    {
        var target = new RichTarget();

        Assert.IsFalse(DrawInspectorFrame(ref target));
        Assert.IsFalse(DrawInspectorFrame(ref target));
    }

    [Test]
    public void StructTargetsRoundTripThroughDraw()
    {
        var target = new Meters { value = 3f };

        Assert.IsFalse(DrawInspectorFrame(ref target));
        Assert.AreEqual(3f, target.value);
    }

    [Test]
    public void NullNestedAndListFieldsAreAutoCreated()
    {
        var target = new NullFieldsTarget();

        Assert.IsTrue(DrawInspectorFrame(ref target), "Creating null nested/list fields reports a change.");
        Assert.IsNotNull(target.nested);
        Assert.IsNotNull(target.numbers);
        Assert.IsNull(target.text, "Null strings draw as empty without being written back.");
        Assert.IsFalse(DrawInspectorFrame(ref target));
    }

    [Test]
    public void CustomDrawerOverridesRowAndWritesBack()
    {
        var target = new DrawerTarget();
        NowInspector.SetDrawer<Meters>((ref Meters value) =>
        {
            value.value = 42f;
            return true;
        });

        try
        {
            Assert.IsTrue(DrawInspectorFrame(ref target));
            Assert.AreEqual(42f, target.distance.value);
        }
        finally
        {
            NowInspector.RemoveDrawer<Meters>();
        }
    }

    [Test]
    public void CustomDrawerAppliesAtTheRoot()
    {
        var target = new Meters { value = 1f };
        NowInspector.SetDrawer<Meters>((ref Meters value) =>
        {
            value.value = 7f;
            return true;
        });

        try
        {
            Assert.IsTrue(DrawInspectorFrame(ref target));
            Assert.AreEqual(7f, target.value);
        }
        finally
        {
            NowInspector.RemoveDrawer<Meters>();
        }
    }

    [Test]
    public void CheckboxRowTogglesBoolField()
    {
        var target = new BoolTarget();
        var pointer = new Vector2(152f, 10f);

        DrawInspectorFrame(ref target, pointer);
        DrawInspectorFrame(ref target, pointer, down: true, pressed: true);
        bool changed = DrawInspectorFrame(ref target, pointer, released: true);

        Assert.IsTrue(changed, "Releasing over the checkbox reports a change.");
        Assert.IsTrue(target.flag);
    }

    [Test]
    public void QuaternionRowsAreStableAcrossFrames()
    {
        var target = new RichTarget();

        DrawInspectorFrame(ref target);
        var before = target.rotation;

        Assert.IsFalse(DrawInspectorFrame(ref target), "Euler cache must not drift the quaternion.");
        Assert.AreEqual(before, target.rotation);
    }

    [Test]
    public void EnumBitsHandleNegativeUnderlyingValues()
    {
        ulong bits = NowInspectorGui.EnumBits(typeof(SignedByteEnum), SignedByteEnum.Negative);

        Assert.AreEqual(ulong.MaxValue, bits);
    }

    [Test]
    public void UnsupportedFieldTypesAreSkippedWithoutError()
    {
        var target = new UnsupportedTarget();

        Assert.IsFalse(DrawInspectorFrame(ref target));

        var members = NowInspectorGui.GetMembers(typeof(UnsupportedTarget));
        Assert.AreEqual(2, members.Length, "Unsupported members stay cached so custom drawers can pick them up.");
    }

    class UnsupportedTarget
    {
        public IntPtr pointer;
        public int supported;
    }
}

public class NowFoldoutTests
{
    sealed class FakePointer : INowInputProvider
    {
        public NowInputSnapshot snapshot;

        public bool TryGetSnapshot(NowInputSurface surface, out NowInputSnapshot result)
        {
            result = snapshot;
            return true;
        }
    }

    static readonly Vector2 Surface = new Vector2(512, 256);
    static readonly NowRect HeaderRect = new NowRect(20, 20, 200, 26);

    FakePointer _pointer;
    NowDrawList _drawList;

    [SetUp]
    public void SetUp()
    {
        NowInput.Reset();
        NowFocus.Reset();
        NowControlState.Reset();
        NowControls.Reset();
        NowOverlay.Reset();
        NowLayout.Reset();

        _pointer = new FakePointer();
        _drawList = new NowDrawList();
    }

    [TearDown]
    public void TearDown()
    {
        _drawList.Dispose();
        NowOverlay.Reset();
        NowInput.Reset();
        NowFocus.Reset();
        NowControlState.Reset();
        NowControls.Reset();
        NowLayout.Reset();
    }

    bool DrawFrame(ref bool expanded, Vector2 pointer, bool down, bool pressed, bool released)
    {
        _pointer.snapshot = new NowInputSnapshot(pointer, down, pressed, released);

        using (NowInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
            return Now.Foldout(HeaderRect, "Header").SetId("foldout").Draw(ref expanded);
    }

    [Test]
    public void ClickTogglesCallerOwnedExpansion()
    {
        bool expanded = false;
        var center = HeaderRect.center;

        DrawFrame(ref expanded, center, false, false, false);
        DrawFrame(ref expanded, center, true, true, false);
        bool toggled = DrawFrame(ref expanded, center, false, false, true);

        Assert.IsTrue(toggled);
        Assert.IsTrue(expanded);

        DrawFrame(ref expanded, center, true, true, false);
        toggled = DrawFrame(ref expanded, center, false, false, true);

        Assert.IsTrue(toggled);
        Assert.IsFalse(expanded);
    }
}

public class NowMaskFieldTests
{
    sealed class FakePointer : INowInputProvider
    {
        public NowInputSnapshot snapshot;

        public bool TryGetSnapshot(NowInputSurface surface, out NowInputSnapshot result)
        {
            result = snapshot;
            return true;
        }
    }

    static readonly Vector2 Surface = new Vector2(512, 420);
    static readonly NowRect FieldRect = new NowRect(20, 20, 180, 30);
    static readonly string[] Options = { "Low", "Medium", "High" };

    FakePointer _pointer;
    NowDrawList _drawList;

    [SetUp]
    public void SetUp()
    {
        NowInput.Reset();
        NowFocus.Reset();
        NowControlState.Reset();
        NowControls.Reset();
        NowOverlay.Reset();
        NowLayout.Reset();

        _pointer = new FakePointer();
        _drawList = new NowDrawList();
    }

    [TearDown]
    public void TearDown()
    {
        _drawList.Dispose();
        NowOverlay.Reset();
        NowInput.Reset();
        NowFocus.Reset();
        NowControlState.Reset();
        NowControls.Reset();
        NowLayout.Reset();
    }

    bool DrawFrame(ref int mask, Vector2 pointer, bool down, bool pressed, bool released)
    {
        _pointer.snapshot = new NowInputSnapshot(pointer, down, pressed, released);

        using (NowInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
        {
            bool changed = Now.MaskField(FieldRect, Options)
                .SetId("mask")
                .SetFitToView(false)
                .Draw(ref mask);
            NowOverlay.Flush();
            NowOverlay.ForceNewFrame();
            return changed;
        }
    }

    [Test]
    public void AllBitsCoversOptionCounts()
    {
        Assert.AreEqual(7, NowMaskField.AllBits(3));
        Assert.AreEqual(-1, NowMaskField.AllBits(32));
        Assert.AreEqual(0, NowMaskField.AllBits(0));
    }

    [Test]
    public void ClickingEverythingSetsAllOptionBits()
    {
        int mask = 1;
        var fieldCenter = FieldRect.center;

        DrawFrame(ref mask, fieldCenter, true, true, false);
        DrawFrame(ref mask, fieldCenter, false, false, true);

        int id;

        using (NowInput.Begin(_pointer, Surface))
            id = NowControls.GetControlId("mask");

        Assert.IsTrue(NowControlState.Get<bool>(id), "Click must open the mask popup.");

        var styles = NowTheme.themeAsset.controlStyles;
        float popupTop = FieldRect.yMax + styles.dropdownPopupGap;
        float itemTop = popupTop + styles.popupPadding;
        var everythingCenter = new Vector2(
            FieldRect.x + FieldRect.width * 0.5f,
            itemTop + styles.dropdownItemHeight * 1.5f);

        DrawFrame(ref mask, everythingCenter, true, true, false);
        DrawFrame(ref mask, everythingCenter, false, false, true);
        bool changed = DrawFrame(ref mask, everythingCenter, false, false, false);

        Assert.IsTrue(changed, "Pending mask applies on the next draw.");
        Assert.AreEqual(7, mask);
        Assert.IsTrue(NowControlState.Get<bool>(id), "Toggling keeps the popup open.");
    }
}

public class NowWideNumericFieldTests
{
    sealed class FakePointer : INowInputProvider
    {
        public NowInputSnapshot snapshot;

        public bool TryGetSnapshot(NowInputSurface surface, out NowInputSnapshot result)
        {
            result = snapshot;
            return true;
        }
    }

    static readonly Vector2 Surface = new Vector2(512, 256);
    static readonly NowRect FieldRect = new NowRect(20, 20, 180, 30);

    FakePointer _pointer;
    NowDrawList _drawList;

    [SetUp]
    public void SetUp()
    {
        NowInput.Reset();
        NowFocus.Reset();
        NowControlState.Reset();
        NowControls.Reset();
        NowOverlay.Reset();
        NowLayout.Reset();

        _pointer = new FakePointer();
        _drawList = new NowDrawList();
    }

    [TearDown]
    public void TearDown()
    {
        _drawList.Dispose();
        NowOverlay.Reset();
        NowInput.Reset();
        NowFocus.Reset();
        NowControlState.Reset();
        NowControls.Reset();
        NowLayout.Reset();
    }

    [Test]
    public void DoubleFieldClampsToRange()
    {
        double value = 50.0;
        bool changed;

        using (NowInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
            changed = Now.TextField(FieldRect, "double").SetRange(0f, 10f).Draw(ref value);

        Assert.IsTrue(changed);
        Assert.AreEqual(10.0, value);
    }

    [Test]
    public void DoubleFieldWithoutEditsReturnsFalse()
    {
        double value = 1.25;
        bool changed;

        using (NowInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
            changed = Now.TextField(FieldRect, "double").Draw(ref value);

        Assert.IsFalse(changed);
        Assert.AreEqual(1.25, value);
    }

    [Test]
    public void LongFieldClampsToRange()
    {
        long value = 50L;
        bool changed;

        using (NowInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
            changed = Now.TextField(FieldRect, "long").SetRange(0, 10).Draw(ref value);

        Assert.IsTrue(changed);
        Assert.AreEqual(10L, value);
    }

    [Test]
    public void RectVectorFieldRoundTripsWithoutEdits()
    {
        var value = new Rect(1f, 2f, 3f, 4f);
        bool changed;

        using (NowInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
            changed = Now.VectorField(FieldRect, "rect").Draw(ref value);

        Assert.IsFalse(changed);
        Assert.AreEqual(new Rect(1f, 2f, 3f, 4f), value);
    }

    [Test]
    public void RectIntVectorFieldRoundTripsWithoutEdits()
    {
        var value = new RectInt(1, 2, 3, 4);
        bool changed;

        using (NowInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
            changed = Now.VectorField(FieldRect, "rect-int").Draw(ref value);

        Assert.IsFalse(changed);
        Assert.AreEqual(new RectInt(1, 2, 3, 4), value);
    }
}

#pragma warning restore 169, 414, 649
