using System.Collections.Generic;
using UnityEngine;
using NowUI.Markup;

namespace NowUI.Markdown
{
    /// <summary>
    /// Renders <c>```markup</c> (or <c>```nowui</c>) fenced blocks inside a
    /// markdown document as live NowUI markup — controls, bindings and events
    /// included — instead of highlighted code. Caller-owned: create one, keep
    /// it in a field, and pass it to
    /// <see cref="NowMarkdownBuilder.SetEmbeds(NowMarkdownEmbedSet)"/>; without
    /// it those fences stay ordinary code blocks, so documents degrade
    /// gracefully in renderers that do not wire embeds up.
    /// <code>
    /// readonly NowMarkupEmbeds _embeds = new NowMarkupEmbeds();
    ///
    /// var result = NowMarkdown.Document(text).SetEmbeds(_embeds).Draw();
    ///
    /// if (_embeds.Clicked("save"))
    ///     Save();
    /// </code>
    /// Every embedded block shares this instance's <see cref="state"/>, so a
    /// slider in one block can drive visibility in another; interaction
    /// identity is still scoped per embed, so identical snippets never fight
    /// over focus or hover.
    /// </summary>
    public sealed class NowMarkupEmbeds : NowMarkdownEmbedSet
    {
        readonly NowMarkupState _state;

        readonly List<NowMarkupEvent> _events = new List<NowMarkupEvent>(4);

        int _eventsFrame = -1;

        /// <summary>Creates the embed set with its own private state store.</summary>
        public NowMarkupEmbeds()
            : this(null)
        {
        }

        /// <summary>
        /// Creates the embed set bound to a caller-owned state store, so app
        /// code can seed and read the values markup controls bind to.
        /// </summary>
        public NowMarkupEmbeds(NowMarkupState state)
        {
            _state = state ?? new NowMarkupState();
            Add("markup", Render);
            Add("nowui", Render);
        }

        /// <summary>The state store every embedded block binds against.</summary>
        public NowMarkupState state => _state;

        /// <summary>
        /// The markup events recorded by embedded blocks this frame, across
        /// every document drawn with this set.
        /// </summary>
        public IReadOnlyList<NowMarkupEvent> events =>
            _eventsFrame == Time.frameCount ? _events : System.Array.Empty<NowMarkupEvent>();

        /// <summary>True when an embedded block recorded a click for this element id this frame.</summary>
        public bool Clicked(string id)
        {
            return HasEvent(NowMarkupEventKind.Click, id);
        }

        /// <summary>True when an embedded block recorded a change for this element id or state key this frame.</summary>
        public bool Changed(string idOrKey)
        {
            return HasEvent(NowMarkupEventKind.Change, idOrKey);
        }

        /// <summary>True when an embedded block emitted this action this frame.</summary>
        public bool Action(string name)
        {
            return HasEvent(NowMarkupEventKind.Action, name);
        }

        bool HasEvent(NowMarkupEventKind kind, string idOrName)
        {
            var items = events;

            for (int i = 0; i < items.Count; ++i)
            {
                var item = items[i];

                if (item.kind != kind)
                    continue;

                if (string.Equals(item.id, idOrName, System.StringComparison.Ordinal) ||
                    string.Equals(item.name, idOrName, System.StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        float Render(in NowMarkdownEmbedContext context)
        {
            int frame = Time.frameCount;

            if (frame != _eventsFrame)
            {
                _eventsFrame = frame;
                _events.Clear();
            }

            int areaId = context.embedId;

            using (NowControls.IdScope(areaId))
            using (NowLayout.Area(areaId, context.rect, default(NowLayoutOptions)))
            {
                var result = NowMarkup.Document(context.source).Draw(_state);
                var recorded = result.events;

                if (recorded != null)
                {
                    for (int i = 0; i < recorded.Count; ++i)
                        _events.Add(recorded[i]);
                }
            }

            return NowLayout.TryGetCachedContentSize(areaId, out Vector2 size) ? size.y : 0f;
        }
    }
}
