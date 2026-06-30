// Pure-C# telegraph data model. No UnityEngine dependency.
// Every obstacle is a telegraphed martial attack — plane → color + input is a fixed mapping.

using System;
using System.Collections.Generic;

namespace Tribulation.Core
{
    public enum AttackPlane { Low, High, Lane, Destructible }
    public enum DodgeInput  { Jump, Slide, Lane, Slash }
    public enum TelegraphColor { Amber, Cyan, Red, White }

    public struct TelegraphInfo
    {
        public AttackPlane   Plane;
        public DodgeInput    Input;
        public TelegraphColor Color;
        public string        TechniqueId;
        public string        DisplayName;
    }

    public class Telegraph
    {
        // note: static plane maps — single source of truth used by Resolve
        public static TelegraphColor PlaneColor(AttackPlane plane) => plane switch
        {
            AttackPlane.Low          => TelegraphColor.Amber,
            AttackPlane.High         => TelegraphColor.Cyan,
            AttackPlane.Lane         => TelegraphColor.Red,
            AttackPlane.Destructible => TelegraphColor.White,
            _ => throw new ArgumentOutOfRangeException(nameof(plane), plane, null),
        };

        public static DodgeInput PlaneInput(AttackPlane plane) => plane switch
        {
            AttackPlane.Low          => DodgeInput.Jump,
            AttackPlane.High         => DodgeInput.Slide,
            AttackPlane.Lane         => DodgeInput.Lane,
            AttackPlane.Destructible => DodgeInput.Slash,
            _ => throw new ArgumentOutOfRangeException(nameof(plane), plane, null),
        };

        // note: static Resolve — no instance state needed for catalog lookup
        public static TelegraphInfo Resolve(HazardKind kind)
        {
            return kind switch
            {
                HazardKind.Block => new TelegraphInfo
                {
                    Plane       = AttackPlane.Low,
                    Input       = DodgeInput.Jump,
                    Color       = TelegraphColor.Amber,
                    TechniqueId = "earth_splitting_sweep",
                    DisplayName = "Earth-Splitting Sweep",
                },
                HazardKind.Bar => new TelegraphInfo
                {
                    Plane       = AttackPlane.High,
                    Input       = DodgeInput.Slide,
                    Color       = TelegraphColor.Cyan,
                    TechniqueId = "heaven_cleaving_slash",
                    DisplayName = "Heaven-Cleaving Slash",
                },
                HazardKind.Enemy => new TelegraphInfo
                {
                    Plane       = AttackPlane.Destructible,
                    Input       = DodgeInput.Slash,
                    Color       = TelegraphColor.White,
                    TechniqueId = "blocking_disciple",
                    DisplayName = "Blocking Disciple",
                },
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
            };
        }

        // ── First-encounter announce tracking ──────────────────────────────────
        readonly HashSet<string> _seen = new HashSet<string>();

        public bool ShouldAnnounce(string techniqueId)
        {
            if (_seen.Contains(techniqueId)) return false;
            _seen.Add(techniqueId);
            return true;
        }

        public IReadOnlyCollection<string> SeenTechniques => _seen;

        public void LoadSeen(IEnumerable<string> ids)
        {
            foreach (var id in ids) _seen.Add(id);
        }
    }
}
