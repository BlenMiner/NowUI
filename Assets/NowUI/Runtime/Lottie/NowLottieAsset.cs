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

        /// <summary>Parsed animation model; null when the asset is empty or invalid.</summary>
        public NowLottieComposition composition
        {
            get
            {
                if (_composition == null && !_parseFailed && !string.IsNullOrEmpty(_json))
                {
                    try
                    {
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
            if (onLoaded == null)
                throw new ArgumentNullException(nameof(onLoaded));

            byte[] bytes = null;
            string error = null;
            yield return DownloadSourceBytes(url, value => bytes = value, value => error = value);

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
                using var reader = new StreamReader(new MemoryStream(bytes), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
                return reader.ReadToEnd();
            }

            using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);

            ZipArchiveEntry best = null;

            foreach (var entry in archive.Entries)
            {
                if (!entry.FullName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                    continue;

                bool isAnimation = entry.FullName.StartsWith("animations/", StringComparison.OrdinalIgnoreCase) ||
                    entry.FullName.StartsWith("a/", StringComparison.OrdinalIgnoreCase);

                if (isAnimation)
                {
                    best = entry;
                    break;
                }

                if (best == null && !entry.FullName.EndsWith("manifest.json", StringComparison.OrdinalIgnoreCase))
                    best = entry;
            }

            if (best == null)
                throw new FormatException("dotLottie archive contains no animation JSON.");

            using var entryReader = new StreamReader(best.Open(), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            return entryReader.ReadToEnd();
        }

        internal static bool IsHttpUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;

            return url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
        }

        static IEnumerator DownloadSourceBytes(string url, Action<byte[]> onLoaded, Action<string> onError)
        {
            if (!IsHttpUrl(url))
            {
                onError?.Invoke("Only http and https Lottie URLs are supported.");
                yield break;
            }

            using var request = UnityWebRequest.Get(url);
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke($"Failed to download Lottie from '{url}': {request.error}");
                yield break;
            }

            onLoaded?.Invoke(request.downloadHandler.data);
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
