using System;
using UnityEngine;
using PrimeTween;

// One place for panel show/hide motion so menus / pause / death-card stop popping in via raw
// SetActive. A CanvasGroup alpha fade on UNSCALED time (works while paused at timeScale 0).
// Deliberately fade-only (no scale): several of these roots hold a full-screen dim overlay,
// and scaling that would leave gaps at the screen edges. A subtle fade reads premium and is
// safe on every panel; per-card scale can be layered on later if wanted.
public static class UiAnim
{
    const float SHOW = 0.18f, HIDE = 0.14f;

    static CanvasGroup Group(GameObject go)
    {
        var cg = go.GetComponent<CanvasGroup>();
        if (cg == null) cg = go.AddComponent<CanvasGroup>();
        return cg;
    }

    // Fade a panel in. Safe to call when already shown (restarts the fade).
    public static void Show(GameObject go, float dur = SHOW)
    {
        if (go == null) return;
        var cg = Group(go);
        Tween.StopAll(cg);              // cancel any in-flight fade on THIS group only
        go.SetActive(true);
        cg.alpha = 0f;
        cg.blocksRaycasts = true;
        cg.interactable   = true;
        Tween.Alpha(cg, 1f, dur, useUnscaledTime: true);
    }

    // Fade a panel out, then SetActive(false). onDone fires after it's hidden.
    public static void Hide(GameObject go, Action onDone = null, float dur = HIDE)
    {
        if (go == null || !go.activeSelf) { onDone?.Invoke(); return; }
        var cg = Group(go);
        Tween.StopAll(cg);
        cg.blocksRaycasts = false;
        cg.interactable   = false;
        Tween.Alpha(cg, 0f, dur, useUnscaledTime: true)
             .OnComplete(() => { if (go != null) go.SetActive(false); onDone?.Invoke(); });
    }
}
