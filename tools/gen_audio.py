#!/usr/bin/env python3
"""Procedural placeholder audio for Tribulation.

Music: a calming, modern, generative ambient loop — a slow-breathing Am9 pad with
sparse pentatonic bells. Built to be SEAMLESS (every partial completes a whole
number of cycles over the loop, bells decay before the boundary) so it can play
for hours without an audible seam and without fatigue.

SFX: soft, pleasant, click-free (smooth attack/release) and mixed low so they sit
under the music. Swap any file for a real recording later; nothing else changes.

Run:  python3 tools/gen_audio.py
"""
import os, math, wave, struct
import numpy as np

SR = 44100
HERE = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
MUSIC = os.path.join(HERE, "assets", "music")
SFX = os.path.join(HERE, "assets", "sfx")


def write_wav(path, data, stereo=False):
    """data: float array in [-1,1], shape (N,) mono or (N,2) stereo."""
    data = np.clip(data, -1.0, 1.0)
    pcm = (data * 32767.0).astype("<i2")
    ch = 2 if stereo else 1
    with wave.open(path, "wb") as w:
        w.setnchannels(ch)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(pcm.tobytes())
    print("  wrote", os.path.relpath(path, HERE), f"({data.shape[0]} frames, {ch}ch)")


def env_ar(n, attack, release):
    """Smooth attack/release envelope over n samples (seconds)."""
    e = np.ones(n)
    a = min(int(attack * SR), n // 2)
    r = min(int(release * SR), n - a)
    if a > 0:
        e[:a] *= np.sin(np.linspace(0, math.pi / 2, a)) ** 2
    if r > 0:
        e[-r:] *= np.cos(np.linspace(0, math.pi / 2, r)) ** 2
    return e


def mix(*signals):
    """Sum signals of differing lengths by zero-padding to the longest."""
    n = max(len(s) for s in signals)
    out = np.zeros(n)
    for s in signals:
        out[:len(s)] += s
    return out


# ---------------------------------------------------------------- MUSIC
def gen_music():
    T = 96.0                      # loop length (s)
    N = int(round(T * SR))
    t = np.arange(N) / SR

    def q(f):                     # quantize to a whole number of cycles over T -> seamless
        return round(f * T) / T

    left = np.zeros(N)
    right = np.zeros(N)

    # Am9 pad: root, octave, minor-3rd, 5th, minor-7th, 9th. Warm, unresolved, calm.
    notes = [
        (55.00, 0.55),   # A1 sub
        (110.00, 0.50),  # A2
        (164.81, 0.30),  # E3 (5th)
        (220.00, 0.34),  # A3
        (261.63, 0.24),  # C4 (min 3rd)
        (329.63, 0.20),  # E4 (5th)
        (392.00, 0.15),  # G4 (min 7th)
        (493.88, 0.11),  # B4 (9th)
    ]
    for i, (f, amp) in enumerate(notes):
        f = q(f)
        # 3 lightly-detuned partials for warmth (detune quantized so it stays seamless)
        voice = np.zeros(N)
        for k, det in enumerate((0.0, q(0.6), q(-0.55))):
            voice += np.sin(2 * math.pi * (f + det) * t + k * 1.3)
        voice /= 3.0
        # slow breathing: amplitude LFO, whole cycles over T so the loop is seamless
        lfo = 0.5 + 0.5 * np.sin(2 * math.pi * (i + 1) / T * t + i * 0.7)
        voice *= amp * (0.55 + 0.45 * lfo)
        # gentle stereo drift (also whole-cycle)
        pan = 0.5 + 0.45 * np.sin(2 * math.pi * ((i % 3) + 1) / T * t + i)
        left += voice * np.cos(pan * math.pi / 2)
        right += voice * np.sin(pan * math.pi / 2)

    # Sparse pentatonic bells (A C D E G) — soft sine + exp decay, placed so every
    # tail dies well before the loop boundary (seamless). Deterministic, no RNG.
    penta = [220.0, 261.63, 293.66, 329.63, 392.00, 440.0, 523.25]
    bells = [  # (start_s, note_index, gain)
        (4.0, 0, 0.16), (11.0, 3, 0.13), (17.0, 1, 0.14), (24.5, 4, 0.11),
        (32.0, 2, 0.13), (39.0, 5, 0.10), (47.0, 0, 0.14), (55.0, 3, 0.12),
        (62.0, 4, 0.10), (70.0, 1, 0.13), (78.0, 2, 0.11), (85.0, 5, 0.09),
    ]
    dur = 3.4
    for start, ni, gain in bells:
        s0 = int(start * SR)
        bn = int(dur * SR)
        if s0 + bn >= N:          # keep tails inside the loop
            bn = N - s0 - 1
        bt = np.arange(bn) / SR
        f = penta[ni]
        tone = (np.sin(2 * math.pi * f * bt) + 0.5 * np.sin(2 * math.pi * 2 * f * bt)
                + 0.25 * np.sin(2 * math.pi * 3 * f * bt))
        tone *= np.exp(-bt * 1.6) * gain
        tone *= env_ar(bn, 0.005, 0.2)
        pan = 0.35 + 0.3 * ((ni % 3))   # spread bells across the field
        left[s0:s0 + bn] += tone * math.cos(pan * math.pi / 2)
        right[s0:s0 + bn] += tone * math.sin(pan * math.pi / 2)

    stereo = np.stack([left, right], axis=1)
    # gentle soft-knee saturation for warmth, then calm headroom
    stereo = np.tanh(stereo * 0.8)
    peak = np.max(np.abs(stereo))
    if peak > 0:
        stereo *= 0.5 / peak       # ~ -6 dBFS peak: present but unobtrusive
    write_wav(os.path.join(MUSIC, "theme.wav"), stereo, stereo=True)


# ---------------------------------------------------------------- SFX
def tone(freqs, dur, attack=0.006, release=None, decay=None, gain=0.4, detune=0.0):
    n = int(dur * SR)
    tt = np.arange(n) / SR
    out = np.zeros(n)
    if isinstance(freqs, (int, float)):
        freqs = [freqs]
    for f in freqs:
        out += np.sin(2 * math.pi * f * tt) + (np.sin(2 * math.pi * f * (1 + detune) * tt) if detune else 0)
    out /= len(freqs)
    if decay is not None:
        out *= np.exp(-tt * decay)
    out *= env_ar(n, attack, release if release is not None else min(0.05, dur * 0.3))
    return out * gain


def sweep(f0, f1, dur, attack=0.006, release=0.04, gain=0.4):
    n = int(dur * SR)
    tt = np.arange(n) / SR
    f = np.linspace(f0, f1, n)
    ph = 2 * math.pi * np.cumsum(f) / SR
    return np.sin(ph) * env_ar(n, attack, release) * gain


def noise_band(center, dur, attack=0.005, release=0.06, gain=0.3, sweep_to=None):
    n = int(dur * SR)
    tt = np.arange(n) / SR
    rng = np.random.default_rng(1234)
    nz = rng.standard_normal(n)
    # crude band feel: ring noise with a (swept) sine carrier + mild smoothing
    c = center if sweep_to is None else np.linspace(center, sweep_to, n)
    car = np.sin(2 * math.pi * (c * tt if sweep_to is None else np.cumsum(c) / SR))
    sig = nz * 0.5 + nz * car
    # simple 1-pole lowpass to take the harsh edge off
    a = 0.25
    for _ in range(2):
        sig = np.concatenate([[sig[0]], sig[1:] * a + sig[:-1] * (1 - a)])
    sig *= env_ar(n, attack, release)
    sig /= max(1e-6, np.max(np.abs(sig)))
    return sig * gain


def gen_sfx():
    penta = [440.0, 523.25, 587.33, 659.25, 783.99, 880.0]
    sfx = {}
    # collectible Qi mote — bright soft chime
    sfx["orb"] = tone([880.0, 1318.5], 0.22, attack=0.004, decay=14, gain=0.32)
    # slay — soft wood-block "tok" + tiny air
    sfx["kill"] = mix(tone(300.0, 0.13, attack=0.002, decay=30, gain=0.3),
                      noise_band(1800, 0.05, release=0.02, gain=0.12))
    # slash — gentle descending swish
    sfx["slash"] = noise_band(2600, 0.16, gain=0.28, sweep_to=900)
    # jump — soft upward whoop
    sfx["jump"] = sweep(420, 760, 0.16, gain=0.26)
    # slide — soft downward shh
    sfx["slide"] = noise_band(1400, 0.24, gain=0.22, sweep_to=500)
    # life gate — pleasant rising two-note (C -> G)
    sfx["gate_good"] = np.concatenate([tone(523.25, 0.12, decay=8, gain=0.3),
                                       tone(783.99, 0.18, decay=7, gain=0.3)])
    # death gate — soft falling minor two-note (not harsh)
    sfx["gate_bad"] = np.concatenate([tone(392.0, 0.12, decay=7, gain=0.26),
                                      tone(311.13, 0.2, decay=6, gain=0.26)])
    # qi burst — warm shimmer swell
    sfx["burst"] = tone([261.63, 392.0, 523.25, 659.25], 0.5, attack=0.02,
                        release=0.25, decay=4, gain=0.34)
    # breakthrough / pickups — soft ascending pentatonic arpeggio (used a lot: must be pleasant)
    arp = []
    for i, f in enumerate([523.25, 659.25, 783.99, 1046.5]):
        arp.append(tone([f, f * 2], 0.16, attack=0.004, decay=12, gain=0.26))
    sfx["breakthrough"] = np.concatenate(arp)
    # death — low warm descending tone, gentle
    sfx["death"] = sweep(330, 110, 1.1, attack=0.02, release=0.4, gain=0.34)
    # start — gentle chord bloom
    sfx["start"] = tone([220.0, 329.63, 440.0, 659.25], 1.0, attack=0.15,
                        release=0.5, decay=1.5, gain=0.3)

    for name, data in sfx.items():
        # final safety: smooth ends, modest peak
        data = data * env_ar(len(data), 0.003, 0.02)
        p = np.max(np.abs(data))
        if p > 0:
            data = data * min(1.0, 0.5 / p)
        write_wav(os.path.join(SFX, name + ".wav"), data)


if __name__ == "__main__":
    os.makedirs(MUSIC, exist_ok=True)
    os.makedirs(SFX, exist_ok=True)
    print("music:")
    gen_music()
    print("sfx:")
    gen_sfx()
    print("done.")
