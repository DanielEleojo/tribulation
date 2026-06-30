using UnityEngine;
using Tribulation.Core;

// Port of balance.gd. Single source of tunable knobs, loaded from Resources/balance.json
// over the built-in defaults — so the game re-tunes by editing one JSON, no recompile,
// and still runs if the file is missing (defaults win).
//
// BalanceData POCO has moved to Assets/Scripts/Core/BalanceData.cs (Tribulation.Core
// assembly) so the pure-C# GameCore can reference it without UnityEngine.
// This class (Assembly-CSharp) keeps only the Resources loader.

public static class Balance
{
    static Tribulation.Core.BalanceData _d;

    public static Tribulation.Core.BalanceData D
    {
        get
        {
            if (_d == null)
            {
                _d = new Tribulation.Core.BalanceData();
                var ta = Resources.Load<TextAsset>("balance");
                if (ta != null) JsonUtility.FromJsonOverwrite(ta.text, _d); // only present keys override
            }
            return _d;
        }
    }
}
