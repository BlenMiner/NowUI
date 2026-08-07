using NowUI.Internal;
using System;
using System.Collections;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace NowUI
{
    /// <summary>
    /// A Lottie vector animation. The source JSON is kept verbatim and parsed into a
    /// runtime model on first use — the animation is never rasterized to textures, so it
    /// scales losslessly at any size.
    /// </summary>
    public sealed class NowLottieAsset : ScriptableObject
    {
        /// <summary>Timeout applied to remote Lottie requests, in seconds.</summary>
        public static int requestTimeoutSeconds = 30;

        /// <summary>Maximum bytes accepted from one remote Lottie request (64 MiB).</summary>
        public static long maxDownloadBytes = 64L * 1024L * 1024L;

        /// <summary>Maximum bytes accepted for a dotLottie archive supplied at runtime (64 MiB).</summary>
        public static long maxArchiveBytes = 64L * 1024L * 1024L;

        /// <summary>Maximum UTF-8 bytes accepted for one animation JSON document (64 MiB).</summary>
        public static long maxJsonBytes = 64L * 1024L * 1024L;

        /// <summary>Maximum number of entries inspected in a dotLottie archive.</summary>
        public static int maxArchiveEntries = 512;

        /// <summary>Maximum declared uncompressed bytes across a dotLottie archive (256 MiB).</summary>
        public static long maxArchiveUncompressedBytes = 256L * 1024L * 1024L;

        /// <summary>Maximum uncompressed-to-compressed ratio for a dotLottie archive.</summary>
        public static float maxArchiveCompressionRatio = 1000f;

        /// <summary>Maximum nesting depth accepted by the Lottie JSON parser.</summary>
        public static int maxJsonDepth = 256;

        /// <summary>Maximum values accepted by the Lottie JSON DOM.</summary>
        public static int maxJsonNodes = 2_000_000;

        /// <summary>Maximum redirects followed by remote Lottie requests.</summary>
        public static int maxRedirects = 8;

        /// <summary>
        /// Whether plain HTTP URLs are allowed. This defaults to true for backwards
        /// compatibility; applications handling untrusted documents should prefer HTTPS.
        /// </summary>
        public static bool allowInsecureHttp = true;

        /// <summary>
        /// Optional application URL policy. Return false to reject a remote URL before
        /// a request starts (for example, to allow-list hosts). This is not a DNS or
        /// network sandbox and is invoked on the calling thread.
        /// </summary>
        public static Func<Uri, bool> remoteUrlPolicy;

        [SerializeField, HideInInspector] string _json;

        [SerializeField, HideInInspector] float _width;

        [SerializeField, HideInInspector] float _height;

        [SerializeField, HideInInspector] float _frameRate;

        [SerializeField, HideInInspector] float _inPoint;

        [SerializeField, HideInInspector] float _outPoint;

        [NonSerialized] NowLottieComposition _composition;

        [NonSerialized] bool _parseFailed;

        public float width => _width;

        public float height => _height;

        public float frameRate => _frameRate;

        public float inPoint => _inPoint;

        public float outPoint => _outPoint;

        public float durationFrames => Mathf.Max(0f, _outPoint - _inPoint);

        public float duration => _frameRate > 0f ? durationFrames / _frameRate : 0f;

        public bool hasJson => !string.IsNullOrEmpty(_json);

        internal long estimatedSourceBytes => string.IsNullOrEmpty(_json) ? 0L : (long)_json.Length * sizeof(char);

        /// <summary>Parsed animation model; null when the asset is empty or invalid.</summary>
        public NowLottieComposition composition
        {
            get
            {
                if (_composition == null && !_parseFailed && !string.IsNullOrEmpty(_json))
                {
                    try
                    {
                        ValidateJsonSize(_json);
                        _composition = NowLottieComposition.Parse(_json);
                    }
                    catch (Exception exception)
                    {
                        _parseFailed = true;
                        Debug.LogError($"Failed to parse Lottie animation '{name}': {exception.Message}", this);
                    }
                }

                return _composition;
            }
        }

        /// <summary>
        /// Assigns the animation JSON. Throws on invalid documents so importers can
        /// surface the error. The parsed model is cached.
        /// </summary>
        public void SetSource(string json)
        {
            ValidateJsonSize(json);
            var parsed = NowLottieComposition.Parse(json);

            _json = json;
            _composition = parsed;
            _parseFailed = false;
            _width = parsed.width;
            _height = parsed.height;
            _frameRate = parsed.frameRate;
            _inPoint = parsed.inPoint;
            _outPoint = parsed.outPoint;
        }

        /// <summary>
        /// Assigns animation bytes. Accepts plain Lottie JSON and dotLottie ZIP
        /// archives, then delegates to <see cref="SetSource(string)"/>.
        /// </summary>
        public void SetSource(byte[] bytes)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));

            SetSource(ExtractSourceJson(bytes));
        }

        /// <summary>
        /// Downloads a Lottie document from an http/https URL and assigns it to this
        /// asset. The previous source remains active if download or parsing fails.
        /// </summary>
        public IEnumerator SetSourceFromUrl(string url, Action<string> onError = null)
        {
            byte[] bytes = null;
            string error = null;
            yield return DownloadSourceBytes(url, value => bytes = value, value => error = value);

            if (error != null)
            {
                onError?.Invoke(error);
                yield break;
            }

            try
            {
                SetSource(bytes);
            }
            catch (Exception exception)
            {
                onError?.Invoke($"Failed to parse Lottie from '{url}': {exception.Message}");
            }
        }

        /// <summary>
        /// Creates a transient runtime asset from an http/https URL. The caller owns
        /// the returned asset and should destroy it when no longer needed.
        /// </summary>
        public static IEnumerator LoadFromUrl(string url, Action<NowLottieAsset> onLoaded, Action<string> onError = null)
        {
            return LoadFromUrlInternal(url, onLoaded, onError, null);
        }

        internal static IEnumerator LoadFromUrlInternal(
            string url,
            Action<NowLottieAsset> onLoaded,
            Action<string> onError,
            Action<UnityWebRequest> onRequestChanged)
        {
            if (onLoaded == null)
                throw new ArgumentNullException(nameof(onLoaded));

            byte[] bytes = null;
            string error = null;
            yield return DownloadSourceBytes(
                url,
                value => bytes = value,
                value => error = value,
                onRequestChanged);

            if (error != null)
            {
                onError?.Invoke(error);
                yield break;
            }

            var asset = CreateInstance<NowLottieAsset>();
            asset.name = GetAssetNameFromUrl(url);

            try
            {
                asset.SetSource(bytes);
            }
            catch (Exception exception)
            {
                DestroyRuntimeAsset(asset);
                onError?.Invoke($"Failed to parse Lottie from '{url}': {exception.Message}");
                yield break;
            }

            onLoaded(asset);
        }

        /// <summary>
        /// Extracts plain Lottie JSON from raw bytes. DotLottie archives choose the
        /// first animation JSON under animations/ or a/, falling back to any non
        /// manifest JSON entry.
        /// </summary>
        public static string ExtractSourceJson(byte[] bytes)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));

            bool isZip = bytes.Length > 2 && bytes[0] == 'P' && bytes[1] == 'K';

            if (!isZip)
            {
                EnsureWithinLimit(bytes.LongLength, maxJsonBytes, "Lottie JSON");
                using var reader = new StreamReader(new MemoryStream(bytes), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
                string json = reader.ReadToEnd();
                ValidateJsonSize(json);
                return json;
            }

            EnsureWithinLimit(bytes.LongLength, maxArchiveBytes, "dotLottie archive");
            NowZipArchivePreflight.Validate(bytes, Mathf.Max(1, maxArchiveEntries));

            using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);

            if (archive.Entries.Count > Mathf.Max(1, maxArchiveEntries))
            {
                throw new FormatException(
                    $"dotLottie archive has {archive.Entries.Count} entries; the configured limit is {Mathf.Max(1, maxArchiveEntries)}.");
            }

            ZipArchiveEntry best = null;
            bool foundAnimation = false;
            long totalUncompressed = 0L;
            long totalCompressed = 0L;
            long uncompressedLimit = EffectiveLimit(maxArchiveUncompressedBytes);

            foreach (var entry in archive.Entries)
            {
                if (entry.Length > uncompressedLimit - totalUncompressed)
                {
                    throw new FormatException(
                        $"dotLottie archive exceeds the configured uncompressed size limit of {uncompressedLimit} bytes.");
                }

                totalUncompressed += entry.Length;

                if (entry.CompressedLength > long.MaxValue - totalCompressed)
                    throw new FormatException("dotLottie archive compressed size metadata is invalid.");

                totalCompressed += entry.CompressedLength;

                if (!entry.FullName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                    continue;

                bool isAnimation = entry.FullName.StartsWith("animations/", StringComparison.OrdinalIgnoreCase) ||
                    entry.FullName.StartsWith("a/", StringComparison.OrdinalIgnoreCase);

                if (isAnimation && !foundAnimation)
                {
                    best = entry;
                    foundAnimation = true;
                    continue;
                }

                if (!foundAnimation && best == null &&
                    !entry.FullName.EndsWith("manifest.json", StringComparison.OrdinalIgnoreCase))
                {
                    best = entry;
                }
            }

            if (best == null)
                throw new FormatException("dotLottie archive contains no animation JSON.");

            float ratioLimit = Mathf.Max(1f, maxArchiveCompressionRatio);

            if (ExceedsCompressionRatio(totalUncompressed, totalCompressed, ratioLimit))
            {
                throw new FormatException(
                    $"dotLottie archive exceeds the configured compression ratio limit of {ratioLimit:0.##}:1.");
            }

            EnsureWithinLimit(best.Length, maxJsonBytes, "dotLottie animation JSON");

            using var entryStream = best.Open();
            byte[] jsonBytes = ReadAllBytes(entryStream, maxJsonBytes, "dotLottie animation JSON");
            using var entryReader = new StreamReader(new MemoryStream(jsonBytes), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            string result = entryReader.ReadToEnd();
            ValidateJsonSize(result);
            return result;
        }

        internal static bool IsHttpUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;

            return url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool TryValidateRemoteUrl(string url, out Uri uri, out string error)
        {
            uri = null;

            if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed) ||
                (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps))
            {
                error = "Only http and https Lottie URLs are supported.";
                return false;
            }

            if (!allowInsecureHttp && parsed.Scheme == Uri.UriSchemeHttp)
            {
                error = "Plain HTTP Lottie URLs are disabled by NowLottieAsset.allowInsecureHttp.";
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
                    error = $"The Lottie URL policy threw an exception: {exception.Message}";
                    return false;
                }

                if (!allowed)
                {
                    error = "The Lottie URL was rejected by NowLottieAsset.remoteUrlPolicy.";
                    return false;
                }
            }

            uri = parsed;
            error = null;
            return true;
        }

        static IEnumerator DownloadSourceBytes(
            string url,
            Action<byte[]> onLoaded,
            Action<string> onError,
            Action<UnityWebRequest> onRequestChanged = null)
        {
            if (!TryValidateRemoteUrl(url, out Uri currentUri, out string validationError))
            {
                onError?.Invoke(validationError);
                yield break;
            }

            int redirects = 0;
            long totalDownloaded = 0L;
            long byteLimit = EffectiveLimit(maxDownloadBytes);

            while (true)
            {
                long remaining = Math.Max(0L, byteLimit - totalDownloaded);
                var request = new UnityWebRequest(
                    currentUri.AbsoluteUri,
                    UnityWebRequest.kHttpVerbGET);
                var downloadHandler = new NowBoundedDownloadHandler(remaining);
                request.downloadHandler = downloadHandler;
                request.disposeDownloadHandlerOnDispose = true;
                request.timeout = Mathf.Max(1, requestTimeoutSeconds);
                // Redirects are followed manually so every target runs through the
                // same scheme/host policy as the original URL.
                request.redirectLimit = 0;
                onRequestChanged?.Invoke(request);

                try
                {
                    var operation = request.SendWebRequest();

                    while (!operation.isDone)
                    {
                        if (downloadHandler.limitExceeded ||
                            RequestExceedsLimit(request, remaining))
                        {
                            request.Abort();
                            onError?.Invoke(
                                $"Lottie download from '{url}' exceeds the configured limit of {byteLimit} bytes across redirects.");
                            yield break;
                        }

                        yield return null;
                    }

                    if (downloadHandler.limitExceeded ||
                        RequestExceedsLimit(request, remaining))
                    {
                        onError?.Invoke(
                            $"Lottie download from '{url}' exceeds the configured limit of {byteLimit} bytes across redirects.");
                        yield break;
                    }

                    totalDownloaded += downloadHandler.receivedByteCount;

                    if (IsRedirectStatus(request.responseCode))
                    {
                        if (redirects >= Mathf.Max(0, maxRedirects))
                        {
                            onError?.Invoke(
                                $"Lottie download from '{url}' exceeded the configured redirect limit of {Mathf.Max(0, maxRedirects)}.");
                            yield break;
                        }

                        if (!TryResolveRedirect(
                            currentUri,
                            request.GetResponseHeader("Location"),
                            out var redirectUri,
                            out var redirectError))
                        {
                            onError?.Invoke($"Refused Lottie redirect from '{url}': {redirectError}");
                            yield break;
                        }

                        if (!TryValidateRemoteUrl(redirectUri.AbsoluteUri, out var validatedUri, out redirectError))
                        {
                            onError?.Invoke($"Refused Lottie redirect from '{url}': {redirectError}");
                            yield break;
                        }

                        currentUri = validatedUri;
                        ++redirects;
                        continue;
                    }

                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        onError?.Invoke($"Failed to download Lottie from '{url}': {request.error}");
                        yield break;
                    }

                    byte[] data = downloadHandler.GetBytes();

                    if (data == null)
                    {
                        onError?.Invoke($"Lottie download from '{url}' returned no data.");
                        yield break;
                    }

                    onLoaded?.Invoke(data);
                    yield break;
                }
                finally
                {
                    onRequestChanged?.Invoke(null);
                    request.Dispose();
                }
            }
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

        static byte[] ReadAllBytes(Stream stream, long configuredLimit, string label)
        {
            long limit = Math.Min(EffectiveLimit(configuredLimit), int.MaxValue);
            int capacity = stream.CanSeek ? (int)Math.Min(stream.Length, limit) : 0;
            using var output = capacity > 0 ? new MemoryStream(capacity) : new MemoryStream();
            var buffer = new byte[81920];
            long total = 0L;

            while (true)
            {
                int read = stream.Read(buffer, 0, buffer.Length);

                if (read <= 0)
                    break;

                total += read;

                if (total > limit)
                    throw new FormatException($"{label} exceeds the configured limit of {limit} bytes.");

                output.Write(buffer, 0, read);
            }

            return output.ToArray();
        }

        static bool ExceedsCompressionRatio(long uncompressed, long compressed, float ratioLimit)
        {
            if (uncompressed <= 0L)
                return false;

            if (compressed <= 0L)
                return true;

            return (double)uncompressed / compressed > ratioLimit;
        }

        static void ValidateJsonSize(string json)
        {
            if (json == null)
                throw new ArgumentNullException(nameof(json));

            long limit = EffectiveLimit(maxJsonBytes);

            if (json.Length > limit || Encoding.UTF8.GetByteCount(json) > limit)
                throw new FormatException($"Lottie JSON exceeds the configured limit of {limit} UTF-8 bytes.");
        }

        static void EnsureWithinLimit(long value, long configuredLimit, string label)
        {
            long limit = EffectiveLimit(configuredLimit);

            if (value > limit)
                throw new FormatException($"{label} exceeds the configured limit of {limit} bytes.");
        }

        static long EffectiveLimit(long configuredLimit)
        {
            return Math.Max(1L, configuredLimit);
        }

        internal static string GetAssetNameFromUrl(string url)
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                string fileName = Path.GetFileNameWithoutExtension(uri.LocalPath);

                if (!string.IsNullOrEmpty(fileName))
                    return fileName;
            }

            return "Lottie";
        }

        internal static void DestroyRuntimeAsset(UnityEngine.Object asset)
        {
            if (asset == null)
                return;

            if (Application.isPlaying)
                Destroy(asset);
            else
                DestroyImmediate(asset);
        }
    }

}
