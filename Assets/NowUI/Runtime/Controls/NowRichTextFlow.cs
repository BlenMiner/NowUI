using System.Collections.Generic;
using UnityEngine;

namespace NowUI
{
    public delegate void NowRichTextInlineDraw(in NowRichTextRun run, NowRect mask);

    public struct NowRichTextCursor
    {
        public float x;
        public float y;
        public float lineStart;
        public float limit;
        public float lineHeight;
        public int lineIndex;
    }

    public struct NowRichTextStyle
    {
        public float fontSize;

        public NowFontStyle fontStyle;

        public Vector4 color;

        public bool hasColor;

        public bool underline;

        public bool strikethrough;

        public NowRichTextStyle(float fontSize, NowFontStyle fontStyle = NowFontStyle.Regular)
        {
            this.fontSize = fontSize;
            this.fontStyle = fontStyle;
            color = default;
            hasColor = false;
            underline = false;
            strikethrough = false;
        }

        public NowRichTextStyle SetColor(Color color)
        {
            this.color = color;
            hasColor = true;
            return this;
        }

        public NowRichTextStyle SetColor(Vector4 color)
        {
            this.color = color;
            hasColor = true;
            return this;
        }

        public NowRichTextStyle SetUnderline(bool value = true)
        {
            underline = value;
            return this;
        }

        public NowRichTextStyle SetStrikethrough(bool value = true)
        {
            strikethrough = value;
            return this;
        }
    }

    /// <summary>A positioned styled range. Unlike NowTextLine, this models mixed-size and mixed-style text.</summary>
    public struct NowRichTextRun
    {
        public NowRect rect;
        public int start;
        public int length;
        public float fontSize;
        public NowFontStyle fontStyle;
        public Vector4 color;
        public bool hasColor;
        public NowFontAsset font;
        public bool underline;
        public bool strikethrough;
        public bool isInline;
        public object payload;
        public NowRichTextInlineDraw drawInline;
        public int lineIndex;
        public int tag;
    }

    public struct NowRichTextLine
    {
        public float y;
        public float height;
        public float width;
        public int firstRun;
        public int runCount;
    }

    public struct NowRichTextHit
    {
        public bool valid;
        public int lineIndex;
        public int runIndex;
        public int textIndex;
        public int runTextIndex;
        public int tag;
        public NowRect rect;
    }

    public struct NowRichTextTagPayload
    {
        public string name;
        public string value;
        public object payload;
    }

    public struct NowRichTextInline
    {
        public int index;
        public float width;
        public float height;
        public int tag;
        public object payload;
        public NowRichTextInlineDraw draw;
    }

    /// <summary>
    /// Retained layout data for one rich-text region: positioned styled runs,
    /// line summaries and the plain-text projection used for selection/copy.
    /// </summary>
    public sealed class NowRichTextLayout
    {
        readonly System.Text.StringBuilder _text = new System.Text.StringBuilder(128);
        string _textCache;
        float _lastSelectionY = float.MinValue;

        public readonly List<NowRichTextRun> runs = new List<NowRichTextRun>(32);

        public readonly List<NowRichTextLine> lines = new List<NowRichTextLine>(8);

        public readonly List<NowTextSelectionLine> selectionLines = new List<NowTextSelectionLine>(32);

        public readonly List<NowRichTextTagPayload> tags = new List<NowRichTextTagPayload>(8);

        public string text => _textCache ??= _text.ToString();

        public int textLength => _text.Length;

        public NowRect bounds { get; private set; }

        public void Clear()
        {
            _text.Length = 0;
            _textCache = null;
            _lastSelectionY = float.MinValue;
            runs.Clear();
            lines.Clear();
            selectionLines.Clear();
            tags.Clear();
            bounds = default;
        }

        public int AddTag(string name, string value, object payload = null)
        {
            tags.Add(new NowRichTextTagPayload
            {
                name = name ?? string.Empty,
                value = value ?? string.Empty,
                payload = payload
            });

            return tags.Count;
        }

        public string GetTagValue(int tag)
        {
            int index = tag - 1;
            return index >= 0 && index < tags.Count ? tags[index].value : string.Empty;
        }

        public object GetTagPayload(int tag)
        {
            int index = tag - 1;
            return index >= 0 && index < tags.Count ? tags[index].payload : null;
        }

        public bool TryGetTag(int tag, out NowRichTextTagPayload payload)
        {
            int index = tag - 1;

            if (index >= 0 && index < tags.Count)
            {
                payload = tags[index];
                return true;
            }

            payload = default;
            return false;
        }

        public void AddTagPayloads(IReadOnlyList<NowRichTextTagPayload> payloads)
        {
            if (payloads == null)
                return;

            for (int i = 0; i < payloads.Count; ++i)
                tags.Add(payloads[i]);
        }

        public NowRichTextCursor Cursor(float x, float y, float limit, float lineHeight)
        {
            return new NowRichTextCursor
            {
                x = x,
                y = y,
                lineStart = x,
                limit = limit,
                lineHeight = lineHeight,
                lineIndex = 0
            };
        }

        public NowRichTextRun AddRun(
            ref NowRichTextCursor cursor,
            string source,
            int start,
            int length,
            NowFontAsset font,
            NowRichTextStyle style,
            bool wrap,
            int tag = 0,
            bool selectable = true,
            bool separate = true)
        {
            var run = NowRichTextFlow.AddRun(ref cursor, source, start, length, font, style, wrap, runs, tag);

            if (selectable)
            {
                run.start = Capture(source, start, length, run.rect, run.fontSize, run.fontStyle, separate);
                runs[runs.Count - 1] = run;
            }

            return run;
        }

        public NowRect AddWord(
            ref NowRichTextCursor cursor,
            string word,
            NowFontAsset font,
            float fontSize,
            NowFontStyle style,
            bool wrap,
            int tag = 0,
            bool selectable = true,
            bool separate = true)
        {
            return AddRun(ref cursor, word, 0, word.Length, font,
                new NowRichTextStyle(fontSize, style), wrap, tag, selectable, separate).rect;
        }

        public int AppendText(string text)
        {
            int start = _text.Length;
            _text.Append(text);
            _textCache = null;
            return start;
        }

        public int Capture(string text, NowRect rect, float fontSize, NowFontStyle fontStyle = NowFontStyle.Regular, bool separate = true)
        {
            return Capture(text, 0, text?.Length ?? 0, rect, fontSize, fontStyle, separate);
        }

        public int Capture(string text, int sourceStart, int length, NowRect rect, float fontSize, NowFontStyle fontStyle = NowFontStyle.Regular, bool separate = true)
        {
            if (separate && _text.Length > 0)
                _text.Append(rect.y > _lastSelectionY + 0.5f ? '\n' : ' ');

            _lastSelectionY = rect.y;
            int start = _text.Length;
            _text.Append(text, sourceStart, length);
            _textCache = null;

            selectionLines.Add(new NowTextSelectionLine
            {
                rect = rect,
                start = start,
                length = length,
                fontSize = fontSize,
                fontStyle = fontStyle
            });

            return start;
        }

        public NowRichTextRun AddPlacedRun(
            string text,
            NowRect rect,
            NowRichTextStyle style,
            int lineIndex = 0,
            int tag = 0,
            bool selectable = true,
            NowFontAsset font = null,
            bool separate = true)
        {
            int length = text?.Length ?? 0;
            var run = new NowRichTextRun
            {
                rect = rect,
                start = -1,
                length = length,
                fontSize = style.fontSize,
                fontStyle = style.fontStyle,
                color = style.color,
                hasColor = style.hasColor,
                font = font,
                underline = style.underline,
                strikethrough = style.strikethrough,
                lineIndex = lineIndex,
                tag = tag
            };

            if (selectable && length > 0)
                run.start = Capture(text, rect, style.fontSize, style.fontStyle, separate);

            runs.Add(run);
            return run;
        }

        public NowRichTextRun AddInline(
            ref NowRichTextCursor cursor,
            float width,
            float height,
            int tag = 0,
            object payload = null,
            NowRichTextInlineDraw draw = null,
            bool wrap = true)
        {
            width = Mathf.Max(width, 0f);
            height = Mathf.Max(height, 0f);
            NowRichTextFlow.WrapBefore(ref cursor, width, wrap);

            int start = AppendText("\uFFFC");
            var rect = new NowRect(
                cursor.x,
                cursor.y + Mathf.Max(cursor.lineHeight - height, 0f) * 0.5f,
                width,
                Mathf.Max(height, cursor.lineHeight));

            var run = new NowRichTextRun
            {
                rect = rect,
                start = start,
                length = 1,
                lineIndex = cursor.lineIndex,
                tag = tag,
                isInline = true,
                payload = payload,
                drawInline = draw
            };

            runs.Add(run);
            selectionLines.Add(new NowTextSelectionLine
            {
                rect = rect,
                start = start,
                length = 1,
                fontSize = cursor.lineHeight
            });

            cursor.x += width;
            return run;
        }

        public void CompleteLines()
        {
            NowRichTextFlow.BuildLines(runs, lines);
            bounds = NowRichTextFlow.Bounds(runs);
        }

        public bool TryHit(Vector2 point, out NowRichTextHit hit)
        {
            return NowRichTextFlow.TryHit(text, runs, lines, point, false, out hit);
        }

        public int HitTextIndex(Vector2 point)
        {
            return NowRichTextFlow.HitTextIndex(text, runs, lines, point);
        }

        public bool TryHitRun(Vector2 point, out int runIndex)
        {
            if (TryHit(point, out var hit))
            {
                runIndex = hit.runIndex;
                return runIndex >= 0;
            }

            runIndex = -1;
            return false;
        }

        public NowRect TextIndexRect(int textIndex)
        {
            return NowRichTextFlow.TextIndexRect(text, runs, textIndex);
        }
    }

    /// <summary>
    /// Helpers for custom rich text renderers that layout styled runs while
    /// still producing reusable hit-test and selection geometry.
    /// </summary>
    public static class NowRichTextFlow
    {
        public static void NewLine(ref NowRichTextCursor cursor)
        {
            cursor.x = cursor.lineStart;
            cursor.y += cursor.lineHeight;
            ++cursor.lineIndex;
        }

        public static bool WrapBefore(ref NowRichTextCursor cursor, float width, bool enabled)
        {
            if (!enabled || cursor.x <= cursor.lineStart || cursor.x + width <= cursor.limit)
                return false;

            NewLine(ref cursor);
            return true;
        }

        public static NowRichTextRun AddRun(
            ref NowRichTextCursor cursor,
            string source,
            int start,
            int length,
            NowFontAsset font,
            NowRichTextStyle style,
            bool wrap,
            List<NowRichTextRun> runs = null,
            int tag = 0)
        {
            float width = NowTextMetrics.Advance(source, font, style.fontSize, style.fontStyle, start, length);
            WrapBefore(ref cursor, width, wrap);

            var run = new NowRichTextRun
            {
                rect = new NowRect(cursor.x, cursor.y, width + 1f, cursor.lineHeight),
                start = start,
                length = length,
                fontSize = style.fontSize,
                fontStyle = style.fontStyle,
                color = style.color,
                hasColor = style.hasColor,
                font = font,
                underline = style.underline,
                strikethrough = style.strikethrough,
                lineIndex = cursor.lineIndex,
                tag = tag
            };

            runs?.Add(run);
            cursor.x += width;
            return run;
        }

        public static NowRect Bounds(IReadOnlyList<NowRichTextRun> runs)
        {
            if (runs == null || runs.Count == 0)
                return default;

            NowRect bounds = runs[0].rect;

            for (int i = 1; i < runs.Count; ++i)
                bounds = bounds.Union(runs[i].rect);

            return bounds;
        }

        public static NowRect AddWord(
            ref NowRichTextCursor cursor,
            string source,
            int start,
            int length,
            NowFontAsset font,
            float fontSize,
            NowFontStyle style,
            bool wrap)
        {
            return AddRun(ref cursor, source, start, length, font,
                new NowRichTextStyle(fontSize, style), wrap).rect;
        }

        public static void BuildLines(IReadOnlyList<NowRichTextRun> runs, List<NowRichTextLine> lines)
        {
            lines.Clear();

            if (runs == null || runs.Count == 0)
                return;

            int firstRun = 0;
            int lineIndex = runs[0].lineIndex;
            float y = runs[0].rect.y;
            float height = runs[0].rect.height;
            float left = runs[0].rect.x;
            float width = runs[0].rect.width;

            for (int i = 1; i < runs.Count; ++i)
            {
                var run = runs[i];

                if (run.lineIndex != lineIndex)
                {
                    lines.Add(new NowRichTextLine
                    {
                        y = y,
                        height = height,
                        width = width,
                        firstRun = firstRun,
                        runCount = i - firstRun
                    });

                    firstRun = i;
                    lineIndex = run.lineIndex;
                    y = run.rect.y;
                    height = run.rect.height;
                    left = run.rect.x;
                    width = run.rect.width;
                    continue;
                }

                height = Mathf.Max(height, run.rect.height);
                width = Mathf.Max(width, run.rect.xMax - left);
            }

            lines.Add(new NowRichTextLine
            {
                y = y,
                height = height,
                width = width,
                firstRun = firstRun,
                runCount = runs.Count - firstRun
            });
        }

        public static bool TryHit(
            string text,
            IReadOnlyList<NowRichTextRun> runs,
            IReadOnlyList<NowRichTextLine> lines,
            Vector2 point,
            bool clamp,
            out NowRichTextHit hit)
        {
            hit = default;

            if (runs == null || runs.Count == 0 || lines == null || lines.Count == 0)
                return false;

            int lineIndex = -1;

            for (int i = 0; i < lines.Count; ++i)
            {
                var line = lines[i];

                if (point.y >= line.y && point.y <= line.y + line.height)
                {
                    lineIndex = i;
                    break;
                }
            }

            if (lineIndex < 0)
            {
                if (!clamp)
                    return false;

                lineIndex = point.y < lines[0].y ? 0 : lines.Count - 1;
            }

            var targetLine = lines[lineIndex];

            if (targetLine.runCount <= 0)
                return false;

            int firstRun = targetLine.firstRun;
            int lastRun = firstRun + targetLine.runCount - 1;

            for (int i = firstRun; i <= lastRun; ++i)
            {
                var run = runs[i];

                if (point.x >= run.rect.x && point.x <= run.rect.xMax)
                    return HitRun(text, run, i, lineIndex, point.x - run.rect.x, out hit);
            }

            if (!clamp)
            {
                var first = runs[firstRun];
                var last = runs[lastRun];

                if (point.x < first.rect.x || point.x > last.rect.xMax)
                    return false;
            }

            int nearestRun = firstRun;
            float nearestDistance = float.MaxValue;

            for (int i = firstRun; i <= lastRun; ++i)
            {
                var run = runs[i];
                float distance = point.x < run.rect.x
                    ? run.rect.x - point.x
                    : point.x - run.rect.xMax;

                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestRun = i;
                }
            }

            var nearest = runs[nearestRun];
            float localX = point.x < nearest.rect.x ? 0f : nearest.rect.width;
            return HitRun(text, nearest, nearestRun, lineIndex, localX, out hit);
        }

        public static int HitTextIndex(
            string text,
            IReadOnlyList<NowRichTextRun> runs,
            IReadOnlyList<NowRichTextLine> lines,
            Vector2 point)
        {
            return TryHit(text, runs, lines, point, true, out var hit) ? hit.textIndex : 0;
        }

        public static NowRect TextIndexRect(string text, IReadOnlyList<NowRichTextRun> runs, int textIndex)
        {
            if (runs == null || runs.Count == 0)
                return default;

            text ??= string.Empty;
            textIndex = Mathf.Clamp(textIndex, 0, text.Length);

            for (int i = 0; i < runs.Count; ++i)
            {
                var run = runs[i];

                if (run.start < 0)
                    continue;

                int end = run.start + run.length;

                if (textIndex < run.start || textIndex > end)
                    continue;

                float x = run.rect.x + Advance(text, run, run.start, textIndex - run.start);
                return new NowRect(x, run.rect.y, 1f, run.rect.height);
            }

            var last = runs[runs.Count - 1];
            return new NowRect(last.rect.xMax, last.rect.y, 1f, last.rect.height);
        }

        static bool HitRun(
            string text,
            in NowRichTextRun run,
            int runIndex,
            int lineIndex,
            float localX,
            out NowRichTextHit hit)
        {
            int runTextIndex = HitRunTextIndex(text, run, localX);
            int textIndex = run.start >= 0 ? run.start + runTextIndex : runTextIndex;

            hit = new NowRichTextHit
            {
                valid = true,
                lineIndex = lineIndex,
                runIndex = runIndex,
                runTextIndex = runTextIndex,
                textIndex = textIndex,
                tag = run.tag,
                rect = run.rect
            };

            return true;
        }

        static int HitRunTextIndex(string text, in NowRichTextRun run, float x)
        {
            if (run.start < 0 || run.length <= 0 || string.IsNullOrEmpty(text) || run.font == null || x <= 0f)
                return 0;

            int index = run.start;
            int end = Mathf.Min(run.start + run.length, text.Length);
            float advance = 0f;

            while (index < end)
            {
                int next = NowTextEdit.NextIndex(text, index);
                float glyph = Advance(text, run, index, next - index);

                if (advance + glyph * 0.5f >= x)
                    return index - run.start;

                advance += glyph;
                index = next;
            }

            return end - run.start;
        }

        static float Advance(string text, in NowRichTextRun run, int start, int length)
        {
            return run.font == null || length <= 0
                ? 0f
                : run.font.MeasureText(text, start, length, run.fontSize, run.fontStyle).x;
        }

        public static NowTextSelectionLine ToSelectionLine(in NowRichTextRun run)
        {
            return new NowTextSelectionLine
            {
                rect = run.rect,
                start = run.start,
                length = run.length,
                fontSize = run.fontSize,
                fontStyle = run.fontStyle
            };
        }

        public static void CaptureSegment(
            string text,
            NowRect rect,
            float fontSize,
            NowFontStyle fontStyle,
            System.Text.StringBuilder builder,
            List<NowTextSelectionLine> selectionLines,
            ref float lastY)
        {
            if (builder.Length > 0)
                builder.Append(rect.y > lastY + 0.5f ? '\n' : ' ');

            lastY = rect.y;
            int flatStart = builder.Length;
            builder.Append(text);

            selectionLines.Add(new NowTextSelectionLine
            {
                rect = rect,
                start = flatStart,
                length = text.Length,
                fontSize = fontSize,
                fontStyle = fontStyle
            });
        }

        public static void CaptureRun(
            string text,
            in NowRichTextRun run,
            System.Text.StringBuilder builder,
            List<NowTextSelectionLine> selectionLines,
            ref float lastY)
        {
            CaptureSegment(text, run.rect, run.fontSize, run.fontStyle, builder, selectionLines, ref lastY);
        }
    }
}
