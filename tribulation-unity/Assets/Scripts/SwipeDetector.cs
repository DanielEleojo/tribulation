using System;
using UnityEngine;

// Port of swipe_detector.gd. Touch gestures -> high-level events.
// Dominant axis decides the gesture; negligible travel = tap.
// Keyboard stays the primary path for desktop testing (handled in PlayerRunner).
// Note: Unity touch Y is bottom-origin (up = +y), opposite Godot's top-origin — so
// swipe-up is delta.y > 0 here (it was < 0 in the Godot version).
public class SwipeDetector : MonoBehaviour
{
    public static SwipeDetector I;

    public float swipeThreshold = 60f; // min travel (px) to count as a swipe

    public event Action SwipedUp, SwipedDown, SwipedLeft, SwipedRight, Tapped;

    Vector2 _start;
    bool _touching;
    bool _swiped;                        // a directional swipe already fired this touch
    public bool IsHolding => _touching;  // used for touch-hold glide

    void Awake() { I = this; }

    void Update()
    {
        if (Input.touchCount == 0) return;
        Touch t = Input.GetTouch(0);

        if (t.phase == TouchPhase.Began) { _start = t.position; _touching = true; _swiped = false; }
        else if (t.phase == TouchPhase.Moved || t.phase == TouchPhase.Stationary)
        {
            // Fire the swipe the MOMENT travel crosses the threshold — while the finger is
            // still down. Waiting for release (the old behaviour) added ~100-200ms of lag.
            if (_touching && !_swiped) TryFireSwipe(t.position - _start);
        }
        else if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
        {
            // A tap = short travel + release with no directional swipe fired.
            if (_touching && !_swiped)
            {
                Vector2 d = t.position - _start;
                if (Mathf.Abs(d.x) < swipeThreshold && Mathf.Abs(d.y) < swipeThreshold) Tapped?.Invoke();
            }
            _touching = false;
        }
    }

    // Fire a directional swipe once travel on the dominant axis exceeds the threshold.
    void TryFireSwipe(Vector2 d)
    {
        if (Mathf.Abs(d.x) < swipeThreshold && Mathf.Abs(d.y) < swipeThreshold) return;
        _swiped = true;
        if (Mathf.Abs(d.x) > Mathf.Abs(d.y)) (d.x > 0 ? SwipedRight : SwipedLeft)?.Invoke();
        else (d.y > 0 ? SwipedUp : SwipedDown)?.Invoke();
    }
}
