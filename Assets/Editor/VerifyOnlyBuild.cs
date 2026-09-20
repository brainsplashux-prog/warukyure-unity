using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// 2026-09-20: サブエージェントが検証専用に追加した新規ファイル(未コミット・未push)。
// 既存の WebGLBuilder.cs / WarukyureBuilder.cs は1行も変更していない。
// 出力先だけをスクラッチパス配下に変え、共有の本番ステージング先
// (~/Desktop/warukyure/client) には一切書き込まない検証用ビルド。
public class VerifyOnlyBuild
{
    [MenuItem("Verify/WebGL Build (scratch output)")]
    public static void Build()
    {
        AdVirtuaFrontmostValidator.ValidateAdVirtuaFrontmost();

        BuildPlayerOptions options = new BuildPlayerOptions();
        options.scenes = new[] { "Assets/Scenes/Main.unity" };
        options.locationPathName = "/private/tmp/claude-501/-Users-suzukimasahiro-Desktop-poicasi-platform/8378ca84-17c8-40f5-9a13-612ea6597153/scratchpad/waru-onprod-build/WebGL";
        options.target = BuildTarget.WebGL;
        options.options = BuildOptions.None;

        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result == BuildResult.Succeeded)
        {
            Debug.Log("WebGL build succeeded.");
        }
        else
        {
            Debug.LogError("WebGL build failed.");
            EditorApplication.Exit(1);
        }
    }
}
