// Pure-C# POCO — no UnityEngine dependency.
// Field names match balance.gd's JSON keys verbatim.
// The MonoBehaviour layer (Balance.cs) fills these from Resources/balance.json via JsonUtility.
namespace Tribulation.Core
{
    [System.Serializable]
    public class BalanceData
    {
        public int[]  realm_span           = { 50, 120, 300, 750, 1800, 999999 };
        public float  difficulty_per_realm = 12f;

        public float  player_base_speed      = 12f;
        public float  player_max_speed       = 22f;
        public float  player_speed_ramp_time = 90f;
        public float  player_speed_creep     = 0.07f;
        public float  player_speed_creep_cap = 16f;

        public float  spawn_start_interval    = 1.4f;
        public float  spawn_min_interval      = 0.7f;
        public float  spawn_ramp_time         = 60f;
        public float  spawn_hard_min_interval = 0.42f;
        public float  spawn_endless_ramp      = 200f;
        public float  spawn_gate_interval     = 11f;
        public float  spawn_orb_interval      = 2.4f;
        public float  spawn_pill_interval     = 9f;

        public float  qi_max            = 100f;
        public float  qi_per_kill       = 20f;
        public float  net_close_rate    = 0.025f;
        public float  net_push_per_kill = 0.12f;
        public float  net_burst_relief  = 0.30f;
        // Non-lethal contact: Net spike per hit. Enemy (failed-to-slash pursuer) hurts more
        // than a dumb obstacle. Placeholders — tune in playtest.
        public float  net_hit_enemy     = 0.25f;
        public float  net_hit_obstacle  = 0.12f;
        public float  revive_net_reset  = 0.35f;

        public int    daily_base  = 80;
        public int    ach_reward  = 150;
    }
}
