// Thin MonoBehaviour wrapper around GameCore.
// Owns: file I/O for save/load, ticking the core, forwarding events to HUD and other systems.
// Does NOT own: game logic — that lives entirely in GameCore (Tribulation.Core).
//
// Ported from game.gd. Out-of-scope items (audio buses, shop, dailies, achievements,
// trials, powerups, HUD Canvas authoring) are noted with "ponytail:" and deferred to
// later issues.
using System.IO;
using UnityEngine;
using Tribulation.Core;
using SaveData = Tribulation.Core.SaveData;

public class Game : MonoBehaviour
{
    public static Game I { get; private set; }

    // The pure-C# state machine
    public GameCore Core { get; private set; }

    // First-encounter technique name persistence
    public Tribulation.Core.Telegraph Tele { get; private set; }

    // ── Wiring ──────────────────────────────────────────────────────────────
    PlayerRunner  _player;
    Spawner       _spawner;

    // ── Lifecycle ────────────────────────────────────────────────────────────
    void Awake()
    {
        I    = this;
        // Balance.D already returns a Tribulation.Core.BalanceData (fully loaded from
        // Resources/balance.json over defaults) — hand it straight to the core.
        Core = new GameCore(Balance.D);
        Tele = new Tribulation.Core.Telegraph();
    }

    void Start()
    {
        _player  = FindObjectOfType<PlayerRunner>();
        _spawner = FindObjectOfType<Spawner>();

        // Wire core events
        Core.Died             += OnCoreDied;
        Core.Burst            += () =>
        {
            if (SoundManager.I != null) SoundManager.I.Play("burst");
            // Feel: the Qi Burst is your panic button — make it hit hard.
            Feel.Hitstop(0.08f);
            var cam = FindObjectOfType<CameraFollow>();
            if (cam != null) { cam.AddFovKick(8f); cam.AddTrauma(0.5f); }
            _player?.FeelPop(0.40f);
        };
        Core.Breakthrough     += () => { if (SoundManager.I != null) SoundManager.I.Play("breakthrough"); };
        Core.TrialFulfilled   += r => { if (SoundManager.I != null) SoundManager.I.Play("breakthrough"); };
        // ponytail: trial banner + HUD trial list — Batch 2 (HudOverlay)
        // ponytail: QiChanged / NetChanged / SoulsChanged / ComboChanged → HUD deferred (later issue)

        // Telemetry: wire achievement + daily events (local JSONL only — no data leaves device)
        Core.AchievementUnlocked += id => Telemetry.Event("achievement", $"\"id\":\"{id}\"");
        Core.DailyClaimed        += (s, r) => Telemetry.Event("daily", $"\"streak\":{s},\"reward\":{r}");

        // Load saved state; run begins when the player taps "Begin Cultivation" (MainMenu).
        LoadSave();

        // ponytail: apply audio settings to buses — deferred
    }

    void Update()
    {
        if (Core == null) return;
        Core.Tick(Time.deltaTime);
        if (_player != null) Core.TrialMax("li", _player.GetDistance());

        // ponytail: tier-up logic, powerup ticks, autosave timer — deferred
    }

    // ── Core event handlers ──────────────────────────────────────────────────
    void OnCoreDied()
    {
        int dist = _player != null ? Mathf.RoundToInt(_player.GetDistance()) : 0;
        Core.RecordDistance(dist);
        // Stop the runner + trigger its death animation (the Net closing IS death now).
        if (_player != null) _player.HaltForDeath();
        SaveGame();
        // The Net closing over you IS death now — give it the death sfx/shake here, since
        // it no longer routes through PlayerRunner.Die (contact is non-lethal).
        if (SoundManager.I != null) SoundManager.I.Play("death");
        var cam = FindObjectOfType<CameraFollow>(); if (cam != null) cam.AddTrauma(0.8f);
        Debug.Log($"[Game] Died. Realm={Core.Realm}  Total={Core.TotalStones}  Best={Core.BestLi}");
        // Notify GameLoop so tap-to-restart still works (single death route through Game).
        if (GameLoop.I != null) GameLoop.I.OnPlayerDied();
    }

    // ── Public API (called by Hazard, Spawner, Gate, etc.) ──────────────────
    public void OnPlayerHit()   { Core?.OnPlayerHit(_player != null ? _player.GetDistance() : 0); }
    public void OnContactHit(bool isEnemy) { Core?.OnContactHit(isEnemy); }
    public void OnEnemyKilled(int count = 1) { Core?.OnEnemyKilled(count); }
    public void OnOrbCollected()
    {
        Core?.OnOrbCollected();
        if (SoundManager.I != null) SoundManager.I.Play("orb");
        _player?.FeelPop(0.16f); // feel: light scale-pop on pickup
        HudOverlay.I?.PunchStones(); // feel: punch-scale stones counter on every collect
    }
    public void ActivatePowerup(string id)
    {
        Core?.ActivatePowerup(id);
        if (id == "aegis" && _player != null) _player.GrantShield();
    }
    public bool IsPowerupActive(string id)   => Core != null && Core.IsPowerupActive(id);
    public void RestartRun()                 { Core?.RestartRun(); } // re-init run-state after death
    /// <summary>Called by MenuScreens after a shop purchase or settings change to persist immediately.</summary>
    public void SaveProgress() => SaveGame();

    /// <summary>
    /// Called by PauseMenu "Quit to Menu" (and cultivation reset): ends the run and idles
    /// the core, player, and spawner so nothing simulates behind the main menu. The next
    /// BeginRun() starts fresh.
    /// </summary>
    public void EndRunToMenu()
    {
        Core?.EndRun();
        _player?.StopForMenu();
        _spawner?.ClearAll();
        SaveGame();
    }

    /// <summary>Called by MainMenu "Begin Cultivation" button. Starts the first run.</summary>
    public void BeginRun()
    {
        Core.StartRun();
        if (SoundManager.I != null) SoundManager.I.Play("start");
        float off = Core.DifficultyOffset();
        _player?.BeginRunning(off);
        if (_spawner != null) _spawner.BeginRun(off);
    }
    /// <summary>Called by Gate when the player passes through a curtain.</summary>
    public void OnGate(bool safe)
    {
        Core?.OnGate(safe);
        if (SoundManager.I != null) SoundManager.I.Play(safe ? "gate_good" : "gate_bad");
    }

    // ── Save / Load via JsonUtility + persistentDataPath ─────────────────────
    string SavePath => Path.Combine(Application.persistentDataPath, "tribulation_save.json");

    void LoadSave()
    {
        try
        {
            if (File.Exists(SavePath))
            {
                string json = File.ReadAllText(SavePath);
                var save    = JsonUtility.FromJson<SaveData>(json);
                Core.LoadSave(save);
                if (save.seenTechniques != null) Tele.LoadSeen(save.seenTechniques);
                // ponytail: apply saved audio volumes to buses — deferred
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[Game] LoadSave failed: {e.Message}");
        }
    }

    void SaveGame()
    {
        try
        {
            var save  = Core.ToSave();
            save.seenTechniques = new System.Collections.Generic.List<string>(Tele.SeenTechniques);
            string json = JsonUtility.ToJson(save, true);
            File.WriteAllText(SavePath, json);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[Game] SaveGame failed: {e.Message}");
        }
    }

}
