using System.Globalization;

namespace Fulcrum
{
    /// <summary>
    /// Formats numbers and terms for <see cref="GrowthModel.Describe"/>.
    /// </summary>
    /// <remarks>
    /// These strings are shown next to the graph, so they favour readability over
    /// round-tripping: trailing zeros are dropped and a term whose coefficient is 1
    /// or 0 collapses. Formatting is culture invariant — a comma decimal separator
    /// would read as a term separator.
    /// </remarks>
    internal static class FormulaText
    {
        private const string NumberFormat = "0.######";

        /// <summary>Formats a bare number, e.g. <c>1.35</c> or <c>100</c>.</summary>
        public static string Number(double value)
            => value.ToString(NumberFormat, CultureInfo.InvariantCulture);

        /// <summary>
        /// Formats a leading coefficient applied to <paramref name="body"/>, collapsing
        /// the multiplier when it is 1 (<c>x^2</c>) and the whole term when it is 0.
        /// </summary>
        public static string Scaled(double coefficient, string body)
        {
            if (coefficient == 0d) return "0";
            if (coefficient == 1d) return body;
            if (coefficient == -1d) return "-" + body;
            return Number(coefficient) + " · " + body;
        }

        /// <summary>
        /// Formats a trailing addend as <c> + 5</c> or <c> - 5</c>, returning an empty
        /// string for zero so a plain term stays plain.
        /// </summary>
        public static string Addend(double value)
        {
            if (value == 0d) return string.Empty;
            return value < 0d
                ? " - " + Number(-value)
                : " + " + Number(value);
        }
    }
}
