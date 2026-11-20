from PIL import Image, ImageDraw

BG = (122, 46, 40, 255)
PAGE = (243, 236, 221, 255)
LINE = (122, 46, 40, 255)


def render(size):
    s = size
    scale = 4
    big = s * scale
    img = Image.new("RGBA", (big, big), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    radius = int(big * 0.22)
    d.rounded_rectangle([2, 2, big - 3, big - 3], radius=radius, fill=BG)

    left = [(0.20, 0.30), (0.475, 0.25), (0.475, 0.73), (0.20, 0.78)]
    right = [(0.80, 0.30), (0.525, 0.25), (0.525, 0.73), (0.80, 0.78)]
    for poly in (left, right):
        d.polygon([(x * big, y * big) for x, y in poly], fill=PAGE)

    # spine
    d.line([(0.5 * big, 0.26 * big), (0.5 * big, 0.75 * big)], fill=BG, width=max(2, int(big * 0.018)))

    # text lines
    width = max(2, int(big * 0.016))
    for i, y in enumerate((0.40, 0.48, 0.56, 0.64)):
        d.line([(0.25 * big, y * big), (0.43 * big, (y - 0.01) * big)], fill=BG, width=width)
        d.line([(0.57 * big, (y - 0.01) * big), (0.75 * big, y * big)], fill=BG, width=width)

    return img.resize((s, s), Image.LANCZOS)


sizes = [16, 24, 32, 48, 64, 128, 256]
images = [render(s) for s in sizes]
images[-1].save(
    "src/SmartLearner.App/Assets/app.ico",
    format="ICO",
    sizes=[(s, s) for s in sizes],
)
render(256).save("src/SmartLearner.App/Assets/app.png")
print("icon written")
