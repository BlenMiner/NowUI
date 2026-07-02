using System;
using System.Collections.Generic;
using UnityEngine;

namespace NowUI
{
    public interface INowView
    {
        void Draw(NowViewContext context);
    }

    public enum NowViewPresentationKind
    {
        FullScreen,
        Popup
    }

    public enum NowViewTransitionPreset
    {
        None,
        Fade,
        ScaleFade,
        SlideFromBottom,
        SlideFromRight
    }

    public delegate NowViewTransitionState NowViewTransitionDelegate(NowViewTransitionContext context);

    public struct NowViewTransitionState
    {
        public Vector2 offset;

        public Vector2 scale;

        public float alpha;

        public NowViewTransitionState(Vector2 offset, Vector2 scale, float alpha)
        {
            this.offset = offset;
            this.scale = scale;
            this.alpha = alpha;
        }

        public static NowViewTransitionState Identity =>
            new NowViewTransitionState(Vector2.zero, Vector2.one, 1f);
    }

    public readonly struct NowViewTransitionContext
    {
        public readonly NowViewPresentationKind presentationKind;

        public readonly NowRect surface;

        public readonly NowRect rect;

        public readonly float visibleT;

        public readonly bool isEntering;

        public readonly bool isExiting;

        internal NowViewTransitionContext(
            NowViewPresentationKind presentationKind,
            NowRect surface,
            NowRect rect,
            float visibleT,
            bool isEntering,
            bool isExiting)
        {
            this.presentationKind = presentationKind;
            this.surface = surface;
            this.rect = rect;
            this.visibleT = visibleT;
            this.isEntering = isEntering;
            this.isExiting = isExiting;
        }
    }

    public struct NowViewOptions
    {
        const byte PresentationSet = 1 << 0;
        const byte ModalSet = 1 << 1;
        const byte CloseOnCancelSet = 1 << 2;
        const byte CloseOnOutsideClickSet = 1 << 3;
        const byte FitToSurfaceSet = 1 << 4;
        const byte TransitionSet = 1 << 5;
        const byte DurationSet = 1 << 6;
        const byte ScrimSet = 1 << 7;

        const float FullScreenTransitionDuration = 0.16f;
        const float PopupTransitionDuration = 0.12f;

        NowViewPresentationKind _presentationKind;
        NowRect _rect;
        bool _modal;
        bool _closeOnCancel;
        bool _closeOnOutsideClick;
        bool _fitToSurface;
        float _transitionDuration;
        NowViewTransitionPreset _transitionPreset;
        NowViewTransitionDelegate _customTransition;
        Color _scrimColor;
        byte _setMask;

        public NowViewPresentationKind presentationKind => _presentationKind;

        public NowRect rect => _rect;

        public bool modal => _modal;

        public bool closeOnCancel => _closeOnCancel;

        public bool closeOnOutsideClick => _closeOnOutsideClick;

        public bool fitToSurface => _fitToSurface;

        public float transitionDuration => _transitionDuration;

        public NowViewTransitionPreset transitionPreset => _transitionPreset;

        public NowViewTransitionDelegate customTransition => _customTransition;

        public Color scrimColor => _scrimColor;

        public bool hasCustomTransition => _customTransition != null;

        public static NowViewOptions FullScreen(
            NowViewTransitionPreset transition = NowViewTransitionPreset.Fade,
            float transitionDuration = FullScreenTransitionDuration)
        {
            var options = default(NowViewOptions);
            options._presentationKind = NowViewPresentationKind.FullScreen;
            options._modal = true;
            options._closeOnCancel = true;
            options._closeOnOutsideClick = false;
            options._fitToSurface = true;
            options._transitionPreset = transition;
            options._transitionDuration = Mathf.Max(0f, transitionDuration);
            options._setMask = PresentationSet |
                ModalSet |
                CloseOnCancelSet |
                CloseOnOutsideClickSet |
                FitToSurfaceSet |
                TransitionSet |
                DurationSet;
            return options;
        }

        public static NowViewOptions Popup(
            NowRect rect,
            NowViewTransitionPreset transition = NowViewTransitionPreset.ScaleFade,
            float transitionDuration = PopupTransitionDuration)
        {
            var options = FullScreen(transition, transitionDuration);
            options._presentationKind = NowViewPresentationKind.Popup;
            options._rect = rect;
            options._closeOnOutsideClick = true;
            return options;
        }

        public NowViewOptions SetModal(bool value)
        {
            _modal = value;
            _setMask |= ModalSet;
            return this;
        }

        public NowViewOptions SetCloseOnCancel(bool value)
        {
            _closeOnCancel = value;
            _setMask |= CloseOnCancelSet;
            return this;
        }

        public NowViewOptions SetCloseOnOutsideClick(bool value)
        {
            _closeOnOutsideClick = value;
            _setMask |= CloseOnOutsideClickSet;
            return this;
        }

        public NowViewOptions SetFitToSurface(bool value)
        {
            _fitToSurface = value;
            _setMask |= FitToSurfaceSet;
            return this;
        }

        public NowViewOptions SetTransition(NowViewTransitionPreset preset, float duration)
        {
            _transitionPreset = preset;
            _customTransition = null;
            _transitionDuration = Mathf.Max(0f, duration);
            _setMask |= TransitionSet | DurationSet;
            return this;
        }

        public NowViewOptions SetTransition(NowViewTransitionDelegate transition, float duration)
        {
            _transitionPreset = NowViewTransitionPreset.None;
            _customTransition = transition;
            _transitionDuration = Mathf.Max(0f, duration);
            _setMask |= TransitionSet | DurationSet;
            return this;
        }

        public NowViewOptions SetScrim(Color color)
        {
            _scrimColor = color;
            _setMask |= ScrimSet;
            return this;
        }

        internal NowViewOptions Normalized()
        {
            var options = this;

            if ((options._setMask & PresentationSet) == 0)
                options._presentationKind = NowViewPresentationKind.FullScreen;

            if ((options._setMask & ModalSet) == 0)
                options._modal = true;

            if ((options._setMask & CloseOnCancelSet) == 0)
                options._closeOnCancel = true;

            if ((options._setMask & CloseOnOutsideClickSet) == 0)
                options._closeOnOutsideClick = options._presentationKind == NowViewPresentationKind.Popup;

            if ((options._setMask & FitToSurfaceSet) == 0)
                options._fitToSurface = true;

            if ((options._setMask & TransitionSet) == 0)
            {
                options._transitionPreset = options._presentationKind == NowViewPresentationKind.Popup
                    ? NowViewTransitionPreset.ScaleFade
                    : NowViewTransitionPreset.Fade;
            }

            if ((options._setMask & DurationSet) == 0)
            {
                options._transitionDuration = options._presentationKind == NowViewPresentationKind.Popup
                    ? PopupTransitionDuration
                    : FullScreenTransitionDuration;
            }

            if ((options._setMask & ScrimSet) == 0)
                options._scrimColor = default;

            options._transitionDuration = Mathf.Max(0f, options._transitionDuration);
            return options;
        }
    }

    public readonly struct NowViewHandle : IEquatable<NowViewHandle>
    {
        readonly NowViewStack _stack;
        readonly int _entryId;
        readonly int _version;

        internal int entryId => _entryId;

        internal int version => _version;

        internal NowViewHandle(NowViewStack stack, int entryId, int version)
        {
            _stack = stack;
            _entryId = entryId;
            _version = version;
        }

        public bool isValid => _stack != null && _stack.Contains(this);

        public bool Close()
        {
            return _stack != null && _stack.Pop(this);
        }

        public bool Equals(NowViewHandle other)
        {
            return ReferenceEquals(_stack, other._stack) &&
                _entryId == other._entryId &&
                _version == other._version;
        }

        public override bool Equals(object obj)
        {
            return obj is NowViewHandle other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = _stack != null ? _stack.GetHashCode() : 0;
                hash = (hash * 397) ^ _entryId;
                hash = (hash * 397) ^ _version;
                return hash;
            }
        }

        public static bool operator ==(NowViewHandle left, NowViewHandle right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(NowViewHandle left, NowViewHandle right)
        {
            return !left.Equals(right);
        }
    }

    public readonly struct NowViewContext
    {
        readonly NowViewStack _stack;

        public readonly NowViewHandle handle;

        public readonly NowId key;

        public readonly NowRect surface;

        public readonly NowRect rect;

        public readonly float visibleT;

        public readonly bool isTop;

        public readonly bool isEntering;

        public readonly bool isExiting;

        public NowViewStack stack => _stack;

        internal NowViewContext(
            NowViewStack stack,
            NowViewHandle handle,
            NowId key,
            NowRect surface,
            NowRect rect,
            float visibleT,
            bool isTop,
            bool isEntering,
            bool isExiting)
        {
            _stack = stack;
            this.handle = handle;
            this.key = key;
            this.surface = surface;
            this.rect = rect;
            this.visibleT = visibleT;
            this.isTop = isTop;
            this.isEntering = isEntering;
            this.isExiting = isExiting;
        }

        public bool Close()
        {
            return handle.Close();
        }
    }

    public sealed class NowViewStack
    {
        enum Phase
        {
            Entering,
            Open,
            Exiting
        }

        enum PendingKind
        {
            Push,
            Exit,
            Clear,
            Replace
        }

        sealed class Entry
        {
            public INowView view;
            public NowViewOptions options;
            public NowId key;
            public bool hasKey;
            public int entryId;
            public int overlayId;
            public int version;
            public NowViewHandle handle;
            public Phase phase;
            public double transitionStartTime;
            public bool closeQueued;
            public NowRect surface;
            public NowRect rect;
        }

        struct PendingMutation
        {
            public PendingKind kind;
            public Entry entry;
            public bool animate;
        }

        struct DrawTarget
        {
            public NowViewStack stack;
            public Entry entry;
        }

        static readonly Dictionary<int, DrawTarget> s_drawTargets = new Dictionary<int, DrawTarget>(16);

        static int s_nextStackId = 1;

        readonly List<Entry> _entries = new List<Entry>(4);
        readonly List<PendingMutation> _pending = new List<PendingMutation>(4);

        readonly int _stackId;

        int _nextEntryId = 1;
        int _nextVersion = 1;
        int _drawDepth;

        public int count => _entries.Count;

        public bool hasOpenView => _entries.Count > 0;

        public bool isTransitioning
        {
            get
            {
                for (int i = 0; i < _entries.Count; ++i)
                {
                    if (_entries[i].phase != Phase.Open)
                        return true;
                }

                return false;
            }
        }

        public NowViewStack()
        {
            _stackId = NextNonZero(ref s_nextStackId);
        }

        public NowViewHandle Push(INowView view, NowViewOptions options = default)
        {
            return Push(default, view, options);
        }

        public NowViewHandle Push(NowId key, INowView view, NowViewOptions options = default)
        {
            if (view == null)
                throw new ArgumentNullException(nameof(view));

            if (key.hasValue && ContainsKeyIncludingPending(key))
                throw new InvalidOperationException($"A view with key '{key}' is already in the stack.");

            var entry = CreateEntry(key, view, options);

            if (IsDeferringMutations)
            {
                _pending.Add(new PendingMutation { kind = PendingKind.Push, entry = entry });
            }
            else
            {
                _entries.Add(entry);
            }

            NowControlState.RequestRepaint();
            return entry.handle;
        }

        public NowViewHandle PushOrReplace(NowId key, INowView view, NowViewOptions options = default)
        {
            if (!key.hasValue)
                throw new ArgumentException("A replacement key is required.", nameof(key));

            if (view == null)
                throw new ArgumentNullException(nameof(view));

            var entry = FindByKey(key);

            if (entry == null)
                return Push(key, view, options);

            if (IsDeferringMutations)
            {
                _pending.Add(new PendingMutation
                {
                    kind = PendingKind.Replace,
                    entry = CreateReplacement(entry, view, options)
                });
            }
            else
            {
                ReplaceNow(entry, view, options);
            }

            NowControlState.RequestRepaint();
            return entry.handle;
        }

        public bool Pop()
        {
            var entry = FindTopClosableEntry();
            return entry != null && Pop(entry);
        }

        public bool Pop(NowViewHandle handle)
        {
            var entry = FindByHandle(handle);

            if (entry != null)
                return Pop(entry);

            return RemovePendingPush(handle);
        }

        public bool PopKey(NowId key)
        {
            var entry = FindByKey(key);

            if (entry != null)
                return Pop(entry);

            return RemovePendingPush(key);
        }

        public bool PopTo(NowViewHandle handle)
        {
            var target = FindByHandle(handle);

            if (target == null)
                return false;

            return PopEntriesAbove(target);
        }

        public bool PopToKey(NowId key)
        {
            var target = FindByKey(key);

            if (target == null)
                return false;

            return PopEntriesAbove(target);
        }

        public void Clear(bool animate = false)
        {
            if (_entries.Count == 0)
                return;

            if (IsDeferringMutations)
            {
                for (int i = 0; i < _entries.Count; ++i)
                    _entries[i].closeQueued = true;

                _pending.Add(new PendingMutation { kind = PendingKind.Clear, animate = animate });
                return;
            }

            ClearNow(animate);
        }

        public bool Contains(NowViewHandle handle)
        {
            return FindByHandle(handle) != null || FindPendingPush(handle) != null;
        }

        public bool ContainsKey(NowId key)
        {
            return ContainsKeyIncludingPending(key);
        }

        public void Draw(NowRect surface)
        {
            if (surface.isEmpty || NowInput.isPassive)
                return;

            for (int i = 0; i < _entries.Count; ++i)
            {
                var entry = _entries[i];

                if (entry.view == null)
                    continue;

                entry.surface = surface;
                entry.rect = ResolveRect(entry.options, surface);
                s_drawTargets[entry.overlayId] = new DrawTarget { stack = this, entry = entry };

                if (entry.options.modal &&
                    entry.options.presentationKind == NowViewPresentationKind.Popup)
                {
                    NowOverlay.BlockAllSurfaces(entry.overlayId);
                }

                NowOverlay.DeferScreen(entry.rect, entry.overlayId, DrawDeferred);
            }
        }

        Entry CreateEntry(NowId key, INowView view, NowViewOptions options)
        {
            int entryId = NextNonZero(ref _nextEntryId);
            int version = NextNonZero(ref _nextVersion);
            int overlayId = NowInput.CombineId(_stackId, entryId);
            options = options.Normalized();

            var entry = new Entry
            {
                view = view,
                options = options,
                key = key,
                hasKey = key.hasValue,
                entryId = entryId,
                overlayId = overlayId,
                version = version,
                phase = HasAnimatedTransition(options)
                    ? Phase.Entering
                    : Phase.Open,
                transitionStartTime = NowTime.realtimeSinceStartup
            };

            entry.handle = new NowViewHandle(this, entryId, version);
            return entry;
        }

        Entry CreateReplacement(Entry existing, INowView view, NowViewOptions options)
        {
            return new Entry
            {
                view = view,
                options = options,
                entryId = existing.entryId,
                version = existing.version
            };
        }

        void ReplaceNow(Entry entry, INowView view, NowViewOptions options)
        {
            entry.view = view;
            entry.options = options.Normalized();
            entry.phase = HasAnimatedTransition(entry.options)
                    ? Phase.Entering
                    : Phase.Open;
            entry.transitionStartTime = NowTime.realtimeSinceStartup;
            entry.closeQueued = false;
        }

        bool Pop(Entry entry)
        {
            if (entry.phase == Phase.Exiting || entry.closeQueued)
                return false;

            entry.closeQueued = true;

            if (IsDeferringMutations)
            {
                _pending.Add(new PendingMutation { kind = PendingKind.Exit, entry = entry });
            }
            else
            {
                BeginExitNow(entry);
            }

            NowControlState.RequestRepaint();
            return true;
        }

        bool PopEntriesAbove(Entry target)
        {
            int index = _entries.IndexOf(target);

            if (index < 0 || index == _entries.Count - 1)
                return index >= 0;

            bool changed = false;

            for (int i = _entries.Count - 1; i > index; --i)
                changed |= Pop(_entries[i]);

            return changed;
        }

        void BeginExitNow(Entry entry)
        {
            if (!_entries.Contains(entry))
                return;

            entry.closeQueued = false;

            if (entry.options.transitionDuration <= 0f ||
                entry.options.transitionPreset == NowViewTransitionPreset.None &&
                !entry.options.hasCustomTransition)
            {
                _entries.Remove(entry);
                return;
            }

            entry.phase = Phase.Exiting;
            entry.transitionStartTime = NowTime.realtimeSinceStartup;
        }

        void ClearNow(bool animate)
        {
            if (!animate)
            {
                _entries.Clear();
                return;
            }

            for (int i = _entries.Count - 1; i >= 0; --i)
                BeginExitNow(_entries[i]);
        }

        void ApplyPendingMutations()
        {
            if (_pending.Count == 0)
                return;

            for (int i = 0; i < _pending.Count; ++i)
            {
                var mutation = _pending[i];

                switch (mutation.kind)
                {
                    case PendingKind.Push:
                        _entries.Add(mutation.entry);
                        break;
                    case PendingKind.Exit:
                        BeginExitNow(mutation.entry);
                        break;
                    case PendingKind.Clear:
                        ClearNow(mutation.animate);
                        break;
                    case PendingKind.Replace:
                    {
                        var entry = FindByEntryId(mutation.entry.entryId, mutation.entry.version);

                        if (entry != null)
                            ReplaceNow(entry, mutation.entry.view, mutation.entry.options);

                        break;
                    }
                }
            }

            _pending.Clear();
        }

        bool IsDeferringMutations => _drawDepth > 0;

        Entry TopEntry()
        {
            return _entries.Count > 0 ? _entries[_entries.Count - 1] : null;
        }

        Entry FindTopClosableEntry()
        {
            for (int i = _entries.Count - 1; i >= 0; --i)
            {
                var entry = _entries[i];

                if (entry.phase != Phase.Exiting && !entry.closeQueued)
                    return entry;
            }

            return null;
        }

        Entry FindByHandle(NowViewHandle handle)
        {
            if (handle.entryId == 0)
                return null;

            return FindByEntryId(handle.entryId, handle.version);
        }

        Entry FindPendingPush(NowViewHandle handle)
        {
            if (handle.entryId == 0)
                return null;

            for (int i = _pending.Count - 1; i >= 0; --i)
            {
                var mutation = _pending[i];
                var entry = mutation.entry;

                if (mutation.kind == PendingKind.Push &&
                    entry != null &&
                    entry.entryId == handle.entryId &&
                    entry.version == handle.version)
                {
                    return entry;
                }
            }

            return null;
        }

        bool RemovePendingPush(NowViewHandle handle)
        {
            if (handle.entryId == 0)
                return false;

            for (int i = _pending.Count - 1; i >= 0; --i)
            {
                var mutation = _pending[i];
                var entry = mutation.entry;

                if (mutation.kind == PendingKind.Push &&
                    entry != null &&
                    entry.entryId == handle.entryId &&
                    entry.version == handle.version)
                {
                    _pending.RemoveAt(i);
                    return true;
                }
            }

            return false;
        }

        Entry FindByEntryId(int entryId, int version)
        {
            for (int i = 0; i < _entries.Count; ++i)
            {
                var entry = _entries[i];

                if (entry.entryId == entryId && entry.version == version)
                    return entry;
            }

            return null;
        }

        Entry FindByKey(NowId key)
        {
            if (!key.hasValue)
                return null;

            for (int i = _entries.Count - 1; i >= 0; --i)
            {
                var entry = _entries[i];

                if (entry.hasKey && entry.key == key)
                    return entry;
            }

            return null;
        }

        bool ContainsKeyIncludingPending(NowId key)
        {
            if (FindByKey(key) != null)
                return true;

            for (int i = 0; i < _pending.Count; ++i)
            {
                var entry = _pending[i].entry;

                if (entry != null && entry.hasKey && entry.key == key)
                    return true;
            }

            return false;
        }

        bool RemovePendingPush(NowId key)
        {
            if (!key.hasValue)
                return false;

            for (int i = _pending.Count - 1; i >= 0; --i)
            {
                var mutation = _pending[i];
                var entry = mutation.entry;

                if (mutation.kind == PendingKind.Push &&
                    entry != null &&
                    entry.hasKey &&
                    entry.key == key)
                {
                    _pending.RemoveAt(i);
                    return true;
                }
            }

            return false;
        }

        static void DrawDeferred(int overlayId)
        {
            if (!s_drawTargets.TryGetValue(overlayId, out var target))
                return;

            s_drawTargets.Remove(overlayId);
            target.stack.DrawEntry(target.entry);
        }

        void DrawEntry(Entry entry)
        {
            if (entry == null || entry.view == null || !_entries.Contains(entry))
                return;

            ++_drawDepth;

            try
            {
                if (!UpdateTransition(entry, out float visibleT, out bool isEntering, out bool isExiting))
                    return;

                var top = TopEntry();
                bool isTop = ReferenceEquals(entry, top);
                bool liveInput = isTop && entry.phase != Phase.Exiting;
                var transition = EvaluateTransition(entry, visibleT, isEntering, isExiting);
                var context = new NowViewContext(
                    this,
                    entry.handle,
                    entry.key,
                    entry.surface,
                    entry.rect,
                    visibleT,
                    isTop,
                    isEntering,
                    isExiting);

                if (liveInput)
                {
                    DrawEntryContent(entry, context, transition);
                    HandleTopClosePolicy(entry);
                }
                else
                {
                    NowInput.BeginPassive();

                    try
                    {
                        DrawEntryContent(entry, context, transition);
                    }
                    finally
                    {
                        NowInput.EndPassive();
                    }
                }
            }
            finally
            {
                --_drawDepth;

                if (_drawDepth == 0)
                    ApplyPendingMutations();
            }
        }

        bool UpdateTransition(Entry entry, out float visibleT, out bool isEntering, out bool isExiting)
        {
            visibleT = 1f;
            isEntering = entry.phase == Phase.Entering;
            isExiting = entry.phase == Phase.Exiting;

            if (entry.phase == Phase.Open)
                return true;

            float duration = Mathf.Max(0f, entry.options.transitionDuration);

            if (duration <= 0f)
            {
                if (entry.phase == Phase.Exiting)
                    _entries.Remove(entry);
                else
                    entry.phase = Phase.Open;

                return entry.phase != Phase.Exiting;
            }

            float progress = Mathf.Clamp01((float)((NowTime.realtimeSinceStartup - entry.transitionStartTime) / duration));

            if (entry.phase == Phase.Entering)
            {
                visibleT = progress;

                if (progress >= 1f)
                {
                    entry.phase = Phase.Open;
                    isEntering = false;
                    visibleT = 1f;
                }
                else
                {
                    NowControlState.RequestRepaint();
                }

                return true;
            }

            visibleT = 1f - progress;

            if (progress >= 1f)
            {
                _entries.Remove(entry);
                return false;
            }

            NowControlState.RequestRepaint();
            return true;
        }

        NowViewTransitionState EvaluateTransition(Entry entry, float visibleT, bool isEntering, bool isExiting)
        {
            var transitionContext = new NowViewTransitionContext(
                entry.options.presentationKind,
                entry.surface,
                entry.rect,
                visibleT,
                isEntering,
                isExiting);

            if (entry.options.customTransition != null)
                return entry.options.customTransition(transitionContext);

            switch (entry.options.transitionPreset)
            {
                case NowViewTransitionPreset.Fade:
                    return new NowViewTransitionState(Vector2.zero, Vector2.one, visibleT);
                case NowViewTransitionPreset.ScaleFade:
                    return new NowViewTransitionState(
                        Vector2.zero,
                        Vector2.one * Mathf.Lerp(0.96f, 1f, EaseOutCubic(visibleT)),
                        visibleT);
                case NowViewTransitionPreset.SlideFromBottom:
                    return new NowViewTransitionState(
                        new Vector2(0f, entry.rect.height * (1f - EaseOutCubic(visibleT))),
                        Vector2.one,
                        visibleT);
                case NowViewTransitionPreset.SlideFromRight:
                    return new NowViewTransitionState(
                        new Vector2(entry.rect.width * (1f - EaseOutCubic(visibleT)), 0f),
                        Vector2.one,
                        visibleT);
                default:
                    return NowViewTransitionState.Identity;
            }
        }

        void DrawEntryContent(Entry entry, NowViewContext context, NowViewTransitionState transition)
        {
            if (entry.options.modal && entry.options.scrimColor.a > 0f)
            {
                var scrim = entry.options.scrimColor;
                scrim.a *= Mathf.Clamp01(context.visibleT);
                Now.Rectangle(entry.surface).SetColor(scrim).Draw();
            }

            Vector2 scale = transition.scale;

            if (Mathf.Approximately(scale.x, 0f))
                scale.x = 1f;

            if (Mathf.Approximately(scale.y, 0f))
                scale.y = 1f;

            float alpha = Mathf.Clamp01(transition.alpha);
            bool useTransform = transition.offset != Vector2.zero ||
                !Mathf.Approximately(scale.x, 1f) ||
                !Mathf.Approximately(scale.y, 1f);
            bool useAlpha = !Mathf.Approximately(alpha, 1f);
            NowTransformScope transformScope = default;
            bool colorScope = false;

            try
            {
                if (useTransform)
                {
                    Vector2 center = entry.rect.center;
                    Vector2 origin = new Vector2(
                        center.x - center.x * scale.x + transition.offset.x,
                        center.y - center.y * scale.y + transition.offset.y);
                    transformScope = Now.Transform(scale, origin);
                }

                if (useAlpha)
                {
                    Now.BeginColorMultiplier(new Color(1f, 1f, 1f, alpha));
                    colorScope = true;
                }

                entry.view.Draw(context);
            }
            finally
            {
                if (colorScope)
                    Now.EndColorMultiplier();

                transformScope.Dispose();
            }
        }

        void HandleTopClosePolicy(Entry entry)
        {
            if (!NowInput.hasContext || NowInput.isPassive)
                return;

            var snapshot = NowInput.current;

            if (entry.options.closeOnCancel && snapshot.cancelPressed)
            {
                Pop(entry);
                return;
            }

            if (!entry.options.closeOnOutsideClick || !snapshot.hasPointer)
                return;

            bool pressed = snapshot.WasPointerPressed(NowPointerButton.Primary) ||
                snapshot.WasPointerPressed(NowPointerButton.Secondary);

            if (pressed && !entry.rect.Contains(snapshot.pointerPosition))
                Pop(entry);
        }

        static NowRect ResolveRect(NowViewOptions options, NowRect surface)
        {
            if (options.presentationKind == NowViewPresentationKind.FullScreen)
                return surface;

            var rect = options.rect;

            if (!options.fitToSurface || surface.isEmpty)
                return rect;

            float width = rect.width > 0f ? Mathf.Min(rect.width, surface.width) : rect.width;
            float height = rect.height > 0f ? Mathf.Min(rect.height, surface.height) : rect.height;
            float x = width < surface.width
                ? Mathf.Clamp(rect.x, surface.x, surface.xMax - width)
                : surface.x;
            float y = height < surface.height
                ? Mathf.Clamp(rect.y, surface.y, surface.yMax - height)
                : surface.y;

            return new NowRect(x, y, width, height);
        }

        static float EaseOutCubic(float t)
        {
            t = Mathf.Clamp01(t);
            float inv = 1f - t;
            return 1f - inv * inv * inv;
        }

        static bool HasAnimatedTransition(NowViewOptions options)
        {
            return options.transitionDuration > 0f &&
                (options.transitionPreset != NowViewTransitionPreset.None || options.hasCustomTransition);
        }

        static int NextNonZero(ref int value)
        {
            unchecked
            {
                int result = value++;

                if (value == 0)
                    value = 1;

                return result != 0 ? result : 1;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForRuntimeLoad()
        {
            s_drawTargets.Clear();
            s_nextStackId = 1;
        }
    }
}
