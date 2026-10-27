using System;
using UnityEngine;

namespace Fulcrum.Editor.Graph
{
    /// <summary>One plotted line.</summary>
    public sealed class GraphSeries
    {
        public GraphSeries(string name, double[] values, int firstAxisPosition, Color color)
        {
            Name = name;
            Values = values ?? Array.Empty<double>();
            FirstAxisPosition = firstAxisPosition;
            Color = color;
        }

        public string Name { get; }
        public double[] Values { get; }

        /// <summary>Axis position the first sample corresponds to.</summary>
        public int FirstAxisPosition { get; }

        public Color Color { get; }

        public int Count => Values.Length;
        public int LastAxisPosition => FirstAxisPosition + Values.Length - 1;

        /// <summary>Reads a sample by axis position.</summary>
        public bool TryGetAt(int axisPosition, out double value)
        {
            var index = axisPosition - FirstAxisPosition;
            if (index < 0 || index >= Values.Length)
            {
                value = 0d;
                return false;
            }

            value = Values[index];
            return true;
        }

        /// <summary>Palette used when several series share a graph.</summary>
        /// <remarks>
        /// Chosen to stay distinguishable in both editor skins and to survive the common
        /// red–green colour blindness, since the series being compared are usually player
        /// presets and confusing two of them defeats the comparison.
        /// </remarks>
        public static readonly Color[] Palette =
        {
            new Color(0.35f, 0.75f, 1f),
            new Color(1f, 0.72f, 0.30f),
            new Color(0.62f, 0.85f, 0.45f),
            new Color(0.85f, 0.55f, 0.95f),
            new Color(0.95f, 0.45f, 0.45f),
        };

        public static Color ColorAt(int index) => Palette[Mathf.Abs(index) % Palette.Length];
    }

    /// <summary>A span of the axis to shade, used for validation findings.</summary>
    public readonly struct GraphHighlight
    {
        public readonly int From;
        public readonly int To;
        public readonly Color Color;
        public readonly string Label;

        public GraphHighlight(int from, int to, Color color, string label = null)
        {
            From = from;
            To = to;
            Color = color;
            Label = label;
        }
    }
}
