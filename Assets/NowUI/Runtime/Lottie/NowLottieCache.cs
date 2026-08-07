using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace NowUI
{
    public enum NowLottieCacheState
    {
        Empty,
        Loading,
        Loaded,
        Failed
    }

    public static class NowLottieCache
    {
        sealed class Entry
        {
            public string url;
            public NowLottieAsset asset;
            public NowLottieCacheState state;
            public string error;
            public bool ownsAsset;
            public bool active;
            public long lastAccess;
            public Coroutine coroutine;
            public UnityWebRequest request;
            public LinkedListNode<Entry> pendingNode;
        }

        sealed class Runner : MonoBehaviour
        {
            void Update()
            {
                Tick();
            }
        }

        static readonly Dictionary<string, Entry> _entries = new Dictionary<string, Entry>(16);

        static readonly LinkedList<Entry> _pending = new LinkedList<Entry>();

        static Runner _runner;

        static int _activeLoads;

        static long _accessClock;

        /// <summary>Maximum URL entries retained by the automatic Lottie cache.</summary>
        public static int maxEntries = 64;

        /// <summary>Maximum automatic Lottie downloads in flight at once.</summary>
        public static int maxConcurrentDownloads = 4;

        /// <summary>
        /// Approximate UTF-16 source bytes retained by loaded automatic-cache assets
        /// before least-recently-used entries are evicted (256 MiB).
        /// </summary>
        public static long maxCachedSourceBytes = 256L * 1024L * 1024L;

        /// <summary>Current number of queued, loaded, or failed URL entries.</summary>
        public static int cachedEntryCount => _entries.Count;

        public static NowLottieAsset GetAsset(string url)
        {
            GetState(url, out var asset, out _);
            return asset;
        }

        public static NowLottieCacheState GetState(string url, out NowLottieAsset asset, out string error)
        {
            TrimCache();

            if (string.IsNullOrWhiteSpace(url))
            {
                asset = null;
                error = "Lottie URL is empty.";
                return NowLottieCacheState.Failed;
            }

            if (_entries.TryGetValue(url, out var entry))
            {
                Touch(entry);
                asset = entry.asset;
                error = entry.error;
                return entry.state;
            }

            if (!NowLottieAsset.TryValidateRemoteUrl(url, out _, out error))
            {
                asset = null;
                return NowLottieCacheState.Failed;
            }

            entry = new Entry
            {
                url = url,
                state = NowLottieCacheState.Loading
            };
            Touch(entry);
            _entries[url] = entry;
            entry.pendingNode = _pending.AddLast(entry);
            TrimCache(entry);
            PumpLoads();

            asset = null;
            error = null;
            return NowLottieCacheState.Loading;
        }

        public static void SetAsset(string url, NowLottieAsset asset)
        {
            if (string.IsNullOrWhiteSpace(url))
                throw new ArgumentException("Lottie URL cannot be null or empty.", nameof(url));

            if (_entries.TryGetValue(url, out var previous))
                RemoveEntry(previous);

            var entry = new Entry
            {
                url = url,
                asset = asset,
                state = asset != null ? NowLottieCacheState.Loaded : NowLottieCacheState.Failed,
                error = asset != null ? null : "Cached Lottie asset is null.",
                ownsAsset = false
            };
            Touch(entry);
            _entries[url] = entry;
            TrimCache(entry);
        }

        public static void Reset()
        {
            foreach (var entry in _entries.Values)
            {
                if (entry.request != null)
                    entry.request.Abort();
            }

            if (_runner != null)
                _runner.StopAllCoroutines();

            foreach (var entry in _entries.Values)
            {
                if (entry.request != null)
                {
                    entry.request.Dispose();
                    entry.request = null;
                }

                if (entry.ownsAsset)
                    NowLottieAsset.DestroyRuntimeAsset(entry.asset);
            }

            _entries.Clear();
            _pending.Clear();
            _activeLoads = 0;
            _accessClock = 0L;

            if (_runner != null)
            {
                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(_runner.gameObject);
                else
                    UnityEngine.Object.DestroyImmediate(_runner.gameObject);

                _runner = null;
            }
        }

        static void Tick()
        {
            TrimCache();
            PumpLoads();
        }

        static void PumpLoads()
        {
            int concurrency = Mathf.Max(1, maxConcurrentDownloads);

            while (_activeLoads < concurrency && _pending.First != null)
            {
                var node = _pending.First;
                _pending.RemoveFirst();
                var entry = node.Value;
                entry.pendingNode = null;

                if (!_entries.TryGetValue(entry.url, out var current) ||
                    !ReferenceEquals(current, entry) ||
                    entry.state != NowLottieCacheState.Loading)
                {
                    continue;
                }

                var runner = GetRunner();

                if (runner == null)
                {
                    entry.state = NowLottieCacheState.Failed;
                    entry.error = "Could not create Lottie cache runner.";
                    continue;
                }

                entry.active = true;
                ++_activeLoads;
                entry.coroutine = runner.StartCoroutine(Load(entry));
            }
        }

        static IEnumerator Load(Entry entry)
        {
            NowLottieAsset loaded = null;
            string error = null;

            yield return NowLottieAsset.LoadFromUrlInternal(
                entry.url,
                asset => loaded = asset,
                value => error = value,
                request =>
                {
                    entry.request = request;

                    if (request != null &&
                        (!_entries.TryGetValue(entry.url, out var current) || !ReferenceEquals(current, entry)))
                    {
                        request.Abort();
                    }
                });

            entry.request = null;
            entry.coroutine = null;

            if (entry.active)
            {
                entry.active = false;
                _activeLoads = Mathf.Max(0, _activeLoads - 1);
            }

            if (!_entries.TryGetValue(entry.url, out var current) || !ReferenceEquals(current, entry))
            {
                NowLottieAsset.DestroyRuntimeAsset(loaded);
                PumpLoads();
                yield break;
            }

            if (error != null)
            {
                entry.state = NowLottieCacheState.Failed;
                entry.error = error;
                NowLottieAsset.DestroyRuntimeAsset(loaded);
                PumpLoads();
                yield break;
            }

            if (loaded == null)
            {
                entry.state = NowLottieCacheState.Failed;
                entry.error = $"Failed to load Lottie from '{entry.url}'.";
                PumpLoads();
                yield break;
            }

            entry.asset = loaded;
            entry.state = NowLottieCacheState.Loaded;
            entry.error = null;
            entry.ownsAsset = true;
            Touch(entry);
            TrimCache(entry);
            PumpLoads();
        }

        static Runner GetRunner()
        {
            if (_runner != null)
                return _runner;

            var go = new GameObject("Now Lottie Cache")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            UnityEngine.Object.DontDestroyOnLoad(go);
            _runner = go.AddComponent<Runner>();
            return _runner;
        }

        static void TrimCache(Entry preserve = null)
        {
            int entryLimit = Mathf.Max(1, maxEntries);
            long byteLimit = System.Math.Max(1L, maxCachedSourceBytes);

            while (_entries.Count > entryLimit || CachedSourceBytes() > byteLimit)
            {
                Entry oldest = null;

                foreach (var candidate in _entries.Values)
                {
                    if (ReferenceEquals(candidate, preserve))
                        continue;

                    if (oldest == null || candidate.lastAccess < oldest.lastAccess)
                        oldest = candidate;
                }

                if (oldest == null)
                    break;

                RemoveEntry(oldest);
            }
        }

        static long CachedSourceBytes()
        {
            long total = 0L;

            foreach (var entry in _entries.Values)
            {
                long size = entry.asset != null ? entry.asset.estimatedSourceBytes : 0L;

                if (size > long.MaxValue - total)
                    return long.MaxValue;

                total += size;
            }

            return total;
        }

        static void RemoveEntry(Entry entry)
        {
            if (entry == null)
                return;

            if (_entries.TryGetValue(entry.url, out var current) && ReferenceEquals(current, entry))
                _entries.Remove(entry.url);

            if (entry.pendingNode != null)
            {
                _pending.Remove(entry.pendingNode);
                entry.pendingNode = null;
            }

            if (entry.request != null)
                entry.request.Abort();

            if (entry.ownsAsset)
                NowLottieAsset.DestroyRuntimeAsset(entry.asset);

            entry.asset = null;
            entry.ownsAsset = false;
            PumpLoads();
        }

        static void Touch(Entry entry)
        {
            if (_accessClock == long.MaxValue)
            {
                _accessClock = 0L;

                foreach (var value in _entries.Values)
                    value.lastAccess = 0L;
            }

            entry.lastAccess = ++_accessClock;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForRuntimeLoad()
        {
            Reset();
        }
    }
}
