using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NowUI;
using NowUI.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class NowEditorGUIHostTests
{
    const BindingFlags InstanceNonPublic =
        BindingFlags.Instance | BindingFlags.NonPublic;

    const BindingFlags StaticNonPublic =
        BindingFlags.Static | BindingFlags.NonPublic;

    NowEditorGUIHostTestWindow _owner;

    NowEditorGUIHostTestWindow _other;

    [SetUp]
    public void SetUp()
    {
        NowEditorGUI.DisposeAll();
    }

    [TearDown]
    public void TearDown()
    {
        NowEditorGUI.DisposeAll();
        CloseWindow(ref _other);
        CloseWindow(ref _owner);
    }

    [Test]
    public void HostViewActualViewResolvesToOwningEditorWindow()
    {
        _owner = CreateWindow("NowUI HostView owner", show: true);

        FieldInfo parentField = typeof(EditorWindow).GetField(
            "m_Parent",
            InstanceNonPublic);
        Assert.NotNull(
            parentField,
            "Unity's EditorWindow host field changed; update the GUIView compatibility bridge.");

        object hostView = parentField.GetValue(_owner);
        Assert.NotNull(hostView, "Showing an EditorWindow must attach it to a HostView.");

        TypeInfo hostViewType = typeof(EditorWindow).Assembly
            .GetType("UnityEditor.HostView")
            ?.GetTypeInfo();
        Assert.NotNull(
            hostViewType,
            "Unity's HostView type changed; update the GUIView compatibility bridge.");
        Assert.IsTrue(hostViewType.IsInstanceOfType(hostView));

        Assert.AreSame(
            _owner,
            NowEditorGUI.ResolveEditorWindowFromGUIView(hostView),
            "HostView.actualView must resolve to the EditorWindow that owns the active NowUI panel.");
    }

    [Test]
    public void ProviderRepaintTargetsRegisteredOwnerWhenAnotherWindowIsFocused()
    {
        _owner = CreateWindow("NowUI repaint owner", show: false);
        _other = CreateWindow("NowUI focused distractor", show: true);
        _other.Focus();

        Assert.AreSame(
            _other,
            EditorWindow.focusedWindow,
            "The fixture requires a different focused window so the public fallback cannot select the owner.");
        Assert.AreNotSame(
            _owner,
            EditorWindow.mouseOverWindow,
            "An unshown owner cannot be Unity's mouse-over window.");
        Assert.AreNotSame(
            _owner,
            EditorWindow.mouseOverWindow ?? EditorWindow.focusedWindow,
            "The public mouse-over/focus fallback must point somewhere other than the registered owner.");

        object hostContext = new object();
        MethodInfo trackHost = typeof(NowEditorGUI).GetMethod(
            "TrackHost",
            StaticNonPublic);
        Assert.NotNull(trackHost);
        trackHost.Invoke(null, new object[] { hostContext, _owner });

        var provider = new NowIMGUIInputProvider(17001, hostContext);
        provider.RequestHostRepaint(markGUIChanged: false);

        FieldInfo pendingField = typeof(NowEditorGUI).GetField(
            "PendingRepaints",
            StaticNonPublic);
        Assert.NotNull(pendingField);

        var pending = pendingField.GetValue(null) as HashSet<EditorWindow>;
        Assert.NotNull(pending);
        Assert.AreEqual(1, pending.Count);
        Assert.IsTrue(
            pending.Contains(_owner),
            "A provider repaint must be queued for its registered owning EditorWindow.");
        Assert.IsFalse(
            pending.Contains(_other),
            "Focus or mouse-over in another editor window must not steal the provider's repaint.");
    }

    [Test]
    public void DelayedProviderRepaintWaitsAndKeepsItsOwningWindow()
    {
        _owner = CreateWindow("NowUI delayed repaint owner", show: false);
        object hostContext = new object();
        MethodInfo trackHost = typeof(NowEditorGUI).GetMethod(
            "TrackHost",
            StaticNonPublic);
        Assert.NotNull(trackHost);
        trackHost.Invoke(null, new object[] { hostContext, _owner });

        var provider = new NowIMGUIInputProvider(17002, hostContext);
        provider.RequestHostRepaintAfter(10f);

        FieldInfo scheduledField = typeof(NowEditorGUI).GetField(
            "ScheduledRepaints",
            StaticNonPublic);
        FieldInfo pendingField = typeof(NowEditorGUI).GetField(
            "PendingRepaints",
            StaticNonPublic);
        Assert.NotNull(scheduledField);
        Assert.NotNull(pendingField);

        var scheduled = scheduledField.GetValue(null) as IDictionary;
        var pending = pendingField.GetValue(null) as HashSet<EditorWindow>;
        Assert.NotNull(scheduled);
        Assert.NotNull(pending);
        Assert.IsTrue(scheduled.Contains(provider));
        Assert.IsFalse(
            pending.Contains(_owner),
            "A future caret phase must not force an immediate full panel repaint.");
    }

    [Test]
    public void ImmediateProviderRepaintReplacesItsScheduledDeadline()
    {
        _owner = CreateWindow("NowUI immediate repaint owner", show: false);
        object hostContext = new object();
        TrackHost(hostContext, _owner);

        var provider = new NowIMGUIInputProvider(17003, hostContext);
        provider.RequestHostRepaintAfter(10f);

        IDictionary scheduled = GetDictionary("ScheduledRepaints");
        var pending = GetPendingRepaints();
        Assert.IsTrue(scheduled.Contains(provider));

        provider.RequestHostRepaint(markGUIChanged: false);

        Assert.IsFalse(
            scheduled.Contains(provider),
            "An immediate repaint supersedes this provider's old deadline.");
        Assert.IsTrue(pending.Contains(_owner));
    }

    [Test]
    public void CurrentTrackedPassReplacesItsObsoleteScheduledDeadline()
    {
        _owner = CreateWindow("NowUI current pass owner", show: false);
        object hostContext = new object();
        TrackHost(hostContext, _owner);

        const int controlId = 17004;
        NowGUI.CacheEntry entry = GetEntry(hostContext, controlId);
        entry.inputProvider.RequestHostRepaintAfter(10f);

        IDictionary scheduled = GetDictionary("ScheduledRepaints");
        Assert.IsTrue(scheduled.Contains(entry.inputProvider));

        using (NowGUI.AutoForEvent(
            hostContext,
            controlId,
            new Rect(0f, 0f, 100f, 40f),
            Color.clear,
            1f,
            repaint: false,
            hostFocused: true,
            trackInputRepaint: true))
        {
        }

        Assert.IsFalse(
            scheduled.Contains(entry.inputProvider),
            "A completed tracked pass must replace, rather than retain, its previous deadline.");
    }

    [Test]
    public void ClosedHostIsPrunedWithItsCacheAndQueuedRepaints()
    {
        _owner = CreateWindow("NowUI stale host", show: false);
        object hostContext = new object();
        TrackHost(hostContext, _owner);

        const int controlId = 17005;
        NowGUI.CacheEntry oldEntry = GetEntry(hostContext, controlId);
        oldEntry.inputProvider.RequestHostRepaint(markGUIChanged: false);
        oldEntry.inputProvider.RequestHostRepaintAfter(10f);

        IDictionary hosts = GetDictionary("HostWindows");
        IDictionary scheduled = GetDictionary("ScheduledRepaints");
        var pending = GetPendingRepaints();
        Assert.IsTrue(hosts.Contains(hostContext));
        Assert.IsTrue(scheduled.Contains(oldEntry.inputProvider));
        Assert.AreEqual(1, pending.Count);

        Object.DestroyImmediate(_owner);
        _owner = null;
        InvokeStatic("TrackEditorFocusChanges");

        Assert.IsFalse(hosts.Contains(hostContext));
        Assert.IsFalse(scheduled.Contains(oldEntry.inputProvider));
        Assert.AreEqual(0, pending.Count);
        Assert.AreNotSame(
            oldEntry,
            GetEntry(hostContext, controlId),
            "Pruning a closed host must dispose and remove its cached NowGUI entries.");
    }

    static void TrackHost(object context, EditorWindow window)
    {
        MethodInfo method = typeof(NowEditorGUI).GetMethod("TrackHost", StaticNonPublic);
        Assert.NotNull(method);
        method.Invoke(null, new object[] { context, window });
    }

    static void InvokeStatic(string methodName)
    {
        MethodInfo method = typeof(NowEditorGUI).GetMethod(methodName, StaticNonPublic);
        Assert.NotNull(method);
        method.Invoke(null, null);
    }

    static IDictionary GetDictionary(string fieldName)
    {
        FieldInfo field = typeof(NowEditorGUI).GetField(fieldName, StaticNonPublic);
        Assert.NotNull(field);
        var dictionary = field.GetValue(null) as IDictionary;
        Assert.NotNull(dictionary);
        return dictionary;
    }

    static HashSet<EditorWindow> GetPendingRepaints()
    {
        FieldInfo field = typeof(NowEditorGUI).GetField(
            "PendingRepaints",
            StaticNonPublic);
        Assert.NotNull(field);
        var pending = field.GetValue(null) as HashSet<EditorWindow>;
        Assert.NotNull(pending);
        return pending;
    }

    static NowGUI.CacheEntry GetEntry(object context, int controlId)
    {
        MethodInfo method = typeof(NowGUI).GetMethod("GetEntry", StaticNonPublic);
        Assert.NotNull(method);
        return (NowGUI.CacheEntry)method.Invoke(
            null,
            new object[] { context, controlId });
    }

    static NowEditorGUIHostTestWindow CreateWindow(string title, bool show)
    {
        var window = ScriptableObject.CreateInstance<NowEditorGUIHostTestWindow>();
        window.titleContent = new GUIContent(title);

        if (show)
            window.ShowUtility();

        return window;
    }

    static void CloseWindow(ref NowEditorGUIHostTestWindow window)
    {
        if (!window)
            return;

        FieldInfo parentField = typeof(EditorWindow).GetField(
            "m_Parent",
            InstanceNonPublic);

        if (parentField?.GetValue(window) != null)
            window.Close();

        if (window)
            Object.DestroyImmediate(window);

        window = null;
    }
}

sealed class NowEditorGUIHostTestWindow : EditorWindow
{
}
