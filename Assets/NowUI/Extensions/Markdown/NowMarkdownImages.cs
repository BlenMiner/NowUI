using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace NowUI.Markdown
{
    public enum NowMarkdownImageState
    {
        Loading,
        Loaded,
        Failed
    }

    /// <summary>
    /// Async download cache for markdown images. The first request for an
    /// http/https URL starts a <see cref="UnityWebRequestTexture"/> download;
    /// documents poll the state while laying out and relayout when
    /// <see cref="version"/> changes. Non-http URLs fail immediately (no file
    /// or bundle pipeline). Textures live until <see cref="Reset"/>.
    /// </summary>
    public static class NowMarkdownImages
    {
        sealed class Entry
        {
            public NowMarkdownImageState state;
            public Texture2D texture;

            /// <summary>Only textures this cache downloaded get destroyed on Reset.</summary>
            public bool owned;
        }

        static readonly Dictionary<string, Entry> _entries = new Dictionary<string, Entry>(8);

        static int _version;

        /// <summary>Bumps whenever any download settles; layouts key off it.</summary>
        public static int version => _version;

        public static NowMarkdownImageState GetState(string url, out Texture2D texture)
        {
            texture = null;

            if (string.IsNullOrEmpty(url))
                return NowMarkdownImageState.Failed;

            if (_entries.TryGetValue(url, out var entry))
            {
                texture = entry.texture;
                return entry.state;
            }

            entry = new Entry();
            _entries[url] = entry;

            bool http = url.StartsWith("http://", System.StringComparison.OrdinalIgnoreCase) ||
                url.StartsWith("https://", System.StringComparison.OrdinalIgnoreCase);

            if (!http)
            {
                var resource = Resources.Load<Texture2D>(url);
                entry.texture = resource;
                entry.state = resource != null ? NowMarkdownImageState.Loaded : NowMarkdownImageState.Failed;
                ++_version;
                texture = entry.texture;
                return entry.state;
            }

            entry.state = NowMarkdownImageState.Loading;
            var request = UnityWebRequestTexture.GetTexture(url);
            var operation = request.SendWebRequest();

            operation.completed += _ =>
            {
                if (request.result == UnityWebRequest.Result.Success)
                {
                    entry.texture = DownloadHandlerTexture.GetContent(request);
                    entry.owned = true;
                    entry.state = NowMarkdownImageState.Loaded;
                }
                else
                {
                    entry.state = NowMarkdownImageState.Failed;
                }

                ++_version;
                request.Dispose();
            };

            return entry.state;
        }

        /// <summary>Injects a texture for a URL without downloading (tests, local art).</summary>
        public static void SetTexture(string url, Texture2D texture)
        {
            _entries[url] = new Entry
            {
                state = texture != null ? NowMarkdownImageState.Loaded : NowMarkdownImageState.Failed,
                texture = texture
            };
            ++_version;
        }

        public static void Reset()
        {
            foreach (var entry in _entries.Values)
            {
                if (entry.texture == null || !entry.owned)
                    continue;

                if (Application.isPlaying)
                    Object.Destroy(entry.texture);
                else
                    Object.DestroyImmediate(entry.texture);
            }

            _entries.Clear();
            ++_version;
        }
    }
}
