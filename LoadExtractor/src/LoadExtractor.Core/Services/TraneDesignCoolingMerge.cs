using System.Globalization;
using System.Text.RegularExpressions;
using LoadExtractor.Core.Models;

namespace LoadExtractor.Core.Services;

/// <summary>Match Design Cooling pages to Room Checksum rows and run load cross-checks.</summary>
public static class TraneDesignCoolingMerge
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static void AttachAndCrossCheck(List<TraneRoomLoad> rooms, List<TraceDesignCoolingRoomExtract> designPages)
    {
        foreach (var r in rooms)
            r.DesignCooling = null;

        if (designPages.Count == 0)
            return;

        var map = new Dictionary<string, TraceDesignCoolingRoomExtract>(StringComparer.OrdinalIgnoreCase);
        foreach (var d in designPages)
        {
            var key = NormalizeRoomKey(d.RoomNumber, d.RoomName);
            if (!map.ContainsKey(key))
                map[key] = d;
        }

        foreach (var r in rooms)
        {
            var key = NormalizeRoomKey(r.RoomNumber, r.RoomName);
            if (!map.TryGetValue(key, out var d))
                continue;

            r.DesignCooling = ToSupplement(d);
            r.DesignCooling.LoadsCrossCheck = ComputeCrossCheck(r, d);
        }
    }

    public static string NormalizeRoomKey(string roomNumber, string roomName)
    {
        var rn = roomNumber.Trim();
        if (int.TryParse(rn, NumberStyles.Integer, Invariant, out var n))
            rn = n.ToString("D3", Invariant);
        var name = Regex.Replace(roomName.Trim(), @"\s+", " ").ToUpperInvariant();
        return $"{rn}|{name}";
    }

    private static DesignCoolingSupplement ToSupplement(TraceDesignCoolingRoomExtract d) =>
        new()
        {
            CalcBy = d.CalcBy,
            HeaderProjectTitle = d.HeaderProjectTitle,
            FooterProjectName = d.FooterProjectName,
            ZoneText = d.ZoneText,
            SystemName = d.SystemName,
            CoilSensibleMbh = d.CoilSensibleMbh,
            CoilTotalMbh = d.CoilTotalMbh,
            TotalCoolingAirflowCfm = d.TotalCoolingAirflowCfm,
            EngTotalCoolingLoad = d.EngTotalCoolingLoad,
            EngAreaPerLoad = d.EngAreaPerLoad,
            ReportSensibleBtuH = d.ReportSensibleBtuH,
            ReportLatentBtuH = d.ReportLatentBtuH,
            ReportTotalBtuH = d.ReportTotalBtuH,
            DesignSourcePage = d.SourcePage,
            LoadsCrossCheck = "—"
        };

    private static string ComputeCrossCheck(TraneRoomLoad r, TraceDesignCoolingRoomExtract d)
    {
        var hasReport = d.ReportSensibleBtuH.HasValue || d.ReportTotalBtuH.HasValue;
        if (!hasReport)
            return "Mismatch (no report totals)";

        var okS = true;
        if (r.SensibleCapacityMbh.HasValue && d.ReportSensibleBtuH.HasValue)
        {
            var reportSensMbh = d.ReportSensibleBtuH.Value / 1000.0;
            okS = NearlyEqual(r.SensibleCapacityMbh.Value, reportSensMbh, 0.35, 0.006);
        }

        var okT = true;
        if (r.TotalCapacityMbh.HasValue && d.ReportTotalBtuH.HasValue)
        {
            var reportTotMbh = d.ReportTotalBtuH.Value / 1000.0;
            okT = NearlyEqual(r.TotalCapacityMbh.Value, reportTotMbh, 0.35, 0.006);
        }

        var okC = true;
        if (r.CoilAirflowCfm.HasValue && d.TotalCoolingAirflowCfm.HasValue)
        {
            var tol = Math.Max(25.0, r.CoilAirflowCfm.Value * 0.02);
            okC = Math.Abs(r.CoilAirflowCfm.Value - d.TotalCoolingAirflowCfm.Value) <= tol;
        }

        return okS && okT && okC ? "Match" : "Mismatch";
    }

    private static bool NearlyEqual(double a, double b, double absTol, double relTol)
    {
        var d = Math.Abs(a - b);
        return d <= absTol || d <= Math.Abs(b) * relTol;
    }
}
