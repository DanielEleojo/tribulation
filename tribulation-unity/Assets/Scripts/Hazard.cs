using UnityEngine;

// Marker for anything that kills the player on contact. Detected by component (not tag)
// so Bootstrap needs no TagManager setup. PlayerRunner.OnTriggerEnter checks for this.
public class Hazard : MonoBehaviour
{
    // Near-miss bookkeeping (Spawner near-miss scan): latched true once this hazard has
    // been evaluated after passing the player, so the reward fires at most once per life.
    [System.NonSerialized] public bool NearChecked;

    // Pool-safe re-arm: Spawner.Acquire calls SetActive(true) on every reuse, so a
    // recycled hazard can be near-checked again on its next life.
    void OnEnable() { NearChecked = false; }
}
