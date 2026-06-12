using System;

namespace NowUI
{
    /// <summary>
    /// Marks a struct as an inert NowUI builder: constructing it has no effect
    /// until the chain is consumed by Draw()/Begin(). The bundled Roslyn
    /// analyzer (NOWUI001) warns when a marked builder is discarded as a bare
    /// statement. Apply it to your own builder structs to get the same check.
    /// </summary>
    [AttributeUsage(AttributeTargets.Struct, Inherited = false)]
    public sealed class NowBuilderAttribute : Attribute
    {
    }
}
