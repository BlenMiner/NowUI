using System.Runtime.CompilerServices;
using UnityEngine;

namespace NowUI
{
    /// <summary>
    /// Collapsible section header:
    /// <code>
    /// if (NowLayout.Foldout("Advanced").Draw(ref expanded))
    ///     RepaintSection();
    /// </code>
    /// <see cref="Draw(ref bool)"/> edits caller-owned expansion in place and
    /// returns true when toggled this frame; <see cref="Draw()"/> keeps the
    /// expansion in control state instead and returns whether the section is
    /// open. Click anywhere on the row, or submit while focused, to toggle.
    /// Renders through <see cref="NowControlRenderer.DrawTreeRow"/> so themes
    /// restyle foldouts together with tree rows.
    /// </summary>
    [NowBuilder]
    public struct NowFoldout
    {
        NowId _id;
        readonly int _site;

        const int ExpandedSeed = 0x4e464458;

        readonly string _label;
        NowFocusNavigation _navigation;
        NowLayoutOptions _options;
        readonly NowRect _rect;
        readonly bool _hasRect;

        internal NowFoldout(string label, NowId id, int site)
        {
            _id = id;
            _site = site;
            _label = label ?? string.Empty;
            _navigation = default;
            _options = default;
            _rect = default;
            _hasRect = false;
        }

        internal NowFoldout(NowRect rect, string label, NowId id, int site) : this(label, id, site)
        {
            _rect = rect;
            _hasRect = true;
        }

        public NowFoldout SetOptions(NowLayoutOptions options) { _options = options; return this; }

        public NowFoldout SetWidth(float width) { _options = _options.SetWidth(width); return this; }

        public NowFoldout SetStretchWidth(float weight = 1f) { _options = _options.SetStretchWidth(weight); return this; }

        /// <summary>Explicit control id, decoupling identity from the call site.</summary>
        public NowFoldout SetId(NowId id) { _id = id; return this; }

        /// <summary>Explicit directional/Tab focus targets for this control.</summary>
        public NowFoldout SetNavigation(NowFocusNavigation navigation) { _navigation = navigation; return this; }

        /// <summary>
        /// Control-owned expansion: the state lives in
        /// <see cref="NowControlState"/> keyed by this control's id. Returns
        /// true while the section is open.
        /// </summary>
        public bool Draw()
        {
            int id = NowControls.GetControlId(_id, _site);
            ref bool expanded = ref NowControlState.Get<bool>(NowInput.CombineId(id, ExpandedSeed));
            DrawHeader(id, ref expanded);
            return expanded;
        }

        /// <summary>Caller-owned expansion; returns true when toggled this frame.</summary>
        public bool Draw(ref bool expanded)
        {
            int id = NowControls.GetControlId(_id, _site);
            return DrawHeader(id, ref expanded);
        }

        bool DrawHeader(int id, ref bool expanded)
        {
            var theme = NowTheme.themeAsset;
            var styles = theme.controlStyles;
            var textStyle = NowControls.Text(theme, NowTextStyle.Body);
            float disclosure = styles.treeDisclosureSize;
            float contentWidth = disclosure + 8f + textStyle.Measure(_label).x + 8f;

            var rect = NowControls.ReserveRect(_hasRect, _rect, GroupOptions(), new Vector2(contentWidth, styles.treeRowHeight));
            var interaction = NowControls.Interact(id, rect, _navigation, out bool focused, out bool submitted);
            bool toggled = interaction.clicked || submitted;

            if (toggled)
                expanded = !expanded;

            var disclosureRect = new NowRect(
                rect.x + 2f,
                rect.y + (rect.height - disclosure) * 0.5f,
                disclosure,
                disclosure);

            float hoverT = NowControlState.Transition(interaction, interaction.hovered || interaction.held);
            theme.controlRenderer.DrawTreeRow(new NowTreeRowRenderContext(
                theme,
                rect,
                _label,
                0,
                true,
                expanded,
                false,
                disclosureRect,
                interaction,
                focused,
                hoverT));

            return toggled;
        }

        NowLayoutOptions GroupOptions()
        {
            var options = _options;

            if (!options.Has(NowLayoutOptions.Field.Width) && !options.Has(NowLayoutOptions.Field.StretchWidth))
                options = options.SetStretchWidth();

            return options;
        }
    }

    public static partial class Now
    {
        public static NowFoldout Foldout(NowRect rect, string label = "", NowId id = default, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowFoldout(rect, label, id, NowControls.SiteId(file, line));
        }
    }

    public static partial class NowLayout
    {
        public static NowFoldout Foldout(string label = "", NowId id = default, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowFoldout(label, id, NowControls.SiteId(file, line));
        }
    }
}
