using System;
using System.Collections.Generic;

namespace Fulcrum
{
    /// <summary>
    /// A curve that turns an axis position into a value — stage into monster HP,
    /// character level into required experience, and so on.
    /// </summary>
    /// <remarks>
    /// Concrete models are held through <c>[SerializeReference]</c> so a track can swap
    /// one formula for another without changing its own type. That serialisation records
    /// the concrete class name, namespace and assembly as text, so <b>renaming a subclass
    /// silently breaks every asset that referenced it</b> — the field becomes null and
    /// Unity reports nothing.
    ///
    /// Two rules follow, and both are load-bearing:
    /// <list type="number">
    /// <item>Subclass names are fixed once shipped.</item>
    /// <item>Every concrete subclass carries <c>[MovedFrom]</c> from the day it is written,
    /// not from the day someone wants to rename it. Adding it afterwards is too late.</item>
    /// </list>
    /// </remarks>
    [Serializable]
    public abstract class GrowthModel
    {
        /// <summary>
        /// Evaluates the curve at <paramref name="x"/>.
        /// </summary>
        /// <param name="x">Position along the track's axis, such as a stage number.</param>
        /// <param name="context">Globals and the index being evaluated.</param>
        /// <returns>The raw value, before any post-processing.</returns>
        public abstract double Evaluate(double x, EvalContext context);

        /// <summary>
        /// A short human-readable form of the formula, shown next to the graph
        /// (for example <c>100 · x^1.35</c>).
        /// </summary>
        public abstract string Describe();

        /// <summary>
        /// Reports the ids of any tracks this model reads.
        /// </summary>
        /// <remarks>
        /// Declared rather than inferred. Working the references out by reflection would
        /// mean guessing which string fields happen to be track ids, and it would silently
        /// miss anything a model in someone else's project does — so a curve that depends
        /// on another would evaluate before its input, or a cycle would go undetected.
        ///
        /// Models that hold other models must forward the call; a reference nested inside
        /// a piecewise segment is still a dependency of the track.
        /// </remarks>
        public virtual void CollectTrackReferences(ICollection<string> into) { }
    }
}
