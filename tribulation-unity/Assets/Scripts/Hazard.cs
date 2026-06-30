using UnityEngine;

// Marker for anything that kills the player on contact. Detected by component (not tag)
// so Bootstrap needs no TagManager setup. PlayerRunner.OnTriggerEnter checks for this.
public class Hazard : MonoBehaviour { }
