using System.Collections.Generic;
using System.Reflection;
using NowUI;
using NowUI.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

public class NowEditorThemeComparisonTests
{
    [Test]
    public void DefaultComparisonThemeLoadsTheUnityEditorDarkPreset()
    {
        NowThemeAsset theme = NowEditorThemeComparisonWindow.LoadDefaultTheme();

        Assert.IsNotNull(theme);
        Assert.AreEqual("UnityEditorDark", theme.name);
        Assert.IsInstanceOf<NowUnityEditorControlRenderer>(theme.controlRenderer);
    }

    [Test]
    public void RealEditorWindowCaptureCompatibilitySeamsExist()
    {
        MethodInfo repaint = typeof(EditorWindow).GetMethod(
            "RepaintImmediately",
            BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo capture = typeof(InternalEditorUtility).GetMethod(
            "CaptureEditorWindow",
            BindingFlags.Static | BindingFlags.Public,
            null,
            new[] { typeof(EditorWindow), typeof(RenderTexture) },
            null);

        Assert.IsNotNull(repaint, "Unity no longer exposes the synchronous editor-window repaint seam.");
        Assert.IsNotNull(capture, "Unity no longer exposes editor-window capture with the expected signature.");
        Assert.AreEqual(typeof(bool), capture.ReturnType);
    }

    [Test]
    public void SharedSpecimenRowsAreOrderedAndRemainInsideBothPanels()
    {
        const float Width = 420f;
        List<NowEditorThemeComparisonRow> rows =
            NowEditorThemeComparisonSpecimen.Build(Width);
        float previousBottom = 0f;

        Assert.Greater(rows.Count, 12);

        for (int i = 0; i < rows.Count; ++i)
        {
            NowEditorThemeComparisonRow row = rows[i];
            Assert.GreaterOrEqual(row.rect.y, previousBottom, row.label);
            Assert.Greater(row.rect.width, 0f, row.label);
            Assert.Greater(row.rect.height, 0f, row.label);
            Assert.LessOrEqual(row.rect.xMax, Width, row.label);
            Assert.LessOrEqual(
                row.rect.yMax,
                NowEditorThemeComparisonSpecimen.Height,
                row.label);
            previousBottom = row.rect.yMax;

            if (row.element == NowEditorThemeComparisonElement.Section)
                continue;

            Rect label = NowEditorThemeComparisonSpecimen.LabelRect(row);
            Rect control = NowEditorThemeComparisonSpecimen.ControlRect(row);
            Assert.AreEqual(row.rect.y, label.y, row.label);
            Assert.AreEqual(row.rect.y, control.y, row.label);
            Assert.AreEqual(row.rect.height, label.height, row.label);
            Assert.AreEqual(row.rect.height, control.height, row.label);
            Assert.AreEqual(
                NowEditorThemeComparisonSpecimen.LabelGap,
                control.x - label.xMax,
                0.0001f,
                row.label);
            Assert.AreEqual(row.rect.xMax, control.xMax, 0.0001f, row.label);
        }
    }

    [TestCase(1099f, 1f)]
    [TestCase(1100f, 1f)]
    [TestCase(1099f, 1.25f)]
    [TestCase(1100f, 1.25f)]
    [TestCase(1099f, 1.5f)]
    [TestCase(1100f, 1.5f)]
    [TestCase(1099f, 2f)]
    [TestCase(1100f, 2f)]
    public void NowColumnOriginIsAlignedToTheEditorPixelGrid(
        float contentWidth,
        float pixelsPerPoint)
    {
        float origin = NowEditorThemeComparisonWindow.CalculateNowColumnOrigin(
            contentWidth,
            pixelsPerPoint);
        float physicalOrigin = origin * pixelsPerPoint;

        Assert.AreEqual(
            Mathf.Round(physicalOrigin),
            physicalOrigin,
            0.0001f,
            "A fractional physical origin bilinearly softens the complete NowUI surface.");
    }

    [Test]
    public void LiveSingleLineControlsUseTheNativeEighteenPointRow()
    {
        List<NowEditorThemeComparisonRow> rows =
            NowEditorThemeComparisonSpecimen.Build(420f);
        var expected = new HashSet<NowEditorThemeComparisonElement>
        {
            NowEditorThemeComparisonElement.Button,
            NowEditorThemeComparisonElement.ToggleOff,
            NowEditorThemeComparisonElement.ToggleOn,
            NowEditorThemeComparisonElement.TextField,
            NowEditorThemeComparisonElement.IntegerField,
            NowEditorThemeComparisonElement.FloatField,
            NowEditorThemeComparisonElement.Popup,
            NowEditorThemeComparisonElement.Slider,
            NowEditorThemeComparisonElement.ProgressBar,
            NowEditorThemeComparisonElement.Foldout
        };

        for (int i = 0; i < rows.Count; ++i)
        {
            if (!expected.Remove(rows[i].element))
                continue;

            Assert.AreEqual(
                NowEditorThemeComparisonSpecimen.StandardRowHeight,
                rows[i].rect.height,
                0.0001f,
                rows[i].label);
        }

        Assert.IsEmpty(expected, "Every baseline single-line control must be present in the specimen.");
    }
}
