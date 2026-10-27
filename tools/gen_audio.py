#!/usr/bin/env python3
"""Generates every sound the game ships, from nothing but the standard library.

Sound is the one asset class a design document cannot hand over, and the two
usual answers are both bad: a dependency on numpy and scipy for arithmetic this
simple, or downloaded files with licences nobody checked. Synthesis keeps the
sound in version control as the twelve lines of maths that made it, and lets a
change be a change to a number rather than a hunt for a replacement file.

Every file is verified after it is written, not merely produced: the peak is
measured off the samples that went to disk, and loops are checked for the
discontinuity that makes a seam audible.
"""

import math
import os
import struct
import sys
import wave

RATE = 44100
PEAK_CEILING_DB = -1.0

# The drone is levelled by loudness, not by peak. Normalising a continuous bed to the same
# peak as a decaying one-shot makes it far louder than one: measured, the drone sat at -9.4
# dBFS RMS against -11.8 for a pass tone, so at the default music volume it was level with
# the sound it is supposed to sit underneath, all the time.
AMBIENT_RMS_DB = -24.0

OUT_DIR = os.path.join(
    os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
    "Assets", "_Project", "Audio",
)

# Minor pentatonic on A, then the octave. CONTRACT.md G-13 caps the streak at
# one octave, so this list is the whole ladder a run can climb.
PENTATONIC_HZ = [220.00, 261.63, 293.66, 329.63, 392.00, 440.00]


def envelope(index, count, attack_s, decay_s, release_s=0.030):
    """A click-free shape: every one-shot starts and ends at exactly zero.

    The release is not decoration. An exponential decay approaches zero and
    never arrives, so the file ends on a non-zero sample and the speaker steps
    to silence in one sample — which is precisely the click this is supposed to
    avoid. The verify pass caught it on the first run; eight of ten files ended
    as much as 0.09 above zero. The taper is a raised cosine so the signal
    reaches silence with no step in value or in slope.
    """
    t = index / RATE
    attack_n = max(1, int(attack_s * RATE))

    shape = index / attack_n if index < attack_n else math.exp(-(t - attack_s) / decay_s)

    release_n = max(1, int(release_s * RATE))
    if index >= count - release_n:
        remaining = (count - index) / release_n
        shape *= 0.5 - 0.5 * math.cos(math.pi * remaining)

    return shape


def tone(freq, seconds, attack_s=0.004, decay_s=0.18, partials=(1.0, 2.0, 3.0),
         weights=(1.0, 0.35, 0.12)):
    """A struck note: a few partials under one decaying envelope."""
    count = int(seconds * RATE)
    out = []
    for i in range(count):
        shape = envelope(i, count, attack_s, decay_s)
        value = 0.0
        for partial, weight in zip(partials, weights):
            value += weight * math.sin(2.0 * math.pi * freq * partial * i / RATE)
        out.append(value * shape)
    return out


def ambient_loop(seconds=8.0, root=110.0):
    """A drone that loops without a seam, by construction rather than by fade.

    Every partial is an exact integer number of cycles long, so the last sample
    runs into the first with no step in value or slope. Cross-fading the ends
    would hide a seam; this one has none to hide.
    """
    count = int(seconds * RATE)
    base = 1.0 / seconds

    # Snapped to the loop's own harmonic grid, which is what keeps the seam shut.
    voices = []
    for ratio, weight in ((1.0, 1.0), (1.5, 0.42), (2.0, 0.3), (3.0, 0.14), (4.02, 0.08)):
        cycles = max(1, round(root * ratio / base))
        voices.append((cycles * base, weight))

    out = []
    for i in range(count):
        phase = i / RATE
        value = 0.0
        for freq, weight in voices:
            value += weight * math.sin(2.0 * math.pi * freq * phase)

        # A slow swell, also an exact whole number of cycles over the loop.
        value *= 0.75 + 0.25 * math.sin(2.0 * math.pi * 2.0 * base * phase)
        out.append(value)
    return out


def knock(seconds=0.12):
    """Ball against ring: a low thump with a short, bright transient on top."""
    count = int(seconds * RATE)
    out = []
    noise = 0.0
    # A cheap deterministic noise source, so the same build makes the same file.
    state = 123456789
    for i in range(count):
        state = (1103515245 * state + 12345) % (1 << 31)
        noise = (state / (1 << 30)) - 1.0

        shape = envelope(i, count, 0.001, 0.025)
        body = math.sin(2.0 * math.pi * 150.0 * i / RATE)
        out.append((0.75 * body + 0.25 * noise) * shape)
    return out


def lose(seconds=0.9):
    """A fall: pitch slides down while the note dies away."""
    count = int(seconds * RATE)
    out = []
    phase = 0.0
    for i in range(count):
        t = i / RATE
        freq = 330.0 * math.pow(0.35, t / seconds)
        phase += 2.0 * math.pi * freq / RATE
        shape = envelope(i, count, 0.006, 0.42)
        out.append((math.sin(phase) + 0.3 * math.sin(2.0 * phase)) * shape)
    return out


def click(seconds=0.045):
    """A tap for the interface: short enough to feel like the button, not a note."""
    return tone(880.0, seconds, attack_s=0.0015, decay_s=0.012,
                partials=(1.0, 2.5), weights=(1.0, 0.25))


def normalise(samples, ceiling_db=PEAK_CEILING_DB):
    """Scales to the ceiling. Clipping is not a stylistic choice here."""
    peak = max(abs(s) for s in samples) or 1.0
    target = math.pow(10.0, ceiling_db / 20.0)
    gain = target / peak
    return [s * gain for s in samples]


def normalise_rms(samples, target_db, ceiling_db=PEAK_CEILING_DB):
    """Levels by loudness, then still refuses to go over the peak ceiling."""
    rms = math.sqrt(sum(s * s for s in samples) / len(samples)) or 1.0
    gain = math.pow(10.0, target_db / 20.0) / rms

    peak = max(abs(s) for s in samples) * gain
    ceiling = math.pow(10.0, ceiling_db / 20.0)
    if peak > ceiling:
        gain *= ceiling / peak

    return [s * gain for s in samples]


def write_wav(path, samples):
    data = b"".join(struct.pack("<h", int(max(-1.0, min(1.0, s)) * 32767)) for s in samples)
    with wave.open(path, "wb") as handle:
        handle.setnchannels(1)
        handle.setsampwidth(2)
        handle.setframerate(RATE)
        handle.writeframes(data)
    return len(data)


def read_samples(path):
    with wave.open(path, "rb") as handle:
        frames = handle.readframes(handle.getnframes())
    return [v / 32768.0 for (v,) in struct.iter_unpack("<h", frames)]


def verify(path, looping):
    """Reads back what landed on disk and measures it. Writing is not proof."""
    samples = read_samples(path)
    problems = []

    peak = max(abs(s) for s in samples)
    peak_db = 20.0 * math.log10(peak) if peak > 0 else -999.0
    rms = math.sqrt(sum(s * s for s in samples) / len(samples))
    rms_db = 20.0 * math.log10(rms) if rms > 0 else -999.0
    if peak_db > PEAK_CEILING_DB + 0.05:
        problems.append(f"peak {peak_db:.2f} dBFS exceeds {PEAK_CEILING_DB}")

    if looping:
        # The seam is a one-sample jump from the end back to the start. Compared
        # against the largest step inside the file, so the bar scales with the
        # material instead of being a number guessed in advance.
        seam = abs(samples[0] - samples[-1])
        inner = max(abs(samples[i + 1] - samples[i]) for i in range(0, len(samples) - 1, 7))
        if seam > max(inner * 1.5, 0.002):
            problems.append(f"loop seam steps {seam:.5f} against {inner:.5f} inside")
    else:
        if abs(samples[0]) > 0.002 or abs(samples[-1]) > 0.002:
            problems.append(f"one-shot does not start and end at silence "
                            f"({samples[0]:.4f} .. {samples[-1]:.4f})")

    return peak_db, rms_db, problems


def main():
    os.makedirs(OUT_DIR, exist_ok=True)

    jobs = [("ambient", normalise_rms(ambient_loop(), AMBIENT_RMS_DB), True),
            ("knock", knock(), False),
            ("lose", lose(), False), ("click", click(), False)]

    for step, freq in enumerate(PENTATONIC_HZ):
        jobs.append((f"pass-{step}", tone(freq, 0.45), False))

    total_bytes = 0
    failures = 0

    for name, samples, looping in jobs:
        path = os.path.join(OUT_DIR, name + ".wav")
        # The drone arrives already levelled; everything else is brought up to the ceiling.
        total_bytes += write_wav(path, samples if name == "ambient" else normalise(samples))

        peak_db, rms_db, problems = verify(path, looping)
        mark = "ok   " if not problems else "FAIL "
        print(f"  {mark}{name + '.wav':<14} {os.path.getsize(path) / 1024:7.1f} KiB  "
              f"peak {peak_db:6.2f}  rms {rms_db:6.2f} dBFS")
        for problem in problems:
            print(f"        {problem}")
            failures += 1

    budget = 5 * 1024 * 1024
    print(f"\n  total {total_bytes / 1024 / 1024:.2f} MiB of {budget / 1024 / 1024:.0f} MiB budget")
    if total_bytes > budget:
        print("  FAIL over the audio budget")
        failures += 1

    print("audio: clean" if not failures else f"audio: {failures} problem(s)")
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main())
