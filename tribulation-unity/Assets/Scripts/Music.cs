// Music shaping: loads theme.ogg from Resources, loops it, and shapes pitch+volume
// each frame based on speed fraction, death state, and tribulation state.
// Ported from scripts/music.gd _process() logic.
// note: AudioSource linear volume instead of AudioMixer bus — no bus graph needed.
using UnityEngine;
using Tribulation.Core;

public class Music : MonoBehaviour
{
    AudioSource _src;

    void Start()
    {
        var clip = Resources.Load<AudioClip>("audio/music/theme");
        if (clip == null) return; // no-op if clip missing

        _src = gameObject.AddComponent<AudioSource>();
        _src.clip = clip;
        _src.loop = true;
        _src.playOnAwake = false;
        _src.volume = AudioShaping.DbToLinear(-9f);
        _src.pitch  = 1f;
        _src.Play();
    }

    void Update()
    {
        if (_src == null) return;

        // Self-heal: the loop only stops if the audio engine was reinitialized
        // (AudioSettings.Reset after an iOS interruption) — start it again.
        if (!_src.isPlaying) _src.Play();

        var core = (Game.I != null) ? Game.I.Core : null;
        float speedFrac = 0f;
        var player = FindObjectOfType<PlayerRunner>();
        if (player != null) speedFrac = player.GetSpeedFraction();

        bool isDead = core != null && core.IsDead;
        bool inTrib = core != null && core.InTribulation;

        float targetPitch = AudioShaping.TargetPitch(speedFrac, inTrib);
        float targetDb    = AudioShaping.TargetVolumeDb(isDead, inTrib);
        float targetVol   = AudioShaping.DbToLinear(targetDb);

        // Apply music volume setting and mute.
        if (core != null)
        {
            if (core.Muted) targetVol = 0f;
            else targetVol *= core.MusicVol;
        }

        // Smooth toward targets (mirrors music.gd k = clamp(2*delta, 0, 1)).
        float k = Mathf.Clamp(2f * Time.deltaTime, 0f, 1f);
        _src.pitch  = Mathf.Lerp(_src.pitch,  targetPitch, k);
        _src.volume = Mathf.Lerp(_src.volume, targetVol,   k);
    }
}
