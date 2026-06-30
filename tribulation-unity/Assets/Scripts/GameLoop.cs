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
        if (SwipeDetector.I != null) SwipeDetector.I.Tapped += OnTap;
    }

    public void OnPlayerDied()
    {
        _dead = true;
        Debug.Log($"DIED — distance {_player.GetDistance()}m. Tap / Space to restart.");
    }

    void OnTap() { if (_dead) Restart(); }

    void Update()
    {
        if (_dead && (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))) Restart();
    }

    void Restart()
    {
        _dead = false;
        HudOverlay.I?.HideDeathCard();
        if (Game.I != null) Game.I.RestartRun(); // reset run-state core, else spawner stays dead
        _player.ResetRun();
        _spawner.ClearAll();
    }
}
