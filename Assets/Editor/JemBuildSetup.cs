using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

// 2026-09-21 JEM_BUILD切替専用(jem-warukyure)。指揮指示: シーン/プレハブYAMLは触らない。
// WarukyureBuilder/WebGLBuilder(既存warukyure本体ビルド)へは影響させない。
// Scripting Define Symbols はEditorが「起動時に一度だけ」コンパイルへ反映するため、
// 同一プロセス内でこの定義変更→即ビルド、はできない(反映前の古いアセンブリでビルドされる)。
// 運用は2段階のUnity起動に分ける: ①EnableJemDefineだけを実行してプロセスを終了
// (ProjectSettings.asset へ保存) ②次のUnity起動でビルド実行(この時点でJEM_BUILD付きで
// コンパイル済み)。本ブランチ(feat/jem-warukyure)専用ワークツリーではProjectSettings.assetへ
// WebGL: JEM_BUILD がコミット済みのため、以後は②だけで良い。
// (参照実装: Poinoshin-wt-jem-poinoshin-20260918/Assets/Editor/JemBuildSetup.cs)
public static class JemBuildSetup
{
    const string Symbol = "JEM_BUILD";

    [MenuItem("JEM/Enable JEM_BUILD define")]
    public static void EnableJemDefine()
    {
        SetSymbol(true);
    }

    [MenuItem("JEM/Disable JEM_BUILD define")]
    public static void DisableJemDefine()
    {
        SetSymbol(false);
    }

    static void SetSymbol(bool enabled)
    {
        var target = NamedBuildTarget.WebGL;
        string existing = PlayerSettings.GetScriptingDefineSymbols(target);
        var symbols = new List<string>(existing.Split(new[] { ';' }, System.StringSplitOptions.RemoveEmptyEntries));
        bool has = symbols.Contains(Symbol);
        if (enabled && !has) symbols.Add(Symbol);
        if (!enabled && has) symbols.Remove(Symbol);
        string joined = string.Join(";", symbols);
        PlayerSettings.SetScriptingDefineSymbols(target, joined);
        AssetDatabase.SaveAssets();
        Debug.Log($"[JEM_BUILD_SETUP] enabled={enabled} symbols={joined}");
    }
}
