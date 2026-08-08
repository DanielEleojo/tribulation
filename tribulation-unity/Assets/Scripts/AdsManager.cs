// LevelPlay (ironSource) ads hub. Owns: SDK init, privacy flags, and the ONE interstitial +
// ONE rewarded ad instance for the whole session (LevelPlay wants ads reused, not
// re-created per show). Restart flow (GameLoop/PauseMenu) routes through here so an
// interstitial can gate a restart every Nth death. Rewarded ad backs the on-death
// "revive" flow (HudOverlay's death-card button).
using System;
using System.Collections;
using UnityEngine;
using Unity.Services.LevelPlay;

public class AdsManager : MonoBehaviour
{
    public static AdsManager I { get; private set; }

    // Vallicade Games account (Tribulation Runner, LevelPlay app 2786c2105).
    const string APP_KEY = "2786c2105";
    const string INTERSTITIAL_AD_UNIT = "pf2ysm15akmib3xg";
    const string REWARDED_AD_UNIT = "qzcjr7s3id1hxqv";
    const int INTERSTITIAL_EVERY_N_DEATHS = 3;

    // LevelPlay documents OnAdRewarded can arrive slightly AFTER OnAdClosed. If close
    // fires with no reward yet, hold this long (real seconds — unscaled, ad panel isn't
    // paused by Time.timeScale anyway) for a late reward before giving up.
    const float LATE_REWARD_WINDOW = 1.5f;

    LevelPlayInterstitialAd _interstitial;
    LevelPlayRewardedAd _rewarded;

    // Pending restart callback stashed while an interstitial is on screen. Guarded so
    // OnAdClosed/OnAdDisplayFailed can never both fire it (double-invoke would double-restart).
    Action _pendingRestart;

    // Pending revive completion stashed while a rewarded ad is on screen. Same
    // clear-before-invoke guard as _pendingRestart — fires exactly once with the final answer.
    Action<bool> _pendingRevive;
    bool _rewardEarned;      // set by OnRewardedRewarded; read when resolving the completion
    Coroutine _lateRewardCo; // holds resolution open briefly after a no-reward close

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

        _rewarded = new LevelPlayRewardedAd(REWARDED_AD_UNIT);
        _rewarded.OnAdRewarded      += OnRewardedRewarded;
        _rewarded.OnAdClosed        += OnRewardedClosed;
        _rewarded.OnAdDisplayFailed += OnRewardedDisplayFailed;
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

    // ── Rewarded (revive) handlers ───────────────────────────────────────────
    void OnRewardedRewarded(LevelPlayAdInfo info, LevelPlayReward reward)
    {
        _rewardEarned = true;
        // If OnAdClosed already fired with no reward yet, it started _lateRewardCo, which
        // polls _rewardEarned every frame — that coroutine picks this up and resolves true.
        // Nothing else to do here; ResolveRevive is never called directly from this handler
        // so a reward that arrives BEFORE close still waits for close to actually resolve.
    }

    void OnRewardedClosed(LevelPlayAdInfo info)
    {
        _rewarded.LoadAd(); // reload-after-show lifecycle
        if (_rewardEarned)
            ResolveRevive(true);
        else
            _lateRewardCo = StartCoroutine(HoldForLateReward());
    }

    void OnRewardedDisplayFailed(LevelPlayAdInfo info, LevelPlayAdError error)
    {
        _rewarded.LoadAd();
        ResolveRevive(false); // never leave the death card stuck waiting on a failed show
    }

    // Bridges the OnAdRewarded-after-OnAdClosed race: keep the completion open a short
    // while past close so a reward landing just after still counts.
    IEnumerator HoldForLateReward()
    {
        float t = 0f;
        while (t < LATE_REWARD_WINDOW)
        {
            if (_rewardEarned) { ResolveRevive(true); yield break; }
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        ResolveRevive(false);
    }

    void ResolveRevive(bool success)
    {
        if (_lateRewardCo != null) { StopCoroutine(_lateRewardCo); _lateRewardCo = null; }
        var cb = _pendingRevive;
        _pendingRevive = null; // clear first — same exactly-once guard as _pendingRestart
        cb?.Invoke(success);
    }

    /// <summary>True when a rewarded ad has finished preloading and can be shown now.</summary>
    public bool IsRewardedReady() => _rewarded != null && _rewarded.IsAdReady();

    /// <summary>
    /// Show the rewarded ad for a death-card revive. onComplete fires exactly once:
    /// immediately with false if no ad is ready, otherwise after the ad resolves
    /// (true only if a reward was actually earned — closing early without reward is a decline).
    /// </summary>
    public void ShowRewardedRevive(Action<bool> onComplete)
    {
        if (!IsRewardedReady()) { onComplete?.Invoke(false); return; }
        _pendingRevive = onComplete;
        _rewardEarned = false;
        _rewarded.ShowAd();
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
