// 非GUIテスト: WarukyureBoard.PushJemSelectorState が使う JemSelectorState.BuildStateJson の
// 実出力を検証する。JSONのparse検証は本番と同じパーサ(node の JSON.parse、
// tools/tests/jem_selector_state_parse_test.js)で行い、ここでは出力ファイルの生成と
// fail closed(異常値→null返却)を検査する。
// 実行: csc で Assets/Scripts/JemSelectorState.cs と共にコンパイル → mono <exe> <outdir> → node 検証。
using System;
using System.Collections.Generic;
using System.IO;

public static class JemSelectorStateTest
{
    static int failures;

    static void Check(bool cond, string name)
    {
        if (!cond) { failures++; Console.WriteLine("FAIL: " + name); }
        else Console.WriteLine("PASS: " + name);
    }

    public static int Main(string[] args)
    {
        string outDir = args.Length > 0 ? args[0] : ".";
        Directory.CreateDirectory(outDir);

        // parse検証用ケースを実出力のままファイルへ書き出す
        File.WriteAllText(Path.Combine(outDir, "case_nobal.json"),
            JemSelectorState.BuildStateJson(true, 5, null));
        File.WriteAllText(Path.Combine(outDir, "case_bal.json"),
            JemSelectorState.BuildStateJson(false, 100,
                new Dictionary<string, int> { { "GEM_RUBY", 3 }, { "GEM_SAPPHIRE", 0 }, { "GEM_CITRINE", 120 } }));
        File.WriteAllText(Path.Combine(outDir, "case_escape.json"),
            JemSelectorState.BuildStateJson(true, 1,
                new Dictionary<string, int> { { "GEM_X\"\\\n", 7 } }));
        File.WriteAllText(Path.Combine(outDir, "case_empty.json"),
            JemSelectorState.BuildStateJson(true, 2, new Dictionary<string, int>()));

        // 異常値はfail closed(null返却＝送信しない)。Dictionaryはnull keyを持てないため
        // null-keyケースは構文上到達しない（Add時点でArgumentNullException）。
        Check(JemSelectorState.BuildStateJson(true, 1,
            new Dictionary<string, int> { { "GEM_RUBY", -1 } }) == null,
            "negative balance -> null (fail closed)");
        Check(JemSelectorState.BuildStateJson(true, 1,
            new Dictionary<string, int> { { "GEM_RUBY", int.MinValue } }) == null,
            "int.MinValue balance -> null (fail closed)");
        Check(JemSelectorState.BuildStateJson(true, 1,
            new Dictionary<string, int> { { "", 5 } }) == null,
            "empty key -> null (fail closed)");

        Console.WriteLine(failures == 0 ? "CS SIDE ALL PASS" : failures + " CS FAILURE(S)");
        return failures == 0 ? 0 : 1;
    }
}
