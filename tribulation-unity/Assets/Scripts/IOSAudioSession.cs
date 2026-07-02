// IOSAudioSession.cs — C# bridge to Plugins/iOS/IgnoreMuteSwitch.mm.
// Routes game audio through the Playback session category so the iPhone's
// ring/silent switch doesn't mute it (Unity's default Ambient category is
// silenced by the switch). No-op everywhere except real iOS builds.

using UnityEngine;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

public static class IOSAudioSession
{
#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")] static extern void Tribulation_SetAudioSessionPlayback();
#endif

    /// <summary>Play audio regardless of the silent switch. Safe to call repeatedly —
    /// Bootstrap re-asserts it whenever the app regains focus, because interruptions
    /// (calls, Siri, other apps) can reset the audio session.</summary>
    public static void IgnoreMuteSwitch()
    {
#if UNITY_IOS && !UNITY_EDITOR
        Tribulation_SetAudioSessionPlayback();
#endif
    }
}
