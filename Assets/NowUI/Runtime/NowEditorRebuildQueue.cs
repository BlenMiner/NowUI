#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace NowUI
{
    /// <summary>
    /// Editor-only deferred rebuild scheduling for retained hosts that render
    /// outside the canvas update loop: MeshRenderer-based hosts have no edit-time
    /// rebuild tick, so inspector/scene changes queue a one-shot rebuild via
    /// <see cref="EditorApplication.delayCall"/>. NowGraphic deliberately does not
    /// use this — UGUI's CanvasUpdateRegistry already defers its
    /// OnValidate-driven rebuilds to the next canvas update.
    /// </summary>
    internal static class NowEditorRebuildQueue
    {
        /// <summary>
        /// Queues <paramref name="callback"/> for the next editor tick. Returns
        /// true when it queued just now (callers can then register any one-time
        /// editor hooks); no-op while playing or already queued.
        /// </summary>
        public static bool Queue(ref bool queued, EditorApplication.CallbackFunction callback)
        {
            if (Application.isPlaying || queued)
                return false;

            queued = true;
            EditorApplication.delayCall += callback;
            EditorApplication.QueuePlayerLoopUpdate();
            return true;
        }

        public static void Cancel(ref bool queued, EditorApplication.CallbackFunction callback)
        {
            if (!queued)
                return;

            EditorApplication.delayCall -= callback;
            queued = false;
        }
    }
}
#endif
