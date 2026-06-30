// Plain serializable save struct — no UnityEngine.
// The MonoBehaviour wrapper serializes this to/from JSON via JsonUtility + persistentDataPath.
namespace Tribulation.Core
{
    [System.Serializable]
    public class SaveData
    {
        // Cultivation (lifetime progress, drives realm gate)
        public int   realm        = 0;
        public int   totalStones  = 0;   // lifetime Spirit Stones earned (spendable currency)
        public int   spent        = 0;   // stones spent on upgrades

        // Run meta
        public int   bestLi       = 0;   // best distance ("li") ever

        // Lifetime stats
        public int   statRuns     = 0;
        public int   statFoes     = 0;
        public int   statTribs    = 0;
        public int   statDeaths   = 0;

        // Audio settings
        public float musicVol     = 0.8f;
        public float sfxVol       = 0.9f;
        public bool  muted        = false;

        // Technique first-encounter persistence
        public System.Collections.Generic.List<string> seenTechniques = new System.Collections.Generic.List<string>();

        // Coach-mark tutorial: lessons the player has permanently learned
        public System.Collections.Generic.List<string> learnedLessons = new System.Collections.Generic.List<string>();

        // Upgrade levels (index-parallel to GameCore.Upgrades list)
        public System.Collections.Generic.List<int> upgradeLevels = new System.Collections.Generic.List<int>();
    }
}
