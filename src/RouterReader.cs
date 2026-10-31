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

// Monitoring is read-only. Restart is a separate, explicitly confirmed operation.
public class RouterReader {
    readonly AppSettings settings;
    readonly string baseUrl;
    const string WanPath = "?_type=menuData&_tag=wan_internetstatus_lua.lua&TypeUplink=2&pageType=1";
    CookieContainer cookies = new CookieContainer();
    DateTime nextLoginUtc;
    bool loggedIn;
    string loginPage;
    internal Action<string,string,string> RequestObserver { get; set; }
    readonly Func<string,string,string> transport;
    readonly Func<DateTime> utcNow;
    public RouterReader(AppSettings configuration) : this(configuration, null) { }
    internal RouterReader(AppSettings configuration, Func<string,string,string> transport, Func<DateTime> utcNow = null) {
        settings = configuration;
        baseUrl = AppSettings.ValidateUrl(configuration.RouterUrl);
        this.transport = transport;
        this.utcNow = utcNow ?? (() => DateTime.UtcNow);
    }

    string Request(string path, string postBody, string check = null) {
        if (RequestObserver != null) RequestObserver(path, postBody, check);
        if (transport != null) return transport(path, postBody);
        var req = (HttpWebRequest)WebRequest.Create(baseUrl + path);
        req.Proxy = null; req.CookieContainer = cookies;
        req.Timeout = 4000; req.ReadWriteTimeout = 4000;
        req.AllowAutoRedirect = false;
        if (check != null) req.Headers["Check"] = check;
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
        if (utcNow() < nextLoginUtc) throw new IOException(L.T("Login del router en espera"));
        // A router still booting is not a credential rejection. Retry next poll.
        nextLoginUtc = utcNow().AddSeconds(30);
        cookies = new CookieContainer();
        string html = Request("", null);
        loginPage = html;
        string user = String.IsNullOrEmpty(settings.Username) ? Prefilled(html, "Frm_Username") : settings.Username;
        string password = settings.Password();
        if (String.IsNullOrEmpty(password)) password = Prefilled(html, "Frm_Password");
        var serializer = new JavaScriptSerializer();
        var entry = serializer.Deserialize<Dictionary<string, object>>(Request("?_type=loginData&_tag=login_entry", null));
        var challenge = ParseXml(Request("?_type=loginData&_tag=login_token", null));
        string digest;
        using (var sha = SHA256.Create()) digest = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(password + challenge.DocumentElement.InnerText))).Replace("-", "").ToLowerInvariant();
        password = null;
        string body = "action=login&Username=" + Uri.EscapeDataString(user) + "&Password=" + digest + "&_sessionTOKEN=" + Uri.EscapeDataString((string)entry["sess_token"]);
        var result = serializer.Deserialize<Dictionary<string, object>>(Request("?_type=loginData&_tag=login_entry", body));
        object refresh;
        if (!result.TryGetValue("login_need_refresh", out refresh) || !(refresh is bool) || !(bool)refresh) {
            nextLoginUtc = utcNow().AddMinutes(10);
            throw new IOException(L.T("El router no acepto el inicio de sesion"));
        }
        Request("?_type=menuView&_tag=ethWanStatus&Menu3Location=0", null);
        loggedIn = true; nextLoginUtc = DateTime.MinValue;
    }
    public ConnectionReading Read() {
        if (!settings.RouterEnabled) throw new IOException(L.T("Lectura de router desactivada"));
        for (int attempt = 0; attempt < 2; attempt++) {
            if (!loggedIn) Login();
            string response;
            try { response = Request(WanPath, null); }
            catch (WebException ex) {
                var http = ex.Response as HttpWebResponse;
                if (http == null || (http.StatusCode != HttpStatusCode.Unauthorized && http.StatusCode != HttpStatusCode.Forbidden)) throw;
                ex.Response.Close(); loggedIn = false; continue;
            }
            if (response.IndexOf("SessionTimeout", StringComparison.OrdinalIgnoreCase) >= 0 || response.IndexOf("Frm_Username", StringComparison.Ordinal) >= 0) {
                loggedIn = false; continue;
            }
            // Missing PPPoE data is not an expired router login. Keep the session.
            return ParseWan(response, utcNow());
        }
        throw new IOException(L.T("El router no acepto el inicio de sesion"));
    }
    internal static XmlDocument ParseXml(string xml) {
        var document = new XmlDocument { XmlResolver = null };
        using (var reader = XmlReader.Create(new StringReader(xml), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null })) document.Load(reader);
        return document;
    }
    internal RestartRequest PrepareRestart() {
        if (!settings.RouterEnabled) throw new IOException(L.T("Lectura de router desactivada"));
        for (int attempt = 0; attempt < 2; attempt++) {
            if (!loggedIn) Login();
            string view = Request("?_type=menuView&_tag=rebootAndReset&Menu3Location=0", null);
            if (view.IndexOf("SessionTimeout", StringComparison.OrdinalIgnoreCase) >= 0 || view.IndexOf("Frm_Username", StringComparison.Ordinal) >= 0) { loggedIn = false; continue; }
            return RestartRequest.Create(view, loginPage);
        }
        throw new IOException(L.T("El router no acepto el inicio de sesion"));
    }
    public bool Restart() {
        RestartRequest command = PrepareRestart();
        string response;
        try {
            // Send exactly once: a lost response can mean the router is already rebooting.
            response = Request(RestartRequest.Path, command.Body, command.Check);
        } catch (WebException) { return false; }
          catch (IOException) { return false; }
        finally { loggedIn = false; nextLoginUtc = utcNow().AddSeconds(60); }
        XmlDocument document;
        try { document = ParseXml(response); }
        catch (XmlException) { return false; }
        XmlNode result = document.SelectSingleNode("//IF_ERRORSTR");
        if (result == null) return false;
        if (result.InnerText != "SUCC") throw new IOException(L.T("El router rechazo el reinicio."));
        return true;
    }
    public static ConnectionReading ParseWan(string xml, DateTime now) {
        var document = ParseXml(xml);
        foreach (XmlNode instance in document.SelectNodes("//Instance")) {
            var fields = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
            foreach (XmlNode node in instance.ChildNodes) {
                if (node.Name != "ParaName") continue;
                XmlNode value = node.NextSibling;
                while (value != null && value.NodeType != XmlNodeType.Element) value = value.NextSibling;
                if (value != null && value.Name == "ParaValue") fields[node.InnerText] = value.InnerText;
            }
            string type, status, ip, uptime;
            if (!fields.TryGetValue("wantype", out type) || type != "pppoe" || !fields.TryGetValue("ConnStatus", out status) || status != "Connected" || !fields.TryGetValue("IPAddress", out ip) || !NetworkAddress.IsPublicV4(ip) || !fields.TryGetValue("UpTime", out uptime)) continue;
            long seconds;
            if (!Int64.TryParse(uptime, out seconds) || seconds < 0 || seconds > 315360000) continue;
            return new ConnectionReading { Ip = ip, ReadUtc = now, SessionStartUtc = now.AddSeconds(-seconds), FromRouter = true };
        }
        throw new IOException(L.T("No se encontro una sesion PPPoE IPv4 conectada"));
    }
}
