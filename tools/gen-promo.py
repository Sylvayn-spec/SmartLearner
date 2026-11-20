import math
import os
import subprocess
import wave

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

W, H = 1920, 1080
FPS = 30
DURATION = 30.0

CREAM = (246, 241, 231)
CARD = (255, 253, 248)
INK = (47, 42, 38)
SUBTLE = (138, 128, 120)
BROWN = (122, 46, 40)
GOLD = (185, 138, 60)
LINE = (231, 221, 205)

FONT_CN_BOLD = "C:/Windows/Fonts/msyhbd.ttc"
FONT_CN = "C:/Windows/Fonts/msyh.ttc"
FONT_EN = "C:/Windows/Fonts/georgiab.ttf"

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SHOTS = os.path.join(ROOT, "docs", "screenshots")
FRAMES = os.path.join(ROOT, "artifacts", "promo-frames")
OUT_DIR = os.path.join(ROOT, "dist")


def font(path, size):
    return ImageFont.truetype(path, size)


def make_background(width, height):
    sx = width / W
    sy = height / H
    y, x = np.mgrid[0:height, 0:width].astype(np.float32)
    base = np.zeros((height, width, 3), dtype=np.float32)
    base[:, :] = (250, 245, 236)

    def glow(cx, cy, radius, color, strength):
        d = np.sqrt((x - cx * sx) ** 2 + (y - cy * sy) ** 2) / (radius * sx)
        a = np.clip(1 - d, 0, 1) ** 2 * strength
        for c in range(3):
            base[:, :, c] = base[:, :, c] * (1 - a) + color[c] * a

    glow(1660, -60, 1250, (243, 231, 210), 0.55)
    glow(120, 1040, 1100, (245, 235, 217), 0.5)
    glow(980, 560, 1500, (252, 249, 242), 0.35)

    # 轻微暗角
    dx = (x - width / 2) / (width / 2)
    dy = (y - height / 2) / (height / 2)
    v = np.clip(1 - 0.06 * (dx ** 2 + dy ** 2), 0, 1)[:, :, None]
    base *= v

    return Image.fromarray(np.clip(base, 0, 255).astype(np.uint8), "RGB")


BG = make_background(W, H)
BG2X = make_background(W * 2, H * 2)


def rounded_mask(size, radius):
    mask = Image.new("L", size, 0)
    d = ImageDraw.Draw(mask)
    d.rounded_rectangle([0, 0, size[0] - 1, size[1] - 1], radius=radius, fill=255)
    return mask


def screenshot_card(name, card_w=2640, card_h=1534, radius=36):
    """在 2 倍画布上合成截图，供逐帧浮点裁剪，避免放大时像素级抖动。"""
    shot = Image.open(os.path.join(SHOTS, name)).convert("RGB")
    shot = shot.resize((card_w, card_h), Image.LANCZOS)
    mask = rounded_mask((card_w, card_h), radius)

    x = (W * 2 - card_w) // 2
    y = 128

    base = BG2X.copy()

    shadow = Image.new("RGBA", (W * 2, H * 2), (0, 0, 0, 0))
    sd = ImageDraw.Draw(shadow)
    sd.rounded_rectangle(
        [x - 20, y + 36, x + card_w + 20, y + card_h + 68],
        radius=radius + 16,
        fill=(80, 56, 32, 90),
    )
    shadow = shadow.filter(ImageFilter.GaussianBlur(52))
    base = Image.alpha_composite(base.convert("RGBA"), shadow).convert("RGB")

    base.paste(shot, (x, y), mask)
    return base


def text_layer(size, xy, text, fnt, fill, anchor="la", spacing=0):
    layer = Image.new("RGBA", size, (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    if spacing <= 0:
        d.text(xy, text, font=fnt, fill=fill, anchor=anchor)
        return layer

    total = sum(d.textlength(ch, font=fnt) + spacing for ch in text) - spacing
    x, y = xy
    if anchor.startswith("m"):
        x -= total / 2
    for ch in text:
        d.text((x, y), ch, font=fnt, fill=fill, anchor="l" + anchor[1])
        x += d.textlength(ch, font=fnt) + spacing
    return layer


def paste_layer(img, layer, alpha, offset_y=0):
    if alpha <= 0:
        return
    a = int(max(0, min(255, 255 * alpha)))
    if offset_y:
        shifted = Image.new("RGBA", layer.size, (0, 0, 0, 0))
        shifted.alpha_composite(layer, (0, offset_y))
        layer = shifted
    mask = layer.getchannel("A").point(lambda v: int(v * a / 255))
    img.paste(layer, (0, 0), mask)


def ease_out(p):
    p = max(0.0, min(1.0, p))
    return 1 - (1 - p) ** 3


def ease_in_out(p):
    p = max(0.0, min(1.0, p))
    return p * p * (3 - 2 * p)


# ---------- 场景基图与文字层 ----------

TITLE_TEXT_TOP = text_layer(
    (W, H), (W / 2, 360), "AI × 知识与探索", font(FONT_CN, 26), (150, 120, 80, 255), "ma", 6
)
TITLE_TEXT_MAIN = text_layer(
    (W, H), (W / 2, 470), "Smart Learner", font(FONT_EN, 132), INK + (255,), "ma"
)
TITLE_TEXT_SUB = text_layer(
    (W, H), (W / 2, 648), "把课程、笔记和问答，收进一张书桌", font(FONT_CN, 40), INK + (255,), "ma"
)
TITLE_TEXT_META = text_layer(
    (W, H), (W / 2, 736), "Windows 桌面应用 · 本地存储 · 知乎开放平台", font(FONT_CN, 23), SUBTLE + (255,), "ma"
)

PROBLEM_HEAD = text_layer(
    (W, H), (W / 2, 300), "我们每天都在经历", font(FONT_CN, 30), SUBTLE + (255,), "ma", 4
)
PROBLEM_LINES = [
    text_layer((W, H), (W / 2, 420), "课件散在群里、网盘和桌面", font(FONT_CN_BOLD, 51), INK + (255,), "ma"),
    text_layer((W, H), (W / 2, 520), "复习时找不到重点", font(FONT_CN_BOLD, 51), INK + (255,), "ma"),
    text_layer((W, H), (W / 2, 620), "有问题，却没人接着讲", font(FONT_CN_BOLD, 51), INK + (255,), "ma"),
]

END_MAIN = text_layer((W, H), (W / 2, 420), "Smart Learner", font(FONT_EN, 112), INK + (255,), "ma")
END_SLOGAN = text_layer((W, H), (W / 2, 570), "学而时习之", font(FONT_CN_BOLD, 46), BROWN + (255,), "ma", 8)
END_LINE = text_layer(
    (W, H), (W / 2, 660), "把知识读薄，把理解读厚", font(FONT_CN, 30), SUBTLE + (255,), "ma", 3
)
END_META = text_layer(
    (W, H), (W / 2, 742), "本地存储 · Windows 10 / 11 · 30 秒了解它", font(FONT_CN, 22), SUBTLE + (255,), "ma"
)

SHOT_SCENES = {
    "home": ("home-tiles.png", "01 · 一屏开始", "课程、热榜、关注", "打开就知道今天学什么", None),
    "course": ("course-folders.png", "02 · 资料归位", "Note 与 TextBook 分开放", "PDF、Markdown 直接预览", None),
    "organize": (
        "organize-fullscreen.png",
        "03 · 把课件读薄",
        "AI 整理成中文知识点",
        "一键导出 .md，回到课程里继续看",
        None,
    ),
    "assistant": (
        "assistant-normal.png",
        "04 · 有人接着讲",
        "书僮只依据你的资料回答",
        "还标注来源；问问知乎:[问题] 带回专业回答",
        None,
    ),
}


def build_shot_layers(key):
    name, kicker, title1, title2, _ = SHOT_SCENES[key]
    base = screenshot_card(name)
    k = text_layer((W, H), (300, 880), kicker, font(FONT_CN, 22), (170, 120, 60, 255), "la", 4)
    t1 = text_layer((W, H), (300, 922), title1, font(FONT_CN_BOLD, 42), INK + (255,), "la")
    t2 = text_layer((W, H), (300, 982), title2, font(FONT_CN, 30), SUBTLE + (255,), "la")
    return base, [(k, 0.0, 0), (t1, 0.08, 10), (t2, 0.16, 10)]


CACHE = {key: build_shot_layers(key) for key in SHOT_SCENES}


def render_title(local, duration):
    img = BG.copy().convert("RGBA")
    p = ease_out(local / 0.9)
    paste_layer(img, TITLE_TEXT_TOP, p)
    paste_layer(img, TITLE_TEXT_MAIN, ease_out((local - 0.12) / 0.9), int(18 * (1 - ease_out((local - 0.12) / 0.9))))
    paste_layer(img, TITLE_TEXT_SUB, ease_out((local - 0.35) / 0.9), int(12 * (1 - ease_out((local - 0.35) / 0.9))))
    paste_layer(img, TITLE_TEXT_META, ease_out((local - 0.55) / 0.9))
    return img.convert("RGB")


def render_problem(local, duration):
    img = BG.copy().convert("RGBA")
    paste_layer(img, PROBLEM_HEAD, ease_out(local / 0.6))
    for i, layer in enumerate(PROBLEM_LINES):
        start = 0.35 + i * 0.45
        a = ease_out((local - start) / 0.6)
        paste_layer(img, layer, a, int(14 * (1 - ease_out((local - start) / 0.6))))
    return img.convert("RGB")


def render_shot(key, local, duration):
    base, layers = CACHE[key]
    zoom = 1.0 + 0.035 * ease_in_out(local / duration)

    # 从 2 倍画布按浮点坐标裁剪再缩回 1080p，坐标连续变化，
    # 不会出现逐帧 1 像素跳变导致的抖动。
    crop_w = base.width / zoom
    crop_h = base.height / zoom
    scale = crop_w / W
    tx = base.width / 2 - crop_w / 2
    ty = base.height / 2 - crop_h / 2

    img = base.transform(
        (W, H),
        Image.AFFINE,
        (scale, 0, tx, 0, scale, ty),
        resample=Image.BICUBIC,
    ).convert("RGBA")

    for layer, delay, dy in layers:
        a = ease_out((local - delay) / 0.7)
        paste_layer(img, layer, a, int(dy * (1 - a)))
    return img.convert("RGB")


def render_end(local, duration):
    img = BG.copy().convert("RGBA")
    paste_layer(img, END_MAIN, ease_out(local / 0.8))
    paste_layer(img, END_SLOGAN, ease_out((local - 0.2) / 0.8))
    paste_layer(img, END_LINE, ease_out((local - 0.4) / 0.8))
    paste_layer(img, END_META, ease_out((local - 0.6) / 0.8))
    return img.convert("RGB")


SCENES = [
    ("title", 4.0, render_title),
    ("problem", 3.0, render_problem),
    ("home", 5.0, lambda l, d: render_shot("home", l, d)),
    ("course", 5.0, lambda l, d: render_shot("course", l, d)),
    ("organize", 5.0, lambda l, d: render_shot("organize", l, d)),
    ("assistant", 5.0, lambda l, d: render_shot("assistant", l, d)),
    ("end", 3.0, render_end),
]

STARTS = []
acc = 0.0
for _, duration, _ in SCENES:
    STARTS.append(acc)
    acc += duration

TRANSITION = 0.55


def scene_at(t):
    t = min(max(t, 0), DURATION - 1e-6)
    idx = 0
    for i, start in enumerate(STARTS):
        if t >= start:
            idx = i
    return idx, t - STARTS[idx]


def render_frame(t):
    idx, local = scene_at(t)
    _, duration, fn = SCENES[idx]
    img = fn(local, duration)

    if idx > 0 and local < TRANSITION:
        prev_idx = idx - 1
        prev_duration = SCENES[prev_idx][1]
        prev = SCENES[prev_idx][2](prev_duration, prev_duration)
        alpha = ease_in_out(local / TRANSITION)
        img = Image.blend(prev, img, alpha)

    return img


def make_audio(path):
    sr = 44100
    n = int(sr * DURATION)
    t = np.arange(n) / sr
    audio = np.zeros(n, dtype=np.float64)

    chords = [
        [261.63, 329.63, 392.00, 493.88],  # Cmaj7
        [220.00, 261.63, 329.63, 392.00],  # Am7
        [174.61, 220.00, 261.63, 329.63],  # Fmaj7
        [196.00, 246.94, 293.66, 349.23],  # G7
    ]
    seg = DURATION / len(chords)
    for i, chord in enumerate(chords):
        start = i * seg
        end = start + seg
        env = np.clip((t - start) / 2.0, 0, 1) * np.clip((end + 0.8 - t) / 1.6, 0, 1)
        env = np.clip(env, 0, 1)
        for f in chord:
            for detune, amp in ((0.0, 1.0), (0.6, 0.55), (-0.5, 0.45)):
                audio += env * amp * 0.035 * np.sin(2 * np.pi * (f + detune) * t)
                audio += env * amp * 0.012 * np.sin(2 * np.pi * (f * 2 + detune) * t)

    fade = np.clip(t / 2.5, 0, 1) * np.clip((DURATION - t) / 3.0, 0, 1)
    audio *= fade
    audio = audio / max(1e-9, np.max(np.abs(audio))) * 0.22

    stereo = np.stack([audio, np.roll(audio, 37) * 0.96], axis=1)
    pcm = (stereo * 32767).astype(np.int16)
    with wave.open(path, "wb") as f:
        f.setnchannels(2)
        f.setsampwidth(2)
        f.setframerate(sr)
        f.writeframes(pcm.tobytes())


def main():
    os.makedirs(FRAMES, exist_ok=True)
    os.makedirs(OUT_DIR, exist_ok=True)

    total = int(DURATION * FPS)
    for i in range(total):
        t = i / FPS
        frame = render_frame(t)
        frame.save(os.path.join(FRAMES, f"f_{i:04d}.png"))
        if i % 90 == 0:
            print(f"frame {i}/{total}", flush=True)

    audio_path = os.path.join(OUT_DIR, "promo-pad.wav")
    make_audio(audio_path)
    print("audio ready", flush=True)


if __name__ == "__main__":
    main()
