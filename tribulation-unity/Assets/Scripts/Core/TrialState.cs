// Cultivation Trial data — one active vow in a run.
// Ported from game.gd _trial templates and _roll_trials().

namespace Tribulation.Core
{
    public class TrialState
    {
        public string Id;
        public string Fmt;      // e.g. "Slay %d foes"
        public int    Goal;
        public int    Reward;
        public float  Progress;
        public bool   Done;
    }
}
