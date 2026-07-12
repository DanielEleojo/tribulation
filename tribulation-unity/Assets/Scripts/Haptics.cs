using System.Runtime.InteropServices;

// iOS Taptic Engine bridge. No-ops in the editor and on non-iOS platforms.
// Mute-gated: the Settings mute toggle also silences haptics (Core.Muted,
// same flag SoundManager checks).
public static class Haptics
{
#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")] static extern void _HapticImpact(int style);
    [DllImport("__Internal")] static extern void _HapticNotify(int type);
    [DllImport("__Internal")] static extern void _HapticPrepare();
#endif

    static bool Muted => Game.I != null && Game.I.Core != null && Game.I.Core.Muted;

    public static void Prepare()
    {
#if UNITY_IOS && !UNITY_EDITOR
        _HapticPrepare();
#endif
    }
    static void Impact(int style)
    {
        if (Muted) return;
#if UNITY_IOS && !UNITY_EDITOR
        _HapticImpact(style);
#endif
    }
    static void Notify(int type)
    {
        if (Muted) return;
#if UNITY_IOS && !UNITY_EDITOR
        _HapticNotify(type);
#endif
    }
    public static void Light()   => Impact(0);
    public static void Medium()  => Impact(1);
    public static void Heavy()   => Impact(2);
    public static void Rigid()   => Impact(3);
    public static void Soft()    => Impact(4);
    public static void Success() => Notify(0);
    public static void Warning() => Notify(1);
    public static void Error()   => Notify(2);
}
