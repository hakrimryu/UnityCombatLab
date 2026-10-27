using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace Fulcrum
{
    /// <summary>How <see cref="CompositeModel"/> combines its parts.</summary>
    public enum CompositeOperation
    {
        /// <summary>Sum of every part. An empty list gives 0.</summary>
        Sum = 0,

        /// <summary>Product of every part. An empty list gives 0, not 1.</summary>
        Product = 1,

        /// <summary>Smallest part.</summary>
        Min = 2,

        /// <summary>Largest part.</summary>
        Max = 3,
    }

    /// <summary>
    /// Combines several models into one curve.
    /// </summary>
    /// <remarks>
    /// Paired with <see cref="TrackReferenceModel"/> this is what expresses a derived
    /// value: expected gold is the product of monster count, drop value and drop rate,
    /// each of which is its own track with its own tuning.
    ///
    /// An empty composite evaluates to 0 for every operation, including
    /// <see cref="CompositeOperation.Product"/> where the mathematical identity would be 1.
    /// A half-built track quietly contributing a 1 into a product is a worse failure than
    /// one that reads as obviously unfinished.
    /// </remarks>
    [Serializable]
    [FulcrumModel("Several combined", "Adds, multiplies or picks between other models. Expected gold is count x drop x rate.")]
    [MovedFrom(false, sourceNamespace: "Fulcrum", sourceAssembly: "Fulcrum.Core", sourceClassName: nameof(CompositeModel))]
    public sealed class CompositeModel : GrowthModel
    {
        [SerializeField] private CompositeOperation _operation = CompositeOperation.Sum;
        [SerializeReference] private List<GrowthModel> _parts = new List<GrowthModel>();

        public CompositeModel() { }

        public CompositeModel(CompositeOperation operation, params GrowthModel[] parts)
        {
            _operation = operation;
            _parts = new List<GrowthModel>(parts ?? Array.Empty<GrowthModel>());
        }

        public CompositeOperation Operation
        {
            get => _operation;
            set => _operation = value;
        }

        /// <summary>The models being combined. Null entries are skipped.</summary>
        public List<GrowthModel> Parts
        {
            get => _parts ?? (_parts = new List<GrowthModel>());
            set => _parts = value ?? new List<GrowthModel>();
        }

        public override double Evaluate(double x, EvalContext context)
        {
            var parts = _parts;
            if (parts == null) return 0d;

            var result = 0d;
            var count = 0;

            foreach (var part in parts)
            {
                if (part == null) continue;

                var value = part.Evaluate(x, context);
                if (count == 0)
                {
                    result = value;
                }
                else
                {
                    switch (_operation)
                    {
                        case CompositeOperation.Product: result *= value; break;
                        case CompositeOperation.Min: if (value < result) result = value; break;
                        case CompositeOperation.Max: if (value > result) result = value; break;
                        default: result += value; break;
                    }
                }

                count++;
            }

            return count == 0 ? 0d : result;
        }

        public override void CollectTrackReferences(ICollection<string> into)
        {
            var parts = _parts;
            if (parts == null) return;

            foreach (var part in parts)
            {
                part?.CollectTrackReferences(into);
            }
        }

        public override string Describe()
        {
            var parts = _parts;
            if (parts == null || parts.Count == 0) return "composite (empty)";

            var separator = OperatorText(_operation);
            var sb = new StringBuilder();
            var written = 0;

            foreach (var part in parts)
            {
                if (part == null) continue;
                if (written > 0) sb.Append(separator);
                sb.Append(part.Describe());
                written++;
            }

            if (written == 0) return "composite (empty)";
            if (_operation == CompositeOperation.Min || _operation == CompositeOperation.Max)
            {
                return $"{_operation.ToString().ToLowerInvariant()}({sb})";
            }

            return sb.ToString();
        }

        private static string OperatorText(CompositeOperation operation)
        {
            switch (operation)
            {
                case CompositeOperation.Product: return " × ";
                case CompositeOperation.Min:
                case CompositeOperation.Max: return ", ";
                default: return " + ";
            }
        }
    }
}
