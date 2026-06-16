using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
            public NowLottieAsset asset;
            public NowLottieCacheState state;
            public string error;
            public bool ownsAsset;
        }

        sealed class Runner : MonoBehaviour
        {
        }

        static readonly Dictionary<string, Entry> _entries = new Dictionary<string, Entry>(16);

        static Runner _runner;

        public static NowLottieAsset GetAsset(string url)
        {
            GetState(url, out var asset, out _);
            return asset;
        }

        public static NowLottieCacheState GetState(string url, out NowLottieAsset asset, out string error)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                asset = null;
                error = "Lottie URL is empty.";
                return NowLottieCacheState.Failed;
            }

            if (_entries.TryGetValue(url, out var entry))
            {
                asset = entry.asset;
                error = entry.error;
                return entry.state;
            }

            if (!NowLottieAsset.IsHttpUrl(url))
            {
                asset = null;
                error = "Only http and https Lottie URLs are supported.";
                return NowLottieCacheState.Failed;
            }

            entry = new Entry { state = NowLottieCacheState.Loading };
            _entries[url] = entry;
            StartLoad(url, entry);

            asset = null;
            error = null;
            return NowLottieCacheState.Loading;
        }

        public static void SetAsset(string url, NowLottieAsset asset)
        {
            if (string.IsNullOrWhiteSpace(url))
                throw new ArgumentException("Lottie URL cannot be null or empty.", nameof(url));

            ReleaseOwnedAsset(url);
            _entries[url] = new Entry
            {
                asset = asset,
                state = asset != null ? NowLottieCacheState.Loaded : NowLottieCacheState.Failed,
                error = asset != null ? null : "Cached Lottie asset is null.",
                ownsAsset = false
            };
        }

        public static void Reset()
        {
            foreach (var pair in _entries)
            {
                if (pair.Value.ownsAsset)
                    NowLottieAsset.DestroyRuntimeAsset(pair.Value.asset);
            }

            _entries.Clear();

            if (_runner != null)
            {
                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(_runner.gameObject);
                else
                    UnityEngine.Object.DestroyImmediate(_runner.gameObject);

                _runner = null;
            }
        }

        static void StartLoad(string url, Entry entry)
        {
            var runner = GetRunner();
            if (runner == null)
            {
                entry.state = NowLottieCacheState.Failed;
                entry.error = "Could not create Lottie cache runner.";
                return;
            }

            runner.StartCoroutine(Load(url, entry));
        }

        static IEnumerator Load(string url, Entry entry)
        {
            NowLottieAsset loaded = null;
            string error = null;

            yield return NowLottieAsset.LoadFromUrl(
                url,
                asset => loaded = asset,
                value => error = value);

            if (!_entries.TryGetValue(url, out var current) || !ReferenceEquals(current, entry))
            {
                NowLottieAsset.DestroyRuntimeAsset(loaded);
                yield break;
            }

            if (error != null)
            {
                entry.state = NowLottieCacheState.Failed;
                entry.error = error;
                NowLottieAsset.DestroyRuntimeAsset(loaded);
                yield break;
            }

            if (loaded == null)
            {
                entry.state = NowLottieCacheState.Failed;
                entry.error = $"Failed to load Lottie from '{url}'.";
                yield break;
            }

            entry.asset = loaded;
            entry.state = NowLottieCacheState.Loaded;
            entry.error = null;
            entry.ownsAsset = true;
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

        static void ReleaseOwnedAsset(string url)
        {
            if (!_entries.TryGetValue(url, out var entry) || !entry.ownsAsset)
                return;

            NowLottieAsset.DestroyRuntimeAsset(entry.asset);
        }
    }
}
