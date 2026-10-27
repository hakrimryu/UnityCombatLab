using System.Collections.Generic;

namespace Fulcrum
{
    /// <summary>
    /// Everything a <see cref="GrowthModel"/> can see besides its own parameters.
    /// </summary>
    /// <remarks>
    /// One context is built per evaluation pass and reused across every sample, so it is
    /// read-only.
    /// </remarks>
    public sealed class EvalContext
    {
        private static readonly IReadOnlyDictionary<string, double> NoGlobals =
            new Dictionary<string, double>();

        /// <summary>Index currently being evaluated, counted from the track's range start.</summary>
        public int CurrentIndex { get; }

        /// <summary>Profile-wide constants, such as a difficulty multiplier.</summary>
        public IReadOnlyDictionary<string, double> Globals { get; }

        /// <summary>
        /// Access to other tracks, or null when a track is evaluated on its own.
        /// </summary>
        /// <remarks>
        /// Null is a legitimate state — a single track can be evaluated for a preview
        /// without the rest of the profile — so models that read it must cope with its
        /// absence rather than assume a whole profile is present.
        /// </remarks>
        public ITrackResolver Tracks { get; }

        public EvalContext(
            int currentIndex,
            IReadOnlyDictionary<string, double> globals = null,
            ITrackResolver tracks = null)
        {
            CurrentIndex = currentIndex;
            Globals = globals ?? NoGlobals;
            Tracks = tracks;
        }

        /// <summary>Returns a copy positioned at <paramref name="index"/>, sharing globals and resolver.</summary>
        public EvalContext AtIndex(int index) => new EvalContext(index, Globals, Tracks);

        /// <summary>Returns a copy that can also reach other tracks.</summary>
        public EvalContext WithTracks(ITrackResolver tracks)
            => new EvalContext(CurrentIndex, Globals, tracks);

        /// <summary>
        /// Reads another track, falling back when it is unreachable. A missing track is an
        /// authoring problem the editor surfaces, not something to throw over here.
        /// </summary>
        public double GetTrack(string trackId, double x, double fallback = 0d)
            => Tracks != null && Tracks.TryGet(trackId, x, out var value) ? value : fallback;

        /// <summary>Looks up a global, reporting whether it was defined.</summary>
        public bool TryGetGlobal(string key, out double value)
        {
            if (key != null && Globals.TryGetValue(key, out value)) return true;

            value = 0d;
            return false;
        }

        /// <summary>
        /// Looks up a global, falling back to <paramref name="fallback"/> when it is missing.
        /// A missing global is a normal state — profiles are edited incrementally — so this
        /// does not throw.
        /// </summary>
        public double GetGlobal(string key, double fallback = 0d)
            => TryGetGlobal(key, out var value) ? value : fallback;
    }
}
