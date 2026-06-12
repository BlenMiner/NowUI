using System;

namespace NowUI
{
    /// <summary>
    /// Marks a disposable struct as a using-only NowUI scope: it restores state
    /// exclusively in Dispose and has no public end-call alternative, so
    /// discarding one as a bare statement is always a bug. The bundled Roslyn
    /// analyzer (NOWUI002) warns on that pattern. Not applied to
    /// <see cref="NowLayoutScope"/>, whose groups can also be closed with
    /// NowLayout.EndArea/EndHorizontal/EndVertical.
    /// </summary>
    [AttributeUsage(AttributeTargets.Struct, Inherited = false)]
    public sealed class NowScopeAttribute : Attribute
    {
    }
}
