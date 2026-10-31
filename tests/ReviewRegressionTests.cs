using System;
using System.IO;
using System.Drawing;
using System.Globalization;
using System.Xml;
using System.Windows.Forms;

public static partial class RegressionTests {
    [STAThread]
    public static int Main() {
        if (String.IsNullOrEmpty(Environment.GetEnvironmentVariable("MONITORIP_DATA_DIR"))) {
            Console.Error.WriteLine("Run scripts/test.ps1 to isolate test data."); return 1;
        }
        try {
            Dir=AppFiles.DataDir;
            L.SetLanguage("es");
            SelfTest(); TestStorage(); TestRouterSessions(); TestFastIpChange();
            Console.WriteLine("PASS: " + assertions + " checks (connection timing, router sessions, storage, languages and tray icons).");
            return 0;
        } catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }

    static void TestLanguages() {
        Assert(L.Resolve("auto",CultureInfo.GetCultureInfo("es-ES")) == "es","Spanish detection");
        Assert(L.Resolve("auto",CultureInfo.GetCultureInfo("fr-FR")) == "en","English fallback");
        Assert(L.Resolve("es",CultureInfo.GetCultureInfo("en-US")) == "es","explicit language");
        foreach (string language in new string[] { "es", "en" }) {
            L.SetLanguage(language);
            for (int i=0;i<L.Entries.GetLength(0);i++)
                Assert(L.T(L.Entries[i,0]) == L.Entries[i,language == "es" ? 0 : 1],"translation " + i);
        }
        using (var label = new Label { Text="Configurar" }) {
            L.Capture(label); L.SetLanguage("en"); L.Apply(label);
            Assert(label.Text == "Settings","switch existing controls to English");
            L.SetLanguage("es"); L.Apply(label);
            Assert(label.Text == "Configurar","switch existing controls back to Spanish");
        }
        Assert(L.Minutes(1) == "1 minuto","Spanish singular minute");
        var state = new ConnectionState { AnchorUtc=DateTime.UtcNow, PeriodHours=12.5 };
        string spanishAlertKey=MonitorLogic.AlertKey(state,5);
        L.SetLanguage("en"); Assert(L.Minutes(1) == "1 minute","English singular minute");
        Assert(MonitorLogic.AlertKey(state,5) == spanishAlertKey,"language switch must not repeat countdown alerts");
        L.SetLanguage("es");
    }

    static void TestTray() {
        Assert(TrayCountdown.Describe(TimeSpan.FromHours(3)).Band == "green","3h green");
        Assert(TrayCountdown.Describe(TimeSpan.FromSeconds(10799)).Band == "yellow","under 3h amber");
        Assert(TrayCountdown.Describe(TimeSpan.FromHours(1)).Band == "yellow","1h amber");
        var minutes=TrayCountdown.Describe(TimeSpan.FromSeconds(3599));
        Assert(minutes.Band == "red" && minutes.Top == "59" && minutes.Bottom == "m","minutes when red");
        var hours=TrayCountdown.Describe(new TimeSpan(11,37,25));
        Assert(hours.Top == "11" && hours.Bottom == "","whole hours only");
        Assert(hours.Key == TrayCountdown.Describe(new TimeSpan(11,12,0)).Key,"avoid unnecessary icon redraws");
        Assert(TrayCountdown.Describe(TimeSpan.FromSeconds(45)).Bottom == "s","final seconds");
        Assert(TrayCountdown.Describe(TimeSpan.Zero).Top == "!","expired marker");
        Assert(TrayCountdown.Describe(null).Top == "?","unknown marker");
        foreach (TimeSpan? remaining in new TimeSpan?[] { null, TimeSpan.Zero, TimeSpan.FromSeconds(45), TimeSpan.FromMinutes(59), TimeSpan.FromHours(168) }) {
            using (var icon=TrayCountdown.Create(TrayCountdown.Describe(remaining)))
                Assert(icon.Width == 32 && icon.Height == 32,"native icon rendering");
        }
    }

    static void TestStorage() {
        string path=Path.Combine(Dir,"storage-test.json");
        var state=new ConnectionState { AnchorUtc=DateTime.UtcNow.AddHours(-1), Ip="1.1.1.1" };
        JsonStore.Write(path,state);
        JsonStore.Write(path,state); // Produce a valid backup.
        File.WriteAllText(path,"{broken");
        Assert(JsonStore.Read<ConnectionState>(path,MonitorLogic.ValidState).Ip == "1.1.1.1","recover corrupt primary from backup");
        File.WriteAllText(path,"{\"AnchorUtc\":\"invalid-date\"}");
        Assert(JsonStore.Read<ConnectionState>(path,MonitorLogic.ValidState).Ip == "1.1.1.1","recover invalid date from backup");
        File.Delete(path);
        Assert(JsonStore.Read<ConnectionState>(path,MonitorLogic.ValidState).Ip == "1.1.1.1","recover missing primary from backup");
        foreach (ConnectionState bad in new ConnectionState[] {
            new ConnectionState { PeriodHours=Double.NaN },
            new ConnectionState { PeriodHours=Double.PositiveInfinity },
            new ConnectionState { AnchorUtc=DateTime.MaxValue },
            new ConnectionState { LastSuccessUtc=DateTime.MaxValue },
            new ConnectionState { AnchorUtc=new DateTime(2,1,1) },
            new ConnectionState { Ip="192.168.1.1" }
        }) Assert(!MonitorLogic.ValidState(bad),"reject invalid stored state");
        Assert(MonitorLogic.ValidState(new ConnectionState()),"fresh state without a reference");
        var serializer = new System.Web.Script.Serialization.JavaScriptSerializer();
        var shiftedEmpty = DateTime.SpecifyKind(DateTime.MinValue.AddHours(3),DateTimeKind.Utc);
        var legacy = serializer.Deserialize<ConnectionState>(serializer.Serialize(new ConnectionState { AnchorUtc=shiftedEmpty, LastSuccessUtc=shiftedEmpty }));
        Assert(MonitorLogic.ValidState(legacy) && legacy.AnchorUtc == DateTime.MinValue && legacy.LastSuccessUtc == DateTime.MinValue,"normalize legacy empty dates with a timezone offset");
        Assert(!NetworkAddress.IsPublicV4("100.64.0.1") && !NetworkAddress.IsPublicV4("::1") && NetworkAddress.IsPublicV4("1.1.1.1"),"shared IPv4 validation");
        bool rejected=false;
        try { AppSettings.ValidateUrl(null); } catch (ArgumentException) { rejected=true; }
        Assert(rejected,"null router URL is rejected cleanly");
    }

    static string ConnectedWan() {
        return "<root><Instance><ParaName>wantype</ParaName><ParaValue>pppoe</ParaValue><ParaName>ConnStatus</ParaName><ParaValue>Connected</ParaValue><ParaName>IPAddress</ParaName><ParaValue>1.1.1.1</ParaValue><ParaName>UpTime</ParaName><ParaValue>1200</ParaValue></Instance></root>";
    }
    static void TestRouterSessions() {
        int logins=0;
        bool connected=true, expired=false, rejectLogin=false;
        Func<string,string,string> transport=delegate(string path,string body) {
            if (path == "") return "<input id=\"Frm_Username\" value=\"synthetic\"><input id=\"Frm_Password\" value=\"synthetic\">";
            if (path.Contains("login_token")) return "<token>synthetic</token>";
            if (path.Contains("login_entry")) {
                if (body == null) return "{\"sess_token\":\"synthetic\"}";
                logins++; return rejectLogin ? "{\"login_need_refresh\":false}" : "{\"login_need_refresh\":true}";
            }
            if (path.Contains("menuView")) return "";
            if (expired) { expired=false; return "<root>SessionTimeout</root>"; }
            return connected ? ConnectedWan() : "<root/>";
        };
        var reader=new RouterReader(new AppSettings { RouterEnabled=true },transport);
        Assert(reader.Read().FromRouter && logins == 1,"initial login");
        reader.Read(); Assert(logins == 1,"reuse authenticated session");
        connected=false;
        for (int i=0;i<2;i++) {
            bool failed=false; try { reader.Read(); } catch (IOException) { failed=true; }
            Assert(failed && logins == 1,"WAN outage must not trigger another login");
        }
        connected=true; expired=true;
        reader.Read(); Assert(logins == 2,"expired session is renewed once");
        rejectLogin=true;
        var deniedReader=new RouterReader(new AppSettings { RouterEnabled=true },transport);
        for (int i=0;i<2;i++) {
            bool failed=false; try { deniedReader.Read(); } catch (IOException) { failed=true; }
            Assert(failed && logins == 3,"failed login respects cooldown");
        }
        bool xmlRejected=false;
        try { RouterReader.ParseWan("<!DOCTYPE root [<!ENTITY test 'value'>]><root>&test;</root>",DateTime.UtcNow); }
        catch (XmlException) { xmlRejected=true; }
        Assert(xmlRejected,"reject XML document type declarations");
    }

    static void TestFastIpChange() {
        DateTime now=DateTime.UtcNow;
        var state=new ConnectionState { Ip="1.1.1.1", AnchorUtc=now.AddSeconds(-40), LastSuccessUtc=now.AddSeconds(-30), RouterVerified=true };
        var reading=new ConnectionReading { Ip="8.8.8.8", ReadUtc=now, SessionStartUtc=now.AddSeconds(-10), FromRouter=true };
        MonitorLogic.ObserveRouter(state,reading);
        Assert(state.AnchorUtc == reading.SessionStartUtc,"new IP resets session even inside jitter tolerance");
    }
}
