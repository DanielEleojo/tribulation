// Lightweight SFX hub. Lazy-loads the 11 named clips from Resources on Awake.
// Play(name) is a null-safe no-op if the clip wasn't found.
// Ported from scripts/sound_manager.gd.
// note: no AudioMixer bus — PlayOneShot volume scaled directly.
using UnityEngine;
using System.Collections.Generic;

public class SoundManager : MonoBehaviour
{
    public static SoundManager I { get; private set; }

    static readonly string[] SoundNames = {
        "start", "slash", "kill", "jump", "slide",
        "gate_good", "gate_bad", "burst", "death", "breakthrough", "orb"
    };

    readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
    AudioSource _src;

    void Awake()
    {
        I = this;
        _src = gameObject.AddComponent<AudioSource>();
        _src.playOnAwake = false;

        foreach (var name in SoundNames)
        {
            var clip = Resources.Load<AudioClip>("audio/sfx/" + name);
            if (clip != null) _clips[name] = clip;
        }
    }

    /// <summary>Play a named SFX. Safe no-op if clip missing or core is muted.</summary>
    public void Play(string name)
    {
        if (!_clips.TryGetValue(name, out var clip)) return;

        var core = (Game.I != null) ? Game.I.Core : null;
        if (core != null && core.Muted) return;

        float vol = (core != null) ? core.SfxVol : 1f;
        _src.PlayOneShot(clip, vol);
    }
}
