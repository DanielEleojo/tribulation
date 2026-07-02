// SplashHold.cs — holds the branded splash for a fixed total time from app launch.
//
// iOS dismisses the native launch screen the moment the engine renders its first
// frame, so engine boot time alone (~1s) decides how long the brand shows. This
// overlay re-displays the same artwork from frame one, aspect-filled exactly like
// the launch storyboard (scaleAspectFill), holds until HOLD_SECONDS of real time
// since launch, then fades out — reads as one seamless splash, precisely timed.
//
// Self-bootstrapping (RuntimeInitializeOnLoadMethod) — no scene or Bootstrap wiring.
// Uses Assets/Resources/Splash/MySplash.png (same pixels as the launch image, so
// the native→engine handoff is invisible).

using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class SplashHold : MonoBehaviour
{
    const float HOLD_SECONDS = 2.0f;   // total splash time measured from app launch
    const float FADE_SECONDS = 0.35f;  // fade into the main menu at the end
    const string SPRITE_PATH = "Splash/MySplash";

    // The art's edge navy — backs any sliver the aspect-fill crop could leave.
    static readonly Color C_EDGE = new Color(10f / 255f, 37f / 255f, 70f / 255f, 1f);

    CanvasGroup _group;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        var go = new GameObject("SplashHold");
        DontDestroyOnLoad(go);
        go.AddComponent<SplashHold>();
    }

    void Awake()
    {
        var sprite = Resources.Load<Sprite>(SPRITE_PATH);
        if (sprite == null)
        {
            Debug.LogWarning("[SplashHold] Resources/" + SPRITE_PATH + " missing — skipping splash hold.");
            Destroy(gameObject);
            return;
        }

        // Canvas above every menu/HUD canvas (MainMenu 20, PauseMenu 23).
        var canvasGO = new GameObject("SplashCanvas");
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        canvasGO.AddComponent<GraphicRaycaster>();
        _group = canvasGO.AddComponent<CanvasGroup>();
        _group.blocksRaycasts = true; // swallow taps until the splash is gone

        // Full-screen navy backing.
        var backGO = new GameObject("Backing", typeof(RectTransform));
        backGO.transform.SetParent(canvasGO.transform, false);
        var backRT = backGO.GetComponent<RectTransform>();
        backRT.anchorMin = Vector2.zero;
        backRT.anchorMax = Vector2.one;
        backRT.offsetMin = Vector2.zero;
        backRT.offsetMax = Vector2.zero;
        var backImg = backGO.AddComponent<Image>();
        backImg.color = C_EDGE;

        // The art, aspect-filled: EnvelopeParent scales the image up until it
        // covers the whole screen (cropping the long edge) — the same behaviour
        // as the launch storyboard's scaleAspectFill imageView.
        var artGO = new GameObject("Art", typeof(RectTransform));
        artGO.transform.SetParent(canvasGO.transform, false);
        var artRT = artGO.GetComponent<RectTransform>();
        artRT.anchorMin = new Vector2(0.5f, 0.5f);
        artRT.anchorMax = new Vector2(0.5f, 0.5f);
        artRT.pivot     = new Vector2(0.5f, 0.5f);
        var artImg = artGO.AddComponent<Image>();
        artImg.sprite        = sprite;
        artImg.raycastTarget = false;
        var fitter = artGO.AddComponent<AspectRatioFitter>();
        fitter.aspectMode  = AspectRatioFitter.AspectMode.EnvelopeParent;
        fitter.aspectRatio = sprite.rect.width / sprite.rect.height;

        // Silence everything (music starts at boot) while the brand is up;
        // audio fades back in with the visual fade. AudioListener.volume is a
        // global multiplier nothing else in the project touches.
        AudioListener.volume = 0f;

        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        // Hold until HOLD_SECONDS of real time since launch (boot time counts,
        // so the native launch screen + this overlay total ~HOLD_SECONDS).
        while (Time.realtimeSinceStartup < HOLD_SECONDS)
            yield return null;

        float t = 0f;
        while (t < FADE_SECONDS)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / FADE_SECONDS);
            _group.alpha = 1f - k;
            AudioListener.volume = k;
            yield return null;
        }

        Destroy(gameObject);
    }

    // Safety: never leave the game muted if the overlay dies early for any reason.
    void OnDestroy() { AudioListener.volume = 1f; }
}
