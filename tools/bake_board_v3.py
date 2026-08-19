# art_final.png から art_final_v2.png を生成する（決定的・再実行可）
# v2 からの変更点: BALLマス4個を「キャラ顔メダル」に差し替える
#   - 旧BALL絵（?ボール＋×4/×2バッジ）を、枠のすぐ外側の背景画素で消す
#   - 通常マスと同一寸法（外周34x34 / ring4 36x36 / loop2 26x29 ＝実測）のメダルを置く
#   - どのマスに止まればどのキャラが増えるかが見ただけで分かる（ballType固定表とセット）
# 社長承認: 2026-08-19「OK」（マス寸法=通常マスと一致・顔は内側4pxに縮小）
from PIL import Image, ImageDraw
import numpy as np

SRC = 'Assets/Resources/art_final.png'          # 読み取り専用。絶対に上書きしない
DST = 'Assets/Resources/art_final_v2.png'
CH  = '/Users/suzukimasahiro/Unity/kakekkodoubutsu/書き出し/%s.png'   # 正本=characters.md（メイン4体）
NAMES = ['01_A', '02_A', '03_A', '04_A']        # ballNames の並びと一致（index0..3）

TOP, BOT = 697, 751                              # BALL帯の外枠実測
PX, PY, PW, PH = 460, 230, 130, 104              # 獲得BALLパネル
PAD, GAP = 6, 4

# ---- BALLマス（キャラ顔メダル）------------------------------------------------
# マスの正寸＝通常マスのクリーム地の実測値（WarukyureBoard.CellSizeForTrack と一致）
CELL = {'outer': (34, 34), 'ring4': (36, 36), 'loop2': (26, 29)}

# cellId, track, canvasX, canvasY, ballType, 地色, 旧BALL絵の外形(テクスチャ座標 x0,y0,x1,y1)
BALLS = [
    ('ball_o1', 'outer', 247, 452, 0, (255, 190, 202), (227, 29, 266, 65)),
    ('ball_o2', 'outer', 516, 452, 1, (176, 212, 255), (496, 29, 535, 65)),
    ('ball_i1', 'ring4', 279, 684, 2, (176, 238, 182), (259, 258, 299, 299)),
    ('ball_m1', 'loop2', 360, 806, 3, (255, 224, 140), (344, 385, 375, 417)),
]

# キャラごとの顔の切り出し範囲（アルファbbox比 x0,y0,x1,y1）。顎・耳・帽子を切らない位置を実測
HEAD = {
    '01_A': (0.00, 0.00, 1.00, 0.72),
    '02_A': (0.00, 0.00, 1.00, 0.70),
    '03_A': (0.00, 0.00, 1.00, 0.70),
    '04_A': (0.00, 0.00, 1.00, 0.68),
}


def head(name, w, h):
    im = Image.open(CH % name).convert('RGBA')
    im = im.crop(im.split()[3].getbbox())
    iw, ih = im.size
    x0, y0, x1, y1 = HEAD[name]
    im = im.crop((int(iw * x0), int(ih * y0), int(iw * x1), int(ih * y1)))
    im = im.crop(im.split()[3].getbbox())
    s = min(w / im.width, h / im.height)
    return im.resize((max(1, round(im.width * s)), max(1, round(im.height * s))), Image.LANCZOS)


def erase_ring(arr, old, med):
    """旧BALL絵のうちメダルが覆わない外周だけを、枠のすぐ外側の背景画素で埋める"""
    ox0, oy0, ox1, oy1 = old
    mx0, my0, mx1, my1 = med
    for y in range(oy0, oy1 + 1):
        for x in range(ox0, ox1 + 1):
            if mx0 <= x <= mx1 and my0 <= y <= my1:
                continue
            dl, dr, du, dd = x - ox0, ox1 - x, y - oy0, oy1 - y
            m = min(dl, dr, du, dd)
            if m == du:
                arr[y, x] = arr[oy0 - 1, x]
            elif m == dd:
                arr[y, x] = arr[oy1 + 1, x]
            elif m == dl:
                arr[y, x] = arr[y, ox0 - 1]
            else:
                arr[y, x] = arr[y, ox1 + 1]


def draw_ball_cells(base, cut):
    """base は v2 の詰め処理後。canvasY -> テクスチャy は (canvasY - 405)。"""
    arr = np.asarray(base).copy()
    meds = []
    for cid, track, cx, cy, bt, col, old in BALLS:
        ty = cy - 405
        w, h = CELL[track]
        med = (cx - w // 2, ty - h // 2, cx - w // 2 + w - 1, ty - h // 2 + h - 1)
        erase_ring(arr, old, med)
        meds.append((cx, ty, med, NAMES[bt], col))
    out = Image.fromarray(arr)
    d = ImageDraw.Draw(out)
    for cx, ty, med, name, col in meds:
        d.rounded_rectangle(list(med), radius=5, fill=col + (255,),
                            outline=(150, 96, 24, 255), width=2)
        w = med[2] - med[0] + 1
        h = med[3] - med[1] + 1
        f = head(name, w - 8, h - 8)
        out.alpha_composite(f, (int(cx - f.width / 2), int(ty - f.height / 2)))
    return out


src = Image.open(SRC).convert('RGBA')
W, H = src.size
assert (W, H) == (720, 819), (W, H)
cut = BOT - TOP

base = Image.new('RGBA', (W, H), (0, 0, 0, 0))
base.paste(src.crop((0, 0, W, TOP)), (0, 0))
base.paste(src.crop((0, BOT, W, H)), (0, TOP))
fill = src.getpixel((10, BOT + 5))
d = ImageDraw.Draw(base)
d.rectangle([0, H - cut, W, H], fill=fill)       # 詰めた分の下端を地色で埋める

d.rounded_rectangle([PX, PY, PX + PW, PY + PH], radius=9,
                    fill=(252, 235, 190, 225), outline=(196, 138, 44, 255), width=3)

cw = (PW - PAD * 2 - GAP) // 2
ch = (PH - PAD * 2 - GAP) // 2
for i, n in enumerate(NAMES):
    im = Image.open(CH % n).convert('RGBA')
    im = im.crop(im.split()[3].getbbox())
    s = min(cw / im.width, ch / im.height)
    im = im.resize((max(1, int(im.width * s)), max(1, int(im.height * s))), Image.LANCZOS)
    cx = PX + PAD + (i % 2) * (cw + GAP) + cw // 2
    cy = PY + PAD + (i // 2) * (ch + GAP) + ch // 2
    base.alpha_composite(im, (cx - im.width // 2, cy - im.height // 2))

base = draw_ball_cells(base, cut)
base.save(DST)
print('wrote', DST, base.size)
