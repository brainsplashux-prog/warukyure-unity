using System;
using System.Reflection;
using NUnit.Framework;

// 2026-10-01 社長指示「タイトル画面はスタートボタン以外もどこ押してもゲーム開始」の是正検査。
// TitleScreen は Assembly-CSharp（asmdefなし）にあり直接参照できないため、リフレクションで純関数 DecideTap を叩く。
public sealed class TitleTapAnywhereTests
{
    static Type TitleType()
    {
        var t = Type.GetType("TitleScreen, Assembly-CSharp");
        Assert.NotNull(t, "TitleScreen not found");
        return t;
    }

    // 戻り値は TapAction の名前（Ignore/Start/ShowSessionError/ShowConnecting）
    static string Decide(bool isShowing, bool hasBoard, bool ready, bool failed, bool onOther)
    {
        var m = TitleType().GetMethod("DecideTap", BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(m, "DecideTap not found");
        return m.Invoke(null, new object[] { isShowing, hasBoard, ready, failed, onOther }).ToString();
    }

    [Test]
    public void DecideTap_HasNoPositionParameter_SoAnyTapStarts()
    {
        var m = TitleType().GetMethod("DecideTap", BindingFlags.Public | BindingFlags.Static);
        foreach (var p in m.GetParameters())
            Assert.AreNotEqual(typeof(UnityEngine.Vector2), p.ParameterType, "座標を見ない＝全面タップ");
        Assert.AreEqual("Start", Decide(true, true, true, false, false));
    }

    [Test]
    public void FullScreenTap_StartsExactlyOnce()
    {
        bool showing = true; int starts = 0;
        for (int i = 0; i < 3; i++)
        {
            if (Decide(showing, true, true, false, false) == "Start") { starts++; showing = false; } // Close() で IsShowing=false
        }
        Assert.AreEqual(1, starts);
    }

    [Test]
    public void NothingStarts_WhenNotShowing_NoBoard_OrOnMuteButton()
    {
        Assert.AreEqual("Ignore", Decide(false, true, true, false, false), "タイトル非表示(プレイ中・リザルト中)");
        Assert.AreEqual("Ignore", Decide(true, false, true, false, false), "盤面なし");
        Assert.AreEqual("Ignore", Decide(true, true, true, false, true), "ミュートボタン上");
    }

    [Test]
    public void NothingStarts_WhileSessionNotReady()
    {
        Assert.AreEqual("ShowConnecting", Decide(true, true, false, false, false));
        Assert.AreEqual("ShowSessionError", Decide(true, true, false, true, false));
    }
}
