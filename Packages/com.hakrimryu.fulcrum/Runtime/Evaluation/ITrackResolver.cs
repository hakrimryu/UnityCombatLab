namespace Fulcrum
{
    /// <summary>
    /// Supplies the value of another track during evaluation.
    /// </summary>
    /// <remarks>
    /// Some numbers are not independent curves. Expected gold per stage is monster count
    /// times drop value times drop rate — three tracks a designer tunes separately, and one
    /// result that has to follow from them rather than being maintained alongside them.
    ///
    /// Implementations are handed to models through <see cref="EvalContext"/> and are
    /// expected to have already evaluated whatever a caller asks for; ordering is the
    /// dependency graph's job, not the model's.
    /// </remarks>
    public interface ITrackResolver
    {
        /// <summary>
        /// Value of <paramref name="trackId"/> at an axis position.
        /// </summary>
        /// <returns>
        /// False when the track is unknown or the position lies outside its range. A model
        /// referring to a track that does not exist is an authoring mistake to surface, not
        /// an exception to throw mid-evaluation.
        /// </returns>
        bool TryGet(string trackId, double x, out double value);
    }
}
