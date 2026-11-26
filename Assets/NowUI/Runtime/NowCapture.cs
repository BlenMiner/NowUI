using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace NowUI
{
    /// <summary>What a <see cref="NowCaptureItem"/> describes.</summary>
    public enum NowCaptureKind : byte
    {
        /// <summary>An interactive control (anything that ran NowUI interaction).</summary>
        Control,

        /// <summary>A filled or stroked rectangle, shape, line or gradient.</summary>
        Rect,

        /// <summary>A run of text.</summary>
        Text,

        /// <summary>A texture, sprite, icon, vector animation or other image.</summary>
        Image
    }

    /// <summary>State flags of a captured item.</summary>
    [Flags]
    public enum NowCaptureFlags : ushort
    {
        None = 0,
        Hovered = 1,
        Pressed = 2,
        Focused = 4,
        Disabled = 8,
        Selected = 16,

        /// <summary>Text the user types into (text fields, areas, code editors).</summary>
        Editable = 32,

        /// <summary>
        /// Password or masked content. <see cref="NowCaptureItem.text"/> is always
        /// null for these items and for text drawn inside them.
        /// </summary>
        Secret = 64
    }

    /// <summary>
    /// One control or visible primitive drawn by a NowUI host during a captured
    /// frame. See <see cref="NowCapture"/> and the Capture guide.
    /// </summary>
    public struct NowCaptureItem
    {
        public NowCaptureKind kind;

        /// <summary>
        /// Stable across frames. Controls hash their resolved NowUI identity;
        /// primitives hash their enclosing control (or the host) plus their ordinal
        /// among the primitives drawn directly inside it. Unique within a frame.
        /// </summary>
        public ulong id;

        /// <summary>Index (in the same frame) of the innermost enclosing Control item, or -1.</summary>
        public int parent;

        /// <summary>Bounds in the host's draw space: top-left origin, the units the host passed to its draw callback.</summary>
        public NowRect rect;

        /// <summary>Effective clip rect in the same space (the host bounds when nothing clips).</summary>
        public NowRect clip;

        /// <summary>Degrees, clockwise on screen, about the centre of <see cref="rect"/>; 0 when not rotated.</summary>
        public float rotation;

        /// <summary>
        /// Authored display (sRGB) colour with opacity and tints applied: fill for
        /// Rect/Image, text colour for Text, the main/background colour (or clear)
        /// for Control.
        /// </summary>
        public Color color;

        /// <summary>Text only: font size in the same units as <see cref="rect"/>.</summary>
        public float fontSize;

        /// <summary>
        /// Text: the string as drawn (after markup and preprocessing). Control: its
        /// label or value text, if any. Otherwise null, and always null for
        /// <see cref="NowCaptureFlags.Secret"/> items.
        /// </summary>
        public string text;

        /// <summary>Image: the texture, sprite or asset name; otherwise null.</summary>
        public string texture;

        /// <summary>Control: a short control type name ("Button", "Toggle", "Slider", ...); otherwise null.</summary>
        public string control;

        /// <summary>Control: normalized slider/progress/scroll value, 1/0 for toggles, otherwise 0.</summary>
        public float value;

        public NowCaptureFlags flags;
    }

    /// <summary>
    /// The items one host drew in one completed frame, in draw (paint) order.
    /// The instance and its items are reused: read or copy them inside the
    /// <see cref="NowCapture.onFrame"/> callback and do not keep the reference.
    /// </summary>
    public sealed class NowCaptureFrame
    {
        internal struct OpenControl
        {
            public int item;
            public ulong id;
            public NowResolvedId resolvedId;
            public NowRect rect;
            public int maskDepth;
            public int idDepth;
            public int ordinal;
            public bool secret;
            public bool hasColor;
            public bool describedText;
        }

        internal NowCaptureItem[] items = new NowCaptureItem[64];
        internal int itemCount;
        internal object hostObject;
        internal NowRect hostBounds;
        internal ulong hostKey;
        internal int token;
        internal OpenControl[] open = new OpenControl[16];
        internal int openCount;
        internal int barrier;
        internal int rootOrdinal;
        internal readonly HashSet<ulong> ids = new HashSet<ulong>();

        internal NowCaptureFrame()
        {
        }

        /// <summary>
        /// What drew the frame: the NowGraphic / NowLayoutGraphic / NowWorldGraphic /
        /// NowPipelineGraphic component, the NowVisualElement, or for
        /// <see cref="Now.StartUI()"/> the current camera (or <see cref="NowCapture.screenHost"/>).
        /// </summary>
        public object host => hostObject;

        /// <summary>The rect the host drew into: the rect its draw callback received.</summary>
        public NowRect bounds => hostBounds;

        /// <summary>Number of items in this frame.</summary>
        public int count => itemCount;

        /// <summary>The item at <paramref name="index"/>, in draw order.</summary>
        public ref readonly NowCaptureItem this[int index]
        {
            get
            {
                if ((uint)index >= (uint)itemCount)
                    throw new ArgumentOutOfRangeException(nameof(index));

                return ref items[index];
            }
        }

        internal void Reset(object host, NowRect bounds, int frameToken)
        {
            Array.Clear(items, 0, itemCount);
            itemCount = 0;
            hostObject = host;
            hostBounds = bounds;
            hostKey = NowCaptureRecorder.Mix(0x4E6F7755_49486F73UL ^ (ulong)(uint)RuntimeHelpers.GetHashCode(host));
            token = frameToken;
            openCount = 0;
            barrier = 0;
            rootOrdinal = 0;
            ids.Clear();
        }

        internal void Release()
        {
            Array.Clear(items, 0, itemCount);
            itemCount = 0;
            hostObject = null;
            token = 0;
            openCount = 0;
            barrier = 0;
            ids.Clear();
        }

        internal ref NowCaptureItem Append()
        {
            if (itemCount == items.Length)
                Array.Resize(ref items, items.Length * 2);

            return ref items[itemCount++];
        }

        internal ref OpenControl Push()
        {
            if (openCount == open.Length)
                Array.Resize(ref open, open.Length * 2);

            return ref open[openCount++];
        }
    }

    /// <summary>
    /// Read-only view of what NowUI hosts draw: every control and visible
    /// primitive, per host and per completed draw. Hosts collect nothing until
    /// a handler is attached to <see cref="onFrame"/>.
    /// </summary>
    public static class NowCapture
    {
        static Action<NowCaptureFrame> s_onFrame;

        static Action<NowCaptureFrame>[] s_handlers = Array.Empty<Action<NowCaptureFrame>>();

        /// <summary>
        /// Host object reported by <see cref="Now.StartUI()"/> frames drawn while
        /// no camera is rendering (<see cref="Camera.current"/> is null).
        /// </summary>
        public static readonly object screenHost = new ScreenHost();

        /// <summary>
        /// Raised once per host per completed draw whose geometry is displayed, on
        /// the main thread, while at least one handler is attached. Measure and
        /// layout-only passes never raise it, and retained hosts raise it only when
        /// they rebuild: keep the last frame per host. The frame is only valid
        /// during the callback.
        /// </summary>
        public static event Action<NowCaptureFrame> onFrame
        {
            add
            {
                s_onFrame += value;
                RefreshHandlers();
            }
            remove
            {
                s_onFrame -= value;
                RefreshHandlers();
            }
        }

        /// <summary>True while a handler is attached. Hosts collect items only then.</summary>
        public static bool active => s_onFrame != null;

        /// <summary>
        /// Describes the control with <paramref name="id"/> that interacted most
        /// recently in the frame being captured: its type name, normalized value,
        /// extra state flags and label/value text. Custom controls call it right
        /// after <see cref="NowControls.Interact(NowResolvedId, NowRect, out bool, out bool)"/>
        /// (or <see cref="NowInput.Interact(NowResolvedId, NowRect)"/>). It does
        /// nothing while capture is off, so the call is free to leave in place.
        /// Passing <see cref="NowCaptureFlags.Secret"/> clears the control's text and
        /// the text of everything drawn inside it.
        /// </summary>
        public static void Describe(
            NowResolvedId id,
            string control,
            float value = 0f,
            NowCaptureFlags flags = NowCaptureFlags.None,
            string text = null)
        {
            if (NowCaptureRecorder.collecting)
                NowCaptureRecorder.Describe(id, control, value, flags, text);
        }

        static void RefreshHandlers()
        {
            var multicast = s_onFrame;

            if (multicast == null)
            {
                s_handlers = Array.Empty<Action<NowCaptureFrame>>();
                NowCaptureRecorder.SetEnabled(false);
                return;
            }

            var list = multicast.GetInvocationList();
            var handlers = new Action<NowCaptureFrame>[list.Length];

            for (int i = 0; i < list.Length; ++i)
                handlers[i] = (Action<NowCaptureFrame>)list[i];

            s_handlers = handlers;
            NowCaptureRecorder.SetEnabled(true);
        }

        internal static void Raise(NowCaptureFrame frame)
        {
            var handlers = s_handlers;

            for (int i = 0; i < handlers.Length; ++i)
            {
                try
                {
                    handlers[i](frame);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
        }

        sealed class ScreenHost
        {
            public override string ToString() => "NowCapture.screenHost";
        }
    }

    /// <summary>
    /// Internal side of <see cref="NowCapture"/>: hosts open a frame around their
    /// draw pass, drawing code reports into it while <see cref="collecting"/> is
    /// true, and the host publishes it once the geometry is committed.
    /// </summary>
    internal static class NowCaptureRecorder
    {
        /// <summary>The one check every primitive and control performs.</summary>
        internal static bool collecting;

        const float MinAlpha = 0.002f;

        // Primitive drawn this far outside an open control still belongs to it
        // (focus rings, thumbs and outlines that overhang the hit rect).
        const float ContainmentTolerance = 2f;

        const int MaxStringCacheEntries = 4096;

        const int MaxNameCacheEntries = 1024;

        static bool s_enabled;

        static NowCaptureFrame s_current;

        static int s_suspend;

        static int s_nextToken;

        static readonly List<NowCaptureFrame> s_pending = new List<NowCaptureFrame>(4);

        static readonly Stack<NowCaptureFrame> s_pool = new Stack<NowCaptureFrame>(4);

        static readonly Dictionary<ulong, string> s_spanStrings = new Dictionary<ulong, string>(64);

        static readonly Dictionary<UnityEngine.Object, string> s_names =
            new Dictionary<UnityEngine.Object, string>(32, ReferenceComparer.instance);

        internal static bool enabled => s_enabled;

        /// <summary>Token of the frame currently collecting, or 0.</summary>
        internal static int currentToken => s_current != null ? s_current.token : 0;

        internal static void SetEnabled(bool value)
        {
            s_enabled = value;

            if (!value)
            {
                // Detached handlers: drop every frame in flight so nothing is
                // published and drawing stops paying for collection immediately.
                if (s_current != null)
                    Recycle(s_current);

                s_current = null;

                for (int i = 0; i < s_pending.Count; ++i)
                    Recycle(s_pending[i]);

                s_pending.Clear();
                s_spanStrings.Clear();
                s_names.Clear();
            }

            Refresh();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void Refresh()
        {
            collecting = s_current != null && s_suspend == 0;
        }

        // ---------------------------------------------------------------- hosts

        /// <summary>
        /// Opens a frame for <paramref name="host"/> drawing into
        /// <paramref name="bounds"/>. Returns 0 (and does nothing) while no handler
        /// is attached. Pair with <see cref="End"/>.
        /// </summary>
        internal static int Begin(object host, NowRect bounds)
        {
            if (!s_enabled || host == null)
                return 0;

            // Hosts never nest; a frame still current here was abandoned.
            if (s_current != null)
            {
                Recycle(s_current);
                s_current = null;
            }

            var frame = s_pool.Count > 0 ? s_pool.Pop() : new NowCaptureFrame();

            if (++s_nextToken == 0)
                ++s_nextToken;

            frame.Reset(host, bounds, s_nextToken);
            s_current = frame;
            s_suspend = 0;
            Refresh();
            return frame.token;
        }

        /// <summary>
        /// Stops collecting into the frame but keeps it for overlay routing and a
        /// later <see cref="End"/> (hosts that draw several owners before one
        /// overlay flush).
        /// </summary>
        internal static void Detach(int token)
        {
            if (token == 0 || s_current == null || s_current.token != token)
                return;

            s_pending.Add(s_current);
            s_current = null;
            Refresh();
        }

        /// <summary>Closes a frame; publishes it when <paramref name="publish"/> is true.</summary>
        internal static void End(int token, bool publish)
        {
            if (token == 0)
                return;

            NowCaptureFrame frame = null;

            if (s_current != null && s_current.token == token)
            {
                frame = s_current;
                s_current = null;
                s_suspend = 0;
            }
            else
            {
                for (int i = 0; i < s_pending.Count; ++i)
                {
                    if (s_pending[i].token != token)
                        continue;

                    frame = s_pending[i];
                    s_pending.RemoveAt(i);
                    break;
                }
            }

            Refresh();

            if (frame == null)
                return;

            try
            {
                if (publish && s_enabled)
                    NowCapture.Raise(frame);
            }
            finally
            {
                Recycle(frame);
            }
        }

        static void Recycle(NowCaptureFrame frame)
        {
            frame.Release();
            s_pool.Push(frame);
        }

        internal static void Suspend()
        {
            ++s_suspend;
            collecting = false;
        }

        internal static void Resume()
        {
            if (s_suspend > 0)
                --s_suspend;

            Refresh();
        }

        internal static SuspendHandle SuspendScope()
        {
            if (!collecting)
                return default;

            Suspend();
            return new SuspendHandle(true);
        }

        internal readonly struct SuspendHandle : IDisposable
        {
            readonly bool _active;

            internal SuspendHandle(bool active)
            {
                _active = active;
            }

            public void Dispose()
            {
                if (_active)
                    Resume();
            }
        }

        /// <summary>
        /// Routes collection into the frame identified by <paramref name="token"/>
        /// (captured when an overlay was deferred). Controls open in that frame do
        /// not parent the routed content.
        /// </summary>
        internal static RouteScope Route(int token)
        {
            if (s_current == null && s_pending.Count == 0)
                return default;

            var previous = s_current;
            NowCaptureFrame target = null;

            if (token != 0)
            {
                if (previous != null && previous.token == token)
                {
                    target = previous;
                }
                else
                {
                    for (int i = 0; i < s_pending.Count; ++i)
                    {
                        if (s_pending[i].token == token)
                        {
                            target = s_pending[i];
                            break;
                        }
                    }
                }
            }

            int previousBarrier = 0;
            int previousOpen = 0;

            if (target != null)
            {
                previousBarrier = target.barrier;
                previousOpen = target.openCount;
                target.barrier = target.openCount;
            }

            s_current = target;
            Refresh();
            return new RouteScope(
                previous,
                previous != null ? previous.token : 0,
                target,
                target != null ? target.token : 0,
                previousBarrier,
                previousOpen);
        }

        internal readonly struct RouteScope : IDisposable
        {
            readonly NowCaptureFrame _previous;
            readonly int _previousToken;
            readonly NowCaptureFrame _target;
            readonly int _targetToken;
            readonly int _previousBarrier;
            readonly int _previousOpen;
            readonly bool _active;

            internal RouteScope(
                NowCaptureFrame previous,
                int previousToken,
                NowCaptureFrame target,
                int targetToken,
                int previousBarrier,
                int previousOpen)
            {
                _previous = previous;
                _previousToken = previousToken;
                _target = target;
                _targetToken = targetToken;
                _previousBarrier = previousBarrier;
                _previousOpen = previousOpen;
                _active = true;
            }

            public void Dispose()
            {
                if (!_active)
                    return;

                if (_target != null && _target.token == _targetToken)
                {
                    _target.openCount = Mathf.Min(_target.openCount, _previousOpen);
                    _target.barrier = _previousBarrier;
                }

                s_current = _previous != null && _previous.token == _previousToken ? _previous : null;
                Refresh();
            }
        }

        // ------------------------------------------------------------- controls

        /// <summary>Reports an interaction region as a Control item.</summary>
        internal static void Control(in NowInteraction interaction, NowRect screenRect)
        {
            Open(interaction.id, screenRect, StateFlags(in interaction));
        }

        /// <summary>
        /// Opens a Control item for a control that has no interaction of its own
        /// (progress bars, scroll views) and describes it. <paramref name="rect"/>
        /// is in the current local space.
        /// </summary>
        internal static void OpenControl(
            NowResolvedId id,
            NowRect rect,
            string control,
            float value,
            NowCaptureFlags flags = NowCaptureFlags.None,
            string text = null)
        {
            if (Open(id, Now.TransformScreenRect(rect), NowCaptureFlags.None))
                Describe(id, control, value, flags, text);
        }

        static bool Open(NowResolvedId resolvedId, NowRect screenRect, NowCaptureFlags stateFlags)
        {
            var frame = s_current;

            if (frame == null || Now.isDrawSuppressed || !resolvedId.hasValue)
                return false;

            NowRect clip = Now.ApplyAmbientMask(default).Intersect(frame.hostBounds);

            if (clip.isEmpty || !OverlapsLenient(screenRect, clip))
                return false;

            CloseStale(frame, screenRect.center);

            // A control interacting twice in a row (several pointer buttons)
            // remains one item.
            if (frame.openCount > frame.barrier &&
                frame.open[frame.openCount - 1].resolvedId == resolvedId)
            {
                ref var merged = ref frame.items[frame.open[frame.openCount - 1].item];
                merged.flags |= stateFlags;
                return true;
            }

            int parentSlot = frame.openCount > frame.barrier ? frame.openCount - 1 : -1;
            int parentItem = parentSlot >= 0 ? frame.open[parentSlot].item : -1;
            bool parentSecret = parentSlot >= 0 && frame.open[parentSlot].secret;

            ulong id = Unique(frame, Mix(resolvedId.value ^ 0x436F6E74726F6C21UL));
            int index = frame.itemCount;
            ref var item = ref frame.Append();
            item.kind = NowCaptureKind.Control;
            item.id = id;
            item.parent = parentItem;
            item.rect = screenRect;
            item.clip = clip;
            item.rotation = 0f;
            item.color = Color.clear;
            item.fontSize = 0f;
            item.text = null;
            item.texture = null;
            item.control = "Control";
            item.value = 0f;
            item.flags = stateFlags | (parentSecret ? NowCaptureFlags.Secret : 0);

            if (NowFocus.IsFocused(resolvedId))
                item.flags |= NowCaptureFlags.Focused;

            ref var open = ref frame.Push();
            open.item = index;
            open.id = id;
            open.resolvedId = resolvedId;
            open.rect = screenRect;
            open.maskDepth = Now.ambientMaskCount;
            open.idDepth = NowControls.idScopeDepth;
            open.ordinal = 0;
            open.secret = parentSecret;
            open.hasColor = false;
            open.describedText = false;
            return true;
        }

        static NowCaptureFlags StateFlags(in NowInteraction interaction)
        {
            var flags = NowCaptureFlags.None;

            if (interaction.hovered)
                flags |= NowCaptureFlags.Hovered;

            if (interaction.held || interaction.pressed)
                flags |= NowCaptureFlags.Pressed;

            return flags;
        }

        /// <summary>Corrects the focus flag once a control's focus bundle has run.</summary>
        internal static void SetFocused(NowResolvedId id, bool focused)
        {
            var frame = s_current;

            if (frame == null || !TryFindOpen(frame, id, out int slot))
                return;

            ref var item = ref frame.items[frame.open[slot].item];

            if (focused)
                item.flags |= NowCaptureFlags.Focused;
            else
                item.flags &= ~NowCaptureFlags.Focused;
        }

        internal static void Describe(
            NowResolvedId id,
            string control,
            float value,
            NowCaptureFlags flags,
            string text)
        {
            var frame = s_current;

            if (frame == null || Now.isDrawSuppressed || !TryFindOpen(frame, id, out int slot))
                return;

            ref var open = ref frame.open[slot];
            ref var item = ref frame.items[open.item];

            if (control != null)
                item.control = control;

            item.value = float.IsNaN(value) ? 0f : Mathf.Clamp01(value);
            item.flags |= flags;

            if ((flags & NowCaptureFlags.Secret) != 0)
                open.secret = true;

            if (open.secret)
            {
                item.flags |= NowCaptureFlags.Secret;
                item.text = null;

                // Children drawn before the description (rare) lose their text too.
                for (int i = open.item + 1; i < frame.itemCount; ++i)
                {
                    if (IsDescendant(frame, i, open.item))
                    {
                        frame.items[i].text = null;
                        frame.items[i].flags |= NowCaptureFlags.Secret;
                    }
                }

                open.describedText = true;
                return;
            }

            if (text != null)
            {
                item.text = text;
                open.describedText = true;
            }
        }

        static bool IsDescendant(NowCaptureFrame frame, int index, int ancestor)
        {
            int parent = frame.items[index].parent;

            while (parent >= 0)
            {
                if (parent == ancestor)
                    return true;

                parent = frame.items[parent].parent;
            }

            return false;
        }

        static bool TryFindOpen(NowCaptureFrame frame, NowResolvedId id, out int slot)
        {
            for (int i = frame.openCount - 1; i >= frame.barrier; --i)
            {
                if (frame.open[i].resolvedId == id)
                {
                    slot = i;
                    return true;
                }
            }

            slot = -1;
            return false;
        }

        static void CloseStale(NowCaptureFrame frame, Vector2 point)
        {
            if (frame.openCount <= frame.barrier)
                return;

            int maskDepth = Now.ambientMaskCount;
            int idDepth = NowControls.idScopeDepth;

            while (frame.openCount > frame.barrier)
            {
                ref var top = ref frame.open[frame.openCount - 1];
                NowRect reach = top.rect.Outset(ContainmentTolerance);

                if (maskDepth >= top.maskDepth &&
                    idDepth >= top.idDepth &&
                    point.x >= reach.x && point.x <= reach.xMax &&
                    point.y >= reach.y && point.y <= reach.yMax)
                {
                    return;
                }

                --frame.openCount;
            }
        }

        // ----------------------------------------------------------- primitives

        /// <summary>
        /// Reports a primitive. <paramref name="rect"/> and <paramref name="clip"/>
        /// are in host space (transforms applied); <paramref name="clip"/> is the
        /// resolved mask (never "empty means unbounded").
        /// </summary>
        internal static void Primitive(
            NowCaptureKind kind,
            NowRect rect,
            NowRect clip,
            Vector4 color,
            float rotation = 0f,
            float fontSize = 0f,
            string text = null,
            string texture = null)
        {
            var frame = s_current;

            if (frame == null || color.w <= MinAlpha)
                return;

            clip = clip.Intersect(frame.hostBounds);

            if (clip.isEmpty || !OverlapsLenient(rect, clip))
                return;

            AddPrimitive(frame, kind, rect, clip, color, rotation, fontSize, text, texture, out _);
        }

        /// <summary>Text from a span: reuses the string drawn last frame when it matches.</summary>
        internal static void Text(NowRect rect, NowRect clip, Vector4 color, float fontSize, ReadOnlySpan<char> text)
        {
            var frame = s_current;

            if (frame == null || color.w <= MinAlpha || text.IsEmpty)
                return;

            clip = clip.Intersect(frame.hostBounds);

            if (clip.isEmpty || !OverlapsLenient(rect, clip))
                return;

            int index = AddPrimitive(frame, NowCaptureKind.Text, rect, clip, color, 0f, fontSize, null, null, out bool secret);

            if (secret)
                return;

            ref var item = ref frame.items[index];

            if (!s_spanStrings.TryGetValue(item.id, out string cached) || !text.SequenceEqual(cached.AsSpan()))
            {
                if (s_spanStrings.Count >= MaxStringCacheEntries)
                    s_spanStrings.Clear();

                cached = text.ToString();
                s_spanStrings[item.id] = cached;
            }

            item.text = cached;
            FillControlText(frame, index, cached);
        }

        static int AddPrimitive(
            NowCaptureFrame frame,
            NowCaptureKind kind,
            NowRect rect,
            NowRect clip,
            Vector4 color,
            float rotation,
            float fontSize,
            string text,
            string texture,
            out bool secret)
        {
            CloseStale(frame, rect.center);

            int parentSlot = frame.openCount > frame.barrier ? frame.openCount - 1 : -1;
            ulong parentKey;
            int ordinal;
            int parentItem;
            secret = false;

            if (parentSlot >= 0)
            {
                ref var parent = ref frame.open[parentSlot];
                parentKey = parent.id;
                ordinal = parent.ordinal++;
                parentItem = parent.item;
                secret = parent.secret;

                if (!parent.hasColor && kind == NowCaptureKind.Rect && ContainsPoint(rect, parent.rect.center))
                {
                    parent.hasColor = true;
                    frame.items[parentItem].color = color;
                }
            }
            else
            {
                parentKey = frame.hostKey;
                ordinal = frame.rootOrdinal++;
                parentItem = -1;
            }

            int index = frame.itemCount;
            ref var item = ref frame.Append();
            item.kind = kind;
            item.id = Unique(frame, Mix(parentKey ^ Mix((ulong)(ordinal + 1) * 0x9E3779B97F4A7C15UL)));
            item.parent = parentItem;
            item.rect = rect;
            item.clip = clip;
            item.rotation = rotation;
            item.color = color;
            item.fontSize = kind == NowCaptureKind.Text ? fontSize : 0f;
            item.text = secret ? null : text;
            item.texture = texture;
            item.control = null;
            item.value = 0f;
            item.flags = secret ? NowCaptureFlags.Secret : NowCaptureFlags.None;

            if (kind == NowCaptureKind.Text && !secret && text != null)
                FillControlText(frame, index, text);

            return index;
        }

        static void FillControlText(NowCaptureFrame frame, int index, string text)
        {
            int parentSlot = frame.openCount > frame.barrier ? frame.openCount - 1 : -1;

            if (parentSlot < 0)
                return;

            ref var parent = ref frame.open[parentSlot];

            if (parent.describedText || parent.item != frame.items[index].parent)
                return;

            ref var control = ref frame.items[parent.item];

            if (control.text == null)
                control.text = text;
        }

        /// <summary>Applies a finished <see cref="Now.Rotate"/> scope to the items captured inside it.</summary>
        internal static void Rotate(int token, int start, Vector2 pivot, float cos, float sin, float degrees)
        {
            var frame = s_current;

            if (frame == null || frame.token != token)
                return;

            for (int i = Mathf.Max(0, start); i < frame.itemCount; ++i)
            {
                ref var item = ref frame.items[i];
                Vector2 center = item.rect.center;
                float dx = center.x - pivot.x;
                float dy = center.y - pivot.y;
                var rotated = new Vector2(pivot.x + dx * cos - dy * sin, pivot.y + dx * sin + dy * cos);
                item.rect = NowRect.FromCenter(rotated, item.rect.width, item.rect.height);
                item.rotation = NormalizeDegrees(item.rotation + degrees);
            }
        }

        internal static int itemCount => s_current != null ? s_current.itemCount : 0;

        static float NormalizeDegrees(float degrees)
        {
            degrees %= 360f;

            if (degrees > 180f)
                degrees -= 360f;
            else if (degrees <= -180f)
                degrees += 360f;

            return degrees;
        }

        /// <summary>Cached object name; <see cref="UnityEngine.Object.name"/> allocates on every read.</summary>
        internal static string NameOf(UnityEngine.Object source)
        {
            if (source is null || source == null)
                return null;

            if (s_names.TryGetValue(source, out string name))
                return name;

            if (s_names.Count >= MaxNameCacheEntries)
                s_names.Clear();

            name = source.name;
            s_names[source] = name;
            return name;
        }

        // ---------------------------------------------------------------- utils

        static bool OverlapsLenient(NowRect rect, NowRect clip)
        {
            // Zero-size rects (point-anchored text, hairlines) still count when
            // they touch the clip.
            return rect.x <= clip.xMax && rect.xMax >= clip.x &&
                   rect.y <= clip.yMax && rect.yMax >= clip.y;
        }

        static bool ContainsPoint(NowRect rect, Vector2 point)
        {
            return point.x >= rect.x && point.x <= rect.xMax &&
                   point.y >= rect.y && point.y <= rect.yMax;
        }

        static ulong Unique(NowCaptureFrame frame, ulong id)
        {
            if (id == 0UL)
                id = 1UL;

            if (frame.ids.Add(id))
                return id;

            for (ulong occurrence = 1; ; ++occurrence)
            {
                ulong candidate = Mix(id + occurrence * 0xD6E8FEB86659FD93UL);

                if (candidate != 0UL && frame.ids.Add(candidate))
                    return candidate;
            }
        }

        /// <summary>SplitMix64 finalizer: a stable, well-distributed 64-bit mix.</summary>
        internal static ulong Mix(ulong value)
        {
            unchecked
            {
                value += 0x9E3779B97F4A7C15UL;
                value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
                value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
                return value ^ (value >> 31);
            }
        }

        /// <summary>Test hook: frames in flight.</summary>
        internal static bool hasFramesInFlight => s_current != null || s_pending.Count > 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForRuntimeLoad()
        {
            if (s_current != null)
                Recycle(s_current);

            s_current = null;

            for (int i = 0; i < s_pending.Count; ++i)
                Recycle(s_pending[i]);

            s_pending.Clear();
            s_suspend = 0;
            Refresh();
        }

        sealed class ReferenceComparer : IEqualityComparer<UnityEngine.Object>
        {
            internal static readonly ReferenceComparer instance = new ReferenceComparer();

            public bool Equals(UnityEngine.Object x, UnityEngine.Object y) => ReferenceEquals(x, y);

            public int GetHashCode(UnityEngine.Object obj) => RuntimeHelpers.GetHashCode(obj);
        }
    }
}
