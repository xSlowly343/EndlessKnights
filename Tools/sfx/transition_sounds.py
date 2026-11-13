import json
import sys
from pathlib import Path

import numpy as np
from scipy.io import wavfile
from scipy.signal import butter, sosfilt, fftconvolve

RATE = 44100
ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "Assets" / "Game" / "Art" / "Resources" / "Sounds" / "Transitions"
PANEL = ROOT / "Assets" / "StreamingAssets" / "Data" / "panel.json"
PEAK = 10 ** (-1.5 / 20)
TAIL = 0.35
LOUDNESS = 10 ** (-18 / 20)


def seconds(n):
    return np.arange(int(n * RATE)) / RATE


def rng_for(key):
    return np.random.default_rng(sum(ord(c) * (i + 1) for i, c in enumerate(key)))


def band(signal, low, high):
    nyquist = RATE / 2
    if low <= 0:
        sos = butter(4, high / nyquist, "lowpass", output="sos")
    elif high >= nyquist:
        sos = butter(4, low / nyquist, "highpass", output="sos")
    else:
        sos = butter(4, [low / nyquist, high / nyquist], "bandpass", output="sos")
    return sosfilt(sos, signal)


def envelope(length, attack, release, curve=2.0):
    t = seconds(length)
    rise = np.clip(t / max(attack, 1e-4), 0, 1)
    fall = np.clip((length - t) / max(release, 1e-4), 0, 1) ** curve
    return rise * fall


def decay(length, rate):
    return np.exp(-seconds(length) * rate)


def place(target, sound, at):
    start = int(at * RATE)
    end = min(len(target), start + len(sound))
    if end > start:
        target[start:end] += sound[: end - start]
    return target


def pad(sound, length):
    return place(silence(length), sound, 0.0)


def silence(length):
    return np.zeros(int(length * RATE))


def sine(length, freq, phase=0.0):
    t = seconds(length)
    if np.ndim(freq) == 0:
        return np.sin(2 * np.pi * freq * t + phase)
    return np.sin(2 * np.pi * np.cumsum(freq[: len(t)]) / RATE + phase)


def glide(length, start, end):
    return np.geomspace(start, end, int(length * RATE))


def noise(rng, length):
    return rng.standard_normal(int(length * RATE))


def reverb(signal, rng, size, wet):
    length = int(size * RATE)
    impulse = rng.standard_normal(length) * np.exp(-np.arange(length) / RATE * (6.0 / size))
    impulse = band(impulse, 200, 9000)
    tail = fftconvolve(signal, impulse)[: len(signal)]
    tail /= max(np.max(np.abs(tail)), 1e-9)
    dry = signal / max(np.max(np.abs(signal)), 1e-9)
    return dry * (1 - wet) + tail * wet


def metal(rng, length, base, ratios, rate):
    out = silence(length)
    for i, ratio in enumerate(ratios):
        out += sine(length, base * ratio, rng.uniform(0, 6.28)) * (0.8 ** i) * decay(length, rate * (1 + i * 0.35))
    return out


def bell(rng, length, base, rate=4.0):
    return metal(rng, length, base, [1.0, 2.0, 2.76, 4.07, 5.43], rate)


def steel(rng, length, base):
    clang = metal(rng, length, base, [1.0, 1.47, 2.09, 2.56, 3.81, 5.12], 9.0)
    scrape = band(noise(rng, length), 3000, 9000) * decay(length, 30) * 0.4
    return clang + scrape


def hum(rng, length, freqs, wobble=0.6):
    out = silence(length)
    t = seconds(length)
    for f in freqs:
        out += sine(length, f, rng.uniform(0, 6.28)) * (1 + 0.15 * np.sin(2 * np.pi * wobble * t))
    return out / len(freqs)


def whoosh(rng, length, low, high, rising=True):
    raw = noise(rng, length)
    pieces = 24
    out = silence(length)
    edges = np.linspace(0, len(raw), pieces + 1).astype(int)
    centers = np.geomspace(low, high, pieces) if rising else np.geomspace(high, low, pieces)
    for i in range(pieces):
        part = band(raw, centers[i] * 0.7, min(centers[i] * 1.4, RATE / 2 - 100))
        out[edges[i]:edges[i + 1]] = part[edges[i]:edges[i + 1]]
    return band(out, 80, 12000) * envelope(length, length * 0.45, length * 0.4)


def crackle(rng, length, density, bright=4000):
    out = silence(length)
    count = int(length * density)
    for at in rng.uniform(0, length, count):
        size = rng.uniform(0.002, 0.012)
        pop = noise(rng, size) * decay(size, rng.uniform(300, 900)) * rng.uniform(0.3, 1.0)
        place(out, pop, at)
    return band(out, 600, bright) + band(out, 150, 600) * 0.3


def creak(rng, length, start, end):
    t = seconds(length)
    freq = glide(length, start, end) * (1 + 0.04 * np.sin(2 * np.pi * 23 * t))
    tone = np.sign(sine(length, freq)) * 0.4 + sine(length, freq * 2) * 0.3
    grain = (rng.uniform(0, 1, len(t)) < 0.08) * 1.0
    return band(tone * (0.6 + grain), 250, 3000) * envelope(length, 0.05, 0.12)


def thud(rng, length, base):
    body = sine(length, glide(length, base * 1.8, base)) * decay(length, 18)
    grit = band(noise(rng, length), 100, 900) * decay(length, 45) * 0.5
    return body + grit


def footsteps(rng, length, count, base=110):
    out = silence(length)
    for i in range(count):
        at = length * (i + 0.3) / (count + 0.3) + rng.uniform(-0.02, 0.02)
        place(out, thud(rng, 0.18, base * rng.uniform(0.9, 1.1)), at)
        place(out, band(noise(rng, 0.03), 2000, 7000) * decay(0.03, 120) * 0.3, at + 0.005)
    return out


def flap(rng, length, count):
    out = silence(length)
    for i in range(count):
        size = rng.uniform(0.06, 0.12)
        burst = band(noise(rng, size), 300, 2500) * envelope(size, 0.005, size * 0.8)
        place(out, burst, length * (i + 0.2) / count)
    return out


def murmur(rng, length):
    t = seconds(length)
    voices = silence(length)
    for _ in range(7):
        rate = rng.uniform(3, 6)
        gate = np.clip(np.sin(2 * np.pi * rate * t + rng.uniform(0, 6.28)), 0, 1) ** 2
        voices += band(noise(rng, length), rng.uniform(300, 600), rng.uniform(900, 1600)) * gate
    return voices / 7


def horn(rng, length, base):
    t = seconds(length)
    freq = base * (1 + 0.006 * np.sin(2 * np.pi * 5.2 * t)) * np.clip(0.94 + t * 0.6, 0.94, 1.0)
    phase = np.cumsum(freq) / RATE
    saw = 2 * (phase % 1.0) - 1
    return band(saw, 0, 1800) * envelope(length, 0.12, 0.3)


def popper(rng, length):
    bang = noise(rng, length) * decay(length, 55)
    return band(bang, 400, 9000) + sine(length, glide(length, 180, 60)) * decay(length, 30) * 0.6


def jingle(rng, length, count):
    out = silence(length)
    for i in range(count):
        at = rng.uniform(0, length * 0.7)
        place(out, metal(rng, 0.35, rng.uniform(2400, 3600), [1.0, 1.52, 2.33, 3.1], 14) * rng.uniform(0.3, 0.8), at)
    return out


def rumble(rng, length):
    t = seconds(length)
    low = band(noise(rng, length), 25, 140)
    return low * (0.7 + 0.3 * np.sin(2 * np.pi * 0.8 * t)) + sine(length, glide(length, 42, 36)) * 0.4


def tear(rng, length):
    t = seconds(length)
    crack = crackle(rng, length, 260, 7000)
    rip = band(noise(rng, length), 900, 5000) * np.clip(np.sin(np.pi * t / length), 0, 1) ** 3
    return crack * 0.6 + rip * 0.8


def glass(rng, length, count):
    out = silence(length)
    for _ in range(count):
        at = rng.uniform(0, length * 0.75)
        place(out, metal(rng, 0.6, rng.uniform(1800, 4200), [1.0, 2.32, 4.25, 6.63], 7) * rng.uniform(0.2, 0.7), at)
    return out


def finish(sound, length):
    sound = sound[: int(length * RATE)]
    sound = sound - np.mean(sound)
    fade = int(0.03 * RATE)
    sound[:64] *= np.linspace(0, 1, 64)
    sound[-fade:] *= np.linspace(1, 0, fade) ** 2
    rms = np.sqrt(np.mean(sound ** 2))
    if rms <= 0:
        return sound
    sound = np.tanh(sound * (LOUDNESS / rms) / PEAK) * PEAK
    peak = np.max(np.abs(sound))
    return sound / peak * PEAK if peak > PEAK else sound


def phase_sound(place_key, phase, length, rng):
    builder = PLACES[place_key]
    return builder(rng, phase, length)


def home(rng, phase, n):
    if phase == "open":
        return hum(rng, n, [220, 277.2, 329.6]) * envelope(n, n * 0.6, 0.25) * 0.6 + place(silence(n), steel(rng, n * 0.6, 660), n * 0.35) * 0.5
    if phase == "enter":
        return whoosh(rng, n, 300, 4000) * 0.7 + place(silence(n), steel(rng, n * 0.5, 880), n * 0.45) * 0.6
    if phase == "arrive":
        return steel(rng, n, 520) * 0.8 + hum(rng, n, [220, 329.6]) * decay(n, 3) * 0.4
    return whoosh(rng, n, 2500, 300, False) * 0.6 + hum(rng, n, [220, 277.2]) * envelope(n, 0.02, n * 0.9) * 0.4


def hideout(rng, phase, n):
    if phase == "open":
        return pad(creak(rng, n * 0.8, 180, 320), n) * 0.7 + crackle(rng, n, 30) * 0.3
    if phase == "enter":
        return whoosh(rng, n, 200, 1800) * 0.4 + crackle(rng, n, 45) * 0.4 + place(silence(n), bell(rng, n * 0.6, 880), n * 0.3) * 0.5
    if phase == "arrive":
        return bell(rng, n, 660, 3.0) * 0.6 + crackle(rng, n, 60) * 0.5
    return pad(creak(rng, n * 0.7, 300, 160), n) * 0.6 + place(silence(n), thud(rng, 0.2, 90), n * 0.6) * 0.6


def town(rng, phase, n):
    if phase == "open":
        return murmur(rng, n) * envelope(n, n * 0.6, 0.2) * 0.8 + flap(rng, n, 2) * 0.5
    if phase == "enter":
        return whoosh(rng, n, 250, 2500) * 0.5 + flap(rng, n, 3) * 0.6 + murmur(rng, n) * 0.4
    if phase == "arrive":
        return footsteps(rng, n, 3) * 0.8 + murmur(rng, n) * envelope(n, 0.1, n * 0.6) * 0.5
    return footsteps(rng, n, 2, 95) * 0.7 + murmur(rng, n) * envelope(n, 0.02, n * 0.9) * 0.4


def clan_camp(rng, phase, n):
    if phase == "open":
        return horn(rng, n, 147) * 0.8 + crackle(rng, n, 35) * 0.3
    if phase == "enter":
        return whoosh(rng, n, 200, 2200) * 0.5 + flap(rng, n, 2) * 0.6 + crackle(rng, n, 40) * 0.3
    if phase == "arrive":
        return crackle(rng, n, 70) * 0.6 + place(silence(n), flap(rng, 0.25, 1), 0.05) * 0.7 + pad(horn(rng, n * 0.6, 220), n) * 0.3
    return flap(rng, n, 1) * 0.6 + crackle(rng, n, 30) * envelope(n, 0.02, n * 0.9) * 0.4


def dungeon(rng, phase, n):
    rune = hum(rng, n, [55, 82.5, 110.6], 0.3)
    if phase == "open":
        return reverb(rune * envelope(n, n * 0.6, 0.3) + place(silence(n), thud(rng, 0.3, 70), 0.1), rng, 1.2, 0.5)
    if phase == "enter":
        return reverb(footsteps(rng, n, 3, 80) + whoosh(rng, n, 120, 1200) * 0.4, rng, 1.4, 0.55)
    if phase == "arrive":
        return reverb(footsteps(rng, n, 2, 75) + rune * decay(n, 2) * 0.5, rng, 1.6, 0.6)
    return reverb(rune * envelope(n, 0.02, n * 0.9) * 0.6 + whoosh(rng, n, 1500, 150, False) * 0.4, rng, 1.0, 0.5)


def event_fight(rng, phase, n):
    if phase == "open":
        return jingle(rng, n, 8) * 0.6 + place(silence(n), popper(rng, 0.25), n * 0.5) * 0.6
    if phase == "enter":
        return whoosh(rng, n, 400, 5000) * 0.4 + popper(rng, n) * 0.5 + jingle(rng, n, 6) * 0.5
    if phase == "arrive":
        out = silence(n)
        place(out, popper(rng, 0.25), 0.0)
        place(out, popper(rng, 0.25) * 0.8, 0.12)
        return out * 0.7 + jingle(rng, n, 10) * 0.5
    return jingle(rng, n, 4) * envelope(n, 0.02, n * 0.8) * 0.6 + whoosh(rng, n, 3000, 400, False) * 0.3


def rift(rng, phase, n):
    if phase == "open":
        return rumble(rng, n) * envelope(n, n * 0.7, 0.3) * 0.8 + place(silence(n), tear(rng, n * 0.6), n * 0.35) * 0.7
    if phase == "enter":
        return rumble(rng, n) * 0.6 + whoosh(rng, n, 80, 3000) * 0.6 + glass(rng, n, 6) * 0.5
    if phase == "arrive":
        return reverb(glass(rng, n, 10) * 0.7 + rumble(rng, n) * decay(n, 2.5) * 0.6, rng, 1.2, 0.4)
    return tear(rng, n) * 0.6 + rumble(rng, n) * envelope(n, 0.02, n * 0.9) * 0.5


def rift_loading(rng, n):
    t = seconds(n)
    funnel = sine(n, glide(n, 55, 82)) * 0.5 + sine(n, glide(n, 110.5, 164.5)) * 0.3
    swirl = band(noise(rng, n), 200, 1800) * (0.5 + 0.5 * np.sin(2 * np.pi * glide(n, 0.6, 2.4).cumsum() / RATE)) * 0.3
    return reverb((funnel + swirl) * envelope(n, n * 0.4, 0.8) + glass(rng, n, 5) * 0.25, rng, 1.8, 0.45)


PLACES = {
    "home": home, "hideout": hideout, "town": town, "clanCamp": clan_camp,
    "dungeon": dungeon, "eventFight": event_fight, "rift": rift,
}
PHASES = ["open", "enter", "arrive", "exit"]
LOADING_SECONDS = 3.0


def write(key, sound):
    OUT.mkdir(parents=True, exist_ok=True)
    wavfile.write(OUT / (key + ".wav"), RATE, (sound * 32767).astype(np.int16))


def build():
    portals = json.loads(PANEL.read_text(encoding="utf-8"))["strip"]["portals"]
    written = []
    for place_key in PLACES:
        for phase in PHASES:
            key = "portal_" + place_key + "_" + phase
            length = portals[place_key][phase] + TAIL
            write(key, finish(phase_sound(place_key, phase, length, rng_for(key)), length))
            written.append(key)
    write("rift_loading", finish(rift_loading(rng_for("rift_loading"), LOADING_SECONDS), LOADING_SECONDS))
    written.append("rift_loading")
    return written


if __name__ == "__main__":
    keys = build()
    print(len(keys), "sounds in", OUT.relative_to(ROOT))
    sys.exit(0)
