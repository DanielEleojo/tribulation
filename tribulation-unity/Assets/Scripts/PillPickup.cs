using UnityEngine;

// Powerup pill/talisman pickup — calls Game.I.ActivatePowerup(id) on contact.
// Ported from game.gd activate_powerup + pill spawner.
// id is set by Spawner at spawn time from the powerup table.
// ponytail: pill glow colour per type, flash HUD banner — deferred to visual polish
public class PillPickup : MonoBehaviour
{
    [HideInInspector] public string PowerupId;

    void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<PlayerRunner>() == null) return;
        if (Game.I != null) Game.I.ActivatePowerup(PowerupId);
        gameObject.SetActive(false);
    }
}
