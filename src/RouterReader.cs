using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Collections.Generic;
using System.Xml;
using System.Web.Script.Serialization;

public class ConnectionReading {
    public string Ip;
    public DateTime ReadUtc;
    public DateTime SessionStartUtc;
    public bool FromRouter;
}

// Only login and GET status endpoints are used. No connection controls are called.
public class RouterReader {
    readonly AppSettings settings;
    readonly string baseUrl;
    const string WanPath = "?_type=menuData&_tag=wan_internetstatus_lua.lua&TypeUplink=2&pageType=1";
    CookieContainer cookies = new CookieContainer();
    DateTime nextLoginUtc;
    bool loggedIn;
    public int LoginCount;
    public RouterReader(AppSettings configuration) { settings = configuration; baseUrl = AppSettings.ValidateUrl(configuration.RouterUrl); }

    string Request(string path, string postBody) {
        var req = (HttpWebRequest)WebRequest.Create(baseUrl + path);
        req.Proxy = null; req.CookieContainer = cookies;
        req.Timeout = 4000; req.ReadWriteTimeout = 4000;
        req.AllowAutoRedirect = false;
        if (postBody != null) {
            req.Method = "POST"; req.ContentType = "application/x-www-form-urlencoded";
            byte[] bytes = Encoding.UTF8.GetBytes(postBody); req.ContentLength = bytes.Length;
            using (var stream = req.GetRequestStream()) stream.Write(bytes, 0, bytes.Length);
        }
        using (var response = req.GetResponse())
        using (var reader = new StreamReader(response.GetResponseStream())) return reader.ReadToEnd();
    }
    static string Prefilled(string html, string field) {
        var match = Regex.Match(html, "id=\"" + field + "\"[^>]*value=\"([^\"]*)\"", RegexOptions.IgnoreCase);
        if (!match.Success || match.Groups[1].Value.Length == 0) throw new IOException(L.T("El router requiere credenciales no disponibles"));
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }
    void Login() {
        if (DateTime.UtcNow < nextLoginUtc) throw new IOException(L.T("Login del router en espera"));
        nextLoginUtc = DateTime.UtcNow.AddMinutes(10);
        cookies = new CookieContainer();
        string html = Request("", null);
        string user = String.IsNullOrEmpty(settings.Username) ? Prefilled(html, "Frm_Username") : settings.Username;
        string password = settings.Password();
        if (String.IsNullOrEmpty(password)) password = Prefilled(html, "Frm_Password");
        var serializer = new JavaScriptSerializer();
        var entry = serializer.Deserialize<Dictionary<string, object>>(Request("?_type=loginData&_tag=login_entry", null));
        var challenge = new XmlDocument { XmlResolver = null };
        challenge.LoadXml(Request("?_type=loginData&_tag=login_token", null));
        string digest;
        using (var sha = SHA256.Create()) digest = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(password + challenge.DocumentElement.InnerText))).Replace("-", "").ToLowerInvariant();
        password = null;
        string body = "action=login&Username=" + Uri.EscapeDataString(user) + "&Password=" + digest + "&_sessionTOKEN=" + Uri.EscapeDataString((string)entry["sess_token"]);
        var result = serializer.Deserialize<Dictionary<string, object>>(Request("?_type=loginData&_tag=login_entry", body));
        object refresh;
        if (!result.TryGetValue("login_need_refresh", out refresh) || !(refresh is bool) || !(bool)refresh) throw new IOException(L.T("El router no acepto el inicio de sesion"));
        Request("?_type=menuView&_tag=ethWanStatus&Menu3Location=0", null);
        loggedIn = true; LoginCount++; nextLoginUtc = DateTime.MinValue;
    }
    public ConnectionReading Read() {
        if (!settings.RouterEnabled) throw new IOException(L.T("Lectura de router desactivada"));
        if (loggedIn) {
            try { return ParseWan(Request(WanPath, null), DateTime.UtcNow); }
            catch { loggedIn = false; }
        }
        Login();
        return ParseWan(Request(WanPath, null), DateTime.UtcNow);
    }
    public static bool PublicV4(string value) {
        IPAddress ip;
        if (!IPAddress.TryParse(value, out ip) || ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork) return false;
        byte[] b = ip.GetAddressBytes();
        return !(b[0] == 0 || b[0] == 10 || b[0] == 127 || b[0] >= 224 || (b[0] == 192 && b[1] == 168) || (b[0] == 172 && b[1] >= 16 && b[1] <= 31) || (b[0] == 169 && b[1] == 254) || (b[0] == 100 && b[1] >= 64 && b[1] <= 127));
    }
    public static ConnectionReading ParseWan(string xml, DateTime now) {
        var document = new XmlDocument { XmlResolver = null };
        document.LoadXml(xml);
        foreach (XmlNode instance in document.SelectNodes("//Instance")) {
            var fields = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
            foreach (XmlNode node in instance.ChildNodes) {
                if (node.Name != "ParaName") continue;
                XmlNode value = node.NextSibling;
                while (value != null && value.NodeType != XmlNodeType.Element) value = value.NextSibling;
                if (value != null && value.Name == "ParaValue") fields[node.InnerText] = value.InnerText;
            }
            string type, status, ip, uptime;
            if (!fields.TryGetValue("wantype", out type) || type != "pppoe" || !fields.TryGetValue("ConnStatus", out status) || status != "Connected" || !fields.TryGetValue("IPAddress", out ip) || !PublicV4(ip) || !fields.TryGetValue("UpTime", out uptime)) continue;
            long seconds;
            if (!Int64.TryParse(uptime, out seconds) || seconds < 0 || seconds > 315360000) continue;
            return new ConnectionReading { Ip = ip, ReadUtc = now, SessionStartUtc = now.AddSeconds(-seconds), FromRouter = true };
        }
        throw new IOException(L.T("No se encontro una sesion PPPoE IPv4 conectada"));
    }
}
