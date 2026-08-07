using System;
using System.Collections.Generic;
using NowUI.Internal;
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
    /// Bounded async cache for markdown images. Remote downloads are queued and
    /// documents poll the state while laying out. Non-http paths load from Resources.
    /// Downloaded textures are owned by this cache; injected and Resources textures
    /// remain caller/Unity owned.
    /// </summary>
    public static class NowMarkdownImages
    {
        sealed class Entry
        {
            public string url;
            public NowMarkdownImageState state;
            public Texture2D texture;
            public bool owned;
            public bool active;
            public bool completed;
            public long lastAccess;
            public long downloadedBytes;
            public int redirects;
            public string forcedError;
            public Uri currentUri;
            public UnityWebRequest request;
            public NowBoundedDownloadHandler downloadHandler;
            public UnityWebRequestAsyncOperation operation;
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

        static readonly List<Entry> _active = new List<Entry>(4);

        static Runner _runner;

        static int _version;

        static long _accessClock;

        /// <summary>Timeout applied to remote image requests, in seconds.</summary>
        public static int requestTimeoutSeconds = 30;

        /// <summary>Maximum encoded bytes accepted from one remote image (64 MiB).</summary>
        public static long maxDownloadBytes = 64L * 1024L * 1024L;

        /// <summary>Maximum width or height accepted for a decoded remote image.</summary>
        public static int maxTextureDimension = 16384;

        /// <summary>Maximum decoded pixels accepted for one remote image.</summary>
        public static long maxTexturePixels = 100L * 1024L * 1024L;

        /// <summary>Maximum remote image requests in flight at once.</summary>
        public static int maxConcurrentDownloads = 4;

        /// <summary>Maximum URL/resource entries retained by the image cache.</summary>
        public static int maxCacheEntries = 128;

        /// <summary>
        /// Maximum decoded pixels referenced by the cache before least-recently-used
        /// entries are evicted. This includes injected and Resources textures.
        /// </summary>
        public static long maxCachedTexturePixels = 128L * 1024L * 1024L;

        /// <summary>Maximum redirects followed by remote image requests.</summary>
        public static int maxRedirects = 8;

        /// <summary>
        /// Whether plain HTTP image URLs are allowed. This defaults to true for
        /// backwards compatibility; untrusted-content applications should prefer HTTPS.
        /// </summary>
        public static bool allowInsecureHttp = true;

        /// <summary>
        /// Optional application URL policy. Return false to reject a remote URL before
        /// a request starts (for example, to allow-list hosts). This is not a DNS or
        /// network sandbox and is invoked on the calling thread.
        /// </summary>
        public static Func<Uri, bool> remoteUrlPolicy;

        /// <summary>Bumps whenever a cached image settles or is removed.</summary>
        public static int version => _version;

        /// <summary>Current number of queued, loaded, or failed cache entries.</summary>
        public static int cachedEntryCount => _entries.Count;

        /// <summary>Current number of remote requests in flight.</summary>
        public static int activeDownloadCount => _active.Count;

        public static NowMarkdownImageState GetState(string url, out Texture2D texture)
        {
            texture = null;
            TrimCache();

            if (string.IsNullOrEmpty(url))
                return NowMarkdownImageState.Failed;

            if (_entries.TryGetValue(url, out var entry))
            {
                Touch(entry);
                TrimCache(entry);
                texture = entry.texture;
                return entry.state;
            }

            bool remote = IsHttpUrl(url);

            if (!remote)
            {
                var resource = Resources.Load<Texture2D>(url);
                entry = new Entry
                {
                    url = url,
                    texture = resource,
                    state = resource != null ? NowMarkdownImageState.Loaded : NowMarkdownImageState.Failed
                };
                Touch(entry);
                _entries[url] = entry;
                ++_version;
                TrimCache(entry);
                texture = resource;
                return entry.state;
            }

            if (!TryValidateRemoteUrl(url, out var remoteUri, out _))
                return NowMarkdownImageState.Failed;

            entry = new Entry
            {
                url = url,
                state = NowMarkdownImageState.Loading,
                currentUri = remoteUri
            };
            Touch(entry);
            _entries[url] = entry;
            entry.pendingNode = _pending.AddLast(entry);
            TrimCache(entry);
            PumpDownloads();
            return entry.state;
        }

        /// <summary>Injects a texture for a URL without downloading (tests, local art).</summary>
        public static void SetTexture(string url, Texture2D texture)
        {
            if (url == null)
                throw new ArgumentNullException(nameof(url));

            if (_entries.TryGetValue(url, out var previous))
                RemoveEntry(previous, false);

            var entry = new Entry
            {
                url = url,
                state = texture != null ? NowMarkdownImageState.Loaded : NowMarkdownImageState.Failed,
                texture = texture
            };
            Touch(entry);
            _entries[url] = entry;
            ++_version;
            TrimCache(entry);
            PumpDownloads();
        }

        /// <summary>
        /// Aborts queued and active downloads, destroys downloaded textures, and clears
        /// the cache. Configuration fields are left unchanged.
        /// </summary>
        public static void Reset()
        {
            foreach (var entry in _entries.Values)
                CancelEntry(entry);

            _entries.Clear();
            _pending.Clear();
            _active.Clear();
            _accessClock = 0L;

            if (_runner != null)
            {
                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(_runner.gameObject);
                else
                    UnityEngine.Object.DestroyImmediate(_runner.gameObject);

                _runner = null;
            }

            ++_version;
        }

        static void Tick()
        {
            PollDownloads();
            TrimCache();
            PumpDownloads();
        }

        static void PollDownloads()
        {
            long byteLimit = EffectiveLimit(maxDownloadBytes);

            for (int i = _active.Count - 1; i >= 0; --i)
            {
                var entry = _active[i];

                if (entry.completed || entry.request == null)
                    continue;

                long remaining = Math.Max(0L, byteLimit - entry.downloadedBytes);

                if ((entry.downloadHandler?.limitExceeded ?? false) ||
                    RequestExceedsLimit(entry.request, remaining))
                {
                    entry.forcedError =
                        $"Markdown image download from '{entry.url}' exceeds the configured limit of {byteLimit} bytes across redirects.";
                    entry.request.Abort();
                }

                if (entry.operation != null && entry.operation.isDone)
                    CompleteDownload(entry);
            }
        }

        static void PumpDownloads()
        {
            int concurrency = Mathf.Max(1, maxConcurrentDownloads);

            while (_active.Count < concurrency && _pending.First != null)
            {
                var node = _pending.First;
                _pending.RemoveFirst();
                var entry = node.Value;
                entry.pendingNode = null;

                if (!_entries.TryGetValue(entry.url, out var current) ||
                    !ReferenceEquals(current, entry) ||
                    entry.state != NowMarkdownImageState.Loading)
                {
                    continue;
                }

                StartDownload(entry);
            }
        }

        static void StartDownload(Entry entry)
        {
            GetRunner();

            try
            {
                long byteLimit = EffectiveLimit(maxDownloadBytes);
                long remaining = Math.Max(0L, byteLimit - entry.downloadedBytes);
                var request = new UnityWebRequest(
                    entry.currentUri.AbsoluteUri,
                    UnityWebRequest.kHttpVerbGET);
                entry.request = request;
                var downloadHandler = new NowBoundedDownloadHandler(remaining);
                entry.downloadHandler = downloadHandler;
                request.downloadHandler = downloadHandler;
                request.disposeDownloadHandlerOnDispose = true;
                request.timeout = Mathf.Max(1, requestTimeoutSeconds);
                // Follow redirects ourselves so each target is checked by the URL policy.
                request.redirectLimit = 0;
                entry.active = true;
                _active.Add(entry);
                entry.operation = request.SendWebRequest();
                entry.operation.completed += _ => CompleteDownload(entry);

                if (entry.operation.isDone)
                    CompleteDownload(entry);
            }
            catch (Exception exception)
            {
                entry.forcedError = $"Failed to start markdown image download from '{entry.url}': {exception.Message}";
                CompleteDownload(entry);
            }
        }

        static void CompleteDownload(Entry entry)
        {
            if (entry == null || entry.completed)
                return;

            entry.completed = true;
            entry.active = false;
            _active.Remove(entry);

            var request = entry.request;
            var downloadHandler = entry.downloadHandler;
            entry.request = null;
            entry.downloadHandler = null;
            entry.operation = null;

            bool current = _entries.TryGetValue(entry.url, out var cached) && ReferenceEquals(cached, entry);

            if (!current)
            {
                if (request != null)
                    request.Dispose();
                else
                    downloadHandler?.Dispose();

                PumpDownloads();
                return;
            }

            string error = entry.forcedError;
            Texture2D decoded = null;
            long byteLimit = EffectiveLimit(maxDownloadBytes);
            long remaining = Math.Max(0L, byteLimit - entry.downloadedBytes);

            if (error == null && (downloadHandler?.limitExceeded ?? false))
            {
                error =
                    $"The image response exceeds the configured limit of {byteLimit} bytes across redirects.";
            }
            else if (error == null && request != null && RequestExceedsLimit(request, remaining))
            {
                error =
                    $"The image response exceeds the configured limit of {byteLimit} bytes across redirects.";
            }

            if (downloadHandler != null)
                entry.downloadedBytes += downloadHandler.receivedByteCount;

            if (error == null && request != null && IsRedirectStatus(request.responseCode))
            {
                int redirectLimit = Mathf.Max(0, maxRedirects);

                if (entry.redirects >= redirectLimit)
                {
                    error = $"The image request exceeded the configured redirect limit of {redirectLimit}.";
                }
                else if (!TryResolveRedirect(
                    entry.currentUri,
                    request.GetResponseHeader("Location"),
                    out var redirectUri,
                    out error))
                {
                    error = $"The image redirect was refused: {error}";
                }
                else if (!TryValidateRemoteUrl(redirectUri.AbsoluteUri, out var validatedUri, out error))
                {
                    error = $"The image redirect was refused: {error}";
                }
                else
                {
                    request.Dispose();
                    entry.currentUri = validatedUri;
                    ++entry.redirects;
                    entry.completed = false;
                    entry.forcedError = null;
                    entry.pendingNode = _pending.AddFirst(entry);
                    PumpDownloads();
                    return;
                }
            }

            if (error == null && (request == null || request.result != UnityWebRequest.Result.Success))
                error = request != null ? request.error : "The image request was not created.";

            if (error == null)
            {
                byte[] data = downloadHandler?.GetBytes();

                if (data == null)
                {
                    error = "The image response contained no data.";
                }
                else if (!TryDecodeDownloadedTexture(data, entry.url, out decoded, out error))
                {
                    decoded = null;
                }
            }

            if (request != null)
                request.Dispose();
            else
                downloadHandler?.Dispose();

            if (decoded != null)
            {
                entry.texture = decoded;
                entry.owned = true;
                entry.state = NowMarkdownImageState.Loaded;
            }
            else
            {
                entry.state = NowMarkdownImageState.Failed;
            }

            ++_version;
            Touch(entry);
            TrimCache(entry);
            PumpDownloads();
        }

        internal static bool TryDecodeDownloadedTexture(
            byte[] data,
            string name,
            out Texture2D texture,
            out string error)
        {
            texture = null;

            if (data == null || data.LongLength > EffectiveLimit(maxDownloadBytes))
            {
                error = "Encoded image data is empty or exceeds the configured download limit.";
                return false;
            }

            if (TryGetEncodedDimensions(data, out int encodedWidth, out int encodedHeight) &&
                !AreDimensionsWithinLimits(encodedWidth, encodedHeight, out error))
            {
                return false;
            }

            var decoded = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                name = string.IsNullOrEmpty(name) ? "Markdown Image" : name,
                hideFlags = HideFlags.HideAndDontSave
            };

            try
            {
                if (!decoded.LoadImage(data, markNonReadable: true))
                {
                    error = "Unity could not decode the downloaded image.";
                    DestroyTexture(decoded);
                    return false;
                }

                if (!AreDimensionsWithinLimits(decoded.width, decoded.height, out error))
                {
                    DestroyTexture(decoded);
                    return false;
                }

                texture = decoded;
                error = null;
                return true;
            }
            catch (Exception exception)
            {
                error = $"Unity could not decode the downloaded image: {exception.Message}";
                DestroyTexture(decoded);
                return false;
            }
        }

        internal static bool AreDimensionsWithinLimits(int width, int height, out string error)
        {
            int dimensionLimit = Mathf.Max(1, maxTextureDimension);
            long pixelLimit = EffectiveLimit(maxTexturePixels);
            long pixels = (long)width * height;

            if (width < 1 || height < 1 || width > dimensionLimit || height > dimensionLimit)
            {
                error =
                    $"Decoded image dimensions {width}x{height} exceed the configured {dimensionLimit}-pixel dimension limit.";
                return false;
            }

            if (pixels > pixelLimit)
            {
                error =
                    $"Decoded image dimensions {width}x{height} exceed the configured {pixelLimit}-pixel limit.";
                return false;
            }

            error = null;
            return true;
        }

        static bool TryGetEncodedDimensions(byte[] data, out int width, out int height)
        {
            width = 0;
            height = 0;

            // PNG IHDR dimensions are fixed-position and big-endian.
            if (data.Length >= 24 &&
                data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4e && data[3] == 0x47 &&
                data[12] == 0x49 && data[13] == 0x48 && data[14] == 0x44 && data[15] == 0x52)
            {
                width = ReadInt32BigEndian(data, 16);
                height = ReadInt32BigEndian(data, 20);
                return true;
            }

            // GIF logical screen dimensions are little-endian.
            if (data.Length >= 10 && data[0] == (byte)'G' && data[1] == (byte)'I' && data[2] == (byte)'F')
            {
                width = data[6] | data[7] << 8;
                height = data[8] | data[9] << 8;
                return true;
            }

            // BMP dimensions are signed little-endian (negative height means top-down).
            if (data.Length >= 26 && data[0] == (byte)'B' && data[1] == (byte)'M')
            {
                width = AbsoluteDimension(ReadInt32LittleEndian(data, 18));
                height = AbsoluteDimension(ReadInt32LittleEndian(data, 22));
                return true;
            }

            // JPEG dimensions live in one of several start-of-frame segments.
            if (data.Length >= 4 && data[0] == 0xff && data[1] == 0xd8)
            {
                int position = 2;

                while (position + 8 < data.Length)
                {
                    if (data[position++] != 0xff)
                        continue;

                    while (position < data.Length && data[position] == 0xff)
                        ++position;

                    if (position >= data.Length)
                        break;

                    byte marker = data[position++];

                    if (marker == 0xd8 || marker == 0x01 || (marker >= 0xd0 && marker <= 0xd9))
                        continue;

                    if (position + 1 >= data.Length)
                        break;

                    int segmentLength = data[position] << 8 | data[position + 1];

                    if (segmentLength < 2 || position + segmentLength > data.Length)
                        break;

                    bool startOfFrame =
                        (marker >= 0xc0 && marker <= 0xc3) ||
                        (marker >= 0xc5 && marker <= 0xc7) ||
                        (marker >= 0xc9 && marker <= 0xcb) ||
                        (marker >= 0xcd && marker <= 0xcf);

                    if (startOfFrame && segmentLength >= 7)
                    {
                        height = data[position + 3] << 8 | data[position + 4];
                        width = data[position + 5] << 8 | data[position + 6];
                        return true;
                    }

                    position += segmentLength;
                }
            }

            return false;
        }

        static bool TryValidateRemoteUrl(string url, out Uri uri, out string error)
        {
            uri = null;

            if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed) ||
                (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps))
            {
                error = "Only absolute http and https markdown image URLs are supported.";
                return false;
            }

            if (!allowInsecureHttp && parsed.Scheme == Uri.UriSchemeHttp)
            {
                error = "Plain HTTP markdown image URLs are disabled by NowMarkdownImages.allowInsecureHttp.";
                return false;
            }

            var policy = remoteUrlPolicy;

            if (policy != null)
            {
                bool allowed;

                try
                {
                    allowed = policy(parsed);
                }
                catch (Exception exception)
                {
                    error = $"The markdown image URL policy threw an exception: {exception.Message}";
                    return false;
                }

                if (!allowed)
                {
                    error = "The markdown image URL was rejected by NowMarkdownImages.remoteUrlPolicy.";
                    return false;
                }
            }

            uri = parsed;
            error = null;
            return true;
        }

        static bool IsHttpUrl(string url)
        {
            return url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
        }

        static bool RequestExceedsLimit(UnityWebRequest request, long limit)
        {
            if (request.downloadedBytes > (ulong)limit)
                return true;

            string contentLength = request.GetResponseHeader("Content-Length");
            return long.TryParse(contentLength, out long declaredLength) && declaredLength > limit;
        }

        static bool IsRedirectStatus(long responseCode)
        {
            return responseCode == 301L ||
                responseCode == 302L ||
                responseCode == 303L ||
                responseCode == 307L ||
                responseCode == 308L;
        }

        static bool TryResolveRedirect(Uri currentUri, string location, out Uri redirectUri, out string error)
        {
            redirectUri = null;

            if (string.IsNullOrWhiteSpace(location) || !Uri.TryCreate(currentUri, location, out var resolved))
            {
                error = "The server returned a redirect without a valid Location URL.";
                return false;
            }

            redirectUri = resolved;
            error = null;
            return true;
        }

        static void TrimCache(Entry preserve = null)
        {
            int entryLimit = Mathf.Max(1, maxCacheEntries);
            long pixelLimit = EffectiveLimit(maxCachedTexturePixels);

            while (_entries.Count > entryLimit || CachedPixels() > pixelLimit)
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

                RemoveEntry(oldest, true);
            }
        }

        static long CachedPixels()
        {
            long total = 0L;

            foreach (var entry in _entries.Values)
            {
                if (entry.texture == null)
                    continue;

                long pixels = (long)entry.texture.width * entry.texture.height;

                if (pixels > long.MaxValue - total)
                    return long.MaxValue;

                total += pixels;
            }

            return total;
        }

        static void RemoveEntry(Entry entry, bool bumpVersion)
        {
            if (entry == null)
                return;

            if (_entries.TryGetValue(entry.url, out var current) && ReferenceEquals(current, entry))
                _entries.Remove(entry.url);

            CancelEntry(entry);

            if (bumpVersion)
                ++_version;
        }

        static void CancelEntry(Entry entry)
        {
            if (entry.pendingNode != null)
            {
                _pending.Remove(entry.pendingNode);
                entry.pendingNode = null;
            }

            entry.completed = true;
            entry.active = false;
            _active.Remove(entry);

            var request = entry.request;
            var downloadHandler = entry.downloadHandler;
            entry.request = null;
            entry.downloadHandler = null;
            entry.operation = null;

            if (request != null)
            {
                request.Abort();
                request.Dispose();
            }
            else
            {
                downloadHandler?.Dispose();
            }

            if (entry.texture != null && entry.owned)
                DestroyTexture(entry.texture);

            entry.texture = null;
            entry.owned = false;
        }

        static Runner GetRunner()
        {
            if (_runner != null)
                return _runner;

            var go = new GameObject("Now Markdown Image Cache")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            UnityEngine.Object.DontDestroyOnLoad(go);
            _runner = go.AddComponent<Runner>();
            return _runner;
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

        static int ReadInt32BigEndian(byte[] bytes, int offset)
        {
            return bytes[offset] << 24 |
                bytes[offset + 1] << 16 |
                bytes[offset + 2] << 8 |
                bytes[offset + 3];
        }

        static int ReadInt32LittleEndian(byte[] bytes, int offset)
        {
            return bytes[offset] |
                bytes[offset + 1] << 8 |
                bytes[offset + 2] << 16 |
                bytes[offset + 3] << 24;
        }

        static int AbsoluteDimension(int value)
        {
            return value == int.MinValue ? int.MaxValue : Math.Abs(value);
        }

        static long EffectiveLimit(long configuredLimit)
        {
            return Math.Max(1L, configuredLimit);
        }

        static void DestroyTexture(Texture2D texture)
        {
            if (texture == null)
                return;

            if (Application.isPlaying)
                UnityEngine.Object.Destroy(texture);
            else
                UnityEngine.Object.DestroyImmediate(texture);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForRuntimeLoad()
        {
            Reset();
        }
    }
}
