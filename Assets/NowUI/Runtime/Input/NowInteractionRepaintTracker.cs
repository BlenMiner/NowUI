using UnityEngine;

namespace NowUI
{
    /// <summary>
    /// Shared interaction-repaint watcher for retained hosts. A host stores the
    /// input it drew with (<see cref="StoreFrameInput"/>) and whether any hosted
    /// control requested a repaint (<see cref="SetWantsRepaint"/>, fed from
    /// <c>NowFrame.EndRepaintTracking()</c>); each update tick it then asks
    /// <see cref="ShouldRepaint"/> whether a rebuild is warranted. The first
    /// sample after <see cref="Reset"/> primes the watcher and reports no change,
    /// so enabling a host never triggers a spurious rebuild.
    /// </summary>
    internal struct NowInteractionRepaintTracker
    {
        bool _wantsRepaint;

        bool _hasLastInput;

        NowInteractionInputState _lastInput;

        /// <summary>True when a hosted control asked for a repaint during the last rebuild.</summary>
        public bool wantsRepaint => _wantsRepaint;

        public void SetWantsRepaint(bool value)
        {
            _wantsRepaint = value;
        }

        /// <summary>Records the input state the host just rebuilt with.</summary>
        public void StoreFrameInput(in NowInputSnapshot snapshot, Vector2 size)
        {
            _lastInput = NowInteractionInputState.FromSnapshot(snapshot, size);
            _hasLastInput = true;
        }

        /// <summary>
        /// Polls the provider and reports whether input changed since the state
        /// stored at the last rebuild. Does not advance the stored state — the
        /// next rebuild does that via <see cref="StoreFrameInput"/>.
        /// </summary>
        public bool HasInputChanged(INowInputProvider provider, NowInputSurface surface)
        {
            if (provider == null || !provider.TryGetSnapshot(surface, out var snapshot))
                snapshot = default;

            var current = NowInteractionInputState.FromSnapshot(snapshot, surface.size);

            if (!_hasLastInput)
            {
                _lastInput = current;
                _hasLastInput = true;
                return false;
            }

            return current.HasChangedSince(_lastInput);
        }

        /// <summary>Control-requested repaint or fresh input change, in one call.</summary>
        public bool ShouldRepaint(INowInputProvider provider, NowInputSurface surface)
        {
            return _wantsRepaint || HasInputChanged(provider, surface);
        }

        /// <summary>Clears the stored sample so the next poll primes instead of comparing.</summary>
        public void Reset()
        {
            _hasLastInput = false;
        }
    }
}
