using UnityEngine;

// Tracer-bullet stand-in for game.gd: just enough state to die and restart so the
// perf test is a real loop. The full state machine (realms, Qi, net, combo, save) is
// Phase 3. No UI yet — death logs to the console.
public class GameLoop : MonoBehaviour
{
    public static GameLoop I;

    PlayerRunner _player;
    Spawner _spawner;
    bool _dead;

    void Awake() { I = this; }

    void Start()
    {
        _player = FindObjectOfType<PlayerRunner>();
        _spawner = FindObjectOfType<Spawner>();
    }

    public void OnPlayerDied()
    {
        _dead = true;
        Debug.Log($"DIED — distance {_player.GetDistance()}m. Death-card button / Space to restart.");
    }

    /// <summary>Called by Game.PerformRevive() after a successful ad-revive. Clears the
    /// restart gate — without this a stray restart press right after reviving would
    /// restart the freshly-revived run instead of doing nothing.</summary>
    public void OnPlayerRevived() { _dead = false; }

    /// <summary>Death card's "WALK THE ROAD AGAIN" button. Restarting is button-only on
    /// device (the old tap-anywhere handler is gone so card buttons can't double-fire);
    /// the _dead gate makes a stale press after a revive a no-op.</summary>
    public void RestartFromDeathCard() { if (_dead) Restart(); }

    /// <summary>Leaving the run for the main menu (death card's "RETURN TO MAIN MENU").
    /// Clears the restart gate so a stale Space press can't restart under the menu.</summary>
    public void OnRunExited() { _dead = false; }

    void Update()
    {
        if (_dead && (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))) Restart();
    }

    void Restart()
    {
        _dead = false;
        if (AdsManager.I != null) AdsManager.I.RestartWithInterstitial(() => Game.I?.PerformRestart());
        else Game.I?.PerformRestart();
    }
}
