// Lightweight SFX hub. Lazy-loads the named clips from Resources on Awake;
// a couple of event names alias existing clips (asset-free).
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
        "gate_good", "gate_bad", "burst", "death", "breakthrough", "orb",
        "ui_tap"
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

        // Aliases — event names that reuse already-loaded clips.
        if (_clips.TryGetValue("breakthrough", out var newBest)) _clips["new_best"] = newBest;
        if (_clips.TryGetValue("orb", out var nearMiss)) _clips["near_miss"] = nearMiss;
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
