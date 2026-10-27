using System;

namespace Fulcrum
{
    /// <summary>
    /// One step that turns raw formula output into a number a game can ship.
    /// </summary>
    /// <remarks>
    /// Balance values are never the bare output of a formula. They get rounded to
    /// something readable, floored so nothing reaches zero, nudged so each step is
    /// visibly larger than the last. Keeping those adjustments as an explicit ordered
    /// chain — rather than folding them into the formulas — is what separates a curve
    /// editor from a tool a designer can actually ship from.
    ///
    /// Processors operate on the <b>whole series</b>, not one value at a time. Smoothing
    /// needs the neighbours on both sides and a minimum-step rule needs the value that
    /// came before, so a per-value signature cannot express them. Steps that genuinely
    /// look at one value should derive from <see cref="PointwisePostProcessor"/> instead
    /// of implementing this directly.
    ///
    /// The same <c>[SerializeReference]</c> rules as <see cref="GrowthModel"/> apply:
    /// names are fixed once shipped, and every concrete step carries <c>[MovedFrom]</c>
    /// from the day it is written.
    /// </remarks>
    [Serializable]
    public abstract class PostProcessor
    {
        /// <summary>
        /// Adjusts <paramref name="values"/> in place.
        /// </summary>
        /// <param name="values">
        /// The series so far, already shaped by every earlier step in the chain.
        /// Never null; may be empty.
        /// </param>
        /// <param name="context">Globals available to the step.</param>
        public abstract void Process(double[] values, EvalContext context);

        /// <summary>A short label for the chain UI, such as <c>round 3sf</c>.</summary>
        public abstract string Describe();
    }

    /// <summary>
    /// Convenience base for steps whose result depends only on the value at hand.
    /// </summary>
    /// <remarks>
    /// The index is passed explicitly and is authoritative here — do not read
    /// <see cref="EvalContext.CurrentIndex"/>, which describes the evaluation pass that
    /// produced the series rather than the position being adjusted.
    /// </remarks>
    [Serializable]
    public abstract class PointwisePostProcessor : PostProcessor
    {
        public sealed override void Process(double[] values, EvalContext context)
        {
            if (values == null) return;

            for (var i = 0; i < values.Length; i++)
            {
                values[i] = ProcessOne(values[i], i, context);
            }
        }

        /// <summary>Adjusts a single value.</summary>
        /// <param name="value">The value at <paramref name="index"/>.</param>
        /// <param name="index">Position within the series, counted from zero.</param>
        /// <param name="context">Globals available to the step.</param>
        protected abstract double ProcessOne(double value, int index, EvalContext context);
    }
}
