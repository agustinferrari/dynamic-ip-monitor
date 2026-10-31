using System;
using System.IO;
using System.Net;
using System.Drawing;
using System.Windows.Forms;
using System.ComponentModel;
using System.Web.Script.Serialization;
using System.Threading;
using Microsoft.Win32;

public class ConnectionState {
    public string Ip = "";
    public DateTime AnchorUtc;
    public DateTime LastSuccessUtc;
    public string Quality = "Referencia manual aproximada";
    public string AlertKey = "";
    public double PeriodHours = 12;
    public bool RouterVerified;
    public DateTime RouterLastReadUtc;
}

public static class MonitorLogic {
    public static string ObserveRouter(ConnectionState s, ConnectionReading reading) {
        bool hadRouter = s.RouterVerified;
        DateTime oldAnchor = s.AnchorUtc;
        bool sameIp = s.Ip == reading.Ip;
        string result = Observe(s, reading.Ip, reading.ReadUtc);
        bool newSession = hadRouter && (reading.SessionStartUtc - oldAnchor).TotalSeconds > 90;
        if (!hadRouter || Math.Abs((reading.SessionStartUtc - oldAnchor).TotalSeconds) > 90) {
            s.AnchorUtc = reading.SessionStartUtc;
            s.AlertKey = "";
        } else {
            // Keep a stable anchor so sub-second reading jitter does not repeat alerts.
            s.AnchorUtc = oldAnchor;
        }
        s.RouterVerified = true; s.RouterLastReadUtc = reading.ReadUtc;
        s.Quality = "Inicio de sesion confirmado por el router ZTE";
        if (newSession && sameIp) return "session";
        return result;
    }
    public static string Observe(ConnectionState s, string ip, DateTime now) {
        if (String.IsNullOrEmpty(s.Ip)) {
            s.Ip = ip; s.LastSuccessUtc = now; return "initial";
        }
        bool gap = s.LastSuccessUtc == DateTime.MinValue || (now - s.LastSuccessUtc).TotalSeconds > 120;
        string result = "same";
        if (s.Ip != ip) {
            s.Ip = ip; s.AnchorUtc = now; s.AlertKey = "";
            s.RouterVerified = false;
            s.Quality = gap ? "Cambio durante una pausa: hora incierta" : "Cambio observado entre dos lecturas";
            result = gap ? "gap" : "change";
        } else if (gap && s.LastSuccessUtc != DateTime.MinValue) {
            s.Quality = "Hubo una pausa sin monitoreo: estimacion incierta";
        }
        s.LastSuccessUtc = now;
        return result;
    }
    public static int AlertBand(double minutes) {
        if (minutes <= 0) return 0;
        if (minutes <= 1) return 1;
        if (minutes <= 5) return 5;
        if (minutes <= 15) return 15;
        if (minutes <= 30) return 30;
        return -1;
    }
}

public class MonitorWindow : Form {
    static readonly string Dir = AppFiles.DataDir;
    static readonly string StatePath = Path.Combine(Dir, "estado.json");
    readonly JavaScriptSerializer json = new JavaScriptSerializer();
    ConnectionState state;
    NotifyIcon tray;
    Icon countdownIcon;
    string countdownIconKey;
    Label clockLabel, estimateLabel, infoLabel, statusLabel;
    DateTimePicker manual;
    NumericUpDown hours;
    CheckBox top, startup;
    System.Windows.Forms.Timer uiTimer, pollTimer;
    BackgroundWorker worker;
    RouterReader router;
    AppSettings settings;
    string status = L.T("Esperando la primera lectura...");
    bool quitting;
    readonly Font countdownFont = new Font("Segoe UI", 40, FontStyle.Bold);
    readonly Font expiredFont = new Font("Segoe UI", 25, FontStyle.Bold);

    public MonitorWindow() {
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
        try { settings = AppSettings.Load(); }
        catch { settings = new AppSettings(); MessageBox.Show(L.T("No se pudo leer la configuracion. Revisala desde Configurar."),"Dynamic IP Monitor"); }
        L.SetLanguage(settings.Language);
        router = new RouterReader(settings);
        state = LoadState();
        Text = L.T("Dynamic IP Monitor - aviso de reconexion");
        ClientSize = new Size(650, 550);
        MinimumSize = new Size(666, 589);
        Font = new Font("Segoe UI", 10);
        BackColor = Color.FromArgb(20, 26, 38);
        ForeColor = Color.FromArgb(232, 238, 247);
        Icon = SystemIcons.Information;

        Label title = LabelAt(L.T("PROXIMO CAMBIO ESTIMADO"), 24, 23, 600, 30);
        title.ForeColor = Color.FromArgb(135, 167, 204);
        clockLabel = LabelAt("--:--:--", 20, 58, 610, 80);
        clockLabel.Font = countdownFont;
        estimateLabel = LabelAt("", 24, 145, 600, 30);
        infoLabel = LabelAt("", 24, 180, 600, 65);
        statusLabel = LabelAt("", 24, 250, 600, 45);
        statusLabel.ForeColor = Color.FromArgb(150, 183, 210);

        LabelAt(L.T("Inicio de sesion (hora local):"), 24, 310, 215, 28);
        manual = new DateTimePicker { Location = new Point(240,310), Size = new Size(225,27), Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy HH:mm:ss", Value = state.AnchorUtc == DateTime.MinValue ? DateTime.Now : state.AnchorUtc.ToLocalTime() };
        Controls.Add(manual);
        Button apply = ButtonAt(L.T("Usar esta hora"), 475, 307, 148);
        apply.Click += delegate { if (manual.Value > DateTime.Now.AddSeconds(5)) { MessageBox.Show(L.T("El ultimo inicio no puede estar en el futuro.")); return; } state.AnchorUtc = manual.Value.ToUniversalTime(); state.Quality = L.T("Referencia manual (el router la reemplaza si esta habilitado)"); state.RouterVerified = false; state.AlertKey = ""; SaveState(); Render(); };
        LabelAt(L.T("Ciclo estimado (horas):"), 24, 352, 215, 28);
        hours = new NumericUpDown { Location = new Point(240,352), Width = 75, Minimum = 1, Maximum = 168, DecimalPlaces = 2, Value = (decimal)Math.Max(1, Math.Min(168, state.PeriodHours)) };
        Controls.Add(hours);
        hours.ValueChanged += delegate { state.PeriodHours = (double)hours.Value; state.AlertKey = ""; SaveState(); Render(); };
        top = new CheckBox { Text = L.T("Siempre visible"), Location = new Point(340,352), AutoSize = true };
        top.CheckedChanged += delegate { TopMost = top.Checked; };
        Controls.Add(top);
        startup = new CheckBox { Text = L.T("Iniciar con Windows"), Location = new Point(24,394), AutoSize = true, Checked = IsStartup() };
        startup.CheckedChanged += delegate { SetStartup(); };
        Controls.Add(startup);
        Button test = ButtonAt(L.T("Probar aviso"), 240, 390, 130);
        test.Click += delegate { Notify(L.T("Prueba de aviso"), L.T("Los avisos aparecen aqui. Windows puede silenciarlos al jugar o en No molestar.")); };
        Button hide = ButtonAt(L.T("Ocultar a bandeja"), 390, 390, 180);
        hide.Click += delegate { Hide(); };
        Button configure = ButtonAt(L.T("Configurar"), 24, 432, 150);
        configure.Click += delegate { Configure(); };
        Button dataFolder = ButtonAt(L.T("Abrir mis datos"), 190, 432, 150);
        dataFolder.Click += delegate { System.Diagnostics.Process.Start("explorer.exe", "\"" + Dir + "\""); };
        Label note = LabelAt(L.T("Icono: horas completas; en rojo, minutos; ultimo minuto, segundos.\nVerde: 3 h o mas. Amarillo: 1 a 3 h. Rojo: menos de 1 h.\nAvisos: 30, 15, 5 y 1 min. El proximo corte es estimado."), 24, 487, 605, 48);
        note.Font = new Font("Segoe UI", 9);
        note.ForeColor = Color.FromArgb(156, 168, 188);

        tray = new NotifyIcon { Icon = SystemIcons.Information, Text = "Dynamic IP Monitor", Visible = true };
        var menu = new ContextMenuStrip();
        menu.Items.Add(L.T("Abrir contador"), null, delegate { ShowWindow(); });
        menu.Items.Add(L.T("Salir y detener monitoreo"), null, delegate { quitting = true; Close(); });
        tray.ContextMenuStrip = menu;
        tray.DoubleClick += delegate { ShowWindow(); };
        FormClosing += delegate(object sender, FormClosingEventArgs e) { if (!quitting) { e.Cancel = true; Hide(); } };
        FormClosed += delegate { pollTimer.Stop(); uiTimer.Stop(); tray.Visible = false; tray.Dispose(); if (countdownIcon != null) countdownIcon.Dispose(); };

        worker = new BackgroundWorker();
        worker.DoWork += delegate(object sender, DoWorkEventArgs e) {
            try { e.Result = router.Read(); }
            catch { e.Result = new ConnectionReading { Ip = FetchIp(), ReadUtc = DateTime.UtcNow }; }
        };
        worker.RunWorkerCompleted += Polled;
        pollTimer = new System.Windows.Forms.Timer { Interval = 30000 };
        pollTimer.Tick += delegate { Poll(); };
        uiTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        uiTimer.Tick += delegate { Render(); CheckAlerts(); };
        L.Capture(this); ApplyLocalization();
        pollTimer.Start(); uiTimer.Start(); Render();
        Shown += delegate {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--ui-preview") >= 0) { pollTimer.Stop(); return; }
            if (!settings.SetupComplete && Array.IndexOf(Environment.GetCommandLineArgs(), "--smoke-test") < 0) Configure();
            Poll();
            if (settings.SetupComplete && Array.IndexOf(Environment.GetCommandLineArgs(), "--tray") >= 0) Hide();
        };
    }

    Label LabelAt(string text, int x, int y, int width, int height) {
        var label = new Label { Text = text, Location = new Point(x,y), Size = new Size(width,height) }; Controls.Add(label); return label;
    }
    Button ButtonAt(string text, int x, int y, int width) {
        var b = new Button { Text = text, Location = new Point(x,y), Size = new Size(width,33), BackColor = Color.FromArgb(48,65,90), FlatStyle = FlatStyle.Flat, ForeColor = ForeColor }; Controls.Add(b); return b;
    }
    void ShowWindow() { Show(); WindowState = FormWindowState.Normal; Activate(); }
    void Configure() {
        if (worker.IsBusy) { MessageBox.Show(L.T("Hay una lectura en curso. Volve a intentar en unos segundos.")); return; }
        pollTimer.Stop();
        using (var dialog = new SettingsWindow(settings)) {
            if (dialog.ShowDialog(this) == DialogResult.OK) { settings = dialog.Result; router = new RouterReader(settings); }
        }
        L.SetLanguage(settings.Language); ApplyLocalization();
        pollTimer.Start(); Poll();
    }
    void ApplyLocalization() {
        L.Apply(this); manual.CustomFormat=L.DateTimePattern;
        hours.Text=hours.Value.ToString("F2",L.Culture);
        foreach (ToolStripItem item in tray.ContextMenuStrip.Items) {
            if (item.Tag == null) item.Tag=L.Source(item.Text);
            item.Text=L.T((string)item.Tag);
        }
        Render();
    }
    ConnectionState LoadState() {
        if (File.Exists(StatePath)) {
            try {
                var loaded = json.Deserialize<ConnectionState>(File.ReadAllText(StatePath));
                if (loaded == null || loaded.PeriodHours < 1 || loaded.PeriodHours > 168) throw new Exception();
                return loaded;
            } catch { MessageBox.Show(L.T("No se pudo leer estado.json. Hace falta obtener una nueva referencia."), "Dynamic IP Monitor"); }
        }
        return new ConnectionState { Quality = L.T("Sin referencia: configura el router o indica una hora") };
    }
    void SaveState() {
        try {
            File.WriteAllText(StatePath + ".tmp", json.Serialize(state));
            if (File.Exists(StatePath)) File.Replace(StatePath + ".tmp", StatePath, StatePath + ".bak");
            else File.Move(StatePath + ".tmp", StatePath);
        } catch (Exception ex) { status = L.T("No se pudo guardar el estado: ") + ex.Message; }
    }
    static string RequestIp(string url) {
        var req = (HttpWebRequest)WebRequest.Create(url);
        req.Timeout = 6000; req.ReadWriteTimeout = 6000; req.Proxy = null;
        using (var response = req.GetResponse())
        using (var reader = new StreamReader(response.GetResponseStream())) {
            string value = reader.ReadToEnd().Trim(); IPAddress ip;
            if (!IPAddress.TryParse(value, out ip) || ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork) throw new IOException(L.T("Respuesta IPv4 invalida"));
            byte[] b = ip.GetAddressBytes();
            if (b[0] == 0 || b[0] == 10 || b[0] == 127 || b[0] >= 224 || (b[0] == 192 && b[1] == 168) || (b[0] == 172 && b[1] >= 16 && b[1] <= 31) || (b[0] == 169 && b[1] == 254)) throw new IOException(L.T("IP no publica"));
            return value;
        }
    }
    string FetchIp() {
        string ip;
        try { ip = RequestIp("https://api.ipify.org"); }
        catch { ip = RequestIp("https://checkip.amazonaws.com"); }
        // Confirm every new address with an independent provider before resetting.
        if (!String.IsNullOrEmpty(state.Ip) && ip != state.Ip) {
            string confirm = RequestIp("https://ipv4.icanhazip.com");
            if (ip != confirm) throw new IOException(L.T("Las fuentes no coinciden; se reintentara"));
        }
        return ip;
    }
    void Poll() { if (!worker.IsBusy) worker.RunWorkerAsync(); }
    void Polled(object sender, RunWorkerCompletedEventArgs e) {
        if (quitting || IsDisposed) return;
        if (e.Error != null) { status = L.T("Sin lectura de IP. Se reintentara; el contador sigue siendo estimado."); Render(); return; }
        var reading = (ConnectionReading)e.Result;
        DateTime now = reading.ReadUtc;
        string oldIp = state.Ip;
        DateTime previousSuccess = state.LastSuccessUtc;
        DateTime previousAnchor = state.AnchorUtc;
        bool previouslyFromRouter = state.RouterVerified;
        string change = reading.FromRouter ? MonitorLogic.ObserveRouter(state, reading) : MonitorLogic.Observe(state, reading.Ip, now);
        status = (reading.FromRouter ? L.T("Router ZTE: ") : settings.RouterEnabled ? L.T("Router sin lectura; respaldo por IP publica: ") : L.T("Lectura por IP publica: ")) + now.ToLocalTime().ToString("HH:mm:ss");
        if (!reading.FromRouter && state.RouterVerified) state.Quality = L.T("Ultimo inicio leido del router; sin confirmacion actual");
        if (change == "change" || change == "gap" || change == "session") {
            try {
                if (!File.Exists(Path.Combine(Dir,"historial.csv"))) File.AppendAllText(Path.Combine(Dir,"historial.csv"), "ultima_lectura_anterior_utc,primera_lectura_nueva_utc,ip_anterior,ip_nueva,precision\r\n");
                File.AppendAllText(Path.Combine(Dir,"historial.csv"), previousSuccess.ToString("o") + "," + now.ToString("o") + "," + oldIp + "," + state.Ip + "," + change + "\r\n");
            } catch { status += L.T(" (no se pudo guardar historial)"); }
            manual.Value = state.AnchorUtc.ToLocalTime();
            Notify(change == "session" ? L.T("Nueva sesion de Internet detectada") : L.T("Cambio de IP detectado"), reading.FromRouter ? L.T("Inicio de sesion obtenido del router. Contador ajustado automaticamente.") : change == "gap" ? L.T("La IP cambio durante una pausa. Contador reiniciado desde ahora, con hora incierta.") : L.T("Contador reiniciado. Proximo cambio estimado en ") + state.PeriodHours + L.T(" horas."));
        }
        if (reading.FromRouter) manual.Value = state.AnchorUtc.ToLocalTime();
        if (reading.FromRouter && (!previouslyFromRouter || Math.Abs((previousAnchor - state.AnchorUtc).TotalSeconds) > 90)) {
            try {
                string sessionLog = Path.Combine(Dir,"sesiones-router.csv");
                if (!File.Exists(sessionLog)) File.AppendAllText(sessionLog,"lectura_utc,inicio_sesion_utc,ip_publica\r\n");
                File.AppendAllText(sessionLog,now.ToString("o") + "," + state.AnchorUtc.ToString("o") + "," + state.Ip + "\r\n");
            } catch { status += L.T(" (no se pudo guardar historial del router)"); }
        }
        SaveState(); Render();
    }
    void Render() {
        if (state.AnchorUtc == DateTime.MinValue) {
            UpdateTrayIcon(null);
            clockLabel.Text = L.T("SIN REFERENCIA"); clockLabel.Font = expiredFont;
            estimateLabel.Text = L.T("Configura el router o indica la hora del ultimo cambio.");
            infoLabel.Text = L.T("IP publica: ") + (String.IsNullOrEmpty(state.Ip) ? L.T("pendiente") : state.Ip) + L.T("\nSin referencia de inicio de sesion.");
            statusLabel.Text = L.Dynamic(status); tray.Text = L.T("Dynamic IP Monitor - esperando referencia"); return;
        }
        DateTime target = state.AnchorUtc.AddHours(state.PeriodHours);
        TimeSpan left = target - DateTime.UtcNow;
        UpdateTrayIcon(left);
        clockLabel.Text = left.TotalSeconds > 0 ? String.Format("{0:00}:{1:00}:{2:00}", (int)left.TotalHours, left.Minutes, left.Seconds) : L.T("HORA ESTIMADA PASADA");
        clockLabel.Font = left.TotalSeconds > 0 ? countdownFont : expiredFont;
        clockLabel.ForeColor = TrayCountdown.Describe(left).Foreground;
        estimateLabel.Text = target.ToLocalTime().ToString(L.TargetPattern,L.Culture) + L.T(" (hora local, estimada)");
        infoLabel.Text = L.T("IP publica: ") + (String.IsNullOrEmpty(state.Ip) ? L.T("pendiente") : state.Ip) + "\n" + L.T(state.Quality) + L.T("\nUltimo inicio: ") + state.AnchorUtc.ToLocalTime().ToString(L.ShortDateTimePattern,L.Culture);
        statusLabel.Text = L.Dynamic(status);
        tray.Text = left.TotalSeconds > 0 ? L.T("Dynamic IP Monitor - faltan ") + clockLabel.Text : L.T("Dynamic IP Monitor - estimacion vencida");
    }
    void UpdateTrayIcon(TimeSpan? left) {
        var description = TrayCountdown.Describe(left);
        if (description.Key == countdownIconKey) return;
        Icon replacement = TrayCountdown.Create(description);
        Icon previous = countdownIcon;
        tray.Icon = replacement; Icon = replacement;
        countdownIcon = replacement; countdownIconKey = description.Key;
        if (previous != null) previous.Dispose();
    }
    void CheckAlerts() {
        if (state.AnchorUtc == DateTime.MinValue) return;
        int band = MonitorLogic.AlertBand((state.AnchorUtc.AddHours(state.PeriodHours) - DateTime.UtcNow).TotalMinutes);
        if (band < 0) return;
        string key = state.AnchorUtc.Ticks + ":" + state.PeriodHours + ":" + band;
        if (state.AlertKey == key) return;
        state.AlertKey = key; SaveState();
        Notify(band == 0 ? L.T("Se alcanzo la hora estimada") : L.T("Posible reconexion en ") + L.Minutes(band), band == 0 ? L.T("Todavia no se detecto otro cambio de IP. La estimacion puede haber variado.") : L.T("Evita iniciar una carrera larga cerca del cambio estimado."));
    }
    void Notify(string title, string body) { tray.ShowBalloonTip(10000, title, body, ToolTipIcon.Info); }
    bool IsStartup() {
        using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run")) return key != null && (string)key.GetValue("DynamicIPMonitor", "") == "\"" + Application.ExecutablePath + "\" --tray";
    }
    void SetStartup() {
        try {
            using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run")) {
                if (startup.Checked) key.SetValue("DynamicIPMonitor", "\"" + Application.ExecutablePath + "\" --tray");
                else key.DeleteValue("DynamicIPMonitor", false);
            }
        } catch (Exception ex) { MessageBox.Show(L.T("No se pudo cambiar el inicio automatico: ") + ex.Message); }
    }
    [STAThread]
    public static void Main(string[] args) {
        try { L.SetLanguage(AppSettings.Load().Language); } catch { L.SetLanguage("auto"); }
        if (Array.IndexOf(args, "--self-test") >= 0) {
            try { SelfTest(); }
            catch (Exception ex) { File.WriteAllText(Path.Combine(Dir,"self-test-result.txt"),"FAIL: " + ex.ToString()); Environment.Exit(1); }
            return;
        }
        bool previewOnly = Array.IndexOf(args,"--ui-preview") >= 0;
        if (previewOnly || Array.IndexOf(args, "--smoke-test") >= 0) {
            Application.EnableVisualStyles();
            var window = new MonitorWindow();
            var secondPoll = new System.Windows.Forms.Timer { Interval = 3000 };
            secondPoll.Tick += delegate { secondPoll.Stop(); window.Poll(); };
            if (!previewOnly) secondPoll.Start();
            var stop = new System.Windows.Forms.Timer { Interval = previewOnly ? 500 : 8000 };
            stop.Tick += delegate {
                using (var bitmap = new Bitmap(window.Width, window.Height)) {
                    window.DrawToBitmap(bitmap, new Rectangle(0, 0, window.Width, window.Height));
                    bitmap.Save(Path.Combine(Dir, "preview.png"));
                }
                if (previewOnly) {
                    foreach (string language in new string[] { "es", "en" }) {
                        L.SetLanguage(language); window.ApplyLocalization();
                        using (var bitmap = new Bitmap(window.Width,window.Height)) {
                            window.DrawToBitmap(bitmap,new Rectangle(0,0,window.Width,window.Height));
                            bitmap.Save(Path.Combine(Dir,"preview-" + language + ".png"));
                        }
                        using (var dialog = new SettingsWindow(new AppSettings { Language=language })) {
                            dialog.Show();
                            using (var bitmap = new Bitmap(dialog.Width,dialog.Height)) {
                                dialog.DrawToBitmap(bitmap,new Rectangle(0,0,dialog.Width,dialog.Height));
                                bitmap.Save(Path.Combine(Dir,"config-preview-" + language + ".png"));
                            }
                            dialog.Close();
                        }
                    }
                    L.SetLanguage(window.settings.Language); window.ApplyLocalization();
                }
                File.WriteAllText(Path.Combine(Dir,"smoke-test-result.txt"), window.status + "\nIP leida: " + window.state.Ip + "\nFuente router: " + window.state.RouterVerified + "\nInicio de sesion: " + window.state.AnchorUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss") + "\nInicios de sesion HTTP: " + window.router.LoginCount);
                stop.Stop(); window.quitting = true; window.Close();
            };
            stop.Start(); Application.Run(window); return;
        }
        bool first;
        using (var mutex = new Mutex(true, "Local\\DynamicIPMonitor-User", out first)) {
            if (!first) { MessageBox.Show(L.T("El monitor ya esta abierto. Busca el icono en la bandeja junto al reloj.")); return; }
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false); Application.Run(new MonitorWindow());
        }
    }
    static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
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
        L.SelfTest();
        var translatedSettings = new AppSettings { Language="en" };
        Assert(new JavaScriptSerializer().Deserialize<AppSettings>(new JavaScriptSerializer().Serialize(translatedSettings)).Language == "en","language preference persistence");
        TrayCountdown.SelfTest(Dir);
        File.WriteAllText(Path.Combine(Dir,"self-test-result.txt"), "PASS: initial, unchanged IP, reset, offline gap, alert bands, persistence, WAN parsing, management interface filtering, router recovery after sleep, alert deduplication, same-IP reconnection, public fallback, fresh-install reference, router opt-in, URL validation, DPAPI protection, tray countdown, color boundaries, icon rendering, Spanish/English catalogs, Windows language detection, saved language preference.");
    }
}
