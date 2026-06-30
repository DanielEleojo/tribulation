using UnityEngine;

// Port of balance.gd. Single source of tunable knobs, loaded from Resources/balance.json
// over the built-in defaults — so the game re-tunes by editing one JSON, no recompile,
// and still runs if the file is missing (defaults win).
//
// Field names match the JSON keys verbatim because Unity's JsonUtility has no key-remap
// attribute. ponytail: underscore field names instead of a custom JSON parser.
[System.Serializable]
public class BalanceData
{
    public int[] realm_span = { 50, 120, 300, 750, 1800, 999999 };
    public float difficulty_per_realm = 12f;

    public float player_base_speed = 12f;
    public float player_max_speed = 22f;
    public float player_speed_ramp_time = 90f;
    public float player_speed_creep = 0.07f;
    public float player_speed_creep_cap = 16f;

    public float spawn_start_interval = 1.4f;
    public float spawn_min_interval = 0.7f;
    public float spawn_ramp_time = 60f;
    public float spawn_hard_min_interval = 0.42f;
    public float spawn_endless_ramp = 200f;
    public float spawn_gate_interval = 11f;
    public float spawn_orb_interval = 2.4f;
    public float spawn_pill_interval = 9f;

    public float qi_max = 100f;
    public float qi_per_kill = 20f;
    public float net_close_rate = 0.025f;
    public float net_push_per_kill = 0.12f;

    public int daily_base = 80;
    public int ach_reward = 150;
}

public static class Balance
{
    static BalanceData _d;
    public static BalanceData D
    {
        get
        {
            if (_d == null)
            {
                _d = new BalanceData();
                var ta = Resources.Load<TextAsset>("balance");
                if (ta != null) JsonUtility.FromJsonOverwrite(ta.text, _d); // only present keys override
            }
            return _d;
        }
    }
}
