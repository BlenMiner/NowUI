using System;
using System.IO;
using System.Threading;

namespace NowUI.Markup
{
    /// <summary>
    /// Disk-backed markup source. Draw/Refresh polls the file timestamp and
    /// reparses when it changes, which gives immediate play-mode feedback while
    /// editing markup files outside Unity's asset import loop.
    /// </summary>
    public sealed class NowMarkupFile : IDisposable
    {
        static readonly TimeSpan WatcherPollFallback = TimeSpan.FromSeconds(1);

        readonly string _path;
        readonly string _resolvedPath;
        NowMarkupDocument _document;
        FileSystemWatcher _watcher;
        DateTime _lastWriteUtc;
        DateTime _nextPollUtc;
        TimeSpan _pollInterval = WatcherPollFallback;
        long _lastLength;
        string _lastError;
        int _dirty = 1;
        bool _loaded;
        bool _disposed;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        string _lastWarnedError;
#endif

        /// <summary>
        /// Records a load failure so it is both readable through
        /// <see cref="lastError"/> and, in editor/development builds, logged once
        /// per distinct error — a missing file otherwise renders as silent blank UI.
        /// </summary>
        void SetLoadError(string error)
        {
            _lastError = error;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (error == null)
            {
                _lastWarnedError = null;
            }
            else if (error != _lastWarnedError)
            {
                _lastWarnedError = error;
                UnityEngine.Debug.LogWarning($"NowMarkup: {error}");
            }
#endif
        }

        public NowMarkupFile(string path)
        {
            _path = path ?? string.Empty;
            _resolvedPath = ResolvePath(_path);
            TryStartWatcher();
        }

        public string path => _path;

        public string resolvedPath => _resolvedPath;

        public bool exists
        {
            get
            {
                RefreshFileInfo(out var info);
                return info.Exists;
            }
        }

        public bool loaded => _loaded && _document != null;

        public string lastError => _lastError;

        public DateTime lastWriteTimeUtc => _lastWriteUtc;

        public bool watcherActive => _watcher != null;

        /// <summary>
        /// How often Draw/Refresh re-stats the file: the safety-net cadence for
        /// missed watcher events, and the polling cadence when no watcher could
        /// start (mobile players). Defaults to one second; external edits are
        /// picked up within this delay. <see cref="Reload"/> bypasses the gate.
        /// </summary>
        public TimeSpan pollInterval
        {
            get => _pollInterval;
            set => _pollInterval = value < TimeSpan.Zero ? TimeSpan.Zero : value;
        }

        public NowMarkupDocument document
        {
            get
            {
                Refresh();
                return _document;
            }
        }

        public bool Refresh()
        {
            if (!ShouldCheckFile())
                return false;

            RefreshFileInfo(out var info);

            if (!info.Exists)
            {
                SetLoadError($"Markup file not found: {_resolvedPath}");
                _loaded = true;
                _document = null;
                return false;
            }

            if (_loaded && _document != null &&
                info.LastWriteTimeUtc == _lastWriteUtc &&
                info.Length == _lastLength)
            {
                return false;
            }

            return Reload(info);
        }

        public bool Reload()
        {
            RefreshFileInfo(out var info);
            return Reload(info);
        }

        public NowMarkupResult Draw(NowMarkupState state = null)
        {
            Refresh();
            return _document != null ? _document.Draw(state) : default;
        }

        public NowMarkupResult Draw(NowRect rect, NowMarkupState state = null)
        {
            Refresh();
            return _document != null ? _document.Draw(rect, state) : default;
        }

        bool Reload(FileInfo info)
        {
            if (!info.Exists)
            {
                SetLoadError($"Markup file not found: {_resolvedPath}");
                _loaded = true;
                _document = null;
                return false;
            }

            try
            {
                string text = System.IO.File.ReadAllText(_resolvedPath);
                _document = NowMarkupDocument.Parse(text);
                _lastWriteUtc = info.LastWriteTimeUtc;
                _lastLength = info.Length;
                SetLoadError(null);
                _loaded = true;
                Interlocked.Exchange(ref _dirty, 0);
                return true;
            }
            catch (Exception ex)
            {
                SetLoadError($"Failed to load markup file '{_resolvedPath}': {ex.Message}");
                _loaded = true;
                _nextPollUtc = DateTime.UtcNow.AddMilliseconds(250);
                return false;
            }
        }

        bool ShouldCheckFile()
        {
            if (!_loaded)
                return true;

            if (Interlocked.Exchange(ref _dirty, 0) != 0)
                return true;

            var now = DateTime.UtcNow;

            if (now < _nextPollUtc)
                return false;

            _nextPollUtc = now.Add(_pollInterval);
            return true;
        }

        void TryStartWatcher()
        {
            try
            {
                string directory = Path.GetDirectoryName(_resolvedPath);
                string file = Path.GetFileName(_resolvedPath);

                if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(file) || !Directory.Exists(directory))
                    return;

                _watcher = new FileSystemWatcher(directory, file)
                {
                    NotifyFilter = NotifyFilters.FileName |
                        NotifyFilters.LastWrite |
                        NotifyFilters.Size |
                        NotifyFilters.CreationTime
                };

                _watcher.Changed += OnFileChanged;
                _watcher.Created += OnFileChanged;
                _watcher.Deleted += OnFileChanged;
                _watcher.Renamed += OnFileRenamed;
                _watcher.EnableRaisingEvents = true;
            }
            catch (Exception ex)
            {
                _lastError = ex.Message;
                _watcher = null;
            }
        }

        void OnFileChanged(object sender, FileSystemEventArgs args)
        {
            Interlocked.Exchange(ref _dirty, 1);
        }

        void OnFileRenamed(object sender, RenamedEventArgs args)
        {
            Interlocked.Exchange(ref _dirty, 1);
        }

        void RefreshFileInfo(out FileInfo info)
        {
            info = new FileInfo(_resolvedPath);
            info.Refresh();
        }

        static string ResolvePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;

            path = path.Trim();

            if (Path.IsPathRooted(path))
                return Path.GetFullPath(path);

            return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), path));
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            if (_watcher == null)
                return;

            _watcher.EnableRaisingEvents = false;
            _watcher.Changed -= OnFileChanged;
            _watcher.Created -= OnFileChanged;
            _watcher.Deleted -= OnFileChanged;
            _watcher.Renamed -= OnFileRenamed;
            _watcher.Dispose();
            _watcher = null;
        }
    }
}
