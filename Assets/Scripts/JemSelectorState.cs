using System.Collections.Generic;
using System.Text;

// 2026-09-21 社長指示: 共通 jem-selector へ送る state JSON の生成を Unity 非依存に分離し、
// 非GUIテスト(csc+mono、tools/tests/JemSelectorStateTest.cs)で実際の出力文字列を
// parse して検証できるようにする。契約: shared/jem-selector/v1/CONTRACT.md。
public static class JemSelectorState
{
    // 厳密JSONを組み立てる。外側オブジェクトは必ず '}' まで閉じる。
    // balances == null → balancesByAssetCode キー自体を省略（残高未取得＝共通側は ×- 表示）。
    // key が null/空、value が負数など異常値を1件でも含む場合は null を返す
    // (fail closed＝その state は送信しない。呼び出し側は lastJson を更新しない)。
    public static string BuildStateJson(bool ready, int savedRate, Dictionary<string, int> balances)
    {
        var sb = new StringBuilder(128);
        sb.Append("{\"ready\":").Append(ready ? "true" : "false");
        sb.Append(",\"savedRate\":").Append(savedRate);
        if (balances != null)
        {
            sb.Append(",\"balancesByAssetCode\":{");
            bool first = true;
            foreach (var kv in balances)
            {
                if (string.IsNullOrEmpty(kv.Key) || kv.Value < 0) return null;
                if (!first) sb.Append(',');
                first = false;
                sb.Append('"').Append(Escape(kv.Key)).Append("\":").Append(kv.Value);
            }
            sb.Append('}');
        }
        sb.Append('}');
        return sb.ToString();
    }

    // asset_code key の JSON 文字列エスケープ。現行 catalog は GEM_* の安全な
    // 文字だけだが、将来のasset追加で壊れないよう常にエスケープを通す。
    static string Escape(string s)
    {
        var sb = new StringBuilder(s.Length + 8);
        foreach (char c in s)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\b': sb.Append("\\b"); break;
                case '\f': sb.Append("\\f"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                    else sb.Append(c);
                    break;
            }
        }
        return sb.ToString();
    }
}
