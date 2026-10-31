using System;
using System.IO;
using System.Web.Script.Serialization;
public static partial class RegressionTests {
    static string Dir;
    static int assertions;
    static void Assert(bool condition, string message) { assertions++; if (!condition) throw new Exception(message); }
    static void SelfTest() {
        DateTime now = new DateTime(2024,1,1,12,0,0,DateTimeKind.Utc);
        var s = new ConnectionState { AnchorUtc = now.AddHours(-2) };
        Assert(MonitorLogic.Observe(s,"1.1.1.1",now) == "initial", "initial");
        Assert(s.AnchorUtc == now.AddHours(-2), "initial must preserve manual anchor");
        Assert(MonitorLogic.Observe(s,"1.1.1.1",now.AddSeconds(30)) == "same", "same");
        Assert(s.AnchorUtc == now.AddHours(-2), "same must preserve anchor");
        Assert(MonitorLogic.Observe(s,"8.8.8.8",now.AddSeconds(60)) == "change", "change");
        Assert(s.AnchorUtc == now.AddSeconds(60), "change reset");
        Assert(MonitorLogic.Observe(s,"9.9.9.9",now.AddHours(2)) == "gap", "gap detection");
        Assert(s.Quality.Contains("incierta"), "gap uncertainty");
        MonitorLogic.Observe(s,"9.9.9.9",now.AddHours(4));
        Assert(s.Quality.Contains("incierta"), "same IP after gap uncertainty");
        Assert(MonitorLogic.AlertBand(31) == -1 && MonitorLogic.AlertBand(30) == 30 && MonitorLogic.AlertBand(15) == 15 && MonitorLogic.AlertBand(5) == 5 && MonitorLogic.AlertBand(1) == 1 && MonitorLogic.AlertBand(-1) == 0, "alert bands");
        string serialized = new JavaScriptSerializer().Serialize(s);
        var restored = new JavaScriptSerializer().Deserialize<ConnectionState>(serialized);
        Assert(restored.AnchorUtc == s.AnchorUtc && restored.Ip == s.Ip, "persistence");
        string wanSample = "<root><Instance><ParaName>wantype</ParaName><ParaValue>dhcp/static</ParaValue><ParaName>UpTime</ParaName><ParaValue>600000</ParaValue><ParaName>IPAddress</ParaName><ParaValue>10.0.0.1</ParaValue><ParaName>ConnStatus</ParaName><ParaValue>Connected</ParaValue></Instance><Instance><ParaName>wantype</ParaName><ParaValue>pppoe</ParaValue><ParaName>ConnStatus</ParaName><ParaValue>Connected</ParaValue><ParaName>IPAddress</ParaName><ParaValue>1.1.1.1</ParaValue><ParaName>UpTime</ParaName><ParaValue>1200</ParaValue></Instance></root>";
        ConnectionReading routerReading = RouterReader.ParseWan(wanSample,now);
        Assert(routerReading.SessionStartUtc == now.AddMinutes(-20) && routerReading.Ip == "1.1.1.1", "select PPPoE rather than management uptime");
        var rs = new ConnectionState { AnchorUtc = now.AddHours(-4), Ip = "1.1.1.1", LastSuccessUtc = now.AddHours(-2) };
        MonitorLogic.ObserveRouter(rs,routerReading);
        Assert(rs.AnchorUtc == now.AddMinutes(-20) && rs.RouterVerified, "recover exact session after offline gap");
        rs.AlertKey = "already-alerted";
        routerReading.ReadUtc = now.AddSeconds(30); routerReading.SessionStartUtc = now.AddMinutes(-20).AddMilliseconds(500);
        MonitorLogic.ObserveRouter(rs,routerReading);
        Assert(rs.AnchorUtc == now.AddMinutes(-20) && rs.AlertKey == "already-alerted", "jitter preserves alert deduplication");
        routerReading.ReadUtc = now.AddMinutes(10); routerReading.SessionStartUtc = now.AddMinutes(9);
        Assert(MonitorLogic.ObserveRouter(rs,routerReading) == "session", "same IP reconnection");
        Assert(rs.AnchorUtc == now.AddMinutes(9) && rs.AlertKey == "", "same IP reset");
        MonitorLogic.Observe(rs,"8.8.8.8",now.AddMinutes(11));
        Assert(!rs.RouterVerified, "public fallback invalidates old router reference on IP change");
        var blank = new ConnectionState();
        MonitorLogic.Observe(blank,"1.1.1.1",now);
        Assert(blank.AnchorUtc == DateTime.MinValue, "first public IP is not a session start");
        var defaults = new AppSettings();
        Assert(!defaults.RouterEnabled && !defaults.SetupComplete, "router requires per-user opt-in");
        Assert(AppSettings.ValidateUrl("http://192.168.1.1") == "http://192.168.1.1/", "normalize router URL");
        foreach (string invalidUrl in new string[] { "https://example.com/", "http://1.1.1.1/", "http://user:secret@192.168.1.1/", "file:///C:/", "http://192.168.1.1/?token=secret" }) {
            bool rejected = false;
            try { AppSettings.ValidateUrl(invalidUrl); } catch (ArgumentException) { rejected = true; }
            Assert(rejected, "reject invalid router URL");
        }
        defaults.SetPassword("synthetic-test-password");
        Assert(defaults.Password() == "synthetic-test-password" && !defaults.ProtectedPassword.Contains("synthetic-test-password"), "DPAPI roundtrip");
        defaults.SetPassword(""); Assert(defaults.Password() == "", "remove saved password");
        TestLanguages();
        var translatedSettings = new AppSettings { Language="en" };
        Assert(new JavaScriptSerializer().Deserialize<AppSettings>(new JavaScriptSerializer().Serialize(translatedSettings)).Language == "en","language preference persistence");
        TestTray();
    }

}
