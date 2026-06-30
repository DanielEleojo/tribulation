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
    public bool IsHolding => _touching; // used for touch-hold glide later

    void Awake() { I = this; }

    void Update()
    {
        if (Input.touchCount == 0) return;
        Touch t = Input.GetTouch(0);
        if (t.phase == TouchPhase.Began) { _start = t.position; _touching = true; }
        else if (_touching && (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled))
        {
            _touching = false;
            Resolve(t.position - _start);
        }
    }

    void Resolve(Vector2 d)
    {
        if (Mathf.Abs(d.x) < swipeThreshold && Mathf.Abs(d.y) < swipeThreshold) { Tapped?.Invoke(); return; }
        if (Mathf.Abs(d.x) > Mathf.Abs(d.y)) (d.x > 0 ? SwipedRight : SwipedLeft)?.Invoke();
        else (d.y > 0 ? SwipedUp : SwipedDown)?.Invoke();
    }
}
