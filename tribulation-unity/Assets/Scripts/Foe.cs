using UnityEngine;

// Marker component: enemies that can be destroyed by a slash.
// Applied by Spawner to Enemy-kind hazards at spawn time.
// At realm<2 (no slash ability) they remain pure avoid-hazards — the slash gate
// in PlayerRunner.TrySlash() ensures the marker is never acted on before realm 2.
// The Hazard component is kept so contact still kills the player.
// ponytail: enemy animation (bob/sway), GLB mesh — deferred to later visual polish
public class Foe : MonoBehaviour { }
