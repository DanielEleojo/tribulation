using UnityEngine;
using PrimeTween;

// Qi orb pickup — collected on trigger enter; calls Game.I.OnOrbCollected().
// Ported from game.gd _spawn_orb_trail / on_orb_collected.
// Returned to pool by Spawner.Cull() via the _liveOrbs list.
public class OrbPickup : MonoBehaviour
{
    // Tracks whether a pop is already in-flight for this orb instance.
    // Pool-safe: reset to false in OnEnable so re-acquired orbs start clean.
    bool _popping;
    Vector3 _restScale = Vector3.one;
    Tween _popTween;
    OrbVisual _visual;

    void Awake() { _visual = GetComponent<OrbVisual>(); }

    void OnEnable() { _popping = false; if (_visual != null) _visual.enabled = true; }

    // Pool-return safety: if a pop was still mid-flight when the orb is deactivated
    // (e.g. culled from behind during the 0.12s window), kill the tween so its
    // OnComplete can't fire on a LATER re-acquired orb, restore the rest scale so
    // OrbVisual.Init() reads the correct base, and re-enable the idle motion.
    void OnDisable()
    {
        if (_popTween.isAlive) _popTween.Stop();
        transform.localScale = _restScale;
        if (_visual != null) _visual.enabled = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (_popping) return;                                  // already mid-pop
        if (other.GetComponent<PlayerRunner>() == null) return;

        _popping = true;

        // Fire game logic + world VFX immediately (before the pop delay).
        if (Game.I != null) Game.I.OnOrbCollected();
        Feel.CollectPop(transform.position);

        // Stop the idle bob/pulse — OrbVisual.Update() writes localScale every frame and
        // would otherwise overwrite (cancel) the pop tween below. Re-enabled in OnEnable.
        if (_visual != null) _visual.enabled = false;

        // Brief satisfying pop: scale up to 2× then deactivate via OnComplete.
        _restScale = transform.localScale;
        _popTween = Tween.Scale(transform, _restScale * 2f, 0.12f, Ease.OutQuad)
             .OnComplete(() =>
             {
                 transform.localScale = _restScale;   // restore before pool-return
                 gameObject.SetActive(false);
             });
    }
}
