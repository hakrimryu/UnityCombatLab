using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace Fulcrum
{
    /// <summary>
    /// Reads another track, optionally shifted along the axis and scaled.
    /// </summary>
    /// <remarks>
    /// This is what makes one curve depend on another. Expected gold per stage is not an
    /// independent shape — it follows from monster count, drop value and drop rate, each
    /// tuned on its own. Restating it as a fourth curve means keeping four things in sync
    /// by hand; deriving it means changing one input and having the rest follow.
    ///
    /// <see cref="AxisOffset"/> covers the cases where the relationship is not aligned:
    /// the reward for clearing stage <c>n</c> often depends on the difficulty of stage
    /// <c>n</c>, but the cost of the upgrade it buys belongs to <c>n + 1</c>.
    /// </remarks>
    [Serializable]
    [FulcrumModel("Another track", "Reads a different track. Use it when one value follows from another.")]
    [MovedFrom(false, sourceNamespace: "Fulcrum", sourceAssembly: "Fulcrum.Core", sourceClassName: nameof(TrackReferenceModel))]
    public sealed class TrackReferenceModel : GrowthModel
    {
        [Tooltip("Id of the track to read.")]
        [SerializeField] private string _trackId;

        [Tooltip("Multiplies the value read.")]
        [SerializeField] private double _scale = 1d;

        [Tooltip("Shifts the position before reading. -1 reads the previous step.")]
        [SerializeField] private int _axisOffset;

        public TrackReferenceModel() { }

        public TrackReferenceModel(string trackId, double scale = 1d, int axisOffset = 0)
        {
            _trackId = trackId;
            _scale = scale;
            _axisOffset = axisOffset;
        }

        /// <summary>Id of the track being read.</summary>
        public string TrackId
        {
            get => _trackId;
            set => _trackId = value;
        }

        /// <summary>Multiplier applied to the referenced value.</summary>
        public double Scale
        {
            get => _scale;
            set => _scale = value;
        }

        /// <summary>Shift applied to the axis position before reading.</summary>
        public int AxisOffset
        {
            get => _axisOffset;
            set => _axisOffset = value;
        }

        public override double Evaluate(double x, EvalContext context)
            => context.GetTrack(_trackId, x + _axisOffset) * _scale;

        public override void CollectTrackReferences(ICollection<string> into)
        {
            if (!string.IsNullOrEmpty(_trackId)) into.Add(_trackId);
        }

        public override string Describe()
        {
            if (string.IsNullOrEmpty(_trackId)) return "track (unset)";

            var body = _axisOffset == 0
                ? $"track({_trackId})"
                : $"track({_trackId}, x{FormulaText.Addend(_axisOffset)})";

            return FormulaText.Scaled(_scale, body);
        }
    }
}
