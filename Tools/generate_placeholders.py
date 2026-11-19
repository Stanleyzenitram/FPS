"""Genera texturas placeholder y efectos de sonido para el FPS.

Sin dependencias: PNG con zlib y WAV con el modulo wave.
Salida: Assets/ShootingGallery/Art/*.png y Assets/ShootingGallery/Audio/*.wav
"""
import math
import os
import struct
import wave
import zlib

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ART = os.path.join(ROOT, "Assets", "ShootingGallery", "Art")
AUDIO = os.path.join(ROOT, "Assets", "ShootingGallery", "Audio")
os.makedirs(ART, exist_ok=True)
os.makedirs(AUDIO, exist_ok=True)


def write_png(path, width, height, pixels):
    raw = bytearray()
    for y in range(height):
        raw.append(0)
        for x in range(width):
            raw.extend(pixels[y * width + x])

    def chunk(tag, data):
        return (struct.pack(">I", len(data)) + tag + data
                + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF))

    with open(path, "wb") as f:
        f.write(b"\x89PNG\r\n\x1a\n")
        f.write(chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 2, 0, 0, 0)))
        f.write(chunk(b"IDAT", zlib.compress(bytes(raw), 9)))
        f.write(chunk(b"IEND", b""))


def write_rgba_png(path, width, height, pixels):
    """PNG con canal alfa (color type 6)."""
    raw = bytearray()
    for y in range(height):
        raw.append(0)
        for x in range(width):
            raw.extend(pixels[y * width + x])

    def chunk(tag, data):
        return (struct.pack(">I", len(data)) + tag + data
                + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF))

    with open(path, "wb") as f:
        f.write(b"\x89PNG\r\n\x1a\n")
        f.write(chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0)))
        f.write(chunk(b"IDAT", zlib.compress(bytes(raw), 9)))
        f.write(chunk(b"IEND", b""))


def solid(c):
    return c


def make_target():
    n = 128
    px = [solid((235, 235, 235)) for _ in range(n * n)]
    red = (200, 42, 48)
    white = (245, 245, 240)
    dark = (30, 30, 38)
    for y in range(n):
        for x in range(n):
            d = math.hypot(x - 63.5, y - 63.5)
            if d <= 60:
                if d <= 13:
                    c = red
                elif d <= 26:
                    c = white
                elif d <= 39:
                    c = red
                elif d <= 52:
                    c = white
                else:
                    c = dark
                px[y * n + x] = c
    write_png(os.path.join(ART, "target.png"), n, n, px)


def make_x(name, color, bg, thickness=9):
    n = 128
    px = [solid(bg) for _ in range(n * n)]
    for y in range(n):
        for x in range(n):
            if abs(x - y) <= thickness or abs(x + y - 127) <= thickness:
                px[y * n + x] = color
    # borde
    for y in range(n):
        for x in range(n):
            if x < 3 or y < 3 or x >= n - 3 or y >= n - 3:
                px[y * n + x] = (20, 20, 26)
    write_png(os.path.join(ART, name), n, n, px)


def make_wav(name, notes, duration=0.12, volume=0.22, noise=False):
    rate = 22050
    frames = bytearray()
    for freq, dur in notes:
        count = int(rate * dur)
        for i in range(count):
            t = i / rate
            env = min(1.0, i / (rate * 0.01)) * max(0.0, 1.0 - i / count)
            v = math.sin(2 * math.pi * freq * t) * env * volume
            if noise:
                v += (math.sin(i * 12.9898) * 43758.5453 % 1.0 - 0.5) * env * volume * 0.6
            sample = max(-1.0, min(1.0, v))
            frames.extend(struct.pack("<h", int(sample * 32767)))
    with wave.open(os.path.join(AUDIO, name), "wb") as f:
        f.setnchannels(1)
        f.setsampwidth(2)
        f.setframerate(rate)
        f.writeframes(bytes(frames))


def make_muzzle_flash():
    """Estrella luminosa con alfa para el fogonazo del arma."""
    n = 64
    px = [(0, 0, 0, 0) for _ in range(n * n)]
    cx = cy = (n - 1) / 2.0
    for y in range(n):
        for x in range(n):
            dx, dy = x - cx, y - cy
            d = math.hypot(dx, dy)
            ang = math.atan2(dy, dx)
            # nucleo redondo + 4 puntas alargadas
            spikes = max(abs(math.cos(ang * 2.0)), abs(math.sin(ang * 2.0)))
            radius = 9.0 + 20.0 * (spikes ** 6)
            if d > radius:
                continue
            a = int(255 * max(0.0, 1.0 - (d / radius) ** 1.5))
            px[y * n + x] = (255, int(210 + 45 * (1 - d / radius)), int(90 + 90 * (1 - d / radius)), a)
    write_rgba_png(os.path.join(ART, "muzzle_flash.png"), n, n, px)


make_target()
make_x("enemy_x.png", (205, 48, 48), (48, 48, 54))
make_x("boss_x.png", (255, 90, 40), (70, 28, 30), thickness=12)
make_muzzle_flash()
make_wav("hit.wav", [(880, .07), (1320, .08)])
make_wav("hurt.wav", [(150, .10), (110, .16)], volume=0.28, noise=True)
make_wav("win.wav", [(523, .12), (659, .12), (784, .12), (1047, .25)])
make_wav("lose.wav", [(392, .14), (311, .14), (233, .3)])
make_wav("reload.wav", [(240, .05), (420, .05), (260, .06)], volume=0.2, noise=True)
print("placeholders listos")
