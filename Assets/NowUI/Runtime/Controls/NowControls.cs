using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace NowUI
{
    /// <summary>
    /// The control toolkit: id scopes and the shared interaction plumbing that
    /// both the built-in controls and custom controls run on.
    ///
    /// The controls themselves live where they belong:
    /// <see cref="NowLayout"/> for layout-flowing controls
    /// (<c>NowLayout.Button("Save").Draw()</c>) and <see cref="Now"/> for explicit
    /// rects (<c>Now.Button(rect, "Save").Draw()</c>) — mirroring how
    /// <c>NowLayout.Label</c> and <c>Now.Text</c> already split.
    ///
    /// Control API conventions (custom controls should follow them too):
    /// action controls return <c>bool</c> from <c>Draw()</c> — true on click or
    /// submit-while-focused; value controls take <c>Draw(ref value)</c>, mutate
    /// the caller-owned ref and return true when it changed this frame.
    ///
    /// Everything here is public so custom controls are first-class, not
    /// second-class.
    /// </summary>
    public static class NowControls
    {
        static readonly List<NowResolvedId> _idStack = new List<NowResolvedId>(8);

        static readonly NowScopeGuard _idScopes = new NowScopeGuard("NowControls.IdScope", 8);

        static int _idScopeStartedAt = int.MinValue;

        static ulong _nextOwnerNonce;

        sealed class OwnerIdentity
        {
            public readonly NowResolvedId root;

            public OwnerIdentity(NowResolvedId root)
            {
                this.root = root;
            }
        }

        static ConditionalWeakTable<object, OwnerIdentity> _ownerIdentities =
            new ConditionalWeakTable<object, OwnerIdentity>();

        static NowResolvedId _defaultOwnerRoot;

        // The owner root only depends on which input provider is current, and
        // NowInput.Update is the single place that changes it. Caching the last
        // answer turns a per-control ConditionalWeakTable lookup (which locks)
        // into a field read.
        static bool _ownerRootCacheValid;

        static NowResolvedId _ownerRootCache;

        static NowResolvedId AllocateOwnerRoot()
        {
            unchecked
            {
                ++_nextOwnerNonce;

                if (_nextOwnerNonce == 0UL)
                    ++_nextOwnerNonce;

                return NowResolvedId.CreateOwnerRoot(_nextOwnerNonce);
            }
        }

        internal static NowResolvedId AllocateOwnerScope()
        {
            return AllocateOwnerRoot();
        }

        static NowResolvedId CurrentOwnerRoot()
        {
            if (_ownerRootCacheValid)
                return _ownerRootCache;

            object owner = NowInput.currentProvider;
            NowResolvedId root;

            if (owner != null)
            {
                root = _ownerIdentities.GetValue(
                    owner,
                    _ => new OwnerIdentity(AllocateOwnerRoot())).root;
            }
            else
            {
                if (!_defaultOwnerRoot.hasValue)
                    _defaultOwnerRoot = AllocateOwnerRoot();

                root = _defaultOwnerRoot;
            }

            _ownerRootCache = root;
            _ownerRootCacheValid = true;
            return root;
        }

        /// <summary>Called whenever the current input provider changes.</summary>
        internal static void InvalidateOwnerRootCache()
        {
            _ownerRootCacheValid = false;
        }

        static NowResolvedId CurrentIdentityParent()
        {
            return _idStack.Count > 0 ? _idStack[^1] : CurrentOwnerRoot();
        }

        internal static NowResolvedId ResolveScopedControlId(int id)
        {
            return CurrentIdentityParent().Derive(NowIdDomain.Control, id);
        }

        /// <summary>Captures the fully resolved innermost id scope for deferred drawing.</summary>
        internal static NowResolvedId CaptureIdScope()
        {
            return CurrentIdentityParent();
        }

        /// <summary>
        /// Temporarily restores an already-resolved id scope. Unlike IdScope(int),
        /// this does not combine it with the current scope: the captured value
        /// already contains its complete host and nested-scope ancestry.
        /// </summary>
        internal static ControlIdScope RestoreIdScope(NowResolvedId resolvedScopeId)
        {
            if (!resolvedScopeId.hasValue)
                throw new ArgumentException("A resolved scope id is required.", nameof(resolvedScopeId));

            _idStack.Add(resolvedScopeId);
            return new ControlIdScope(EnterIdScope());
        }

        static int EnterIdScope()
        {
            if (_idScopes.count == 0)
                _idScopeStartedAt = Time.frameCount;

            return _idScopes.Enter();
        }

        internal static bool hasActiveIdScopesThisFrame =>
            _idScopes.count > 0 && _idScopeStartedAt == Time.frameCount;

        /// <summary>The active theme, provided by <see cref="NowTheme"/>.</summary>
        public static NowThemeAsset themeAsset => NowTheme.themeAsset;

        /// <summary>Pushes a contextual theme; dispose the scope to restore the previous one.</summary>
        public static ThemeScope Theme(NowThemeAsset value)
        {
            return NowTheme.Scope(value);
        }

        internal static NowText Text(NowThemeAsset activeThemeAsset, NowTextStyle textStyle)
        {
            return activeThemeAsset.Text(default, textStyle);
        }

        /// <summary>
        /// Pushes one identity boundary for a composite custom-control invocation.
        /// Descendant control ids are mixed with this invocation, so reusable
        /// controls can safely use local child ids such as "label" and "input".
        /// When <paramref name="id"/> is default, repeated calls from one site are
        /// occurrence-salted in draw order; provide a stable id for reorderable
        /// data and forward the caller-info parameters from wrapper APIs.
        /// </summary>
        public static ControlIdScope ControlScope(
            NowId id = default,
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0)
        {
            NowResolvedId resolved = GetControlId(id, SiteToken(file, line));
            _idStack.Add(resolved);
            return new ControlIdScope(EnterIdScope());
        }

        /// <summary>Pushes an identity that has already been resolved exactly once.</summary>
        public static ControlIdScope ControlScope(NowResolvedId id)
        {
            return RestoreIdScope(id);
        }

        /// <summary>
        /// Disambiguates controls with the same label drawn in loops or repeated
        /// panels: ids derive from the label hashed against the innermost scope.
        /// <code>
        /// using (NowControls.IdScope($"row-{i}"))
        ///     NowLayout.Button("Delete").Draw();
        /// </code>
        /// </summary>
        public static ControlIdScope IdScope(string name)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("ID scope names cannot be null or empty.", nameof(name));

            _idStack.Add(CurrentIdentityParent().Derive(NowIdDomain.Scope, name));
            return new ControlIdScope(EnterIdScope());
        }

        /// <summary>
        /// Disambiguates repeated panels using an existing <see cref="NowId"/>;
        /// a default (empty) id leaves the scope stack untouched. String ids nest
        /// exactly like <see cref="IdScope(string)"/>.
        /// </summary>
        public static ControlIdScope IdScope(NowId id)
        {
            if (!id.hasValue)
                return default;

            _idStack.Add(CurrentIdentityParent().Derive(NowIdDomain.Scope, id));
            return new ControlIdScope(EnterIdScope());
        }

        /// <summary>Pushes a fully resolved scope without resolving it again.</summary>
        public static ControlIdScope IdScope(NowResolvedId id)
        {
            return RestoreIdScope(id);
        }

        /// <summary>
        /// Disambiguates repeated panels or hosts using an existing stable integer
        /// id, such as a component instance id, without allocating a string.
        /// </summary>
        public static ControlIdScope IdScope(int id)
        {
            _idStack.Add(CurrentIdentityParent().Derive(NowIdDomain.Scope, id));
            return new ControlIdScope(EnterIdScope());
        }

        /// <summary>
        /// Opens a stable identity scope for one item in a repeated collection.
        /// The required domain key survives insertion, removal and reordering;
        /// the caller site supplies a separate list namespace automatically.
        /// </summary>
        public static NowKeyedItemScope KeyedItem(
            NowId key,
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0)
        {
            if (!key.hasValue)
                throw new ArgumentException("A keyed item requires a non-empty domain key.", nameof(key));

            int siteToken = SiteToken(file, line);
            NowResolvedId list = ResolveCallSiteStable(siteToken, NowIdDomain.Scope);
            NowResolvedId item = list.Derive(NowIdDomain.Scope, key);
            return new NowKeyedItemScope(RestoreIdScope(item));
        }

        /// <summary>
        /// Opens a stable repeated-item scope under an explicit list namespace.
        /// Use this overload when helper methods at different call sites draw the
        /// same logical collection.
        /// </summary>
        public static NowKeyedItemScope KeyedItemIn(NowId listId, NowId key)
        {
            if (!listId.hasValue)
                throw new ArgumentException("An explicit list id is required.", nameof(listId));

            if (!key.hasValue)
                throw new ArgumentException("A keyed item requires a non-empty domain key.", nameof(key));

            NowResolvedId list = CurrentIdentityParent().Derive(NowIdDomain.Scope, listId);
            NowResolvedId item = list.Derive(NowIdDomain.Scope, key);
            return new NowKeyedItemScope(RestoreIdScope(item));
        }

        internal static void PopIdScope(int token)
        {
            if (_idScopes.Exit(token) && _idStack.Count > 0)
                _idStack.RemoveAt(_idStack.Count - 1);
        }

        static bool _warnedLeakedIdScope;

        /// <summary>
        /// Frame-entry self-heal called by <c>Now.StartUI</c>: clears id scopes a
        /// previous frame leaked so a forgotten Dispose cannot silently re-scope
        /// every control id in the app, and reports the leak once.
        /// </summary>
        internal static void ResetIdScopesForFrame()
        {
            if (_idStack.Count == 0)
            {
                _warnedLeakedIdScope = false;
                return;
            }

            _idStack.Clear();
            _idScopes.Clear();
            _idScopeStartedAt = int.MinValue;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!_warnedLeakedIdScope)
            {
                _warnedLeakedIdScope = true;
                Debug.LogWarning("NowUI: a NowControls.IdScope from the previous frame was never disposed; the id scope stack was reset. Wrap the scope in a using statement.");
            }
#endif
        }

        static readonly Dictionary<NowResolvedId, int> _labelOccurrences =
            new Dictionary<NowResolvedId, int>(32);

        static readonly Dictionary<NowResolvedId, int> _passiveOccurrences =
            new Dictionary<NowResolvedId, int>(32);

        static readonly Dictionary<NowResolvedId, int> _passiveReplayBase =
            new Dictionary<NowResolvedId, int>(32);

        struct InteractionRepaintState
        {
            public bool hovered;

            public bool held;

            public bool focused;
        }

        readonly struct CallSiteKey : IEquatable<CallSiteKey>
        {
            public readonly string file;

            public readonly int line;

            public CallSiteKey(string file, int line)
            {
                this.file = file;
                this.line = line;
            }

            public bool Equals(CallSiteKey other)
            {
                return line == other.line &&
                    string.Equals(file, other.file, StringComparison.Ordinal);
            }

            public override bool Equals(object obj)
            {
                return obj is CallSiteKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((file != null ? StringComparer.Ordinal.GetHashCode(file) : 0) * 397) ^ line;
                }
            }
        }

        readonly struct CallSiteRecord
        {
            public readonly ulong payload;

            public readonly int line;

            public CallSiteRecord(string file, int line)
            {
                payload = NowIdHash.HashCallSite(file, line);
                this.line = line;
            }
        }

        static readonly Dictionary<CallSiteKey, int> _callSiteTokens =
            new Dictionary<CallSiteKey, int>(128);

        // [CallerFilePath] hands every call site the same interned literal, so a
        // reference-keyed front cache answers repeat lookups without hashing the
        // whole path string. Content-equal paths that are distinct instances
        // still fall through to the ordinal table and share its token.
        sealed class CallSiteReferenceComparer : IEqualityComparer<CallSiteKey>
        {
            public static readonly CallSiteReferenceComparer Instance = new CallSiteReferenceComparer();

            public bool Equals(CallSiteKey x, CallSiteKey y)
            {
                return x.line == y.line && ReferenceEquals(x.file, y.file);
            }

            public int GetHashCode(CallSiteKey key)
            {
                unchecked
                {
                    return (System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(key.file) * 397) ^ key.line;
                }
            }
        }

        const int CallSiteReferenceCacheLimit = 4096;

        static readonly Dictionary<CallSiteKey, int> _callSiteTokensByReference =
            new Dictionary<CallSiteKey, int>(128, CallSiteReferenceComparer.Instance);

        static readonly Dictionary<int, CallSiteRecord> _callSites =
            new Dictionary<int, CallSiteRecord>(128);

        static int _nextCallSiteToken;

        /// <summary>
        /// Interns a call site (file + line) into an opaque runtime token. The token
        /// is not itself an authored identity: the full path and line are hashed once,
        /// then combined with the active 64-bit owner/scope path at resolution,
        /// avoiding the old 32-bit call-site collision boundary. The control
        /// factories capture their caller via [CallerFilePath]/[CallerLineNumber]
        /// and pass it here, so every textual call site is automatically its own
        /// control — no explicit id needed. Loops share a site and are
        /// disambiguated by per-frame occurrence when the typed fallback is
        /// passed to <see cref="GetControlId(NowId, NowCallSiteId)"/>.
        /// Custom controls get the same behavior by declaring the caller-info
        /// parameters themselves and forwarding them here.
        /// Equivalent path strings share a token even when they are not the same
        /// string instance. Tokens are process-local and must not be persisted.
        /// </summary>
        public static NowCallSiteId SiteId(string file, int line)
        {
            return new NowCallSiteId(SiteToken(file, line));
        }

        internal static int SiteToken(string file, int line)
        {
            var key = new CallSiteKey(file, line);

            if (_callSiteTokensByReference.TryGetValue(key, out int token))
                return token;

            if (_callSiteTokens.TryGetValue(key, out token))
            {
                if (_callSiteTokensByReference.Count >= CallSiteReferenceCacheLimit)
                    _callSiteTokensByReference.Clear();

                _callSiteTokensByReference[key] = token;
                return token;
            }

            do
            {
                token = unchecked(++_nextCallSiteToken);
            }
            while (token == 0 || _callSites.ContainsKey(token));

            _callSiteTokens.Add(key, token);
            _callSites.Add(token, new CallSiteRecord(file, line));

            if (_callSiteTokensByReference.Count >= CallSiteReferenceCacheLimit)
                _callSiteTokensByReference.Clear();

            _callSiteTokensByReference[key] = token;
            return token;
        }

        /// <summary>
        /// Derives a control id from an explicit string id within the active id
        /// scope. Explicit ids are stable — never occurrence-salted — so the same
        /// name resolves to the same control from anywhere under the same scope;
        /// two controls sharing one explicit id in one frame share state, which
        /// is the caller's bug, not something to silently disambiguate.
        /// </summary>
        public static NowResolvedId GetControlId(string id)
        {
            if (string.IsNullOrEmpty(id))
                throw new ArgumentException("Control id strings cannot be null or empty.", nameof(id));

            return CurrentIdentityParent().Derive(NowIdDomain.Control, id);
        }

        /// <summary>
        /// Resolves an authored id once beneath the active owner/scope. Default
        /// identity uses the caller site and receives loop occurrence salting.
        /// </summary>
        public static NowResolvedId GetControlId(
            NowId id = default,
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0)
        {
            return GetControlId(id, SiteToken(file, line));
        }

        /// <summary>
        /// Resolves an optional explicit id to a control id, falling back to the
        /// captured call-site identity when the id is default. Explicit ids are
        /// stable (both strings and integers are authored local path segments);
        /// only the call-site fallback is occurrence-salted. Pass an already
        /// resolved identity directly to the API that consumes it.
        /// </summary>
        public static NowResolvedId GetControlId(
            NowId id,
            NowCallSiteId fallbackIdentity)
        {
            return GetControlId(id, fallbackIdentity.token);
        }

        internal static NowResolvedId GetControlId(NowId id, int fallbackIdentity)
        {
            if (!id.hasValue)
                return GetControlId(fallbackIdentity);

            return CurrentIdentityParent().Derive(NowIdDomain.Control, id);
        }

        internal static NowResolvedId ResolveNavigationTargetId(NowId id)
        {
            if (!id.hasValue)
                return NowResolvedId.None;

            return CurrentIdentityParent().Derive(NowIdDomain.Control, id);
        }

        /// <summary>
        /// Derives a control id from an internal call-site token within the active
        /// id scope. Repeated draws of the
        /// same identity in one frame — loop iterations over a single call site —
        /// are salted by occurrence so they never share interaction state; the
        /// first occurrence keeps the stable id. Occurrence order follows draw
        /// order, so when looped controls appear, vanish, or reorder, prefer
        /// <c>SetId</c> or an <see>
        ///     <cref>IdScope</cref>
        /// </see>
        /// keyed by your data — explicit ids are never salted.
        /// </summary>
        internal static NowResolvedId GetControlId(int identity)
        {
            return Salt(ResolveCallSiteStable(identity, NowIdDomain.Control));
        }

        internal static NowResolvedId ResolveCallSiteStable(int identity, NowIdDomain domain)
        {
            return ResolveCallSiteStable(CurrentIdentityParent(), identity, domain);
        }

        internal static NowResolvedId ResolveCallSiteStable(
            NowResolvedId parent,
            int identity,
            NowIdDomain domain)
        {
            if (!parent.hasValue)
                throw new ArgumentException("A resolved call-site parent is required.", nameof(parent));

            if (_callSites.TryGetValue(identity, out var site))
                return NowIdHash.DeriveCallSiteHash(parent, domain, site.payload, site.line);

            return NowIdHash.DeriveCallSiteToken(parent, domain, identity);
        }

        internal static NowResolvedId ResolveScopedId(NowIdDomain domain, NowId id)
        {
            if (!id.hasValue)
                throw new ArgumentException("A non-empty authored id is required.", nameof(id));

            return CurrentIdentityParent().Derive(domain, id);
        }

        internal static NowResolvedId ResolveCallSite(int identity, NowIdDomain domain)
        {
            return Salt(ResolveCallSiteStable(identity, domain));
        }

        static NowResolvedId Salt(NowResolvedId id)
        {
            // Measure passes draw the same controls again, so they count in a
            // replay table seeded from the real pass's current occurrence offsets:
            // occurrence N during measurement resolves to occurrence N again when
            // the real replay rewinds to that same base.
            var occurrences = NowInput.isPassive ? _passiveOccurrences : _labelOccurrences;

            if (occurrences.TryGetValue(id, out int occurrence))
            {
                occurrences[id] = occurrence + 1;

                return NowIdHash.DeriveOccurrence(id, occurrence);
            }

            occurrences[id] = 1;
            return id;
        }

        /// <summary>Starts a fresh occurrence count; called when an input surface begins.</summary>
        internal static void ResetControlIdOccurrences()
        {
            _labelOccurrences.Clear();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _interactedIds.Clear();
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        static readonly HashSet<NowResolvedId> _interactedIds = new HashSet<NowResolvedId>(64);

        static readonly HashSet<NowResolvedId> _duplicateWarnedIds = new HashSet<NowResolvedId>(8);
#endif

        /// <summary>
        /// Editor/development-build check: warns when two controls resolve to the
        /// same id in one pass. Call-site identity can't collide (occurrence
        /// salting), so a duplicate means an explicit id was reused — the
        /// controls silently share focus, state and interaction, which is
        /// almost never intended. Off in release builds; disable via
        /// <see cref="warnOnDuplicateControlIds"/> if a custom control draws
        /// one identity twice on purpose.
        /// </summary>
        public static bool warnOnDuplicateControlIds = true;

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        static void CheckDuplicateControlId(NowResolvedId id)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!warnOnDuplicateControlIds || NowInput.isPassive)
                return;

            if (_interactedIds.Add(id) || !_duplicateWarnedIds.Add(id))
                return;

            Debug.LogWarning(
                $"NowUI: two controls resolved to the same id ({id}) in one pass — they share focus, state and " +
                "interaction. An explicit id is being reused: give each control its own identity with SetId keyed by " +
                "your data, wrap repeated panels in NowControls.IdScope, or mint sub-region ids with " +
                "parentId.Child(seed). (Warned once per id; NowControls.warnOnDuplicateControlIds " +
                "disables this check.)");
#endif
        }

        /// <summary>
        /// Starts a passive replay from the real pass's current occurrence offsets.
        /// This keeps repeated measured regions called from one loop aligned with
        /// their later real pass instead of restarting every region at occurrence 0.
        /// </summary>
        internal static void ResetPassiveControlIdOccurrences()
        {
            _passiveOccurrences.Clear();

            foreach (var pair in _labelOccurrences)
                _passiveOccurrences.Add(pair.Key, pair.Value);
        }

        /// <summary>Snapshots passive occurrence offsets before a measured replay.</summary>
        internal static void CapturePassiveControlIdOccurrences()
        {
            _passiveReplayBase.Clear();

            foreach (var pair in _passiveOccurrences)
                _passiveReplayBase.Add(pair.Key, pair.Value);
        }

        /// <summary>Rewinds measured passive occurrences so the real replay resolves identical ids.</summary>
        internal static void RestorePassiveControlIdOccurrences()
        {
            _passiveOccurrences.Clear();

            foreach (var pair in _passiveReplayBase)
                _passiveOccurrences.Add(pair.Key, pair.Value);

            _passiveReplayBase.Clear();
        }

        /// <summary>
        /// Reserves every occurrence consumed by a passive-only region in the
        /// surrounding real pass. Exact measure passes rewind their passive table
        /// before leaving, so their suppressed replay still commits only its base.
        /// </summary>
        internal static void CommitPassiveControlIdOccurrences()
        {
            _labelOccurrences.Clear();

            foreach (var pair in _passiveOccurrences)
                _labelOccurrences.Add(pair.Key, pair.Value);
        }

        /// <summary>Clears id scopes, occurrence tables and theme overrides (tests/domain reloads).</summary>
        public static void Reset()
        {
            NowTheme.Reset();
            _ownerRootCacheValid = false;
            _idStack.Clear();
            _idScopes.Clear();
            _idScopeStartedAt = int.MinValue;
            _labelOccurrences.Clear();
            _passiveOccurrences.Clear();
            _passiveReplayBase.Clear();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _interactedIds.Clear();
            _duplicateWarnedIds.Clear();
#endif
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForRuntimeLoad()
        {
            Reset();
        }

        /// <summary>
        /// The standard interaction bundle for a control: pointer interaction, focus
        /// registration, click-to-focus, and submit activation — the same sequence
        /// every built-in control runs first.
        /// </summary>
        public static NowInteraction Interact(
            NowRect rect,
            out bool focused,
            out bool submitted,
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0)
        {
            return Interact(GetControlId(SiteToken(file, line)), rect, out focused, out submitted);
        }

        /// <summary>
        /// The standard interaction bundle with call-site identity and explicit
        /// focus navigation targets.
        /// </summary>
        public static NowInteraction Interact(
            NowRect rect,
            NowFocusNavigation navigation,
            out bool focused,
            out bool submitted,
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0)
        {
            return Interact(GetControlId(SiteToken(file, line)), rect, navigation, out focused, out submitted);
        }

        /// <summary>
        /// The standard interaction bundle with optional explicit identity. When
        /// <paramref name="id"/> is default, identity falls back to the call site.
        /// </summary>
        public static NowInteraction Interact(
            NowId id,
            NowRect rect,
            out bool focused,
            out bool submitted,
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0)
        {
            return Interact(GetControlId(id, SiteToken(file, line)), rect, out focused, out submitted);
        }

        /// <summary>
        /// The standard interaction bundle with optional explicit identity and
        /// explicit focus navigation targets.
        /// </summary>
        public static NowInteraction Interact(
            NowId id,
            NowRect rect,
            NowFocusNavigation navigation,
            out bool focused,
            out bool submitted,
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0)
        {
            return Interact(GetControlId(id, SiteToken(file, line)), rect, navigation, out focused, out submitted);
        }

        /// <summary>
        /// The standard interaction bundle for builders that already captured a
        /// fallback call-site identity in their factory.
        /// </summary>
        public static NowInteraction Interact(
            NowId id,
            NowCallSiteId fallbackIdentity,
            NowRect rect,
            out bool focused,
            out bool submitted)
        {
            return Interact(GetControlId(id, fallbackIdentity), rect, out focused, out submitted);
        }

        /// <summary>
        /// The standard interaction bundle for builders with a captured fallback
        /// identity and explicit focus navigation targets.
        /// </summary>
        public static NowInteraction Interact(
            NowId id,
            NowCallSiteId fallbackIdentity,
            NowRect rect,
            NowFocusNavigation navigation,
            out bool focused,
            out bool submitted)
        {
            return Interact(GetControlId(id, fallbackIdentity), rect, navigation, out focused, out submitted);
        }

        public static NowInteraction Interact(NowResolvedId id, NowRect rect, out bool focused, out bool submitted)
        {
            return Interact(id, rect, default, out focused, out submitted);
        }

        /// <summary>Standard interaction for a composite parent with child hit exclusions.</summary>
        public static NowInteraction Interact(
            NowResolvedId id,
            in NowInteractionRegion region,
            out bool focused,
            out bool submitted)
        {
            return Interact(id, in region, default, out focused, out submitted);
        }

        [Obsolete("Raw integer control identities were removed. Use Interact(NowResolvedId, ...).", true)]
        public static NowInteraction Interact(int id, NowRect rect, out bool focused, out bool submitted)
        {
            return Interact(NowResolvedId.FromLegacy(id), rect, out focused, out submitted);
        }

        /// <summary>
        /// The standard interaction bundle with explicit focus navigation targets.
        /// </summary>
        public static NowInteraction Interact(NowResolvedId id, NowRect rect, NowFocusNavigation navigation, out bool focused, out bool submitted)
        {
            return Interact(id, rect, navigation, NowFocusNavigationLock.None, false, out focused, out submitted);
        }

        public static NowInteraction Interact(
            NowResolvedId id,
            in NowInteractionRegion region,
            NowFocusNavigation navigation,
            out bool focused,
            out bool submitted)
        {
            return Interact(
                id,
                in region,
                navigation,
                NowFocusNavigationLock.None,
                false,
                acceptsSubmit: true,
                out focused,
                out submitted);
        }

        [Obsolete("Raw integer control identities were removed. Use Interact(NowResolvedId, ...).", true)]
        public static NowInteraction Interact(int id, NowRect rect, NowFocusNavigation navigation, out bool focused, out bool submitted)
        {
            return Interact(NowResolvedId.FromLegacy(id), rect, navigation, out focused, out submitted);
        }

        /// <summary>
        /// The standard interaction bundle with focused-input ownership metadata.
        /// </summary>
        public static NowInteraction Interact(NowResolvedId id, NowRect rect, NowFocusNavigation navigation,
            NowFocusNavigationLock navigationLock, bool consumesCancel, out bool focused, out bool submitted)
        {
            return Interact(
                id,
                rect,
                navigation,
                navigationLock,
                consumesCancel,
                acceptsSubmit: true,
                out focused,
                out submitted);
        }

        [Obsolete("Raw integer control identities were removed. Use Interact(NowResolvedId, ...).", true)]
        public static NowInteraction Interact(int id, NowRect rect, NowFocusNavigation navigation,
            NowFocusNavigationLock navigationLock, bool consumesCancel, out bool focused, out bool submitted)
        {
            return Interact(
                NowResolvedId.FromLegacy(id),
                rect,
                navigation,
                navigationLock,
                consumesCancel,
                out focused,
                out submitted);
        }

        /// <summary>
        /// Standard interaction with an explicit submit policy. Text editors
        /// disable generic button-style submit so Space remains text and Enter
        /// is classified by the editor before the native key is claimed.
        /// </summary>
        internal static NowInteraction Interact(NowResolvedId id, NowRect rect, NowFocusNavigation navigation,
            NowFocusNavigationLock navigationLock, bool consumesCancel, bool acceptsSubmit,
            out bool focused, out bool submitted)
        {
            var region = new NowInteractionRegion(rect);
            return Interact(
                id,
                in region,
                navigation,
                navigationLock,
                consumesCancel,
                acceptsSubmit,
                out focused,
                out submitted);
        }

        internal static NowInteraction Interact(NowResolvedId id, in NowInteractionRegion region,
            NowFocusNavigation navigation, NowFocusNavigationLock navigationLock,
            bool consumesCancel, bool acceptsSubmit, out bool focused, out bool submitted)
        {
            CheckDuplicateControlId(id);
            var interaction = NowInput.Interact(id, in region);
            NowFocus.Register(id, region.bounds, navigation, navigationLock, consumesCancel);

            if (interaction.pressed)
                NowFocus.Focus(id);

            focused = NowFocus.IsFocused(id);
            submitted = acceptsSubmit && NowFocus.SubmitPressed(id);

            if (!NowInput.isPassive)
            {
                bool active = interaction.hovered || interaction.held || focused;

                // The private state type already isolates these slots from other
                // control state, so the resolved control id needs no extra child.
                // Untouched controls already have the implicit all-false state,
                // so do not allocate and update a persistent entry for each one.
                // Once a control has been active, preserve the true -> false edge
                // so retained hosts still repaint when hover/hold/focus leaves.
                if (active)
                {
                    ref var repaint = ref NowControlState.Get<InteractionRepaintState>(id);

                    if (repaint.hovered != interaction.hovered ||
                        repaint.held != interaction.held ||
                        repaint.focused != focused)
                    {
                        NowControlState.RequestRepaint();
                    }

                    repaint.hovered = interaction.hovered;
                    repaint.held = interaction.held;
                    repaint.focused = focused;
                }
                else if (NowControlState.TryRead<InteractionRepaintState>(id, out var repaint) &&
                    (repaint.hovered || repaint.held || repaint.focused))
                {
                    NowControlState.RequestRepaint();

                    ref var stored = ref NowControlState.Get<InteractionRepaintState>(id);
                    stored = default;
                }
            }

            return interaction;
        }

        internal static NowInteraction Interact(int id, NowRect rect, NowFocusNavigation navigation,
            NowFocusNavigationLock navigationLock, bool consumesCancel, bool acceptsSubmit,
            out bool focused, out bool submitted)
        {
            return Interact(
                NowResolvedId.FromLegacy(id),
                rect,
                navigation,
                navigationLock,
                consumesCancel,
                acceptsSubmit,
                out focused,
                out submitted);
        }

        /// <summary>
        /// Hover/press state applied on top of a resolved color: mixes toward
        /// white on dark colors and toward black on light ones, so feedback stays
        /// visible in both light and dark themes. Amounts come from the theme's
        /// hover/pressed state opacities.
        /// </summary>
        public static Vector4 StateColor(Vector4 color, float hoverT, bool held)
        {
            return StateColor(NowTheme.themeAsset, color, hoverT, held);
        }

        public static Vector4 StateColor(NowThemeAsset themeAsset, Vector4 color, float hoverT, bool held)
        {
            float opacity;

            if (themeAsset != null)
            {
                ref readonly var styles = ref themeAsset.controlStyles;
                opacity = held ? styles.pressedStateOpacity : styles.hoverStateOpacity;
            }
            else
            {
                var styles = NowControlStyleSet.Default;
                opacity = held ? styles.pressedStateOpacity : styles.hoverStateOpacity;
            }

            float amount = held ? opacity : opacity * Mathf.Clamp01(hoverT);

            if (amount <= 0f)
                return color;

            float luminance = color.x * 0.2126f + color.y * 0.7152f + color.z * 0.0722f;
            float overlay = luminance < 0.5f ? 1f : 0f;
            color.x = Mathf.LerpUnclamped(color.x, overlay, amount);
            color.y = Mathf.LerpUnclamped(color.y, overlay, amount);
            color.z = Mathf.LerpUnclamped(color.z, overlay, amount);
            return color;
        }

        internal static NowRect ReserveRect(bool hasRect, NowRect rect, NowLayoutOptions options, Vector2 contentSize)
        {
            if (hasRect)
                return rect;

            if (!options.Has(NowLayoutOptions.Field.Width) && !options.Has(NowLayoutOptions.Field.StretchWidth))
                options = options.SetWidth(contentSize.x);

            if (!options.Has(NowLayoutOptions.Field.Height) && !options.Has(NowLayoutOptions.Field.StretchHeight))
                options = options.SetHeight(contentSize.y);

            return NowLayout.ReserveRect(options);
        }

        static string _labelMeasureText;
        static NowFontAsset _labelMeasureFont;
        static float _labelMeasureFontSize;
        static NowFontStyle _labelMeasureStyle;
        static Vector2 _labelMeasureSize;
        static int _labelMeasureRevision;

        /// <summary>
        /// One-entry measure memo for the label helpers: controls measure their
        /// label for sizing and again for centering in the same draw, so the
        /// second call is a repeat of the first for free.
        /// </summary>
        static Vector2 MeasureLabel(in NowText text, string label)
        {
            if (label != null &&
                ReferenceEquals(_labelMeasureText, label) &&
                ReferenceEquals(_labelMeasureFont, text.font) &&
                _labelMeasureFontSize == text.fontSize &&
                _labelMeasureStyle == text.fontStyle &&
                _labelMeasureRevision == Now.textPreprocessorRevision)
            {
                return _labelMeasureSize;
            }

            Vector2 size = text.Measure(label);
            _labelMeasureText = label;
            _labelMeasureFont = text.font;
            _labelMeasureFontSize = text.fontSize;
            _labelMeasureStyle = text.fontStyle;
            _labelMeasureSize = size;
            _labelMeasureRevision = Now.textPreprocessorRevision;
            return size;
        }

        internal static void DrawCenteredLabel(NowThemeAsset activeThemeAsset, NowRect rect, string label, NowTextStyle textStyle, NowRect mask)
        {
            DrawCenteredLabel(activeThemeAsset, rect, label, textStyle, mask, default, false);
        }

        internal static void DrawCenteredLabel(NowThemeAsset activeThemeAsset, NowRect rect, string label, NowTextStyle textStyle, NowRect mask, Color color)
        {
            DrawCenteredLabel(activeThemeAsset, rect, label, textStyle, mask, color, true);
        }

        static void DrawCenteredLabel(NowThemeAsset activeThemeAsset, NowRect rect, string label, NowTextStyle textStyle, NowRect mask, Color color, bool overrideColor)
        {
            var text = Text(activeThemeAsset, textStyle);
            Vector2 size = MeasureLabel(in text, label);
            float pad = 1f;

            text.rect = new NowRect(
                rect.x + (rect.width - size.x) * 0.5f,
                rect.y + (rect.height - size.y) * 0.5f,
                size.x + pad,
                size.y + pad);

            if (overrideColor)
                text = text.SetColor(color);

            text.SetMask(mask).Draw(label);
        }

        /// <summary>
        /// Draws a vertically centered, left-aligned label. The style is built from a
        /// default rect whose zero mask would clip everything, so the mask is reset to
        /// the given area (slightly outset so descenders survive; long values get cut).
        /// </summary>
        internal static void DrawLeftLabel(NowThemeAsset activeThemeAsset, NowRect rect, string label, NowTextStyle textStyle)
        {
            DrawLeftLabel(activeThemeAsset, rect, label, textStyle, default, false);
        }

        internal static void DrawLeftLabel(NowThemeAsset activeThemeAsset, NowRect rect, string label, NowTextStyle textStyle, Color color)
        {
            DrawLeftLabel(activeThemeAsset, rect, label, textStyle, color, true);
        }

        /// <summary>
        /// Draws a field's placeholder where its value would go: muted and
        /// italic, which is how <see cref="NowTextField"/> and
        /// <see cref="NowTextArea"/> already draw theirs, so an empty field
        /// reads the same whichever control it is.
        /// </summary>
        internal static void DrawLeftPlaceholder(
            NowThemeAsset activeThemeAsset,
            NowRect rect,
            string placeholder)
        {
            DrawLeftLabel(
                activeThemeAsset,
                rect,
                placeholder,
                NowTextStyle.Muted,
                default,
                overrideColor: false,
                italic: true);
        }

        static void DrawLeftLabel(
            NowThemeAsset activeThemeAsset,
            NowRect rect,
            string label,
            NowTextStyle textStyle,
            Color color,
            bool overrideColor,
            bool italic = false)
        {
            var text = Text(activeThemeAsset, textStyle);

            if (italic)
                text = text.SetItalic();

            Vector2 size = MeasureLabel(in text, label);
            float pad = 1f;

            text.rect = new NowRect(
                rect.x,
                rect.y + (rect.height - size.y) * 0.5f,
                size.x + pad,
                size.y + pad);

            if (overrideColor)
                text = text.SetColor(color);

            text.SetMask(rect.Outset(0f, 4f)).Draw(label);
        }
    }

    [NowScope]
    public struct ControlIdScope : IDisposable
    {
        int _token;

        internal ControlIdScope(int token)
        {
            _token = token;
        }

        public void Dispose()
        {
            if (_token == 0)
                return;

            NowControls.PopIdScope(_token);
            _token = 0;
        }
    }

    /// <summary>Disposable identity scope returned by <see cref="NowControls.KeyedItem(NowId, string, int)"/>.</summary>
    [NowScope]
    public struct NowKeyedItemScope : IDisposable
    {
        ControlIdScope _scope;

        internal NowKeyedItemScope(ControlIdScope scope)
        {
            _scope = scope;
        }

        public void Dispose()
        {
            _scope.Dispose();
            _scope = default;
        }
    }
}
