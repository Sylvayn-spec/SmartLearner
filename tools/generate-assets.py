"""生成 Smart Learner 的装饰素材：窗边茉莉插画 + 纸纹。

全部程序化绘制，无外部素材依赖。
用法：python tools/generate-assets.py
"""
import math
import random
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter

OUT = Path(__file__).resolve().parent.parent / "src" / "SmartLearner.App" / "Assets"
OUT.mkdir(parents=True, exist_ok=True)

S = 2  # 超采样


def lerp(a, b, t):
    return a + (b - a) * t


def bezier(p0, p1, p2, p3, t):
    u = 1 - t
    return (
        u**3 * p0[0] + 3 * u * u * t * p1[0] + 3 * u * t * t * p2[0] + t**3 * p3[0],
        u**3 * p0[1] + 3 * u * u * t * p1[1] + 3 * u * t * t * p2[1] + t**3 * p3[1],
    )


def tangent(p0, p1, p2, p3, t):
    u = 1 - t
    dx = 3 * u * u * (p1[0] - p0[0]) + 6 * u * t * (p2[0] - p1[0]) + 3 * t * t * (p3[0] - p2[0])
    dy = 3 * u * u * (p1[1] - p0[1]) + 6 * u * t * (p2[1] - p1[1]) + 3 * t * t * (p3[1] - p2[1])
    n = math.hypot(dx, dy) or 1
    return dx / n, dy / n


def leaf_polygon(cx, cy, angle, length, width, bend=0.16):
    """以 (cx,cy) 为叶柄，沿 angle 方向生成对称叶片（未缩放坐标）。"""
    ax, ay = math.cos(angle), math.sin(angle)
    nx, ny = -ay, ax
    pts = []
    steps = 30
    for i in range(steps + 1):
        t = i / steps
        w = width * (math.sin(math.pi * t) ** 0.7)
        off = bend * length * math.sin(math.pi * t)
        pts.append((cx + ax * length * t + nx * (off + w),
                    cy + ay * length * t + ny * (off + w)))
    for i in range(steps, -1, -1):
        t = i / steps
        w = width * (math.sin(math.pi * t) ** 0.7)
        off = bend * length * math.sin(math.pi * t)
        pts.append((cx + ax * length * t + nx * (off - w),
                    cy + ay * length * t + ny * (off - w)))
    return [(x * S, y * S) for x, y in pts]


def draw_leaf(d, cx, cy, angle, length, width, fill, vein):
    d.polygon(leaf_polygon(cx, cy, angle, length, width), fill=fill)
    ax, ay = math.cos(angle), math.sin(angle)
    d.line(
        [(cx * S, cy * S), ((cx + ax * length * 0.92) * S, (cy + ay * length * 0.92) * S)],
        fill=vein,
        width=max(1, int(1.1 * S)),
    )


def draw_branch(d, p0, p1, p2, p3, w0, w1):
    steps = 260
    for i in range(steps):
        t = i / steps
        x, y = bezier(p0, p1, p2, p3, t)
        r = lerp(w0, w1, t) * S * 0.5
        d.ellipse([x * S - r, y * S - r, x * S + r, y * S + r], fill=(78, 70, 54, 255))


def draw_flower(d, cx, cy, size, tilt=0.0):
    """五瓣茉莉，圆形花瓣 + 金色花心。"""
    cx, cy = cx * S, cy * S
    pr = size * 0.40 * S
    dist = size * 0.42 * S
    for i in range(5):
        a = -math.pi / 2 + tilt + i * 2 * math.pi / 5
        px = cx + math.cos(a) * dist
        py = cy + math.sin(a) * dist
        d.ellipse(
            [px - pr, py - pr, px + pr, py + pr],
            fill=(255, 253, 244, 250),
            outline=(208, 200, 176, 230),
            width=max(1, int(1.1 * S)),
        )
    r1 = size * 0.17 * S
    d.ellipse([cx - r1, cy - r1, cx + r1, cy + r1], fill=(226, 187, 101, 255))
    r2 = size * 0.09 * S
    d.ellipse([cx - r2, cy - r2, cx + r2, cy + r2], fill=(246, 224, 158, 255))


def draw_bud(d, cx, cy, angle, size):
    ax, ay = math.cos(angle), math.sin(angle)
    px, py = cx * S, cy * S
    d.ellipse(
        [px - size * S * 0.42, py - size * S * 0.62, px + size * S * 0.42, py + size * S * 0.62],
        fill=(247, 249, 230, 245),
        outline=(196, 206, 174, 230),
        width=max(1, int(1.2 * S)),
    )
    d.line(
        [(px, py + size * S * 0.55), (px + ax * size * S * 0.9, py + ay * size * S * 0.9)],
        fill=(116, 142, 100, 240),
        width=max(1, int(1.8 * S)),
    )


def build_hero():
    w, h = 1000, 560
    img = Image.new("RGBA", (w * S, h * S), (0, 0, 0, 0))

    # 柔光背景：径向渐变，保证在画布边缘自然衰减到 0
    import numpy as np

    yy, xx = np.mgrid[0:h * S, 0:w * S]
    dist = np.sqrt((xx - 780 * S) ** 2 + (yy - 290 * S) ** 2)
    alpha = np.clip(1 - dist / (420 * S), 0, 1) ** 2 * 62
    glow_arr = np.zeros((h * S, w * S, 4), dtype=np.uint8)
    glow_arr[..., 0] = 210
    glow_arr[..., 1] = 229
    glow_arr[..., 2] = 196
    glow_arr[..., 3] = alpha.astype(np.uint8)
    glow = Image.fromarray(glow_arr, "RGBA").filter(ImageFilter.GaussianBlur(18 * S))
    img.alpha_composite(glow)

    d = ImageDraw.Draw(img)

    # 主枝：右下 -> 左上
    p0 = (1040, 560)
    p1 = (900, 400)
    p2 = (760, 210)
    p3 = (585, 78)
    draw_branch(d, p0, p1, p2, p3, 7.5, 2.2)

    # 两条侧枝
    t1 = bezier(p0, p1, p2, p3, 0.46)
    draw_branch(d, t1, (t1[0] - 40, t1[1] - 60), (t1[0] - 120, t1[1] - 80), (t1[0] - 185, t1[1] - 62), 4.2, 1.6)
    t2 = bezier(p0, p1, p2, p3, 0.72)
    draw_branch(d, t2, (t2[0] + 30, t2[1] - 55), (t2[0] + 90, t2[1] - 85), (t2[0] + 150, t2[1] - 78), 4.0, 1.4)

    leaf_colors = [
        (128, 156, 112, 240),
        (104, 136, 92, 240),
        (146, 172, 128, 225),
        (88, 118, 78, 240),
    ]
    vein = (208, 222, 192, 130)

    # 主枝叶片
    count = 12
    for i in range(count):
        t = 0.10 + i * (0.82 / (count - 1))
        x, y = bezier(p0, p1, p2, p3, t)
        tx, ty = tangent(p0, p1, p2, p3, t)
        base = math.atan2(ty, tx)
        side = 1 if i % 2 == 0 else -1
        angle = base + side * math.radians(62)
        length = lerp(98, 54, t)
        draw_leaf(d, x, y, angle, length, length * 0.28, leaf_colors[i % 4], vein)

    # 侧枝叶片
    for i in range(4):
        t = 0.18 + i * 0.2
        x, y = bezier(t1, (t1[0] - 40, t1[1] - 60), (t1[0] - 120, t1[1] - 80), (t1[0] - 185, t1[1] - 62), t)
        draw_leaf(d, x, y, math.radians(148 + i * 10), 56, 16, leaf_colors[(i + 1) % 4], vein)
    for i in range(3):
        t = 0.25 + i * 0.25
        x, y = bezier(t2, (t2[0] + 30, t2[1] - 55), (t2[0] + 90, t2[1] - 85), (t2[0] + 150, t2[1] - 78), t)
        draw_leaf(d, x, y, math.radians(-72 - i * 14), 50, 15, leaf_colors[(i + 2) % 4], vein)

    # 花朵下方的影子，让白花从米色底上浮起来
    shadow = Image.new("RGBA", (w * S, h * S), (0, 0, 0, 0))
    sd = ImageDraw.Draw(shadow)
    for (fx, fy, fs) in ((742, 300, 52), (840, 196, 42), (650, 172, 36)):
        sd.ellipse(
            [(fx - fs * 0.8) * S, (fy + fs * 0.5) * S, (fx + fs * 0.8) * S, (fy + fs * 1.05) * S],
            fill=(96, 84, 62, 55),
        )
    shadow = shadow.filter(ImageFilter.GaussianBlur(9 * S))
    img.alpha_composite(shadow)
    d = ImageDraw.Draw(img)

    draw_flower(d, 742, 300, 52, tilt=0.1)
    draw_flower(d, 840, 196, 42, tilt=0.7)
    draw_flower(d, 650, 172, 36, tilt=-0.4)
    draw_bud(d, 880, 330, math.radians(70), 20)
    draw_bud(d, 600, 250, math.radians(-30), 17)

    # 光点
    random.seed(7)
    for _ in range(30):
        x = random.uniform(500, 990)
        y = random.uniform(60, 500)
        r = random.uniform(1.0, 2.4) * S
        d.ellipse([x * S - r, y * S - r, x * S + r, y * S + r],
                  fill=(255, 255, 250, random.randint(45, 120)))

    img = img.resize((w, h), Image.LANCZOS)
    out = OUT / "hero-botanical.png"
    img.save(out)
    return out


def build_paper():
    w, h = 256, 256
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    random.seed(11)
    px = img.load()
    for y in range(h):
        for x in range(w):
            v = random.randint(0, 255)
            if v > 150:
                px[x, y] = (255, 252, 240, random.randint(5, 13))
            else:
                px[x, y] = (92, 78, 60, random.randint(3, 10))
    img = img.filter(ImageFilter.GaussianBlur(0.5))
    out = OUT / "paper-grain.png"
    img.save(out)
    return out


if __name__ == "__main__":
    print(build_hero())
    print(build_paper())
