using UnityEngine;

public class MailClientMockup : MonoBehaviour
{
    [SerializeField] NowFont _font;

    [SerializeField, Range(0.75f, 1.35f)] float _scale = 1f;

    enum TextAlign
    {
        Left,
        Center,
        Right
    }

    struct MailItem
    {
        public readonly string sender;
        public readonly string subject;
        public readonly string preview;
        public readonly string time;
        public readonly bool unread;

        public MailItem(string sender, string subject, string preview, string time, bool unread)
        {
            this.sender = sender;
            this.subject = subject;
            this.preview = preview;
            this.time = time;
            this.unread = unread;
        }
    }

    static readonly MailItem[] _mail = {
        new MailItem("GitHub", "NowUI WebGL artifact is ready", "The nowui-msdf-webgl artifact finished and is available to import.", "10:42 AM", true),
        new MailItem("Unity Cloud", "Build report for WebGL", "Build time was 2m 14s. No player errors were reported.", "9:18 AM", true),
        new MailItem("Maya Chen", "Design pass notes", "The compact sidebar feels better, but the reader pane needs a quieter header.", "Yesterday", false),
        new MailItem("Linear", "UI polish tasks moved", "Runtime fonts, native artifacts, and docs were moved into the next milestone.", "Yesterday", false),
        new MailItem("Actions", "macOS wrapper job failed", "The WebGL job passed. Desktop wrapper linkage still needs a separate pass.", "Mon", true),
        new MailItem("Sam Rivera", "Mock inbox content", "Added more realistic rows so screenshots look like a product workflow.", "Mon", false),
        new MailItem("Calendar", "Weekly sync moved", "The Tuesday sync was moved to 2:30 PM.", "Fri", false),
        new MailItem("Figma", "Comments on the toolbar", "Three comments were added to the mail client mockup frame.", "Fri", false)
    };

    static readonly string[] _labels = {
        "Inbox",
        "Starred",
        "Snoozed",
        "Sent",
        "Drafts",
        "Archive"
    };

    static Color Rgb(int r, int g, int b, float a = 1f)
    {
        return new Color(r / 255f, g / 255f, b / 255f, a);
    }

    static Vector4 Rect(float x, float y, float width, float height)
    {
        return new Vector4(x, y, width, height);
    }

    static Vector4 Inflate(Vector4 rect, float horizontal, float vertical)
    {
        return new Vector4(rect.x - horizontal, rect.y - vertical, rect.z + horizontal * 2f, rect.w + vertical * 2f);
    }

    void DrawRect(Vector4 rect, Color color, float radius = 0f)
    {
        NowUI.Rectangle(rect)
            .SetColor(color)
            .SetRadius(radius)
            .Draw();
    }

    void DrawOutline(Vector4 rect, Color color, float outline = 1f, float radius = 0f)
    {
        NowUI.Rectangle(rect)
            .SetColor(new Color(1f, 1f, 1f, 0f))
            .SetOutlineColor(color)
            .SetOutline(outline)
            .SetRadius(radius)
            .Draw();
    }

    void DrawTextCentered(string text, Vector4 rect, float size, Color color)
    {
        DrawText(text, rect, size, color, TextAlign.Center, true);
    }

    void DrawTextRight(string text, Vector4 rect, float size, Color color)
    {
        DrawText(text, rect, size, color, TextAlign.Right, true);
    }

    void DrawText(string text, Vector4 rect, float size, Color color, TextAlign align = TextAlign.Left, bool fit = true)
    {
        if (_font == null || string.IsNullOrEmpty(text))
            return;

        float fontSize = size * _scale;
        string fitted = fit ? FitText(text, fontSize, rect.z) : text;

        if (string.IsNullOrEmpty(fitted))
            return;

        Vector2 measured = _font.MeasureText(fitted, fontSize);
        Vector4 bounds = _font.MeasureTextBounds(fitted, fontSize);
        bool hasBounds = bounds.z > 0 && bounds.w > 0;
        float visualWidth = hasBounds ? bounds.z : measured.x;
        float visualHeight = hasBounds ? bounds.w : measured.y;
        Vector4 textRect = rect;

        if (align == TextAlign.Center)
            textRect.x += Mathf.Max(0, (rect.z - visualWidth) * 0.5f) - (hasBounds ? bounds.x : 0);
        else if (align == TextAlign.Right)
            textRect.x += Mathf.Max(0, rect.z - visualWidth) - (hasBounds ? bounds.x : 0);

        textRect.y += Mathf.Max(0, (rect.w - visualHeight) * 0.5f) - (hasBounds ? bounds.y : 0);

        Vector4 mask = hasBounds
            ? Inflate(Rect(textRect.x + bounds.x, textRect.y + bounds.y, bounds.z, bounds.w), 3f, 3f)
            : Inflate(rect, 8f, 10f);

        NowUI.Text(textRect, _font)
            .SetFontSize(fontSize)
            .SetColor(color)
            .SetMask(mask)
            .Draw(fitted);
    }

    string FitText(string text, float fontSize, float maxWidth)
    {
        if (maxWidth <= 0 || _font == null)
            return string.Empty;

        if (_font.MeasureText(text, fontSize).x <= maxWidth)
            return text;

        const string ELLIPSIS = "...";

        if (_font.MeasureText(ELLIPSIS, fontSize).x > maxWidth)
            return string.Empty;

        int low = 0;
        int high = text.Length;
        int best = 0;

        while (low <= high)
        {
            int mid = (low + high) / 2;
            string candidate = text.Substring(0, mid).TrimEnd() + ELLIPSIS;

            if (_font.MeasureText(candidate, fontSize).x <= maxWidth)
            {
                best = mid;
                low = mid + 1;
            }
            else
            {
                high = mid - 1;
            }
        }

        return best > 0 ? text.Substring(0, best).TrimEnd() + ELLIPSIS : string.Empty;
    }

    void DrawTopBar(float width)
    {
        DrawRect(Rect(0, 0, width, 68), Color.white);
        DrawRect(Rect(0, 67, width, 1), Rgb(220, 224, 229));

        DrawRect(Rect(22, 18, 34, 34), Rgb(234, 67, 53), 9);
        DrawTextCentered("M", Rect(22, 18, 34, 34), 22, Color.white);

        if (width >= 520)
            DrawText("Now Mail", Rect(68, 21, 150, 28), 22, Rgb(31, 41, 55));

        if (width >= 760)
        {
            float searchX = width < 940 ? 210 : 280;
            float searchWidth = Mathf.Clamp(width - searchX - 170, 260, 620);
            DrawRect(Rect(searchX, 13, searchWidth, 42), Rgb(241, 243, 244), 12);
            DrawText("Search mail", Rect(searchX + 44, 22, searchWidth - 64, 24), 16, Rgb(95, 99, 104));
            DrawTextCentered("/", Rect(searchX + 12, 13, 28, 42), 24, Rgb(95, 99, 104));
        }
        else if (width >= 360)
        {
            DrawRect(Rect(width - 180, 17, 34, 34), Rgb(245, 247, 250), 17);
            DrawTextCentered("/", Rect(width - 180, 17, 34, 34), 20, Rgb(95, 99, 104));
        }

        DrawRect(Rect(width - 132, 17, 34, 34), Rgb(245, 247, 250), 17);
        DrawTextCentered("?", Rect(width - 132, 17, 34, 34), 18, Rgb(75, 85, 99));
        DrawRect(Rect(width - 84, 17, 34, 34), Rgb(31, 41, 55), 17);
        DrawTextCentered("A", Rect(width - 84, 17, 34, 34), 18, Color.white);
    }

    void DrawSidebar(float x, float y, float width, float height, bool compact)
    {
        if (width <= 0)
            return;

        DrawRect(Rect(x, y, width, height), Rgb(248, 250, 252));
        DrawRect(Rect(x + width - 1, y, 1, height), Rgb(226, 232, 240));

        if (!compact)
        {
            Vector4 composeRect = Rect(x + 18, y + 18, 138, 46);
            DrawRect(composeRect, Rgb(194, 231, 255), 14);
            DrawTextCentered("+ Compose", composeRect, 17, Rgb(31, 41, 55));
        }
        else
        {
            Vector4 composeRect = Rect(x + 19, y + 18, 50, 46);
            DrawRect(composeRect, Rgb(194, 231, 255), 14);
            DrawTextCentered("+", composeRect, 20, Rgb(31, 41, 55));
        }

        float navY = y + 86;
        for (int i = 0; i < _labels.Length; ++i)
        {
            bool active = i == 0;
            Vector4 row = Rect(x + 12, navY + i * 40, width - 24, 34);

            if (active)
                DrawRect(row, Rgb(252, 232, 230), 17);

            Color labelColor = active ? Rgb(179, 38, 30) : Rgb(75, 85, 99);
            string icon = _labels[i].Substring(0, 1);
            DrawTextCentered(icon, Rect(row.x + 8, row.y, 30, row.w), 16, labelColor);

            if (!compact)
                DrawText(_labels[i], Rect(row.x + 48, row.y + 7, row.z - 76, 22), 15, labelColor);
        }

        if (!compact)
        {
            DrawText("Storage", Rect(x + 26, y + height - 92, width - 52, 20), 13, Rgb(107, 114, 128));
            DrawRect(Rect(x + 26, y + height - 62, width - 52, 6), Rgb(226, 232, 240), 3);
            DrawRect(Rect(x + 26, y + height - 62, (width - 52) * 0.62f, 6), Rgb(66, 133, 244), 3);
            DrawText("9.3 GB of 15 GB used", Rect(x + 26, y + height - 42, width - 52, 18), 12, Rgb(107, 114, 128));
        }
    }

    void DrawMessageList(float x, float y, float width, float height, bool showReader)
    {
        DrawRect(Rect(x, y, width, height), Color.white);
        DrawRect(Rect(x + width - 1, y, 1, height), Rgb(226, 232, 240));

        DrawText("Inbox", Rect(x + 22, y + 18, 130, 28), 24, Rgb(31, 41, 55));
        DrawText("Primary", Rect(x + 22, y + 58, 90, 22), 14, Rgb(179, 38, 30));
        DrawRect(Rect(x + 22, y + 84, 72, 3), Rgb(179, 38, 30), 2);

        if (width >= 300)
            DrawText("Updates", Rect(x + 126, y + 58, 90, 22), 14, Rgb(107, 114, 128));

        if (width >= 390)
            DrawText("Forums", Rect(x + 230, y + 58, 90, 22), 14, Rgb(107, 114, 128));

        DrawRect(Rect(x, y + 92, width, 1), Rgb(226, 232, 240));

        float rowY = y + 93;
        float rowHeight = Mathf.Clamp((height - 93) / Mathf.Min(_mail.Length, 7), 66, 88);

        for (int i = 0; i < _mail.Length; ++i)
        {
            if (rowY + rowHeight > y + height)
                break;

            bool selected = showReader && i == 0;
            DrawMailRow(_mail[i], Rect(x, rowY, width, rowHeight), selected);
            rowY += rowHeight;
        }
    }

    void DrawMailRow(MailItem item, Vector4 rect, bool selected)
    {
        Color background = selected ? Rgb(232, 240, 254) : item.unread ? Rgb(255, 255, 255) : Rgb(248, 250, 252);
        DrawRect(rect, background);
        DrawRect(Rect(rect.x, rect.y + rect.w - 1, rect.z, 1), Rgb(226, 232, 240));

        float avatarSize = 34;
        Vector4 avatarRect = Rect(rect.x + 18, rect.y + 16, avatarSize, avatarSize);
        DrawRect(avatarRect, item.unread ? Rgb(66, 133, 244) : Rgb(156, 163, 175), avatarSize * 0.5f);
        DrawTextCentered(item.sender.Substring(0, 1), avatarRect, 16, Color.white);

        Color titleColor = item.unread ? Rgb(17, 24, 39) : Rgb(75, 85, 99);
        DrawText(item.sender, Rect(rect.x + 66, rect.y + 12, rect.z - 150, 22), 15, titleColor);
        DrawTextRight(item.time, Rect(rect.x + rect.z - 104, rect.y + 12, 84, 22), 12, item.unread ? Rgb(17, 24, 39) : Rgb(107, 114, 128));
        DrawText(item.subject, Rect(rect.x + 66, rect.y + 34, rect.z - 90, 22), 14, titleColor);
        DrawText(item.preview, Rect(rect.x + 66, rect.y + 55, rect.z - 90, 22), 13, Rgb(107, 114, 128));

        if (item.unread)
            DrawRect(Rect(rect.x + rect.z - 18, rect.y + 39, 8, 8), Rgb(66, 133, 244), 4);
    }

    void DrawReader(float x, float y, float width, float height)
    {
        DrawRect(Rect(x, y, width, height), Color.white);

        float pad = Mathf.Clamp(width * 0.05f, 28, 56);
        DrawText("NowUI WebGL artifact is ready", Rect(x + pad, y + 36, width - pad * 2, 38), 28, Rgb(17, 24, 39));

        Vector4 avatarRect = Rect(x + pad, y + 94, 42, 42);
        DrawRect(avatarRect, Rgb(66, 133, 244), 21);
        DrawTextCentered("G", avatarRect, 18, Color.white);
        DrawText("GitHub", Rect(x + pad + 56, y + 94, 160, 24), 16, Rgb(31, 41, 55));
        DrawText("to me", Rect(x + pad + 56, y + 117, 120, 20), 13, Rgb(107, 114, 128));
        DrawText("10:42 AM", Rect(x + width - pad - 82, y + 100, 78, 20), 13, Rgb(107, 114, 128));

        DrawRect(Rect(x + pad, y + 158, width - pad * 2, 1), Rgb(226, 232, 240));

        DrawText("The WebGL artifact finished successfully and produced a merged nowui-msdf.bc library.", Rect(x + pad, y + 190, width - pad * 2, 34), 17, Rgb(31, 41, 55));
        DrawText("The runtime path can now compile fonts from byte arrays without PNG or JSON files.", Rect(x + pad, y + 232, width - pad * 2, 34), 17, Rgb(31, 41, 55));
        DrawText("Next pass: clean up the desktop wrapper linkage and import those artifacts when they are ready.", Rect(x + pad, y + 274, width - pad * 2, 34), 17, Rgb(31, 41, 55));

        Vector4 replyRect = Rect(x + pad, y + height - 118, 118, 40);
        Vector4 forwardRect = Rect(x + pad + 134, y + height - 118, 124, 40);
        DrawRect(replyRect, Rgb(26, 115, 232), 10);
        DrawTextCentered("Reply", replyRect, 16, Color.white);
        DrawOutline(forwardRect, Rgb(209, 213, 219), 1, 10);
        DrawTextCentered("Forward", forwardRect, 16, Rgb(55, 65, 81));
    }

    void DrawMobileReaderHint(float x, float y, float width)
    {
        DrawRect(Rect(x + 14, y, width - 28, 58), Rgb(232, 240, 254), 12);
        DrawText("Select a message to open the reader pane.", Rect(x + 34, y + 18, width - 68, 24), 15, Rgb(31, 41, 55));
    }

    void OnPostRender()
    {
        float width = Screen.width;
        float height = Screen.height;
        bool compact = width < 980f;
        bool narrow = width < 720f;
        bool showReader = width >= 960f;

        float top = 68f;
        float sidebarWidth = narrow ? 0f : compact ? 90f : 228f;
        float contentY = top;
        float contentHeight = height - top;

        float listX = sidebarWidth;
        float listWidth = showReader
            ? Mathf.Clamp(width * 0.34f, 340f, 430f)
            : width - sidebarWidth;

        if (narrow)
        {
            listX = 0;
            listWidth = width;
        }

        NowUI.StartUI();

        DrawRect(Rect(0, 0, width, height), Rgb(241, 245, 249));
        DrawTopBar(width);
        DrawSidebar(0, contentY, sidebarWidth, contentHeight, compact);
        DrawMessageList(listX, contentY, listWidth, contentHeight, showReader);

        if (showReader)
        {
            DrawReader(listX + listWidth, contentY, width - listX - listWidth, contentHeight);
        }
        else if (height > 520f)
        {
            DrawMobileReaderHint(listX, height - 78f, listWidth);
        }

        NowUI.FlushUI();
    }
}
