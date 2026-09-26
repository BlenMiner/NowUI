using System;
using NUnit.Framework;
using Unity.PerformanceTesting;
using UnityEngine;
using NowUI;

/// <summary>
/// Frame-build timings for drawing features the other suites do not isolate:
/// drop shadows, gradient fills, rotation, transform and opacity scopes, and
/// the text variants that leave the bulk glyph path (alignment, letter spacing,
/// outlines, per-glyph animation, gradients). Each case draws a fixed workload
/// into a draw list; the numbers are for before/after comparison only. The
/// standalone <c>Standalone/Benchmarks/FrameCpu</c> runner draws the same
/// scenes on the engine-free build for sampling profilers.
/// </summary>
public class NowFeaturePerformanceTests
{
    static readonly Vector2 FrameSize = new Vector2(1280f, 720f);

    const string TextSample = "The quick brown fox jumps over 0123456789";

    static readonly NowTextAnimation Wave = NowTextAnimations.Wave();

    NowDrawList _drawList;
    NowFontAsset _previousFont;
    float _time;

    [SetUp]
    public void SetUp()
    {
        var font = Resources.Load<NowFontAsset>("NowUI/NotoSans");
        Assert.NotNull(font, "Default font resource is required for feature benchmarks.");
        _previousFont = Now.defaultFont;
        Now.defaultFont = font;
        _drawList = new NowDrawList();
        _time = 0f;
    }

    [TearDown]
    public void TearDown()
    {
        _drawList.Dispose();
        Now.defaultFont = _previousFont;
    }

    void MeasureFrames(Action draw)
    {
        Measure.Method(() =>
            {
                _time += 1f / 60f;

                using (_drawList.Begin(FrameSize))
                    draw();
            })
            .WarmupCount(5)
            .MeasurementCount(20)
            .Run();
    }

    static NowRect Cell(int index)
    {
        return new NowRect(8f + index % 10 * 126f, 8f + index / 10 * 68f, 118f, 60f);
    }

    static NowRect Row(int index)
    {
        return new NowRect(8f + index % 2 * 620f, index / 2 * 14f, 600f, 14f);
    }

    /// <summary>100 material cards: drop shadow, surface and title.</summary>
    [Test, Performance]
    public void ShadowedCardsFrameBuild()
    {
        var shadow = new Color(0f, 0f, 0f, 0.35f);
        var surface = new Color(0.16f, 0.18f, 0.22f, 1f);

        MeasureFrames(() =>
        {
            for (int i = 0; i < 100; ++i)
            {
                var rect = Cell(i);
                Now.Shadow(rect).SetRadius(8f).SetOffset(0f, 4f).SetBlur(12f).SetColor(shadow).Draw();
                Now.Rectangle(rect).SetColor(surface).SetRadius(8f).Draw();
                Now.Text(rect.Inset(8f)).SetFontSize(13f).SetColor(Color.white).Draw("Card title");
            }
        });
    }

    /// <summary>300 rounded two-color gradient fills in alternating directions.</summary>
    [Test, Performance]
    public void GradientRectanglesFrameBuild()
    {
        var from = new Color(0.2f, 0.5f, 1f, 1f);
        var to = new Color(0.9f, 0.3f, 0.6f, 1f);

        MeasureFrames(() =>
        {
            for (int i = 0; i < 300; ++i)
            {
                Now.Gradient(new NowRect(i * 37 % 1180, i * 23 % 660, 96f, 48f), from, to)
                    .SetLinear(i % 2 == 0 ? NowGradientDirection.ToRight : NowGradientDirection.ToBottom)
                    .SetRadius(6f)
                    .Draw();
            }
        });
    }

    /// <summary>100 rotation scopes, each turning a badge and its label.</summary>
    [Test, Performance]
    public void RotatedContentFrameBuild()
    {
        var fill = new Color(0.3f, 0.4f, 0.8f, 1f);

        MeasureFrames(() =>
        {
            for (int i = 0; i < 100; ++i)
            {
                var rect = Cell(i);

                using (Now.Rotate(i * 3.6f + _time * 30f, rect.center))
                {
                    Now.Rectangle(rect).SetColor(fill).SetRadius(6f).Draw();
                    Now.Text(rect.Inset(8f)).SetFontSize(13f).SetColor(Color.white).Draw("Rotated");
                }
            }
        });
    }

    /// <summary>
    /// A zoomed canvas: 500 rectangles and 100 labels inside one uniform
    /// transform scope, the shape of node editors and zoomable boards.
    /// </summary>
    [Test, Performance]
    public void ScaledCanvasFrameBuild()
    {
        MeasureFrames(() =>
        {
            using (Now.Transform(1.25f + 0.05f * Mathf.Sin(_time), new Vector2(-80f, -40f)))
            {
                for (int i = 0; i < 500; ++i)
                    Now.Rectangle(new NowRect(i * 7 % 1000, i * 13 % 560, 48f, 24f)).SetColor(Color.gray).SetRadius(4f).Draw();

                for (int i = 0; i < 100; ++i)
                    Now.Text(new NowRect(8f + i % 5 * 200f, i / 5 * 28f, 190f, 20f)).SetFontSize(14f).SetColor(Color.white).Draw(TextSample);
            }
        });
    }

    /// <summary>Ten faded panels, each with ten nested faded cards.</summary>
    [Test, Performance]
    public void NestedOpacityFrameBuild()
    {
        var fill = new Color(0.2f, 0.6f, 0.4f, 1f);

        MeasureFrames(() =>
        {
            for (int panel = 0; panel < 10; ++panel)
            {
                using (Now.Opacity(0.5f + panel * 0.05f))
                {
                    for (int i = 0; i < 10; ++i)
                    {
                        var rect = Cell(panel * 10 + i);

                        using (Now.Opacity(0.9f))
                        {
                            Now.Rectangle(rect).SetColor(fill).SetRadius(6f).Draw();
                            Now.Text(rect.Inset(8f)).SetFontSize(13f).SetColor(Color.white).Draw("Faded");
                        }
                    }
                }
            }
        });
    }

    static readonly string[] Tokens =
    {
        "public", " ", "static", " ", "void", " ", "Draw", "(", "NowRect", " ",
        "rect", ")", " ", "{", " ", "return", ";", " ", "}"
    };

    /// <summary>
    /// 1,140 short text draws (60 lines of 19 tokens), the per-draw overhead a
    /// syntax-highlighted editor or a dense table pays, largely independent of
    /// glyph count.
    /// </summary>
    [Test, Performance]
    public void ShortTextRunsFrameBuild()
    {
        MeasureFrames(() =>
        {
            var style = Now.Text(default).SetFontSize(13f).SetColor(Color.white);

            for (int line = 0; line < 60; ++line)
            {
                float x = 8f;

                for (int t = 0; t < Tokens.Length; ++t)
                {
                    var text = style;
                    text.rect = new NowRect(x, 8f + line * 11f, 200f, 16f);
                    text.Draw(Tokens[t]);
                    x += Tokens[t].Length * 7f;
                }
            }
        });
    }

    /// <summary>100 centered or right-aligned, vertically centered labels.</summary>
    [Test, Performance]
    public void AlignedTextFrameBuild()
    {
        MeasureFrames(() =>
        {
            for (int i = 0; i < 100; ++i)
            {
                Now.Text(Row(i))
                    .SetFontSize(13f)
                    .SetColor(Color.white)
                    .SetAlign(i % 4 < 2 ? NowTextAlign.Center : NowTextAlign.Right, NowTextVerticalAlign.Middle)
                    .Draw(TextSample);
            }
        });
    }

    /// <summary>100 letter-spaced labels (per-glyph placement).</summary>
    [Test, Performance]
    public void LetterSpacedTextFrameBuild()
    {
        MeasureFrames(() =>
        {
            for (int i = 0; i < 100; ++i)
                Now.Text(Row(i)).SetFontSize(13f).SetColor(Color.white).SetLetterSpacing(0.08f).Draw(TextSample);
        });
    }

    /// <summary>100 outlined labels (separate outline and fill passes).</summary>
    [Test, Performance]
    public void OutlinedTextFrameBuild()
    {
        var outline = new Vector4(0f, 0f, 0f, 1f);

        MeasureFrames(() =>
        {
            for (int i = 0; i < 100; ++i)
                Now.Text(Row(i)).SetFontSize(13f).SetColor(Color.white).SetOutline(0.15f).SetOutlineColor(outline).Draw(TextSample);
        });
    }

    /// <summary>100 labels with a continuously sampled wave animation.</summary>
    [Test, Performance]
    public void AnimatedTextFrameBuild()
    {
        MeasureFrames(() =>
        {
            for (int i = 0; i < 100; ++i)
                Now.Text(Row(i)).SetFontSize(13f).SetColor(Color.white).SetAnimation(Wave).SetTime(_time).Draw(TextSample);
        });
    }

    /// <summary>100 labels filled with a horizontal two-color gradient.</summary>
    [Test, Performance]
    public void GradientTextFrameBuild()
    {
        MeasureFrames(() =>
        {
            for (int i = 0; i < 100; ++i)
                Now.Text(Row(i)).SetFontSize(13f).SetGradient(Color.cyan, Color.magenta).SetGradientLinear(NowGradientDirection.ToRight).Draw(TextSample);
        });
    }
}
