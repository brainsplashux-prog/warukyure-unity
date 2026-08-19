# art_final.png から art_final_v2.png を生成する（決定的・再実行可）
# 変更点: 下部BALL帯(y697..751)を除去して下を54px詰め、城下の余白に獲得BALL 2x2パネルを焼き込む
# 社長承認: 2026-08-19「CでOK」（BALLラベルなし・キャラ画像・拡大版）
from PIL import Image, ImageDraw

SRC = 'Assets/Resources/art_final.png'          # 読み取り専用。絶対に上書きしない
DST = 'Assets/Resources/art_final_v2.png'
CH  = '/Users/suzukimasahiro/Unity/kakekkodoubutsu/書き出し/%s.png'   # 正本=characters.md（メイン4体）
NAMES = ['01_A', '02_A', '03_A', '04_A']        # ballNames = うさぎ/ねこ/くま/ことり の並び

TOP, BOT = 697, 751                              # BALL帯の外枠実測
PX, PY, PW, PH = 460, 230, 130, 104              # パネル（右端590=外周マス左端608から18px / 上端=城の島下端195から35px）
PAD, GAP = 6, 4

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

base.save(DST)
print('wrote', DST, base.size)
