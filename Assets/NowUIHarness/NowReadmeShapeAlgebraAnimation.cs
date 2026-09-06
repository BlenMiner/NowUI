using System.Collections.Generic;
using NowUI.Sdf;
using UnityEditor;
using UnityEngine;

namespace NowUI.Editor
{
    /// <summary>
    /// README showcase: a walkthrough of SDF shape algebra in the style of the
    /// PanGui "Shapes" demo. Two circles and a triangle are combined step by
    /// step (union, intersection, shell, difference, smooth union, subtraction,
    /// and finally a heart) while a syntax-colored code caption names each
    /// step. Every transition between steps is a real distance-field morph or
    /// a parameter slide, never a crossfade. The heart then pulses, wobbles by
    /// mixing with an orbiting circle, reacts to a hovering cursor, and morphs
    /// back into the opening circle so the loop wraps seamlessly.
    /// </summary>
    internal static partial class NowHarnessAnimationScenarios
    {
        const string ShapesMonoFontPath = "Assets/NowUI/Assets/Fonts/JetBrainsMono/JetBrainsMono.asset";
        const int ShapesTransitionFrames = 8;
        const int ShapesHoldFrames = 14;
        const float ShapesUnit = 0.66f;
        const float ShapesFps = 24f;

        static readonly Vector2 ShapesOrigin = new Vector2(480f, 268f);
        static readonly NowRect ShapesScene = new NowRect(180f, 40f, 600f, 440f);

        static readonly Color ShapesBlue = new Color(0.16f, 0.44f, 0.90f, 1f);
        static readonly Color ShapesRed = new Color(0.88f, 0.14f, 0.16f, 1f);
        static readonly Color ShapesIdentifier = new Color(0.31f, 0.76f, 1f, 1f);
        static readonly Color ShapesType = new Color(0.31f, 0.79f, 0.69f, 1f);
        static readonly Color ShapesMethod = new Color(0.86f, 0.86f, 0.67f, 1f);
        static readonly Color ShapesNumber = new Color(0.71f, 0.81f, 0.66f, 1f);
        static readonly Color ShapesPunctuation = new Color(0.80f, 0.80f, 0.82f, 1f);
        static readonly Color ShapesKeyword = new Color(0.34f, 0.61f, 0.84f, 1f);
        static readonly Color ShapesComment = new Color(0.42f, 0.60f, 0.36f, 1f);

        static readonly NowSdfGraph ShapesGraphA = NowSdf.Graph();
        static readonly NowSdfGraph ShapesGraphB = NowSdf.Graph();
        static readonly NowSdfGraph ShapesGraphLens = NowSdf.Graph();
        static readonly NowSdfGraph ShapesGraphOrbit = NowSdf.Graph();
        static readonly List<(string text, Color color)> ShapesTokens = new List<(string, Color)>(32);

        static NowFontAsset _shapesMonoFont;

        /// <summary>
        /// One step of the walkthrough: the NowUI builder call that produces
        /// the field, an optional setup line introducing any new name, and how
        /// long it holds after its transition.
        /// </summary>
        readonly struct ShapesStep
        {
            public readonly string caption;
            public readonly string setup;
            public readonly int extraHold;

            public ShapesStep(string caption, string setup = null, int extraHold = 0)
            {
                this.caption = caption;
                this.setup = setup ?? string.Empty;
                this.extraHold = extraHold;
            }

            public int Frames => ShapesTransitionFrames + ShapesHoldFrames + extraHold;
        }

        // Captions are the real NowUI calls behind each field. Every step is a
        // chain on one NowSdf.Scene builder; the setup line defines the names
        // the chain uses (c is the scene center, a and b the circle centers).
        static readonly ShapesStep[] ShapesSteps =
        {
            new ShapesStep(".Circle(c, 100)", "Vector2 c = rect.center;"),
            new ShapesStep(".Circle(a, 100)", "Vector2 a = c + new Vector2(-60, 0);"),
            new ShapesStep(".Circle(b, 100)", "Vector2 b = c + new Vector2(60, 0);"),
            new ShapesStep(".Circle(a, 100).Union().Circle(b, 100)"),
            new ShapesStep(".Circle(a, 100).Intersect().Circle(b, 100)"),
            new ShapesStep(".Circle(a, 110).Circle(b, 110).Subtract().Circle(a, 90).Subtract().Circle(b, 90)", "// a 10-unit shell: the union grown by 10, minus the union shrunk by 10"),
            new ShapesStep(".Graph(union).Subtract().Graph(lens)", "var union = NowSdf.Graph().Circle(a, 100).Circle(b, 100);\nvar lens = NowSdf.Graph().Circle(a, 100).Intersect().Circle(b, 100);"),
            new ShapesStep(".Circle(a2, 100).SmoothUnion(100).Circle(b2, 100)", "Vector2 a2 = a + new Vector2(-85, 0);  Vector2 b2 = b + new Vector2(85, 0);", 4),
            new ShapesStep(".Circle(a, 100).SmoothUnion(100).Circle(b, 100)"),
            new ShapesStep(".Circle(a, 100).SmoothUnion(100).Circle(b, 100).Subtract().Circle(a, 80)"),
            new ShapesStep(".Circle(a, 100).SmoothUnion(100).Circle(b, 100).Subtract().Circle(b, 80)"),
            new ShapesStep(".Triangle(t0, t1, t2)", "Vector2 t0 = c + new Vector2(-100, 0);  t1 = c + new Vector2(100, 0);  t2 = c + new Vector2(0, 200);", 4),
            new ShapesStep(".Triangle(t0, t1, t2).Circle(a, 100).Circle(b, 100)"),
            new ShapesStep(".Circle(a, 100).Circle(b, 100).SmoothUnion(100).Triangle(t0, t1, t2)", "var heart = NowSdf.Graph()  // the same chain, kept as a reusable graph"),
            new ShapesStep(".Graph(heart)", "using (Now.TransformAround(Vector2.one * (1 + Mathf.Sin(t * Mathf.PI) * 0.2f), c))", 26),
            new ShapesStep(".Morph(heart, orbit, 0.1f)", "var orbit = NowSdf.Graph().Circle(c + new Vector2(Mathf.Cos(time * 4), Mathf.Sin(time * 4)) * 60, 100);", 70)
        };

        static int ShapesTotalFrames()
        {
            int total = 0;
            for (int i = 0; i < ShapesSteps.Length; ++i)
                total += ShapesSteps[i].Frames;
            return total;
        }

        static void DrawShapeAlgebra(NowRect rect, NowHarnessAnimationFrame frame)
        {
            int f = frame.index;
            ResolveShapesStep(f, out int step, out int local);
            int previous = (step + ShapesSteps.Length - 1) % ShapesSteps.Length;
            // The field settles like a damped lerp: most of the change lands in
            // the first few frames, then it eases in over the rest. A cubic
            // ease-out reaches exactly 1 at frame 16 so the hold frames are
            // bit-identical and the WebP encoder can merge them.
            float t = 1f - Mathf.Pow(1f - Mathf.Clamp01(local / 16f), 3f);
            float holdSeconds = Mathf.Max(0f, local - ShapesTransitionFrames) / ShapesFps;

            Now.Rectangle(rect).SetColor(new Color(0.067f, 0.067f, 0.071f, 1f)).Draw();

            Color fill = ShapesFillColor(step, t);
            Now.Gradient(rect, WithAlpha(fill, 0.10f), WithAlpha(fill, 0f))
                .SetRadial(new Vector2(ShapesOrigin.x / rect.width, ShapesOrigin.y / rect.height), 0.42f)
                .Draw();

            DrawShapesCenteredText(new Vector2(rect.width * 0.5f, 52f), "Shapes", 34f, Color.white, true);
            DrawShapesCenteredText(new Vector2(rect.width * 0.5f, 90f), "User interfaces are made of shapes. NowUI composes them as distance fields:", 14f, new Color(0.62f, 0.62f, 0.67f, 1f), false);
            DrawShapesCenteredText(new Vector2(rect.width * 0.5f, 110f), "booleans, shells, smooth blends, and morphs, all in a few calls.", 14f, new Color(0.62f, 0.62f, 0.67f, 1f), false);

            Vector2 cursor = ShapesCursor(step, local);
            DrawShapesGhosts(step, t);
            DrawShapesField(step, previous, t, holdSeconds, fill, cursor);
            DrawShapesCaption(step, previous, local);

            if (cursor.x > 0f)
                DrawCursor(cursor);
            DrawRenderedTag(new NowRect(760f, 506f, 160f, 24f));
        }

        static void ResolveShapesStep(int frame, out int step, out int local)
        {
            int cursor = 0;
            for (int i = 0; i < ShapesSteps.Length; ++i)
            {
                if (frame < cursor + ShapesSteps[i].Frames)
                {
                    step = i;
                    local = frame - cursor;
                    return;
                }
                cursor += ShapesSteps[i].Frames;
            }

            step = ShapesSteps.Length - 1;
            local = ShapesSteps[step].Frames - 1;
        }

        static Color ShapesFillColor(int step, float t)
        {
            // Blue through the construction, red once it becomes a heart, and
            // back to blue as the heart morphs into the opening circle.
            if (step == 14)
                return Color.Lerp(ShapesBlue, ShapesRed, t);
            if (step == 15)
                return ShapesRed;
            if (step == 0)
                return Color.Lerp(ShapesRed, ShapesBlue, t);
            return ShapesBlue;
        }

        static Vector2 ShapesLocal(float x, float y)
        {
            return new Vector2(ShapesOrigin.x - ShapesScene.x + x * ShapesUnit, ShapesOrigin.y - ShapesScene.y + y * ShapesUnit);
        }

        static Vector2 ShapesWorld(float x, float y)
        {
            return ShapesOrigin + new Vector2(x, y) * ShapesUnit;
        }

        /// <summary>Thin outlines showing where the untouched operands sit.</summary>
        static void DrawShapesGhosts(int step, float t)
        {
            float a = 0f;
            float b = 0f;
            switch (step)
            {
                case 2: a = t; break;
                case 3: a = 1f - t; break;
                case 4: a = 1f; b = 1f; break;
                case 5: a = 1f; b = 1f; break;
                case 6: a = 1f - t; b = 1f - t; break;
                case 7: a = t; b = t; break;
                case 8: a = 1f - t; b = 1f - t; break;
                case 9: a = t; break;
                case 10: a = 1f - t; b = t; break;
                case 11: b = 1f - t; break;
            }

            var ghost = new Color(0.55f, 0.55f, 0.58f, 1f);
            if (a > 0f)
                Now.Circle(ShapesWorld(-60f, 0f), 100f * ShapesUnit).SetColor(Color.clear).SetOutline(1f, WithAlpha(ghost, 0.6f * a)).Draw();
            if (b > 0f)
                Now.Circle(ShapesWorld(60f, 0f), 100f * ShapesUnit).SetColor(Color.clear).SetOutline(1f, WithAlpha(ghost, 0.6f * b)).Draw();
        }

        static NowSdfBuilder ShapesSceneBuilder(Color fill, Vector2 cursor, float hover)
        {
            // Sphere-like shading: an inner shadow pooling away from the light
            // (distance-based, so it stays smooth across union creases), a
            // light emboss rim, a dark outline, and a drop shadow. While the
            // cursor hovers, the light swings toward it.
            Vector2 light = new Vector2(-0.6f, -0.8f);
            if (hover > 0f)
            {
                Vector2 toCursor = (cursor - ShapesOrigin).normalized;
                light = Vector2.Lerp(light, toCursor, hover * 0.8f).normalized;
            }

            NowSdfBuilder scene = NowSdf.Scene(ShapesScene, "readme-shapes-field")
                .SetFeather(1f)
                .SetShadow(new Vector2(0f, 10f), 18f, new Color(0f, 0f, 0f, 0.55f), 1f)
                .SetOutline(1.6f, new Color(0.02f, 0.02f, 0.04f, 0.92f), 0.6f)
                .SetInnerShadow(light * 18f, 56f, new Color(0f, 0f, 0.04f, 0.62f), 4f)
                .SetEmboss(-light, 0.16f + hover * 0.12f, 12f)
                .SetColor(fill)
                .UseColor();
            if (hover > 0f)
                scene = scene.SetGlow(hover * 22f, WithAlpha(new Color(1f, 0.45f, 0.42f, 1f), 0.35f * hover), 1.5f);
            return scene;
        }

        static void DrawShapesField(int step, int previous, float t, float holdSeconds, Color fill, Vector2 cursor)
        {
            float hover = 0f;
            if (step == 15 && cursor.x > 0f)
            {
                float distance = Vector2.Distance(cursor, ShapesOrigin + new Vector2(0f, 20f * ShapesUnit));
                hover = 1f - Smooth(Mathf.InverseLerp(70f, 150f, distance));
            }

            ComposeShapesField(ShapesSceneBuilder(fill, cursor, hover), step, previous, t, holdSeconds, fill).Draw();

            // Glossy sphere shading: the same composed field, rasterized as a
            // mask, clips a top-left highlight and a bottom-right shade so the
            // lighting hugs whatever shape the morph currently produces.
            using (ComposeShapesField(NowSdf.Scene(ShapesScene, "readme-shapes-mask"), step, previous, t, holdSeconds, fill).BeginMask())
            {
                Vector2 highlight = ShapesOrigin + new Vector2(-34f, -52f) * ShapesUnit;
                Vector2 shade = ShapesOrigin + new Vector2(52f, 74f) * ShapesUnit;
                Now.Gradient(ShapesScene, new Color(1f, 1f, 1f, 0.30f + hover * 0.12f), new Color(1f, 1f, 1f, 0f))
                    .SetRadial(new Vector2((highlight.x - ShapesScene.x) / ShapesScene.width, (highlight.y - ShapesScene.y) / ShapesScene.height), 0.30f)
                    .Draw();
                Now.Gradient(ShapesScene, new Color(0f, 0f, 0.03f, 0.42f), new Color(0f, 0f, 0.03f, 0f))
                    .SetRadial(new Vector2((shade.x - ShapesScene.x) / ShapesScene.width, (shade.y - ShapesScene.y) / ShapesScene.height), 0.40f)
                    .Draw();
            }
        }

        /// <summary>
        /// Applies this frame's field to a scene builder. Every step change is
        /// a distance-field morph from the previous step's finished field, so
        /// even a MoveX squashes the circle across rather than sliding it.
        /// </summary>
        static NowSdfBuilder ComposeShapesField(NowSdfBuilder scene, int step, int previous, float t, float holdSeconds, Color fill)
        {
            switch (step)
            {
                case 6:
                    // (a + b) - (a * b) needs the lens as its own operand, so the
                    // scene composes it: the union morphs in from the shell while
                    // the lens hole grows from nothing.
                    BuildShapesOnion(ShapesGraphA, fill);
                    BuildShapesUnion(ShapesGraphB, fill);
                    BuildShapesLens(ShapesGraphLens, fill, Mathf.Lerp(60f, 100f, t));
                    return scene.Morph(ShapesGraphA, ShapesGraphB, t).Subtract().Graph(ShapesGraphLens);
                case 7:
                    BuildShapesUnion(ShapesGraphA, fill);
                    BuildShapesSmoothPair(ShapesGraphB, fill, 85f);
                    BuildShapesLens(ShapesGraphLens, fill, Mathf.Lerp(100f, 60f, t));
                    return scene.Morph(ShapesGraphA, ShapesGraphB, t).Subtract().Graph(ShapesGraphLens);
                case 14:
                {
                    // heart.Scale(1 + Sin(t * PI) * 0.2f): the pulse starts once the
                    // transition has settled and the color has turned red.
                    float scale = 1f + Mathf.Sin(holdSeconds * Mathf.PI) * 0.2f * Smooth(Mathf.InverseLerp(0f, 0.25f, holdSeconds));
                    BuildShapesHeart(ShapesGraphA, fill, 1f);
                    BuildShapesHeart(ShapesGraphB, fill, scale);
                    return scene.Morph(ShapesGraphA, ShapesGraphB, t);
                }
                case 15:
                {
                    // heart.Mix(b.Rotate(time * 4), 0.1f): a real morph toward a
                    // circle orbiting the origin. The mix ramps in with the
                    // transition and back out before the wrap so the last frame
                    // is a still heart.
                    int frames = ShapesSteps[15].Frames;
                    float remaining = (frames - ShapesTransitionFrames) / ShapesFps - holdSeconds;
                    float mix = 0.1f * t * Smooth(Mathf.InverseLerp(0f, 0.5f, remaining));
                    float angle = holdSeconds * 4f;
                    BuildShapesHeart(ShapesGraphA, fill, 1f);
                    BuildShapesCircleAt(ShapesGraphOrbit, fill, new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 60f, 100f);
                    return scene.Morph(ShapesGraphA, ShapesGraphOrbit, mix);
                }
            }

            BuildShapesStepGraph(ShapesGraphA, previous, fill);
            BuildShapesStepGraph(ShapesGraphB, step, fill);
            return scene.Morph(ShapesGraphA, ShapesGraphB, t);
        }

        /// <summary>The finished field of a step, used as a morph endpoint.</summary>
        static void BuildShapesStepGraph(NowSdfGraph graph, int step, Color fill)
        {
            switch (step)
            {
                case 0: BuildShapesCircle(graph, fill, 0f, 100f); break;
                case 1: BuildShapesCircle(graph, fill, -60f, 100f); break;
                case 2: BuildShapesCircle(graph, fill, 60f, 100f); break;
                case 3: BuildShapesUnion(graph, fill); break;
                case 4: BuildShapesLens(graph, fill, 100f); break;
                case 5: BuildShapesOnion(graph, fill); break;
                case 6: BuildShapesUnion(graph, fill); break; // lens subtracted at scene level
                case 7: BuildShapesSmoothPair(graph, fill, 85f); break;
                case 8: BuildShapesSmoothPair(graph, fill, 0f); break;
                case 9: BuildShapesSmoothPairMinus(graph, fill, -60f); break;
                case 10: BuildShapesSmoothPairMinus(graph, fill, 60f); break;
                case 11: BuildShapesTriangle(graph, fill); break;
                case 12: BuildShapesTriangleUnion(graph, fill); break;
                default: BuildShapesHeart(graph, fill, 1f); break;
            }
        }

        static void BuildShapesCircle(NowSdfGraph graph, Color fill, float x, float radius)
        {
            BuildShapesCircleAt(graph, fill, new Vector2(x, 0f), radius);
        }

        static void BuildShapesCircleAt(NowSdfGraph graph, Color fill, Vector2 center, float radius)
        {
            graph.Clear().SetColor(fill).UseColor()
                .Circle(ShapesLocal(center.x, center.y), radius * ShapesUnit);
        }

        static void BuildShapesUnion(NowSdfGraph graph, Color fill)
        {
            graph.Clear().SetColor(fill).UseColor()
                .Circle(ShapesLocal(-60f, 0f), 100f * ShapesUnit)
                .Union()
                .Circle(ShapesLocal(60f, 0f), 100f * ShapesUnit);
        }

        static void BuildShapesLens(NowSdfGraph graph, Color fill, float radius)
        {
            graph.Clear().SetColor(fill).UseColor()
                .Circle(ShapesLocal(-60f, 0f), radius * ShapesUnit)
                .Intersect()
                .Circle(ShapesLocal(60f, 0f), radius * ShapesUnit);
        }

        /// <summary>
        /// Onion(10) is the band within 10 units of the edge: the union grown
        /// by 10 minus the union shrunk by 10, written as two sequential
        /// subtractions.
        /// </summary>
        static void BuildShapesOnion(NowSdfGraph graph, Color fill)
        {
            graph.Clear().SetColor(fill).UseColor()
                .Circle(ShapesLocal(-60f, 0f), 110f * ShapesUnit)
                .Union()
                .Circle(ShapesLocal(60f, 0f), 110f * ShapesUnit)
                .Subtract()
                .Circle(ShapesLocal(-60f, 0f), 90f * ShapesUnit)
                .Subtract()
                .Circle(ShapesLocal(60f, 0f), 90f * ShapesUnit);
        }

        static void BuildShapesSmoothPair(NowSdfGraph graph, Color fill, float spread)
        {
            graph.Clear().SetColor(fill).UseColor()
                .Circle(ShapesLocal(-60f - spread, 0f), 100f * ShapesUnit)
                .SmoothUnion(100f * ShapesUnit)
                .Circle(ShapesLocal(60f + spread, 0f), 100f * ShapesUnit);
        }

        static void BuildShapesSmoothPairMinus(NowSdfGraph graph, Color fill, float holeX)
        {
            graph.Clear().SetColor(fill).UseColor()
                .Circle(ShapesLocal(-60f, 0f), 100f * ShapesUnit)
                .SmoothUnion(100f * ShapesUnit)
                .Circle(ShapesLocal(60f, 0f), 100f * ShapesUnit)
                .Subtract()
                .Circle(ShapesLocal(holeX, 0f), 80f * ShapesUnit);
        }

        static void BuildShapesTriangle(NowSdfGraph graph, Color fill)
        {
            graph.Clear().SetColor(fill).UseColor()
                .Triangle(ShapesLocal(-100f, 0f), ShapesLocal(100f, 0f), ShapesLocal(0f, 200f));
        }

        static void BuildShapesTriangleUnion(NowSdfGraph graph, Color fill)
        {
            graph.Clear().SetColor(fill).UseColor()
                .Triangle(ShapesLocal(-100f, 0f), ShapesLocal(100f, 0f), ShapesLocal(0f, 200f))
                .Union()
                .Circle(ShapesLocal(-60f, 0f), 100f * ShapesUnit)
                .Union()
                .Circle(ShapesLocal(60f, 0f), 100f * ShapesUnit);
        }

        static void BuildShapesHeart(NowSdfGraph graph, Color fill, float scale)
        {
            // Scale about the visual center so the pulse does not drift.
            const float pivotY = 50f;
            float s = scale;
            Vector2 P(float x, float y) => ShapesLocal(x * s, pivotY + (y - pivotY) * s);
            graph.Clear().SetColor(fill).UseColor()
                .Circle(P(-60f, 0f), 100f * ShapesUnit * s)
                .Union()
                .Circle(P(60f, 0f), 100f * ShapesUnit * s)
                .SmoothUnion(100f * ShapesUnit * s)
                .Triangle(P(-100f, 0f), P(100f, 0f), P(0f, 200f));
        }

        static Vector2 ShapesCursor(int step, int local)
        {
            if (step != 15)
                return new Vector2(-100f, -100f);

            int hold = ShapesSteps[15].Frames;
            var enter = new Vector2(820f, 470f);
            var rest = ShapesOrigin + new Vector2(38f, 30f);
            float f = local;
            if (f < 12f)
                return new Vector2(-100f, -100f);
            if (f < 30f)
                return Vector2.Lerp(enter, rest, Smooth(Mathf.InverseLerp(12f, 30f, f)));
            if (f < hold - 22f)
            {
                float wander = (f - 30f) / ShapesFps;
                return rest + new Vector2(Mathf.Sin(wander * 1.7f) * 26f, Mathf.Cos(wander * 1.3f) * 16f);
            }
            if (f < hold - 6f)
            {
                float wander = (hold - 22f - 30f) / ShapesFps;
                Vector2 last = rest + new Vector2(Mathf.Sin(wander * 1.7f) * 26f, Mathf.Cos(wander * 1.3f) * 16f);
                return Vector2.Lerp(last, enter, Smooth(Mathf.InverseLerp(hold - 22f, hold - 6f, f)));
            }
            return new Vector2(-100f, -100f);
        }

        /// <summary>
        /// Captions change the way the reference does: the part of the line
        /// shared with the previous caption stays put (sliding to keep the
        /// line centered), the old suffix drops away, and the new suffix
        /// rises in with the built-in FadeUp text animation.
        /// </summary>
        static void DrawShapesCaption(int step, int previous, int local)
        {
            const float y = 452f;
            string current = ShapesSteps[step].caption;
            string old = ShapesSteps[previous].caption;
            int shared = 0;
            while (shared < current.Length && shared < old.Length && current[shared] == old[shared])
                ++shared;

            float size = ShapesCodeSize(current, 21f, 880f);
            float advance = ShapesCodeAdvance(size);
            float slide = Smooth(Mathf.InverseLerp(0f, ShapesTransitionFrames, local));
            float left = Mathf.Lerp(480f - old.Length * advance * 0.5f, 480f - current.Length * advance * 0.5f, slide);
            float leave = 1f - Smooth(Mathf.InverseLerp(0f, 5f, local));

            DrawShapesCode(current, 0, shared, left, y, advance, size, 1f, -1f);
            if (leave > 0f && shared < old.Length)
                DrawShapesCode(old, shared, old.Length, left, y + (1f - leave) * 12f, advance, size, leave, -1f);
            if (shared < current.Length)
                DrawShapesCode(current, shared, current.Length, left, y, advance, size, 1f, local / ShapesFps);

            // Setup lines: the old one fades out, the new one fades in above
            // the chain. Steps that share a setup keep it steady.
            string setup = ShapesSteps[step].setup;
            string oldSetup = ShapesSteps[previous].setup;
            if (setup == oldSetup)
            {
                DrawShapesSetup(setup, 1f);
            }
            else
            {
                DrawShapesSetup(oldSetup, 1f - Smooth(Mathf.InverseLerp(0f, 4f, local)));
                DrawShapesSetup(setup, Smooth(Mathf.InverseLerp(4f, 12f, local)));
            }
        }

        static void DrawShapesSetup(string setup, float alpha)
        {
            if (alpha <= 0f || setup.Length == 0)
                return;

            string[] lines = setup.Split('\n');
            float size = 14f;
            for (int i = 0; i < lines.Length; ++i)
                size = Mathf.Min(size, ShapesCodeSize(lines[i], 14f, 880f));
            float advance = ShapesCodeAdvance(size);
            float top = 418f - (lines.Length - 1) * 18f;
            for (int i = 0; i < lines.Length; ++i)
            {
                string line = lines[i];
                DrawShapesCode(line, 0, line.Length, 480f - line.Length * advance * 0.5f, top + i * 18f, advance, size, alpha * 0.72f, -1f);
            }
        }

        /// <summary>Largest size up to <paramref name="preferred"/> at which the line fits in <paramref name="maxWidth"/>.</summary>
        static float ShapesCodeSize(string code, float preferred, float maxWidth)
        {
            float ratio = ShapesCodeAdvance(preferred) / preferred;
            return Mathf.Min(preferred, maxWidth / Mathf.Max(1f, code.Length * ratio));
        }

        static float ShapesCodeAdvance(float size)
        {
            return Now.Text(new NowRect(0f, 0f, 100f, 40f)).SetFont(GetShapesMonoFont()).SetFontSize(size).Measure("M").x;
        }

        /// <summary>
        /// Draws characters <paramref name="from"/>..<paramref name="to"/> of one
        /// line of code laid out on a monospace grid starting at
        /// <paramref name="left"/>, colored by a tiny tokenizer. A non-negative
        /// <paramref name="animationTime"/> plays a FadeUp entrance staggered
        /// from the first drawn character.
        /// </summary>
        static void DrawShapesCode(string code, int from, int to, float left, float y, float advance, float size, float alpha, float animationTime)
        {
            NowFontAsset mono = GetShapesMonoFont();
            TokenizeShapesCode(code, ShapesTokens);

            int index = 0;
            for (int i = 0; i < ShapesTokens.Count; ++i)
            {
                (string text, Color color) = ShapesTokens[i];
                int start = Mathf.Max(index, from);
                int end = Mathf.Min(index + text.Length, to);
                if (start < end && !string.IsNullOrWhiteSpace(text))
                {
                    string piece = text.Substring(start - index, end - start);
                    var rect = new NowRect(left + start * advance, y - size * 0.7f, piece.Length * advance + 6f, size * 1.6f);
                    var label = Now.Text(rect).SetFont(mono).SetFontSize(size).SetColor(WithAlpha(color, alpha));
                    if (animationTime >= 0f)
                    {
                        label = label
                            .SetAnimation(NowTextAnimations.FadeUp(10f, 0.28f, 0.016f).SetDelay((start - from) * 0.016f))
                            .SetTime(animationTime);
                    }
                    label.Draw(piece);
                }
                index += text.Length;
            }
        }

        static void TokenizeShapesCode(string code, List<(string, Color)> tokens)
        {
            tokens.Clear();
            int i = 0;
            while (i < code.Length)
            {
                char c = code[i];
                int start = i;
                if (c == '/' && i + 1 < code.Length && code[i + 1] == '/')
                {
                    tokens.Add((code.Substring(start), ShapesComment));
                    return;
                }
                if (char.IsLetter(c) || c == '_')
                {
                    while (i < code.Length && (char.IsLetterOrDigit(code[i]) || code[i] == '_'))
                        ++i;
                    string word = code.Substring(start, i - start);
                    bool member = start > 0 && code[start - 1] == '.';
                    Color color = member ? ShapesMethod
                        : IsShapesKeyword(word) ? ShapesKeyword
                        : IsShapesType(word) ? ShapesType
                        : ShapesIdentifier;
                    tokens.Add((word, color));
                }
                else if (char.IsDigit(c) || ((c == '-' || c == '+') && i + 1 < code.Length && char.IsDigit(code[i + 1]) && (start == 0 || code[start - 1] == '(' || code[start - 1] == ' ')))
                {
                    ++i;
                    while (i < code.Length && (char.IsDigit(code[i]) || code[i] == '.' || code[i] == 'f'))
                        ++i;
                    tokens.Add((code.Substring(start, i - start), ShapesNumber));
                }
                else if (c == ' ')
                {
                    while (i < code.Length && code[i] == ' ')
                        ++i;
                    tokens.Add((code.Substring(start, i - start), ShapesPunctuation));
                }
                else
                {
                    ++i;
                    tokens.Add((code.Substring(start, 1), ShapesPunctuation));
                }
            }
        }

        static bool IsShapesKeyword(string word)
        {
            return word == "var" || word == "new" || word == "using" || word == "float";
        }

        static bool IsShapesType(string word)
        {
            return word == "NowSdf" || word == "Vector2" || word == "Now" || word == "Mathf" || word == "NowRect";
        }

        static void DrawShapesCenteredText(Vector2 center, string value, float size, Color color, bool bold)
        {
            var probe = Now.Text(new NowRect(0f, 0f, 960f, size * 1.5f)).SetFontSize(size);
            if (bold)
                probe = probe.SetBold();
            Vector2 measured = probe.Measure(value);
            DrawText(new NowRect(center.x - measured.x * 0.5f, center.y - measured.y * 0.5f, measured.x + 4f, measured.y + 4f), value, size, color, bold);
        }

        static NowFontAsset GetShapesMonoFont()
        {
            if (_shapesMonoFont != null)
                return _shapesMonoFont;

            _shapesMonoFont = AssetDatabase.LoadAssetAtPath<NowFontAsset>(ShapesMonoFontPath);
            if (_shapesMonoFont == null)
                throw new System.IO.FileNotFoundException($"README shapes loop needs the JetBrains Mono font at '{ShapesMonoFontPath}'.");
            return _shapesMonoFont;
        }
    }
}
