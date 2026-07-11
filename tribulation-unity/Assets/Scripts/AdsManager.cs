// LevelPlay (ironSource) ads hub. Owns: SDK init, privacy flags, and the ONE interstitial +
// ONE rewarded ad instance for the whole session (LevelPlay wants ads reused, not
// re-created per show). Restart flow (GameLoop/PauseMenu) routes through here so an
// interstitial can gate a restart every Nth death.
// Rewarded ad is created/preloaded here too (single place ad objects are born) but its
// show-flow (revive) is a later task — for now it just loads and reloads on close.
using System;
using UnityEngine;
using Unity.Services.LevelPlay;

public class AdsManager : MonoBehaviour
{
    public static AdsManager I { get; private set; }

    const string APP_KEY = "272304f9d";
    const string INTERSTITIAL_AD_UNIT = "2mq1hpkx1t9rkfhm";
    const string REWARDED_AD_UNIT = "exw6u2gd18gicljj"; // used by the revive flow (next task)
    const int INTERSTITIAL_EVERY_N_DEATHS = 3;

    LevelPlayInterstitialAd _interstitial;
    LevelPlayRewardedAd _rewarded;

    // Pending restart callback stashed while an interstitial is on screen. Guarded so
    // OnAdClosed/OnAdDisplayFailed can never both fire it (double-invoke would double-restart).
    Action _pendingRestart;

    void Awake()
    {
        I = this;
    }

    void Start()
    {
        // Privacy must be set BEFORE Init — non-personalized ads only, no ATT prompt.
        LevelPlayPrivacySettings.SetGDPRConsent(false);
        LevelPlayPrivacySettings.SetCCPA(true);

        LevelPlay.OnInitSuccess += OnInitSuccess;
        LevelPlay.OnInitFailed  += OnInitFailed;
        LevelPlay.Init(APP_KEY);
    }

    void OnInitSuccess(LevelPlayConfiguration config)
    {
        _interstitial = new LevelPlayInterstitialAd(INTERSTITIAL_AD_UNIT);
        _interstitial.OnAdClosed        += OnInterstitialClosed;
        _interstitial.OnAdDisplayFailed += OnInterstitialDisplayFailed;
        _interstitial.LoadAd();

        // Created + preloaded here so this file is the single place ad objects are born;
        // the show-flow (revive) is wired in a later task.
        _rewarded = new LevelPlayRewardedAd(REWARDED_AD_UNIT);
        _rewarded.OnAdClosed += (_) => _rewarded.LoadAd();
        _rewarded.LoadAd();
    }

    void OnInitFailed(LevelPlayInitError error)
    {
        Debug.LogWarning($"[AdsManager] Init failed: {error}");
        // No ads this session — RestartWithInterstitial falls back to instant restart
        // because _interstitial stays null / never ready.
    }

    void OnInterstitialClosed(LevelPlayAdInfo info)
    {
        _interstitial.LoadAd(); // reload-after-show lifecycle
        FirePendingRestart();
    }

    void OnInterstitialDisplayFailed(LevelPlayAdInfo info, LevelPlayAdError error)
    {
        _interstitial.LoadAd();
        FirePendingRestart(); // never leave the game stuck on a failed show
    }

    void FirePendingRestart()
    {
        var cb = _pendingRestart;
        _pendingRestart = null; // clear first — guards against double-invoke
        cb?.Invoke();
    }

    /// <summary>Restart gate for GameLoop/PauseMenu: shows an interstitial every
    /// INTERSTITIAL_EVERY_N_DEATHS deaths, otherwise restarts immediately.
    /// INVARIANT: onReadyToRestart fires exactly once on every path (ad shown, no fill,
    /// init failed, editor where ads never load) — a restart can never hang.</summary>
    public void RestartWithInterstitial(Action onReadyToRestart)
    {
        bool due = Game.I?.Core != null
            && Game.I.Core.StatDeaths > 0
            && Game.I.Core.StatDeaths % INTERSTITIAL_EVERY_N_DEATHS == 0;

        if (due && _interstitial != null && _interstitial.IsAdReady())
        {
            _pendingRestart = onReadyToRestart;
            _interstitial.ShowAd();
        }
        else
        {
            onReadyToRestart?.Invoke();
        }
    }
}
