using UnityEngine;

// Qi orb pickup — collected on trigger enter; calls Game.I.OnOrbCollected().
// Ported from game.gd _spawn_orb_trail / on_orb_collected.
// Returned to pool by Spawner.Cull() via the _liveOrbs list.
// ponytail: orb glow / particle trail VFX — deferred to visual polish
public class OrbPickup : MonoBehaviour
{
    void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<PlayerRunner>() == null) return;
        if (Game.I != null) Game.I.OnOrbCollected();
        Feel.CollectPop(transform.position);
        // Disable self; Spawner.Cull() will return us to the pool via _liveOrbs.
        gameObject.SetActive(false);
    }
}
