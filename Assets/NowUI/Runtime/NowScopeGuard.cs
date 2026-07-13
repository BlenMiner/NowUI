using System;
using System.Collections.Generic;

namespace NowUI
{
    /// <summary>
    /// Shared ownership for stack-backed value-type scopes. A disposed copy is
    /// harmless, while disposing a live outer scope before its inner scope fails
    /// without mutating the stack.
    /// </summary>
    internal sealed class NowScopeGuard
    {
        readonly string _name;

        readonly List<int> _tokens;

        int _nextToken;

        internal NowScopeGuard(string name, int capacity = 4)
        {
            _name = name;
            _tokens = new List<int>(capacity);
        }

        internal int count => _tokens.Count;

        internal int Enter()
        {
            int token;

            do
            {
                token = unchecked(++_nextToken);
            }
            while (token == 0);

            _tokens.Add(token);
            return token;
        }

        internal bool IsCurrent(int token)
        {
            if (token == 0)
                return false;

            int last = _tokens.Count - 1;

            if (last >= 0 && _tokens[last] == token)
                return true;

            for (int i = last - 1; i >= 0; --i)
            {
                if (_tokens[i] == token)
                {
                    throw new InvalidOperationException(
                        $"{_name} scopes must be disposed in reverse order. Dispose the inner scope first.");
                }
            }

            // The token was already disposed or invalidated at a frame boundary.
            return false;
        }

        internal bool Exit(int token)
        {
            if (!IsCurrent(token))
                return false;

            _tokens.RemoveAt(_tokens.Count - 1);
            return true;
        }

        internal void ExitCurrent()
        {
            if (_tokens.Count > 0)
                _tokens.RemoveAt(_tokens.Count - 1);
        }

        internal void Clear()
        {
            _tokens.Clear();
        }

        internal void CopyTo(List<int> destination)
        {
            destination.Clear();
            destination.AddRange(_tokens);
        }

        internal void RestoreFrom(List<int> source)
        {
            _tokens.Clear();
            _tokens.AddRange(source);
        }
    }
}
