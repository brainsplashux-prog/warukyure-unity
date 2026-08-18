using System.Collections.Generic;
using UnityEngine;

public static class BoardData
{
    // SAFE area board top is at canvas y=405, see WarukyureBoard SetupCanvas.
    // CenterY = 405 + layout_y + h/2.  Stored as positive canvas y;
    // anchoredPosition uses (centerX, -centerY).
    public static readonly Dictionary<string, Vector2> CellCenters = new Dictionary<string, Vector2>
    {
        {"o_01", new Vector2(193.0f, 452.0f)},
        {"ball_o1", new Vector2(247.0f, 452.0f)},
        {"o_03", new Vector2(301.0f, 452.0f)},
        {"o_04", new Vector2(354.0f, 452.0f)},
        {"o_05", new Vector2(408.0f, 452.0f)},
        {"o_06", new Vector2(462.0f, 452.0f)},
        {"ball_o2", new Vector2(516.0f, 452.0f)},
        {"o_08", new Vector2(569.0f, 454.0f)},
        {"o_09", new Vector2(615.0f, 484.0f)},
        {"o_10", new Vector2(625.0f, 535.0f)},
        {"o_11", new Vector2(625.0f, 589.0f)},
        {"o_12", new Vector2(625.0f, 643.0f)},
        {"o_13", new Vector2(625.0f, 697.0f)},
        {"o_14", new Vector2(625.0f, 750.0f)},
        {"o_15", new Vector2(625.0f, 804.0f)},
        {"ship_r1", new Vector2(625.0f, 858.0f)},
        {"o_17", new Vector2(625.0f, 912.0f)},
        {"o_18", new Vector2(625.0f, 966.0f)},
        {"o_19", new Vector2(625.0f, 1018.0f)},
        {"o_20", new Vector2(579.0f, 1054.0f)},
        {"o_21", new Vector2(526.0f, 1059.0f)},
        {"o_22", new Vector2(472.0f, 1059.0f)},
        {"o_23", new Vector2(418.0f, 1059.0f)},
        {"o_24", new Vector2(365.0f, 1059.0f)},
        {"o_25", new Vector2(311.0f, 1059.0f)},
        {"o_26", new Vector2(257.0f, 1059.0f)},
        {"o_27", new Vector2(203.0f, 1059.0f)},
        {"o_28", new Vector2(150.0f, 1057.0f)},
        {"o_29", new Vector2(104.0f, 1027.0f)},
        {"o_30", new Vector2(94.0f, 976.0f)},
        {"o_31", new Vector2(94.0f, 922.0f)},
        {"ship_l1", new Vector2(94.0f, 868.0f)},
        {"o_33", new Vector2(94.0f, 814.0f)},
        {"o_34", new Vector2(94.0f, 761.0f)},
        {"o_35", new Vector2(94.0f, 707.0f)},
        {"o_36", new Vector2(94.0f, 653.0f)},
        {"o_37", new Vector2(94.0f, 599.0f)},
        {"o_38", new Vector2(94.0f, 545.0f)},
        {"o_39", new Vector2(94.0f, 493.0f)},
        {"o_40", new Vector2(140.0f, 457.0f)},
        {"i_01", new Vector2(279.0f, 524.0f)},
        {"i_07", new Vector2(222.0f, 547.0f)},
        {"i_05", new Vector2(199.0f, 604.0f)},
        {"i_04", new Vector2(222.0f, 661.0f)},
        {"ball_i1", new Vector2(279.0f, 684.0f)},
        {"i_02", new Vector2(336.0f, 661.0f)},
        {"i_06", new Vector2(359.0f, 604.0f)},
        {"key", new Vector2(336.0f, 547.0f)},
        {"m_00", new Vector2(208.0f, 806.0f)},
        {"m_01", new Vector2(246.0f, 806.0f)},
        {"m_02", new Vector2(284.0f, 806.0f)},
        {"m_03", new Vector2(322.0f, 806.0f)},
        {"ball_m1", new Vector2(360.0f, 806.0f)},
        {"m_05", new Vector2(398.0f, 806.0f)},
        {"m_06", new Vector2(436.0f, 806.0f)},
        {"m_07", new Vector2(474.0f, 806.0f)},
        {"m_08", new Vector2(512.0f, 806.0f)},
        {"m_09", new Vector2(512.0f, 844.0f)},
        {"m_10", new Vector2(512.0f, 882.0f)},
        {"m_11", new Vector2(512.0f, 920.0f)},
        {"m_12", new Vector2(512.0f, 958.0f)},
        {"m_13", new Vector2(474.0f, 958.0f)},
        {"m_14", new Vector2(436.0f, 958.0f)},
        {"m_15", new Vector2(398.0f, 958.0f)},
        {"m_16", new Vector2(360.0f, 958.0f)},
        {"m_17", new Vector2(322.0f, 958.0f)},
        {"m_18", new Vector2(284.0f, 958.0f)},
        {"m_19", new Vector2(246.0f, 958.0f)},
        {"m_20", new Vector2(208.0f, 958.0f)},
        {"m_21", new Vector2(208.0f, 920.0f)},
        {"m_22", new Vector2(208.0f, 882.0f)},
        {"m_23", new Vector2(208.0f, 844.0f)},
        {"castle", new Vector2(522.5f, 562.5f)},
    };

    public static readonly string[] OuterTrack = new[] { "o_01", "ball_o1", "o_03", "o_04", "o_05", "o_06", "ball_o2", "o_08", "o_09", "o_10", "o_11", "o_12", "o_13", "o_14", "o_15", "ship_r1", "o_17", "o_18", "o_19", "o_20", "o_21", "o_22", "o_23", "o_24", "o_25", "o_26", "o_27", "o_28", "o_29", "o_30", "o_31", "ship_l1", "o_33", "o_34", "o_35", "o_36", "o_37", "o_38", "o_39", "o_40" };
    public static readonly string[] Ring4Track = new[] { "i_01", "i_07", "i_05", "i_04", "ball_i1", "i_02", "i_06", "key" };
    public static readonly string[] Loop2Track = new[] { "m_00", "m_01", "m_02", "m_03", "ball_m1", "m_05", "m_06", "m_07", "m_08", "m_09", "m_10", "m_11", "m_12", "m_13", "m_14", "m_15", "m_16", "m_17", "m_18", "m_19", "m_20", "m_21", "m_22", "m_23" };
    public static readonly string[] CastleTrack = new[] { "castle" };

    public static readonly Dictionary<string, (string source, string target, string targetTrack)> Warp = new Dictionary<string, (string, string, string)>
    {
        {"warp_whale", ("o_37", "i_05", "ring4")},
        {"warp_ship_l", ("ship_l1", "m_21", "loop2")},
        {"warp_ship_r", ("ship_r1", "m_08", "loop2")},
        {"rainbow_bridge", ("i_04", "m_00", "loop2")},
        {"warp_key", ("key", "castle", "castle")},
    };

    public static readonly Dictionary<string, string> CellTrack = new Dictionary<string, string>();
    public static readonly Dictionary<string, int> CellIndex = new Dictionary<string, int>();

    static BoardData()
    {
        AddTrack("outer", OuterTrack);
        AddTrack("ring4", Ring4Track);
        AddTrack("loop2", Loop2Track);
        AddTrack("castle", CastleTrack);
    }

    static void AddTrack(string track, string[] cells)
    {
        for (int i = 0; i < cells.Length; i++)
        {
            CellTrack[cells[i]] = track;
            CellIndex[cells[i]] = i;
        }
    }

    public static string GetTrack(string cellId)
    {
        if (CellTrack.TryGetValue(cellId, out string t)) return t;
        return "unknown";
    }

    public static int GetIndex(string cellId)
    {
        if (CellIndex.TryGetValue(cellId, out int i)) return i;
        return -1;
    }

    public static bool TryGetCenter(string cellId, out Vector2 center)
    {
        if (CellCenters.TryGetValue(cellId, out center)) return true;
        center = Vector2.zero;
        return false;
    }

    public static string[] GetTrackArray(string track)
    {
        if (track == "outer") return OuterTrack;
        if (track == "ring4") return Ring4Track;
        if (track == "loop2") return Loop2Track;
        if (track == "castle") return CastleTrack;
        return null;
    }
}