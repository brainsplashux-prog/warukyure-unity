# -*- coding: utf-8 -*-
"""下部ボタン帯を縦に拡張して焼き直す（C案: 枠拡張＋BET文字を中央へ）。

- 入力(SRC)は「拡張前」の art_final_v2.png。冪等ではないので必ず未拡張の版を渡すこと。
- 帯 y=697..764 の下にある完全な余白 y=765..818（54行）を食って、帯を y=697..818 にする。
- BETピルは y=725 の無地行を複製して伸ばす（飾り・文字を潰さない唯一の行）。
- SPINは伸ばせる無地行が無いため、元スプライトを 1.25 倍して貼り直す。
使い方: python3 tools/bake_bottombar_v4.py <SRC.png> <DST.png>
"""
import sys
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

SRC, DST = sys.argv[1], sys.argv[2]
Y0, Y1, ADD = 697, 765, 54
BETX = [(26, 99), (129, 202), (232, 305), (335, 408), (438, 511)]
INSET = 12
G1, G2 = (714, 722), (728, 742)      # "BET n" / "100枚" の行範囲
SP_BOX = (552, 700, 688, 754)        # SPINスプライトの切り出し
SP_SCALE = 1.25
CX0, CX1 = 520, 706                  # SPINセル（貼り直しで塗り潰す範囲）

im = Image.open(SRC).convert('RGBA')
alpha = np.asarray(im)[..., 3].copy()
src = im.convert('RGB')
a = np.asarray(src).astype(np.uint8)
assert a.shape[0] == 819 and a.shape[1] == 720, a.shape

# --- 1) 帯を縦に伸ばす（y=725 の行を複製） ---
bar = np.concatenate([a[Y0:725], np.repeat(a[725][None], ADD + 1, 0), a[726:Y1]], 0)

# --- 2) BETの文字を新しい枠の縦中央へ移す ---
top, bot = 706 - Y0, 747 - Y0 + ADD
cy = (top + bot) // 2
for x0, x1 in BETX:
    xa, xb = x0 + INSET, x1 - INSET + 1
    for (ga, gb), off in ((G1, 0), (G2, ADD)):          # 旧位置を行中央値で消す
        for y in range(ga - Y0 + off, gb - Y0 + off):
            bar[y, xa:xb] = np.median(bar[y, xa:xb], axis=0).round().astype(np.uint8)
    blk = a[G1[0]:G2[1], xa:xb].astype(float)
    med = np.median(blk, axis=1, keepdims=True)          # 行ごとの地色
    dark = np.clip((med.mean(2) - blk.mean(2)) / 26.0, 0, 1)[..., None]
    h = blk.shape[0]
    d0 = cy - h // 2
    reg = bar[d0:d0 + h, xa:xb].astype(float)
    bar[d0:d0 + h, xa:xb] = (reg * (1 - dark) + blk * dark).round().astype(np.uint8)

# --- 3) SPINを 1.25 倍で貼り直す ---
sp0 = src.crop(SP_BOX)
w0, h0 = sp0.size
mk0 = Image.new('L', (w0, h0), 0)
ImageDraw.Draw(mk0).rounded_rectangle([1, 1, w0 - 2, h0 - 2], radius=h0 // 2 - 1, fill=255)
mk0 = mk0.filter(ImageFilter.GaussianBlur(0.8))
bar[:, CX0:CX1] = bar[:, 528:529].repeat(CX1 - CX0, 1)
w, h = round(w0 * SP_SCALE), round(h0 * SP_SCALE)
f = np.asarray(sp0.resize((w, h), Image.LANCZOS)).astype(float)
al = (np.asarray(mk0.resize((w, h), Image.LANCZOS)).astype(float) / 255)[..., None]
scy = (4 + (752 - Y0 + ADD)) // 2
sy0, sx0 = scy - h // 2, (CX0 + CX1) // 2 - w // 2
reg = bar[sy0:sy0 + h, sx0:sx0 + w].astype(float)
bar[sy0:sy0 + h, sx0:sx0 + w] = (f * al + reg * (1 - al)).round().astype(np.uint8)

assert bar.shape[0] == 819 - Y0, bar.shape
out = a.copy()
out[Y0:] = bar
res = Image.fromarray(out).convert('RGBA')
res.putalpha(Image.fromarray(alpha))
res.save(DST)

# --- 4) Unity 側で使う実測値を出す ---
GOLD = lambda p: (p[:, :, 0] > 150) & (p[:, :, 1] > 110) & (p[:, :, 2] < 120)
g = GOLD(out[Y0:, 16:112])
ys, xs = np.nonzero(g)
print('BET1 frame  texture x=%d..%d  y=%d..%d' % (16 + xs.min(), 16 + xs.max(), Y0 + ys.min(), Y0 + ys.max()))
red = (out[:, :, 0] > 120) & (out[:, :, 1] < 90) & (out[:, :, 2] < 90)
red[:Y0] = False
ys, xs = np.nonzero(red)
print('SPIN red    texture x=%d..%d  y=%d..%d' % (xs.min(), xs.max(), ys.min(), ys.max()))
print('wrote', DST)
