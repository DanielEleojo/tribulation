// LevelPlay (ironSource) ads hub. Owns: SDK init, privacy flags, and the ONE interstitial +
// ONE rewarded ad instance for the whole session (LevelPlay wants ads reused, not
// re-created per show). Restart flow (GameLoop/PauseMenu) routes through here so an
// interstitial can gate a restart once every INTERSTITIAL_EVERY_N_DEATHS deaths since
// the last displayed full-screen ad — rewarded revives reset that counter too, so a
// player never sits through two ads back-to-back. Rewarded ad backs the on-death
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
    const string REWARDED_AD_UNIT = "qzcjr7s3id1hxqv4";
    const int INTERSTITIAL_EVERY_N_DEATHS = 3;

    // A failed load (no fill, flaky network) retries after this many real seconds.
    // Without a retry, one failed load leaves that ad slot empty for the whole session
    // — the revive button would never appear again.
    const float LOAD_RETRY_DELAY = 6f;

    // "WATCH AD" tapped with nothing preloaded: how long a fresh on-demand load may
    // take before the revive resolves false (death card falls back to restart-only).
    const float REVIVE_LOAD_TIMEOUT = 8f;

    // ShowAd() that neither displays nor fails within this window counts as failed.
    // Without it, a ShowAd the SDK silently swallows (editor stub reports ready but
    // renders nothing; wedged mediation on device) strands _pendingRestart/_pendingRevive
    // forever — the death card hangs with a dead button.
    const float SHOW_WATCHDOG = 6f;

    // LevelPlay documents OnAdRewarded can arrive slightly AFTER OnAdClosed. If close
    // fires with no reward yet, hold this long (real seconds — unscaled, ad panel isn't
    // paused by Time.timeScale anyway) for a late reward before giving up.
    const float LATE_REWARD_WINDOW = 1.5f;

    LevelPlayInterstitialAd _interstitial;
    LevelPlayRewardedAd _rewarded;
    Coroutine _interstitialRetryCo, _rewardedRetryCo; // pending load-retry (one per slot, never stacked)
    Coroutine _interstitialWatchdogCo, _rewardedWatchdogCo; // armed on ShowAd, disarmed on display/close/fail

    // StatDeaths at the moment the last full-screen ad (interstitial OR rewarded) was
    // displayed. The interstitial gate fires only INTERSTITIAL_EVERY_N_DEATHS deaths
    // past this — NOT lifetime-deaths % N, which ignored rewarded ads and could chain
    // an interstitial right after a revive ad. -1 until the session's first gate check
    // captures a baseline (StatDeaths is a persisted lifetime stat, loaded from save).
    int _statDeathsAtLastAd = -1;

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
        _interstitial.OnAdLoadFailed    += OnInterstitialLoadFailed;
        _interstitial.OnAdDisplayed     += OnInterstitialDisplayed;
        _interstitial.OnAdClosed        += OnInterstitialClosed;
        _interstitial.OnAdDisplayFailed += OnInterstitialDisplayFailed;
        _interstitial.LoadAd();

        _rewarded = new LevelPlayRewardedAd(REWARDED_AD_UNIT);
        _rewarded.OnAdLoadFailed    += OnRewardedLoadFailed;
        _rewarded.OnAdDisplayed     += OnRewardedDisplayed;
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

    // ── Shared show/hide + load-retry plumbing ───────────────────────────────

    // Full-screen ads own the device audio; game music/SFX bleeding through underneath
    // is a store-review flag. AudioListener.pause halts every AudioSource in place and
    // the music loop resumes exactly where it stopped on unpause. Displaying also proves
    // the ShowAd wasn't swallowed — disarm that ad's show-watchdog.
    void OnInterstitialDisplayed(LevelPlayAdInfo info)
    {
        CancelWatchdog(ref _interstitialWatchdogCo);
        AudioListener.pause = true;
        MarkAdShown();
    }

    void OnRewardedDisplayed(LevelPlayAdInfo info)
    {
        CancelWatchdog(ref _rewardedWatchdogCo);
        AudioListener.pause = true;
        MarkAdShown();
    }

    // Any full-screen ad the player actually saw restarts the deaths-until-interstitial
    // countdown. Failed shows deliberately don't — the player saw nothing.
    void MarkAdShown()
    {
        var core = Game.I?.Core;
        if (core != null) _statDeathsAtLastAd = core.StatDeaths;
    }

    void CancelWatchdog(ref Coroutine co)
    {
        if (co == null) return;
        StopCoroutine(co);
        co = null;
    }

    IEnumerator InterstitialShowWatchdog()
    {
        yield return new WaitForSecondsRealtime(SHOW_WATCHDOG);
        _interstitialWatchdogCo = null;
        Debug.LogWarning("[AdsManager] Interstitial never displayed after ShowAd — restarting without ad.");
        _interstitial?.LoadAd();
        FirePendingRestart();
    }

    IEnumerator RewardedShowWatchdog()
    {
        yield return new WaitForSecondsRealtime(SHOW_WATCHDOG);
        _rewardedWatchdogCo = null;
        Debug.LogWarning("[AdsManager] Rewarded never displayed after ShowAd — resolving revive as declined.");
        _rewarded?.LoadAd();
        ResolveRevive(false);
    }

    void OnInterstitialLoadFailed(LevelPlayAdError error)
    {
        Debug.LogWarning($"[AdsManager] Interstitial load failed: {error}");
        if (_interstitialRetryCo == null)
            _interstitialRetryCo = StartCoroutine(RetryInterstitialLoad());
    }

    void OnRewardedLoadFailed(LevelPlayAdError error)
    {
        Debug.LogWarning($"[AdsManager] Rewarded load failed: {error}");
        if (_rewardedRetryCo == null)
            _rewardedRetryCo = StartCoroutine(RetryRewardedLoad());
    }

    IEnumerator RetryInterstitialLoad()
    {
        yield return new WaitForSecondsRealtime(LOAD_RETRY_DELAY);
        _interstitialRetryCo = null;
        _interstitial?.LoadAd();
    }

    IEnumerator RetryRewardedLoad()
    {
        yield return new WaitForSecondsRealtime(LOAD_RETRY_DELAY);
        _rewardedRetryCo = null;
        _rewarded?.LoadAd();
    }

    // ── Interstitial (restart-gate) handlers ─────────────────────────────────

    void OnInterstitialClosed(LevelPlayAdInfo info)
    {
        CancelWatchdog(ref _interstitialWatchdogCo);
        AudioListener.pause = false;
        _interstitial.LoadAd(); // reload-after-show lifecycle
        FirePendingRestart();
    }

    void OnInterstitialDisplayFailed(LevelPlayAdInfo info, LevelPlayAdError error)
    {
        CancelWatchdog(ref _interstitialWatchdogCo);
        AudioListener.pause = false;
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
        CancelWatchdog(ref _rewardedWatchdogCo);
        AudioListener.pause = false;
        _rewarded.LoadAd(); // reload-after-show lifecycle
        if (_rewardEarned)
            ResolveRevive(true);
        else
            _lateRewardCo = StartCoroutine(HoldForLateReward());
    }

    void OnRewardedDisplayFailed(LevelPlayAdInfo info, LevelPlayAdError error)
    {
        CancelWatchdog(ref _rewardedWatchdogCo);
        AudioListener.pause = false;
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
    /// after the ad resolves (true only if a reward was actually earned — closing early
    /// without reward is a decline). If nothing is preloaded, a fresh load is kicked off
    /// and the ad shows as soon as it lands, giving up (false) after REVIVE_LOAD_TIMEOUT
    /// so the death card never hangs. Immediate false when the SDK never initialized.
    /// </summary>
    public void ShowRewardedRevive(Action<bool> onComplete)
    {
        if (_rewarded == null) { onComplete?.Invoke(false); return; } // init failed / editor
        _pendingRevive = onComplete;
        _rewardEarned = false;
        if (_rewarded.IsAdReady())
        {
            _rewarded.ShowAd();
            _rewardedWatchdogCo = StartCoroutine(RewardedShowWatchdog());
        }
        else
            StartCoroutine(LoadThenShowRevive());
    }

    // Tap-time fallback when preloading hasn't produced an ad yet: load on demand and
    // show the moment it's ready. The retry loop is parked meanwhile so it can't issue
    // a competing LoadAd.
    IEnumerator LoadThenShowRevive()
    {
        if (_rewardedRetryCo != null) { StopCoroutine(_rewardedRetryCo); _rewardedRetryCo = null; }
        _rewarded.LoadAd();
        float t = 0f;
        while (t < REVIVE_LOAD_TIMEOUT)
        {
            if (_pendingRevive == null) yield break; // resolved elsewhere (shouldn't happen, but never double-fire)
            if (_rewarded.IsAdReady())
            {
                _rewarded.ShowAd();
                _rewardedWatchdogCo = StartCoroutine(RewardedShowWatchdog());
                yield break;
            }
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        ResolveRevive(false); // timed out — death card falls back to restart-only
    }

    /// <summary>Restart gate for GameLoop/PauseMenu: shows an interstitial once
    /// INTERSTITIAL_EVERY_N_DEATHS deaths have passed since the last displayed
    /// full-screen ad (rewarded revives reset the countdown too), otherwise restarts
    /// immediately. The session's first gate check only captures the baseline — the
    /// first interstitial always comes N deaths into the session, never on death one.
    /// INVARIANT: onReadyToRestart fires exactly once on every path (ad shown, no fill,
    /// init failed, editor where ads never load, show swallowed → watchdog) — a restart
    /// can never hang.</summary>
    public void RestartWithInterstitial(Action onReadyToRestart)
    {
        bool due = false;
        var core = Game.I?.Core;
        if (core != null)
        {
            if (_statDeathsAtLastAd < 0) _statDeathsAtLastAd = core.StatDeaths;
            due = core.StatDeaths - _statDeathsAtLastAd >= INTERSTITIAL_EVERY_N_DEATHS;
        }

        if (due && _interstitial != null && _interstitial.IsAdReady())
        {
            _pendingRestart = onReadyToRestart;
            _interstitial.ShowAd();
            _interstitialWatchdogCo = StartCoroutine(InterstitialShowWatchdog());
        }
        else
        {
            onReadyToRestart?.Invoke();
        }
    }
}
