// 非GUIテスト(node): JemSelectorState.BuildStateJson の実出力ファイルを
// 本番と同じ JSON.parse で解析し、共有契約(shared/jem-selector/v1/CONTRACT.md)の
// ready/savedRate/balancesByAssetCode が厳密JSONで届くことを検証する。
// 使い方: node jem_selector_state_parse_test.js <outdir>
'use strict';
const fs = require('fs');
const path = require('path');

const dir = process.argv[2] || '.';
let failures = 0;
function check(cond, name) {
  if (!cond) { failures++; console.log('FAIL: ' + name); }
  else console.log('PASS: ' + name);
}
function parse(name) {
  const text = fs.readFileSync(path.join(dir, name), 'utf8');
  return JSON.parse(text); // 壊れたJSONならここでthrow=テスト失敗
}

// 1) 残高map未取得: ready/savedRateのみ・外側まで閉じてparseできる
let o = parse('case_nobal.json');
check(o.ready === true, 'ready=true reaches contract');
check(o.savedRate === 5, 'savedRate=5 reaches contract');
check(!('balancesByAssetCode' in o), 'balances key omitted when unfetched');

// 2) 残高mapあり: 全entryが往復一致(0残高も非負整数として届く)
o = parse('case_bal.json');
check(o.ready === false, 'ready=false reaches contract');
check(o.savedRate === 100, 'savedRate=100 reaches contract');
const m = o.balancesByAssetCode;
check(m && m.GEM_RUBY === 3 && m.GEM_SAPPHIRE === 0 && m.GEM_CITRINE === 120
  && Object.keys(m).length === 3, 'balancesByAssetCode round-trips all entries');

// 3) エスケープ: 引用符/バックスラッシュ/制御文字を含むkeyも正しく往復する
o = parse('case_escape.json');
const wk = 'GEM_X"\\\n';
check(o.balancesByAssetCode[wk] === 7, 'escaped key round-trips');

// 4) 空mapは {} としてparseできる
o = parse('case_empty.json');
check(o.balancesByAssetCode && Object.keys(o.balancesByAssetCode).length === 0, 'empty map -> {}');

console.log(failures === 0 ? 'JS SIDE ALL PASS' : failures + ' JS FAILURE(S)');
process.exit(failures === 0 ? 0 : 1);
