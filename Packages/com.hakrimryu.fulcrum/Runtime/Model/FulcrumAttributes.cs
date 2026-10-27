using System;

namespace Fulcrum
{
    /// <summary>
    /// A short label and explanation shown wherever a type can be chosen.
    /// </summary>
    /// <remarks>
    /// A dropdown listing <c>Power</c>, <c>Exponential</c> and <c>Piecewise</c> asks the
    /// reader to already know what those do. The people this tool is for think in terms of
    /// "gets steeper slowly" and "costs a fixed percentage more each time", so the type
    /// carries that phrasing with it.
    ///
    /// This is an attribute rather than a table inside the editor so a model written in
    /// someone else's project explains itself the same way the built-in ones do.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class FulcrumModelAttribute : Attribute
    {
        public FulcrumModelAttribute(string displayName, string summary)
        {
            DisplayName = displayName;
            Summary = summary;
        }

        /// <summary>Name shown in the dropdown.</summary>
        public string DisplayName { get; }

        /// <summary>One line explaining when to reach for it.</summary>
        public string Summary { get; }
    }

    /// <summary>
    /// A sensible editing range for a numeric field, so it gets a slider.
    /// </summary>
    /// <remarks>
    /// Unity's <c>[Range]</c> only covers <c>float</c> and <c>int</c>, and every parameter
    /// here is a <c>double</c>. Beyond that, a bare number box gives no sense of scale — a
    /// designer typing into "Exponent" has no way to know whether 1.1 or 11 is the
    /// interesting neighbourhood. The bounds are advisory: the field beside the slider
    /// still accepts anything.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Field, Inherited = false)]
    public sealed class FulcrumRangeAttribute : Attribute
    {
        public FulcrumRangeAttribute(double min, double max)
        {
            Min = min;
            Max = max;
        }

        public double Min { get; }
        public double Max { get; }
    }
}
