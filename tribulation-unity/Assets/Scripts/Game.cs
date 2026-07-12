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
            Haptics.Heavy();
        };
        Core.TribulationStarted += () => Haptics.Heavy();
        Core.NewBest          += OnNewBest;
        Core.Breakthrough     += () => { if (SoundManager.I != null) SoundManager.I.Play("breakthrough"); Haptics.Success(); };
        Core.TrialFulfilled   += r => { if (SoundManager.I != null) SoundManager.I.Play("breakthrough"); };
        // ponytail: trial banner + HUD trial list — Batch 2 (HudOverlay)
        // ponytail: QiChanged / NetChanged / SoulsChanged / ComboChanged → HUD deferred (later issue)

        // Telemetry: wire achievement + daily events (local JSONL only — no data leaves device)
        Core.AchievementUnlocked += id => Telemetry.Event("achievement", $"\"id\":\"{id}\"");
        Core.DailyClaimed        += (s, r) => Telemetry.Event("daily", $"\"streak\":{s},\"reward\":{r}");

        // Load saved state; run begins when the player taps "Begin Cultivation" (MainMenu).
        LoadSave();

        Haptics.Prepare(); // warm the Taptic Engine once at startup

        // ponytail: apply audio settings to buses — deferred
    }

    void Update()
    {
        if (Core == null) return;
        Core.Tick(Time.deltaTime);
        if (_player != null)
        {
            Core.TrialMax("li", _player.GetDistance());
            Core.ReportDistance(_player.GetDistance()); // live BestLi + NewBest fire
        }

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
        Haptics.Error();
        var cam = FindObjectOfType<CameraFollow>(); if (cam != null) cam.AddTrauma(0.8f);
        Debug.Log($"[Game] Died. Realm={Core.Realm}  Total={Core.TotalStones}  Best={Core.BestLi}");
        // Notify GameLoop so tap-to-restart still works (single death route through Game).
        if (GameLoop.I != null) GameLoop.I.OnPlayerDied();
    }

    // Fired by Core.ReportDistance the frame this run passes the old record —
    // celebrate the moment it happens, not on the death screen.
    void OnNewBest()
    {
        HudOverlay.I?.ShowNewBest();
        var cam = FindObjectOfType<CameraFollow>();
        if (cam != null) { cam.AddFovKick(5f); cam.AddTrauma(0.2f); }
        Haptics.Success();
        if (SoundManager.I != null) SoundManager.I.Play("new_best"); // clip registered in a later workstream; Play null-guards missing
    }

    // Called by Spawner's near-miss scan: an Enemy hazard slipped past by a whisker.
    public void OnNearMiss()
    {
        Core?.OnNearMiss();
        HudOverlay.I?.ShowNearMiss();
        Haptics.Light();
        if (SoundManager.I != null) SoundManager.I.Play("near_miss"); // registered later; Play null-guards
        var cam = FindObjectOfType<CameraFollow>();
        if (cam != null) cam.AddFovKick(2f);
    }

    // ── Public API (called by Hazard, Spawner, Gate, etc.) ──────────────────
    public void OnPlayerHit()   { Core?.OnPlayerHit(_player != null ? _player.GetDistance() : 0); }
    public void OnContactHit(bool isEnemy) { Core?.OnContactHit(isEnemy); }
    public void OnEnemyKilled(int count = 1) { Core?.OnEnemyKilled(count); }
    public void OnOrbCollected()
    {
        Core?.OnOrbCollected();
        if (SoundManager.I != null) SoundManager.I.Play("orb");
        Haptics.Light();
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

    /// <summary>Full restart sequence shared by GameLoop's tap-to-restart and PauseMenu's
    /// Restart/Quit-to-Menu buttons: hide the death card, reset run-state core, player, and spawner.</summary>
    public void PerformRestart()
    {
        HudOverlay.I?.HideDeathCard();
        Core?.RestartRun();
        _player?.ResetRun();
        _spawner?.ClearAll();
    }
    /// <summary>Full revive sequence for HudOverlay's death-card "RISE AGAIN" button, called
    /// only after the rewarded ad actually paid out: undoes death without resetting the run
    /// (RunProgress restored, Net relieved — see GameCore.Revive), clears the field of live
    /// hazards so revive doesn't drop you back into whatever killed you, and tells GameLoop
    /// the death is over so a stray tap-to-restart can't fire on the freshly-revived run.</summary>
    public void PerformRevive()
    {
        if (Core == null || !Core.IsDead) return;
        Core.Revive();
        _player?.ReviveInPlace();
        _spawner?.ClearAll();
        HudOverlay.I?.HideDeathCard();
        GameLoop.I?.OnPlayerRevived();
        Haptics.Success();
        SoundManager.I?.Play("breakthrough"); // no dedicated revive sfx yet
    }

    /// <summary>Called by MenuScreens after a shop purchase or settings change to persist immediately.</summary>
    public void SaveProgress() => SaveGame();

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
        if (safe) Haptics.Light(); else Haptics.Warning();
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
