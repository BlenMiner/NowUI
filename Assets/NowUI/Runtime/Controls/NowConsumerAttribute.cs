using System;

namespace NowUI
{
    /// <summary>
    /// Marks a method that consumes a NowUI builder — it performs the actual
    /// work (drawing, reserving layout space) and returns the value only for
    /// further chaining. The bundled Roslyn analyzer treats a statement ending
    /// in a consumer call as used, even when the returned builder is discarded.
    /// Apply it to the Draw-equivalents of your own [NowBuilder] structs when
    /// they return the builder for chaining.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public sealed class NowConsumerAttribute : Attribute
    {
    }
}
