using NUnit.Framework;
using NowUI.Internal;
using System.Reflection;
using UnityEngine;
using NowUI;

public class Simple
{
    [Test]
    public void StaticListEnsureCapacityPreservesExistingValues()
    {
        var list = new StaticList<int>(1);

        list.array[0] = 42;
        list.count = 1;
        list.EnsureCapacity(2);

        Assert.GreaterOrEqual(list.array.Length, 3);
        Assert.AreEqual(42, list.array[0]);
    }

    [Test]
    public void FontReturnsFalseWhenAtlasDataIsMissing()
    {
        var font = ScriptableObject.CreateInstance<NowFont>();

        try
        {
            Assert.IsFalse(font.GetGlyph('A', out _));
        }
        finally
        {
            Object.DestroyImmediate(font);
        }
    }

    [Test]
    public void FontNormalizesAtlasBounds()
    {
        var font = ScriptableObject.CreateInstance<NowFont>();
        font.atlas = new Texture2D(100, 200);
        font.atlasInfo = new NowFontAtlasInfo
        {
            glyphs = new[]
            {
                new NowFontAtlasInfo.Glyph
                {
                    unicode = 'A',
                    advance = 1,
                    atlasBounds = new NowFontAtlasInfo.Bounds
                    {
                        left = 10,
                        bottom = 20,
                        right = 30,
                        top = 60
                    }
                }
            }
        };

        try
        {
            Assert.IsTrue(font.GetGlyph('A', out var glyph));
            Assert.AreEqual(0.1f, glyph.atlasBounds.left, 0.0001f);
            Assert.AreEqual(0.1f, glyph.atlasBounds.bottom, 0.0001f);
            Assert.AreEqual(0.3f, glyph.atlasBounds.right, 0.0001f);
            Assert.AreEqual(0.3f, glyph.atlasBounds.top, 0.0001f);
        }
        finally
        {
            Object.DestroyImmediate(font.atlas);
            Object.DestroyImmediate(font);
        }
    }

    [Test]
    public void FontSupportsSparseGlyphRanges()
    {
        const char SPARSE_CHARACTER = (char)0x2603;
        var font = ScriptableObject.CreateInstance<NowFont>();
        font.atlas = new Texture2D(100, 100);
        font.atlasInfo = new NowFontAtlasInfo
        {
            glyphs = new[]
            {
                new NowFontAtlasInfo.Glyph
                {
                    unicode = 'A',
                    advance = 1,
                    atlasBounds = new NowFontAtlasInfo.Bounds
                    {
                        right = 10,
                        top = 10
                    }
                },
                new NowFontAtlasInfo.Glyph
                {
                    unicode = SPARSE_CHARACTER,
                    advance = 2,
                    atlasBounds = new NowFontAtlasInfo.Bounds
                    {
                        right = 20,
                        top = 20
                    }
                }
            }
        };

        try
        {
            Assert.IsTrue(font.GetGlyph(SPARSE_CHARACTER, out var glyph));
            Assert.AreEqual(2, glyph.advance);
            Assert.IsFalse(font.GetGlyph('B', out _));
        }
        finally
        {
            Object.DestroyImmediate(font.atlas);
            Object.DestroyImmediate(font);
        }
    }

    [Test]
    public void FontMeasuresTextUsingGlyphAdvances()
    {
        var font = ScriptableObject.CreateInstance<NowFont>();
        font.atlas = new Texture2D(100, 100);
        font.atlasInfo = new NowFontAtlasInfo
        {
            metrics = new NowFontAtlasInfo.Metrics
            {
                lineHeight = 1.5f
            },
            glyphs = new[]
            {
                new NowFontAtlasInfo.Glyph
                {
                    unicode = ' ',
                    advance = 0.5f
                },
                new NowFontAtlasInfo.Glyph
                {
                    unicode = 'A',
                    advance = 1
                },
                new NowFontAtlasInfo.Glyph
                {
                    unicode = 'B',
                    advance = 2
                }
            }
        };

        try
        {
            Vector2 measured = font.MeasureText("A\tB\nAA", 10, 2);

            Assert.AreEqual(40, measured.x, 0.0001f);
            Assert.AreEqual(30, measured.y, 0.0001f);
        }
        finally
        {
            Object.DestroyImmediate(font.atlas);
            Object.DestroyImmediate(font);
        }
    }

    [Test]
    public void FontMeasureFallsBackWhenPreparedPrimaryFontMissesGlyph()
    {
        var primary = ScriptableObject.CreateInstance<NowFont>();
        var fallback = ScriptableObject.CreateInstance<NowFont>();
        primary.atlas = new Texture2D(100, 100);
        fallback.atlas = new Texture2D(100, 100);
        primary.atlasInfo = new NowFontAtlasInfo
        {
            metrics = new NowFontAtlasInfo.Metrics { lineHeight = 1f },
            glyphs = new[]
            {
                new NowFontAtlasInfo.Glyph { unicode = 'A', advance = 1 }
            }
        };
        fallback.atlasInfo = new NowFontAtlasInfo
        {
            metrics = new NowFontAtlasInfo.Metrics { lineHeight = 1f },
            glyphs = new[]
            {
                new NowFontAtlasInfo.Glyph { unicode = 'B', advance = 2 }
            }
        };

        try
        {
            typeof(NowFontAsset)
                .GetField("_fallbacks", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(primary, new NowFontAsset[] { fallback });

            Vector2 measured = primary.MeasureText("AB", 10);

            Assert.AreEqual(30, measured.x, 0.0001f);
        }
        finally
        {
            Object.DestroyImmediate(primary.atlas);
            Object.DestroyImmediate(fallback.atlas);
            Object.DestroyImmediate(primary);
            Object.DestroyImmediate(fallback);
        }
    }

    [Test]
    public void FontReadsSurrogatePairAsSingleCodepoint()
    {
        const int GRINNING_FACE = 0x1F600;
        string value = "\U0001F600";
        int index = 0;

        Assert.AreEqual(GRINNING_FACE, NowFont.ReadCodepoint(value, ref index));
        Assert.AreEqual(1, index);
    }

    [Test]
    public void FontCanResolveSupplementaryPlaneGlyphs()
    {
        const int GRINNING_FACE = 0x1F600;
        var font = ScriptableObject.CreateInstance<NowFont>();
        font.atlas = new Texture2D(100, 100);
        font.atlasInfo = new NowFontAtlasInfo
        {
            glyphs = new[]
            {
                new NowFontAtlasInfo.Glyph
                {
                    unicode = GRINNING_FACE,
                    advance = 2,
                    atlasBounds = new NowFontAtlasInfo.Bounds
                    {
                        right = 10,
                        top = 10
                    }
                }
            }
        };

        try
        {
            Assert.IsTrue(font.GetGlyph(GRINNING_FACE, out var glyph));
            Assert.AreEqual(2, glyph.advance);
        }
        finally
        {
            Object.DestroyImmediate(font.atlas);
            Object.DestroyImmediate(font);
        }
    }

    [Test]
    public void FontMeasuresTextBoundsUsingGlyphPlanes()
    {
        var font = ScriptableObject.CreateInstance<NowFont>();
        font.atlas = new Texture2D(100, 100);
        font.atlasInfo = new NowFontAtlasInfo
        {
            metrics = new NowFontAtlasInfo.Metrics
            {
                lineHeight = 1.5f
            },
            glyphs = new[]
            {
                new NowFontAtlasInfo.Glyph
                {
                    unicode = 'A',
                    advance = 1,
                    planeBounds = new NowFontAtlasInfo.Bounds
                    {
                        left = -0.1f,
                        bottom = -0.2f,
                        right = 0.7f,
                        top = 0.8f
                    },
                    atlasBounds = new NowFontAtlasInfo.Bounds
                    {
                        right = 10,
                        top = 10
                    }
                },
                new NowFontAtlasInfo.Glyph
                {
                    unicode = 'B',
                    advance = 2,
                    planeBounds = new NowFontAtlasInfo.Bounds
                    {
                        left = 0.1f,
                        bottom = 0,
                        right = 1.9f,
                        top = 0.9f
                    },
                    atlasBounds = new NowFontAtlasInfo.Bounds
                    {
                        right = 20,
                        top = 20
                    }
                }
            }
        };

        try
        {
            Vector4 bounds = font.MeasureTextBounds("AB", 10);

            Assert.AreEqual(-1, bounds.x, 0.0001f);
            Assert.AreEqual(6, bounds.y, 0.0001f);
            Assert.AreEqual(30, bounds.z, 0.0001f);
            Assert.AreEqual(11, bounds.w, 0.0001f);
        }
        finally
        {
            Object.DestroyImmediate(font.atlas);
            Object.DestroyImmediate(font);
        }
    }
}
