# warukyure / jackpot — 実装済み画面の現状 (design-state/v1)

captured_commit: b4b791f   captured_at: 2026-09-04T06:33:54Z   base: 720x1280
image: state.png (sha256 c96922402c99e90479ccb1c39927e28bbbf4ed1cbd458ef1d4f9b6fde63973b0)   state_sha256: 405bf26cf1bcdcb02813048115b762ef052f2bddfe000ab7a6163b5b7a2505ff

![annotated](state_annotated.png)

## 🛑 固定UI（間違えやすい枠）

| 種別 | id | x | y | w | h | visible | 規約値 | 判定 |
|---|---|---:|---:|---:|---:|---:|---|---|
| ヘッダーA | header_a | 0 | -56 | 720 | 56 | true | x=0 y=-56 w=720 h=56 | 一致 |
| ADVIRTUA | advirtua | 0 | 0 | 720 | 405 | true | x=0 y=0 w=720 h=405 | 一致 |
| ヘッダーB | なし | - | - | - | - | - | x=0 y=0 w=720 h=200 | なし(規約通り) |
| 広告フッター | なし | - | - | - | - | - | x=0 y=1180 w=720 h=100 | なし(規約通り) |

| id | role | x | y | w | h | z | text | desc |
|---|---:|---:|---:|---:|---:|---:|---|---|
| canvas | panel | 0 | 0 | 720 | 1280 | 0 |  | Canvas root |
| board | image | 0 | 405 | 720 | 819 | 1 |  | ボード（盤面） |
| collectionball0 | image | 473 | 642 | 42 | 42 | 2 |  |  |
| collectionball1 | image | 536 | 642 | 42 | 42 | 3 |  |  |
| collectionball2 | image | 473 | 690 | 42 | 42 | 4 |  |  |
| collectionball3 | image | 536 | 690 | 42 | 42 | 5 |  |  |
| dim_ball_o1 | image | 230 | 435 | 34 | 34 | 7 |  | 消灯マス |
| dim_o_03 | image | 284 | 435 | 34 | 34 | 8 |  | 消灯マス |
| dim_o_04 | image | 337 | 435 | 34 | 34 | 9 |  | 消灯マス |
| dim_o_05 | image | 391 | 435 | 34 | 34 | 10 |  | 消灯マス |
| dim_o_06 | image | 445 | 435 | 34 | 34 | 11 |  | 消灯マス |
| dim_ball_o2 | image | 499 | 435 | 34 | 34 | 12 |  | 消灯マス |
| dim_o_08 | image | 552 | 437 | 34 | 34 | 13 |  | 消灯マス |
| dim_o_09 | image | 598 | 467 | 34 | 34 | 14 |  | 消灯マス |
| dim_o_10 | image | 608 | 518 | 34 | 34 | 15 |  | 消灯マス |
| dim_o_11 | image | 608 | 572 | 34 | 34 | 16 |  | 消灯マス |
| dim_o_12 | image | 608 | 626 | 34 | 34 | 17 |  | 消灯マス |
| dim_o_13 | image | 608 | 680 | 34 | 34 | 18 |  | 消灯マス |
| dim_o_14 | image | 608 | 733 | 34 | 34 | 19 |  | 消灯マス |
| dim_o_15 | image | 608 | 787 | 34 | 34 | 20 |  | 消灯マス |
| dim_ship_r1 | image | 608 | 841 | 34 | 34 | 21 |  | 消灯マス |
| dim_o_17 | image | 608 | 895 | 34 | 34 | 22 |  | 消灯マス |
| dim_o_18 | image | 608 | 949 | 34 | 34 | 23 |  | 消灯マス |
| dim_o_19 | image | 608 | 1001 | 34 | 34 | 24 |  | 消灯マス |
| dim_o_20 | image | 562 | 1037 | 34 | 34 | 25 |  | 消灯マス |
| dim_o_21 | image | 509 | 1042 | 34 | 34 | 26 |  | 消灯マス |
| dim_o_22 | image | 455 | 1042 | 34 | 34 | 27 |  | 消灯マス |
| dim_o_23 | image | 401 | 1042 | 34 | 34 | 28 |  | 消灯マス |
| dim_o_24 | image | 348 | 1042 | 34 | 34 | 29 |  | 消灯マス |
| dim_o_25 | image | 294 | 1042 | 34 | 34 | 30 |  | 消灯マス |
| dim_o_26 | image | 240 | 1042 | 34 | 34 | 31 |  | 消灯マス |
| dim_o_27 | image | 186 | 1042 | 34 | 34 | 32 |  | 消灯マス |
| dim_o_28 | image | 133 | 1040 | 34 | 34 | 33 |  | 消灯マス |
| dim_o_29 | image | 87 | 1010 | 34 | 34 | 34 |  | 消灯マス |
| dim_o_30 | image | 77 | 959 | 34 | 34 | 35 |  | 消灯マス |
| dim_o_31 | image | 77 | 905 | 34 | 34 | 36 |  | 消灯マス |
| dim_ship_l1 | image | 77 | 851 | 34 | 34 | 37 |  | 消灯マス |
| dim_o_33 | image | 77 | 797 | 34 | 34 | 38 |  | 消灯マス |
| dim_o_34 | image | 77 | 744 | 34 | 34 | 39 |  | 消灯マス |
| dim_o_35 | image | 77 | 690 | 34 | 34 | 40 |  | 消灯マス |
| dim_o_36 | image | 77 | 636 | 34 | 34 | 41 |  | 消灯マス |
| dim_o_37 | image | 77 | 582 | 34 | 34 | 42 |  | 消灯マス |
| dim_o_38 | image | 77 | 528 | 34 | 34 | 43 |  | 消灯マス |
| dim_o_39 | image | 77 | 476 | 34 | 34 | 44 |  | 消灯マス |
| dim_o_40 | image | 123 | 440 | 34 | 34 | 45 |  | 消灯マス |
| dim_i_01 | image | 261 | 506 | 36 | 36 | 46 |  | 消灯マス |
| dim_i_07 | image | 204 | 529 | 36 | 36 | 47 |  | 消灯マス |
| dim_i_05 | image | 181 | 586 | 36 | 36 | 48 |  | 消灯マス |
| dim_i_04 | image | 204 | 643 | 36 | 36 | 49 |  | 消灯マス |
| dim_ball_i1 | image | 261 | 666 | 36 | 36 | 50 |  | 消灯マス |
| dim_i_02 | image | 318 | 643 | 36 | 36 | 51 |  | 消灯マス |
| dim_i_06 | image | 341 | 586 | 36 | 36 | 52 |  | 消灯マス |
| dim_key | image | 318 | 529 | 36 | 36 | 53 |  | 消灯マス |
| dim_m_00 | image | 195 | 791 | 26 | 30 | 54 |  | 消灯マス |
| dim_m_01 | image | 233 | 791 | 26 | 30 | 55 |  | 消灯マス |
| dim_m_02 | image | 271 | 791 | 26 | 30 | 56 |  | 消灯マス |
| dim_m_03 | image | 309 | 791 | 26 | 30 | 57 |  | 消灯マス |
| dim_ball_m1 | image | 347 | 791 | 26 | 30 | 58 |  | 消灯マス |
| dim_m_05 | image | 385 | 791 | 26 | 30 | 59 |  | 消灯マス |
| dim_m_06 | image | 423 | 791 | 26 | 30 | 60 |  | 消灯マス |
| dim_m_07 | image | 461 | 791 | 26 | 30 | 61 |  | 消灯マス |
| dim_m_08 | image | 499 | 791 | 26 | 30 | 62 |  | 消灯マス |
| dim_m_09 | image | 499 | 830 | 26 | 28 | 63 |  | 消灯マス |
| dim_m_10 | image | 499 | 867 | 26 | 29 | 64 |  | 消灯マス |
| dim_m_11 | image | 499 | 906 | 26 | 29 | 65 |  | 消灯マス |
| dim_m_12 | image | 499 | 943 | 26 | 29 | 66 |  | 消灯マス |
| dim_m_13 | image | 461 | 943 | 26 | 29 | 67 |  | 消灯マス |
| dim_m_14 | image | 423 | 943 | 26 | 29 | 68 |  | 消灯マス |
| dim_m_15 | image | 385 | 943 | 26 | 29 | 69 |  | 消灯マス |
| dim_m_16 | image | 347 | 943 | 26 | 29 | 70 |  | 消灯マス |
| dim_m_17 | image | 309 | 943 | 26 | 29 | 71 |  | 消灯マス |
| dim_m_18 | image | 271 | 943 | 26 | 29 | 72 |  | 消灯マス |
| dim_m_19 | image | 233 | 943 | 26 | 29 | 73 |  | 消灯マス |
| dim_m_20 | image | 195 | 943 | 26 | 29 | 74 |  | 消灯マス |
| dim_m_21 | image | 195 | 906 | 26 | 29 | 75 |  | 消灯マス |
| dim_m_22 | image | 195 | 867 | 26 | 29 | 76 |  | 消灯マス |
| dim_m_23 | image | 195 | 830 | 26 | 28 | 77 |  | 消灯マス |
| adprlabelplate | image | 18 | 423 | 56 | 28 | 79 |  |  |
| adprlabeltext | text | 18 | 423 | 56 | 28 | 80 | PR |  |
| wallettextband | image | 0 | 1056 | 720 | 48 | 81 |  | 残高帯 |
| helpbutton | button | 660 | 1060 | 32 | 32 | 82 |  | ヘルプボタン |
| jackpotpanel | image | 0 | 405 | 720 | 819 | 97 |  | JACKPOTチャレンジパネル |
| jpindicator | image | 55 | 784 | 60 | 60 | 108 |  |  |
| jpawardtext | text | 60 | 605 | 600 | 60 | 109 |  |  |
| soundmutebutton | image | 602 | 423 | 100 | 100 | 110 |  | サウンドミュートボタン |
| speakericon | image | 617 | 438 | 70 | 70 | 111 |  |  |
| wallettext | text | 0 | 1062 | 720 | 36 | 113 | コスト 200 / 純益 +29800 / 残高 42,145 | 残高テキスト |
| advirtua | panel | 0 | 0 | 720 | 405 | 114 |  | Ad-Virtua領域 |
| lampannouncer | panel | 0 | 0 | 720 | 1280 | 115 |  | JACKPOTランプ演出 |
| panel | image | 40 | 778 | 640 | 201 | 116 |  | JACKPOTランプパネル |
| sidering0 | image | 72 | 824 | 85 | 85 | 117 |  |  |
| sidering1 | image | 195 | 824 | 85 | 85 | 118 |  |  |
| sidering3 | image | 441 | 824 | 85 | 85 | 119 |  |  |
| sidering4 | image | 564 | 824 | 85 | 85 | 120 |  |  |
| centerbezel | image | 292 | 798 | 136 | 136 | 121 |  |  |
| lampmask0 | image | 76 | 828 | 76 | 76 | 122 |  |  |
| lamp0 | image | 74 | 826 | 80 | 80 | 123 |  |  |
| lampmask1 | image | 199 | 828 | 76 | 76 | 124 |  |  |
| lamp1 | image | 197 | 826 | 80 | 80 | 125 |  |  |
| lampmask2 | image | 322 | 828 | 76 | 76 | 126 |  |  |
| lamp2 | image | 320 | 826 | 80 | 80 | 127 |  |  |
| lampmask3 | image | 445 | 828 | 76 | 76 | 128 |  |  |
| lamp3 | image | 443 | 826 | 80 | 80 | 129 |  |  |
| lampmask4 | image | 568 | 828 | 76 | 76 | 130 |  |  |
| lamp4 | image | 566 | 826 | 80 | 80 | 131 |  |  |
| jackpotplate | image | 309 | 927 | 104 | 32 | 132 |  |  |
| jackpotglow | panel | 40 | 778 | 640 | 200 | 133 |  |  |
| strokeinner0 | text | 321 | 930 | 82 | 24 | 134 | JACKPOT |  |
| strokeinner1 | text | 321 | 932 | 82 | 24 | 135 | JACKPOT |  |
| strokeinner2 | text | 320 | 932 | 82 | 24 | 136 | JACKPOT |  |
| strokeinner3 | text | 319 | 933 | 82 | 24 | 137 | JACKPOT |  |
| strokeinner4 | text | 318 | 932 | 82 | 24 | 138 | JACKPOT |  |
| strokeinner5 | text | 317 | 932 | 82 | 24 | 139 | JACKPOT |  |
| strokeinner6 | text | 317 | 930 | 82 | 24 | 140 | JACKPOT |  |
| strokeinner7 | text | 317 | 929 | 82 | 24 | 141 | JACKPOT |  |
| strokeinner8 | text | 318 | 929 | 82 | 24 | 142 | JACKPOT |  |
| strokeinner9 | text | 319 | 928 | 82 | 24 | 143 | JACKPOT |  |
| strokeinner10 | text | 320 | 929 | 82 | 24 | 144 | JACKPOT |  |
| strokeinner11 | text | 321 | 929 | 82 | 24 | 145 | JACKPOT |  |
| strokeouter0 | text | 323 | 930 | 82 | 24 | 146 | JACKPOT |  |
| strokeouter1 | text | 322 | 932 | 82 | 24 | 147 | JACKPOT |  |
| strokeouter2 | text | 322 | 933 | 82 | 24 | 148 | JACKPOT |  |
| strokeouter3 | text | 320 | 934 | 82 | 24 | 149 | JACKPOT |  |
| strokeouter4 | text | 319 | 934 | 82 | 24 | 150 | JACKPOT |  |
| strokeouter5 | text | 318 | 934 | 82 | 24 | 151 | JACKPOT |  |
| strokeouter6 | text | 316 | 933 | 82 | 24 | 152 | JACKPOT |  |
| strokeouter7 | text | 316 | 932 | 82 | 24 | 153 | JACKPOT |  |
| strokeouter8 | text | 315 | 930 | 82 | 24 | 154 | JACKPOT |  |
| strokeouter9 | text | 316 | 929 | 82 | 24 | 155 | JACKPOT |  |
| strokeouter10 | text | 316 | 928 | 82 | 24 | 156 | JACKPOT |  |
| strokeouter11 | text | 318 | 927 | 82 | 24 | 157 | JACKPOT |  |
| strokeouter12 | text | 319 | 927 | 82 | 24 | 158 | JACKPOT |  |
| strokeouter13 | text | 320 | 927 | 82 | 24 | 159 | JACKPOT |  |
| strokeouter14 | text | 322 | 928 | 82 | 24 | 160 | JACKPOT |  |
| strokeouter15 | text | 322 | 929 | 82 | 24 | 161 | JACKPOT |  |
| fillcenter | text | 319 | 930 | 82 | 24 | 162 | JACKPOT |  |
| fillinner0 | text | 320 | 930 | 82 | 24 | 163 | JACKPOT |  |
| fillinner1 | text | 320 | 931 | 82 | 24 | 164 | JACKPOT |  |
| fillinner2 | text | 320 | 932 | 82 | 24 | 165 | JACKPOT |  |
| fillinner3 | text | 319 | 932 | 82 | 24 | 166 | JACKPOT |  |
| fillinner4 | text | 318 | 932 | 82 | 24 | 167 | JACKPOT |  |
| fillinner5 | text | 318 | 931 | 82 | 24 | 168 | JACKPOT |  |
| fillinner6 | text | 318 | 930 | 82 | 24 | 169 | JACKPOT |  |
| fillinner7 | text | 318 | 930 | 82 | 24 | 170 | JACKPOT |  |
| fillinner8 | text | 318 | 929 | 82 | 24 | 171 | JACKPOT |  |
| fillinner9 | text | 319 | 929 | 82 | 24 | 172 | JACKPOT |  |
| fillinner10 | text | 320 | 929 | 82 | 24 | 173 | JACKPOT |  |
| fillinner11 | text | 320 | 930 | 82 | 24 | 174 | JACKPOT |  |
| fillouter0 | text | 322 | 930 | 82 | 24 | 175 | JACKPOT |  |
| fillouter1 | text | 322 | 931 | 82 | 24 | 176 | JACKPOT |  |
| fillouter2 | text | 321 | 932 | 82 | 24 | 177 | JACKPOT |  |
| fillouter3 | text | 321 | 933 | 82 | 24 | 178 | JACKPOT |  |
| fillouter4 | text | 320 | 933 | 82 | 24 | 179 | JACKPOT |  |
| fillouter5 | text | 319 | 933 | 82 | 24 | 180 | JACKPOT |  |
| fillouter6 | text | 318 | 933 | 82 | 24 | 181 | JACKPOT |  |
| fillouter7 | text | 317 | 933 | 82 | 24 | 182 | JACKPOT |  |
| fillouter8 | text | 317 | 932 | 82 | 24 | 183 | JACKPOT |  |
| fillouter9 | text | 316 | 931 | 82 | 24 | 184 | JACKPOT |  |
| fillouter10 | text | 316 | 930 | 82 | 24 | 185 | JACKPOT |  |
| fillouter11 | text | 316 | 930 | 82 | 24 | 186 | JACKPOT |  |
| fillouter12 | text | 317 | 929 | 82 | 24 | 187 | JACKPOT |  |
| fillouter13 | text | 317 | 928 | 82 | 24 | 188 | JACKPOT |  |
| fillouter14 | text | 318 | 928 | 82 | 24 | 189 | JACKPOT |  |
| fillouter15 | text | 319 | 927 | 82 | 24 | 190 | JACKPOT |  |
| fillouter16 | text | 320 | 928 | 82 | 24 | 191 | JACKPOT |  |
| fillouter17 | text | 321 | 928 | 82 | 24 | 192 | JACKPOT |  |
| fillouter18 | text | 321 | 929 | 82 | 24 | 193 | JACKPOT |  |
| fillouter19 | text | 322 | 930 | 82 | 24 | 194 | JACKPOT |  |
| label0 | text | 95 | 930 | 38 | 24 | 195 | 3000 |  |
| label1 | text | 218 | 930 | 38 | 24 | 196 | 1000 |  |
| label2 | text | 319 | 930 | 82 | 24 | 197 | JACKPOT |  |
| label3 | text | 464 | 930 | 38 | 24 | 198 | 1000 |  |
| label4 | text | 587 | 930 | 38 | 24 | 199 | 5000 |  |
| header_a | panel | 0 | -56 | 720 | 56 | 1000 |  | ヘッダーA（HTMLオーバーレイ／Unityキャンバスの外・上側／キャンバス座標では y=-56 から始まる／`.common-header-bar`・規約 game-layout-standard.md §1） |
