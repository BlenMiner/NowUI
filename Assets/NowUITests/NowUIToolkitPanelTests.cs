#if NOWUI_UITOOLKIT
using System.Collections;
using NowUI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

/// <summary>
/// Hosts NowUI elements in a real UI Toolkit panel so layout, not just the
/// measure override, is exercised.
/// </summary>
public class NowUIToolkitPanelTests
{
    sealed class ProbeWindow : EditorWindow
    {
    }

    [UnityTest]
    public IEnumerator AutoSizedLayoutVisualElementShrinkWrapsItsContentInAPanel()
    {
        var window = ScriptableObject.CreateInstance<ProbeWindow>();
        window.Show();

        try
        {
            var root = window.rootVisualElement;
            root.style.flexDirection = FlexDirection.Row;
            root.style.alignItems = Align.FlexStart;

            var element = new NowLayoutVisualElement();
            element.rebuildNowUI += (_, view) =>
            {
                using (NowLayout.Column(view).Begin())
                using (NowLayout.Column().Width(120f).Height(40f).Begin())
                    NowLayout.Space(1f);
            };
            root.Add(element);

            Rect layout = default;
            for (int frame = 0; frame < 20; ++frame)
            {
                window.Repaint();
                yield return null;
                layout = element.layout;
                if (layout.width > 0f && layout.height > 0f)
                    break;
            }

            Assert.AreEqual(120f, layout.width, 0.5f,
                "An auto-width NowLayoutVisualElement in a row must take its NowLayout content width.");
            Assert.AreEqual(40f, layout.height, 0.5f,
                "An auto-height NowLayoutVisualElement must take its NowLayout content height.");
        }
        finally
        {
            window.Close();
        }
    }
}
#endif
