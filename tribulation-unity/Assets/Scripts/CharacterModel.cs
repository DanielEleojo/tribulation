using UnityEngine;

// Loads the warrior glTF model (imported by glTFast editor importer as a normal GameObject
// asset in Resources) and swaps it in place of the placeholder capsule visual.
//
// Attach to the player root. It hides the capsule child ("Visual") and instantiates the
// warrior mesh as a sibling child. The CharacterController/Collider/PlayerRunner are
// untouched — only the visual changes.
//
// NO direct glTFast API references — Resources.Load<GameObject> receives the asset that
// glTFast's editor importer baked into the project; at runtime it is plain Unity.
// This means the script compiles and runs even while glTFast is still resolving.
public class CharacterModel : MonoBehaviour
{
    [Tooltip("Resources-relative path to the imported .glb asset (no extension).")]
    public string resourcePath = "Models/warrior";

    [Tooltip("Local rotation applied to the model so it faces -Z (run direction).")]
    public Vector3 modelEuler = new Vector3(0f, 180f, 0f);

    [Tooltip("Local position offset. Model is a child of the player root; a feet-pivot glb sits at ~0. Nudge in the Inspector if her feet aren't on the ground.")]
    public Vector3 modelOffset = new Vector3(0f, 0f, 0f);

    [Tooltip("Uniform scale applied to the instantiated model.")]
    public float modelScale = 1f;

    [Tooltip("Name of the legacy Animation clip (or Animator state) to play on load. " +
             "Silently ignored if the clip/state is absent — bind pose is acceptable.")]
    public string defaultClip = "Running";

    void Start()
    {
        try
        {
            // ── 1. Load the prefab from Resources ────────────────────────────────
            GameObject prefab = Resources.Load<GameObject>(resourcePath);

            if (prefab == null)
            {
                // Fallback: scan all GameObjects in Resources and take the first hit.
                var all = Resources.LoadAll<GameObject>(System.IO.Path.GetDirectoryName(resourcePath)
                                                        ?? "Models");
                if (all != null && all.Length > 0) prefab = all[0];
            }

            if (prefab == null)
            {
                Debug.LogWarning("[CharacterModel] could not load \"" + resourcePath +
                                 "\" — is glTFast imported and the .glb in Assets/Resources/Models/? " +
                                 "Keeping placeholder capsule.");
                return; // graceful fallback: leave capsule visible
            }

            // ── 2. Hide the placeholder capsule visual (child named "Visual") ────
            // The capsule is a child of this player root, NOT on the root itself.
            var visChild = transform.Find("Visual");
            if (visChild != null)
            {
                var mr = visChild.GetComponent<MeshRenderer>();
                if (mr != null) mr.enabled = false;
            }
            else
            {
                // Also check for a MeshRenderer directly on this root, just in case.
                var mr = GetComponent<MeshRenderer>();
                if (mr != null) mr.enabled = false;
            }

            // ── 3. Instantiate model as a child ──────────────────────────────────
            var go = Instantiate(prefab, transform);
            go.name = "WarriorModel";
            go.transform.localPosition = modelOffset;
            go.transform.localEulerAngles = modelEuler;
            go.transform.localScale = Vector3.one * modelScale;

            // ── 4. Best-effort animation (no state machine) ───────────────────────
            // ponytail: full Animator state machine (run/jump/slide/slash/death
            //           synced to PlayerRunner) = next slice.
            try
            {
                // Legacy Animation component (common in glTFast imports when clips exist).
                var legacyAnim = go.GetComponentInChildren<Animation>();
                if (legacyAnim != null)
                {
                    legacyAnim.Play(defaultClip);
                    // Play() silently fails if the clip name isn't found — that's fine.
                }
                else
                {
                    // Mecanim Animator: leave it in bind pose if no controller is set.
                    // A future slice will wire up the Animator state machine.
                    var animator = go.GetComponentInChildren<Animator>();
                    if (animator != null && animator.runtimeAnimatorController == null)
                    {
                        // Bind pose — acceptable for this slice.
                        Debug.Log("[CharacterModel] Warrior loaded in bind pose — " +
                                  "Animator state machine wired in next slice.");
                    }
                }
            }
            catch (System.Exception animEx)
            {
                // Animation failure must never break gameplay.
                Debug.LogWarning("[CharacterModel] Animation init skipped: " + animEx.Message);
            }
        }
        catch (System.Exception ex)
        {
            // Any import/instantiation hiccup must never break the player.
            Debug.LogError("[CharacterModel] Unexpected error loading warrior model — " +
                           "keeping placeholder capsule. Exception: " + ex);
        }
    }
}
