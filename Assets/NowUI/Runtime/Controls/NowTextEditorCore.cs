using System.Collections.Generic;
using UnityEngine;

namespace NowUI
{
    public struct NowTextUndoEntry
    {
        public string text;
        public int caret;
        public int anchor;
    }

    /// <summary>Small undo/redo stack for immediate-mode text editors.</summary>
    public sealed class NowTextUndoStack
    {
        readonly List<NowTextUndoEntry> _undo = new List<NowTextUndoEntry>(32);
        readonly List<NowTextUndoEntry> _redo = new List<NowTextUndoEntry>(8);
        float _lastEditTime;
        bool _lastWasTyping;

        public void Clear()
        {
            _undo.Clear();
            _redo.Clear();
            _lastEditTime = 0f;
            _lastWasTyping = false;
        }

        public void Push(string text, in NowTextEditState state, bool typing)
        {
            float now = Time.realtimeSinceStartup;

            if (typing && _lastWasTyping && now - _lastEditTime < 0.75f && _undo.Count > 0)
            {
                _lastEditTime = now;
                return;
            }

            _undo.Add(new NowTextUndoEntry { text = text, caret = state.caret, anchor = state.anchor });

            if (_undo.Count > 200)
                _undo.RemoveAt(0);

            _redo.Clear();
            _lastEditTime = now;
            _lastWasTyping = typing;
        }

        public bool Undo(ref string text, ref NowTextEditState state)
        {
            if (_undo.Count == 0)
                return false;

            _redo.Add(new NowTextUndoEntry { text = text, caret = state.caret, anchor = state.anchor });
            var entry = _undo[_undo.Count - 1];
            _undo.RemoveAt(_undo.Count - 1);
            text = entry.text;
            state.caret = entry.caret;
            state.anchor = entry.anchor;
            _lastWasTyping = false;
            return true;
        }

        public bool Redo(ref string text, ref NowTextEditState state)
        {
            if (_redo.Count == 0)
                return false;

            _undo.Add(new NowTextUndoEntry { text = text, caret = state.caret, anchor = state.anchor });
            var entry = _redo[_redo.Count - 1];
            _redo.RemoveAt(_redo.Count - 1);
            text = entry.text;
            state.caret = entry.caret;
            state.anchor = entry.anchor;
            _lastWasTyping = false;
            return true;
        }
    }
}
