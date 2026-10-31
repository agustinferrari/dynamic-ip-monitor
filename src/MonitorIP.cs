using System;
using System.IO;
using System.Net;
using System.Drawing;
using System.Windows.Forms;
using System.ComponentModel;
using System.Threading;
using Microsoft.Win32;

public class MonitorWindow : Form {
    static readonly string Dir = AppFiles.DataDir;
    static readonly string StatePath = Path.Combine(Dir, "estado.json");
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
    BackgroundWorker restartWorker;
    Button restartButton;
    Button refreshButton, advancedButton;
    Panel advancedPanel;
    Label trayHelp, routerHelp;
    bool advancedExpanded;
    DateTime restartResumeUtc;
    bool awaitingRouter;
    RouterReader router;
    AppSettings settings;
    string statusKey = "Esperando la primera lectura...", statusDetail = "", statusWarning = "";
    bool quitting;
    readonly Font countdownFont = new Font("Segoe UI", 40, FontStyle.Bold);
    readonly Font expiredFont = new Font("Segoe UI", 25, FontStyle.Bold);

    public MonitorWindow(bool startInTray = false) {
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
        try { settings = AppSettings.Load(); }
        catch { settings = new AppSettings(); MessageBox.Show(L.T("No se pudo leer la configuracion. Revisala desde Configurar."),"Dynamic IP Monitor"); }
        L.SetLanguage(settings.Language);
        router = new RouterReader(settings);
        state = LoadState();
        Text = L.T("Dynamic IP Monitor - aviso de reconexion");
        ClientSize = new Size(700, 650);
        MinimumSize = new Size(716, 560);
        AutoScroll = true;
        DoubleBuffered = true;
        Font = new Font("Segoe UI", 10);
        BackColor = Color.FromArgb(20, 26, 38);
        ForeColor = Color.FromArgb(232, 238, 247);
        Icon = SystemIcons.Information;

        Label title = LabelAt(L.T("Próximo cambio estimado"), 24, 20, 632, 26);
        title.ForeColor = Color.FromArgb(135, 167, 204);
        clockLabel = LabelAt("--:--:--", 20, 48, 636, 80);
        clockLabel.Font = countdownFont;
        estimateLabel = LabelAt("", 24, 132, 632, 30);
        Button hide = ButtonAt(L.T("Seguir en bandeja"), 24, 178, 232);
        hide.Height = 44; hide.TabIndex = 0;
        hide.BackColor = Color.FromArgb(46,109,180); hide.FlatAppearance.BorderSize = 0;
        hide.Click += delegate { Hide(); };
        refreshButton = ButtonAt(L.T("Actualizar ahora"), 268, 178, 184);
        refreshButton.Height = 44; refreshButton.TabIndex = 1;
        refreshButton.Click += delegate { RefreshReading(); };
        Button configure = ButtonAt(L.T("Configurar"), 464, 178, 192);
        configure.Height = 44; configure.TabIndex = 2;
        configure.Click += delegate { Configure(); };
        statusLabel = LabelAt("", 24, 241, 632, 44);
        statusLabel.ForeColor = Color.FromArgb(150, 183, 210);
        infoLabel = LabelAt("", 24, 295, 632, 68);
        Button test = ButtonAt(L.T("Probar aviso"), 24, 372, 170);
        test.TabIndex = 3;
        test.Click += delegate { Notify(L.T("Prueba de aviso"), L.T("Los avisos aparecen aqui. Windows puede silenciarlos al jugar o en No molestar.")); };
        Label alertsHelp = LabelAt(L.T("Avisos a los 30, 15, 5 y 1 min.\nWindows puede silenciarlos al jugar."), 210, 370, 446, 44);
        alertsHelp.ForeColor = Color.FromArgb(156,176,199);
        Controls.Add(new Panel { Location = new Point(24,424), Size = new Size(632,1), BackColor = Color.FromArgb(51,67,91), TabStop = false });
        Label routerTitle = LabelAt(L.T("Control del router"), 24, 443, 400, 24);
        routerHelp = LabelAt("", 24, 473, 400, 44);
        routerHelp.Font = new Font("Segoe UI",9); routerHelp.ForeColor = Color.FromArgb(156,176,199);
        restartButton = ButtonAt(L.T("Reiniciar router"), 456, 455, 200);
        restartButton.Height = 40; restartButton.TabIndex = 4;
        restartButton.BackColor = BackColor; restartButton.ForeColor = Color.FromArgb(244,161,168);
        restartButton.FlatAppearance.BorderColor = Color.FromArgb(160,83,98);
        restartButton.Click += delegate { RestartRouter(); };
        advancedButton = ButtonAt(L.T("Mostrar ajustes avanzados"), 24, 535, 280);
        advancedButton.TabIndex = 5;
        advancedButton.Click += delegate { ToggleAdvanced(); };
        advancedPanel = new Panel { Location = new Point(24,583), Size = new Size(632,207), Visible = false, TabIndex = 6 };
        Controls.Add(advancedPanel);

        Label referenceTitle = LabelAt(L.T("Inicio de sesion (hora local):"), 0, 0, 350, 26);
        advancedPanel.Controls.Add(referenceTitle);
        manual = new DateTimePicker { Location = new Point(0,30), Size = new Size(270,27), Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy HH:mm:ss", Value = state.AnchorUtc == DateTime.MinValue ? DateTime.Now : state.AnchorUtc.ToLocalTime(), TabIndex = 0 };
        advancedPanel.Controls.Add(manual);
        Button apply = ButtonAt(L.T("Usar esta hora"), 284, 27, 180);
        advancedPanel.Controls.Add(apply); apply.TabIndex = 1;
        apply.Click += delegate { if (manual.Value > DateTime.Now.AddSeconds(5)) { MessageBox.Show(L.T("El ultimo inicio no puede estar en el futuro.")); return; } state.AnchorUtc = manual.Value.ToUniversalTime(); state.Quality = "Referencia manual (el router la reemplaza si esta habilitado)"; state.RouterVerified = false; state.AlertKey = ""; SaveState(); Render(); };
        Label periodTitle = LabelAt(L.T("Ciclo estimado (horas):"), 0, 80, 230, 28);
        advancedPanel.Controls.Add(periodTitle);
        hours = new NumericUpDown { Location = new Point(240,80), Width = 85, Minimum = 1, Maximum = 168, DecimalPlaces = 2, Value = (decimal)Math.Max(1, Math.Min(168, state.PeriodHours)), TabIndex = 2 };
        advancedPanel.Controls.Add(hours);
        hours.ValueChanged += delegate { state.PeriodHours = (double)hours.Value; state.AlertKey = ""; SaveState(); Render(); };
        top = new CheckBox { Text = L.T("Siempre visible"), Location = new Point(310,125), AutoSize = true, TabIndex = 4 };
        top.CheckedChanged += delegate { TopMost = top.Checked; };
        advancedPanel.Controls.Add(top);
        startup = new CheckBox { Text = L.T("Iniciar con Windows"), Location = new Point(0,125), AutoSize = true, Checked = IsStartup(), TabIndex = 3 };
        startup.CheckedChanged += StartupChanged;
        advancedPanel.Controls.Add(startup);
        Button dataFolder = ButtonAt(L.T("Abrir mis datos"), 0, 164, 200);
        advancedPanel.Controls.Add(dataFolder); dataFolder.TabIndex = 5;
        dataFolder.Click += delegate { System.Diagnostics.Process.Start("explorer.exe", "\"" + Dir + "\""); };
        trayHelp = LabelAt(L.T("Bandeja: horas completas; en rojo, minutos; último minuto, segundos.\nVerde: 3 h o mas. Amarillo: 1 a 3 h. Rojo: menos de 1 h."), 24, 583, 632, 48);
        trayHelp.Font = new Font("Segoe UI", 9);
        trayHelp.ForeColor = Color.FromArgb(156,176,199);
        AutoScrollMinSize = new Size(0,647);

        tray = new NotifyIcon { Icon = SystemIcons.Information, Text = "Dynamic IP Monitor", Visible = true };
        var menu = new ContextMenuStrip();
        menu.Items.Add(L.T("Abrir contador"), null, delegate { ShowWindow(); });
        menu.Items.Add(L.T("Salir y detener monitoreo"), null, delegate { quitting = true; Close(); });
        tray.ContextMenuStrip = menu;
        tray.DoubleClick += delegate { ShowWindow(); };
        FormClosing += delegate(object sender, FormClosingEventArgs e) {
            if (!quitting && e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); }
            else quitting = true;
        };
        FormClosed += delegate {
            pollTimer.Dispose(); uiTimer.Dispose(); tray.Visible = false;
            tray.ContextMenuStrip.Dispose(); tray.Dispose();
            if (countdownIcon != null) countdownIcon.Dispose();
            countdownFont.Dispose(); expiredFont.Dispose();
        };

        worker = new BackgroundWorker();
        worker.DoWork += delegate(object sender, DoWorkEventArgs e) {
            try { e.Result = router.Read(); }
            catch { e.Result = new ConnectionReading { Ip = FetchIp(), ReadUtc = DateTime.UtcNow }; }
        };
        worker.RunWorkerCompleted += Polled;
        restartWorker = new BackgroundWorker();
        restartWorker.DoWork += delegate(object sender, DoWorkEventArgs e) { e.Result = router.Restart(); };
        restartWorker.RunWorkerCompleted += delegate(object sender, RunWorkerCompletedEventArgs e) {
            if (quitting || IsDisposed) return;
            if (e.Error != null) {
                awaitingRouter = false;
                SetStatus("No se pudo reiniciar el router. Revisa la conexion y la configuracion.");
                MessageBox.Show(this, StatusText, L.T("Reiniciar router"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            } else {
                awaitingRouter = true;
                restartResumeUtc = DateTime.UtcNow.AddSeconds(60);
                SetStatus((bool)e.Result ? "Reinicio aceptado. Esperando que vuelva Internet..." : "Respuesta no confirmada. Esperando al router; no se repetira el reinicio.");
            }
            pollTimer.Start(); Render();
        };
        pollTimer = new System.Windows.Forms.Timer { Interval = 30000 };
        pollTimer.Tick += delegate { Poll(); };
        uiTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        uiTimer.Tick += delegate { Render(); CheckAlerts(); };
        L.Capture(this); ApplyLocalization();
        pollTimer.Start(); uiTimer.Start(); Render();
        Shown += delegate {
            if (!settings.SetupComplete) Configure();
            Poll();
            if (settings.SetupComplete && startInTray) Hide();
        };
    }

    Label LabelAt(string text, int x, int y, int width, int height) {
        var label = new Label { Text = text, Location = new Point(x,y), Size = new Size(width,height) }; Controls.Add(label); return label;
    }
    Button ButtonAt(string text, int x, int y, int width) {
        var b = new Button { Text = text, Location = new Point(x,y), Size = new Size(width,33), BackColor = Color.FromArgb(34,46,65), FlatStyle = FlatStyle.Flat, ForeColor = ForeColor };
        b.FlatAppearance.BorderColor = Color.FromArgb(65,83,109);
        Controls.Add(b); return b;
    }
    void ToggleAdvanced() {
        // Restore logical coordinates before moving scrollable content.
        AutoScrollPosition = Point.Empty;
        advancedExpanded = !advancedExpanded;
        advancedPanel.Visible = advancedExpanded;
        advancedButton.Tag = advancedExpanded ? "Ocultar ajustes avanzados" : "Mostrar ajustes avanzados";
        advancedButton.Text = L.T((string)advancedButton.Tag);
        trayHelp.Top = advancedExpanded ? 812 : 583;
        AutoScrollMinSize = new Size(0,trayHelp.Bottom + 16);
    }
    void RefreshReading() {
        if (worker.IsBusy || restartWorker.IsBusy || DateTime.UtcNow < restartResumeUtc) return;
        SetStatus("Actualizando lectura..."); Poll(); Render();
    }
    void ShowWindow() { Show(); WindowState = FormWindowState.Normal; Activate(); }
    void Configure() {
        if (worker.IsBusy || restartWorker.IsBusy) { MessageBox.Show(L.T("Hay una lectura en curso. Volve a intentar en unos segundos.")); return; }
        pollTimer.Stop();
        using (var dialog = new SettingsWindow(settings)) {
            if (dialog.ShowDialog(this) == DialogResult.OK) { settings = dialog.Result; router = new RouterReader(settings); awaitingRouter = false; restartResumeUtc = DateTime.MinValue; }
        }
        L.SetLanguage(settings.Language); ApplyLocalization();
        pollTimer.Start(); Poll();
    }
    void RestartRouter() {
        if (!settings.RouterEnabled || worker.IsBusy || restartWorker.IsBusy || awaitingRouter) return;
        pollTimer.Stop();
        string warning = L.T("Se cortara Internet para todos los dispositivos durante varios minutos.\nEl contador se ajustara al leer la nueva sesion.\n\nReiniciar este router?") + "\n" + settings.RouterUrl;
        if (MessageBox.Show(this, warning, L.T("Reiniciar router"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) { pollTimer.Start(); return; }
        SetStatus("Enviando solicitud de reinicio...");
        restartWorker.RunWorkerAsync(); Render();
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
        if (JsonStore.Exists(StatePath)) {
            try {
                return JsonStore.Read<ConnectionState>(StatePath, MonitorLogic.ValidState);
            } catch { MessageBox.Show(L.T("No se pudo leer estado.json. Hace falta obtener una nueva referencia."), "Dynamic IP Monitor"); }
        }
        return new ConnectionState { Quality = "Sin referencia: configura el router o indica una hora" };
    }
    void SaveState() {
        try {
            JsonStore.Write(StatePath, state);
        } catch (Exception ex) { SetStatus("No se pudo guardar el estado: ", ex.Message); }
    }
    void SetStatus(string key, string detail = "") { statusKey=key; statusDetail=detail; statusWarning=""; }
    string StatusText { get { return L.T(statusKey) + statusDetail + L.T(statusWarning); } }
    static string RequestIp(string url) {
        var req = (HttpWebRequest)WebRequest.Create(url);
        req.Timeout = 6000; req.ReadWriteTimeout = 6000; req.Proxy = null;
        using (var response = req.GetResponse())
        using (var reader = new StreamReader(response.GetResponseStream())) {
            string value = reader.ReadToEnd().Trim();
            if (!NetworkAddress.IsPublicV4(value)) throw new IOException(L.T("IP no publica"));
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
    void Poll() { if (!worker.IsBusy && !restartWorker.IsBusy && DateTime.UtcNow >= restartResumeUtc) worker.RunWorkerAsync(); }
    void Polled(object sender, RunWorkerCompletedEventArgs e) {
        if (quitting || IsDisposed) return;
        if (e.Error != null) { SetStatus("Sin lectura de IP. Se reintentara; el contador sigue siendo estimado."); Render(); return; }
        var reading = (ConnectionReading)e.Result;
        if (reading.FromRouter) awaitingRouter = false;
        DateTime now = reading.ReadUtc;
        string oldIp = state.Ip;
        DateTime previousSuccess = state.LastSuccessUtc;
        DateTime previousAnchor = state.AnchorUtc;
        bool previouslyFromRouter = state.RouterVerified;
        string change = reading.FromRouter ? MonitorLogic.ObserveRouter(state, reading) : MonitorLogic.Observe(state, reading.Ip, now);
        SetStatus(reading.FromRouter ? "Router ZTE: " : settings.RouterEnabled ? "Router sin lectura; respaldo por IP publica: " : "Lectura por IP publica: ", now.ToLocalTime().ToString("HH:mm:ss"));
        if (awaitingRouter) SetStatus("Internet disponible. Esperando datos del router; se reintenta cada 30 segundos.");
        if (!reading.FromRouter && state.RouterVerified) state.Quality = "Ultimo inicio leido del router; sin confirmacion actual";
        if (change == "change" || change == "gap" || change == "session") {
            try {
                if (!File.Exists(Path.Combine(Dir,"historial.csv"))) File.AppendAllText(Path.Combine(Dir,"historial.csv"), "ultima_lectura_anterior_utc,primera_lectura_nueva_utc,ip_anterior,ip_nueva,precision\r\n");
                File.AppendAllText(Path.Combine(Dir,"historial.csv"), previousSuccess.ToString("o") + "," + now.ToString("o") + "," + oldIp + "," + state.Ip + "," + change + "\r\n");
            } catch { statusWarning = " (no se pudo guardar historial)"; }
            Notify(change == "session" ? L.T("Nueva sesion de Internet detectada") : L.T("Cambio de IP detectado"), reading.FromRouter ? L.T("Inicio de sesion obtenido del router. Contador ajustado automaticamente.") : change == "gap" ? L.T("La IP cambio durante una pausa. Contador reiniciado desde ahora, con hora incierta.") : L.T("Contador reiniciado. Proximo cambio estimado en ") + state.PeriodHours + L.T(" horas."));
        }
        if (state.AnchorUtc != previousAnchor) manual.Value = state.AnchorUtc.ToLocalTime();
        if (reading.FromRouter && (!previouslyFromRouter || oldIp != state.Ip || Math.Abs((previousAnchor - state.AnchorUtc).TotalSeconds) > 90)) {
            try {
                string sessionLog = Path.Combine(Dir,"sesiones-router.csv");
                if (!File.Exists(sessionLog)) File.AppendAllText(sessionLog,"lectura_utc,inicio_sesion_utc,ip_publica\r\n");
                File.AppendAllText(sessionLog,now.ToString("o") + "," + state.AnchorUtc.ToString("o") + "," + state.Ip + "\r\n");
            } catch { statusWarning = " (no se pudo guardar historial del router)"; }
        }
        SaveState(); Render();
    }
    void Render() {
        routerHelp.Text = L.T(settings.RouterEnabled ? "Reiniciar corta Internet en todos los dispositivos.\nTe pediremos confirmación antes de continuar." : "Habilita la lectura del router en Configurar\npara poder reiniciarlo desde aqui.");
        refreshButton.Enabled = !worker.IsBusy && !restartWorker.IsBusy && DateTime.UtcNow >= restartResumeUtc;
        restartButton.Enabled = settings.RouterEnabled && !worker.IsBusy && !restartWorker.IsBusy && !awaitingRouter;
        if (awaitingRouter || restartWorker.IsBusy) {
            UpdateTrayIcon(null); clockLabel.Font = expiredFont; clockLabel.Text = L.T("RECONECTANDO...");
            clockLabel.ForeColor = TrayCountdown.Describe(null).Foreground;
            estimateLabel.Text = L.T("Esperando una nueva lectura del router.");
            infoLabel.Text = L.T("IP publica: ") + (String.IsNullOrEmpty(state.Ip) ? L.T("pendiente") : state.Ip);
            statusLabel.Text = StatusText; tray.Text = L.T("Dynamic IP Monitor - reconectando"); return;
        }
        if (state.AnchorUtc == DateTime.MinValue) {
            UpdateTrayIcon(null);
            clockLabel.Text = L.T("SIN REFERENCIA"); clockLabel.Font = expiredFont;
            clockLabel.ForeColor = TrayCountdown.Describe(null).Foreground;
            estimateLabel.Text = L.T("Configura el router o indica la hora del ultimo cambio.");
            infoLabel.Text = L.T("IP publica: ") + (String.IsNullOrEmpty(state.Ip) ? L.T("pendiente") : state.Ip) + L.T("\nSin referencia de inicio de sesion.");
            statusLabel.Text = StatusText; tray.Text = L.T("Dynamic IP Monitor - esperando referencia"); return;
        }
        DateTime target = state.AnchorUtc.AddHours(state.PeriodHours);
        TimeSpan left = target - DateTime.UtcNow;
        UpdateTrayIcon(left);
        clockLabel.Text = left.TotalSeconds > 0 ? String.Format("{0:00}:{1:00}:{2:00}", (int)left.TotalHours, left.Minutes, left.Seconds) : L.T("HORA ESTIMADA PASADA");
        clockLabel.Font = left.TotalSeconds > 0 ? countdownFont : expiredFont;
        clockLabel.ForeColor = TrayCountdown.Describe(left).Foreground;
        estimateLabel.Text = target.ToLocalTime().ToString(L.TargetPattern,L.Culture) + L.T(" (hora local, estimada)");
        infoLabel.Text = L.T("IP publica: ") + (String.IsNullOrEmpty(state.Ip) ? L.T("pendiente") : state.Ip) + "\n" + L.T(state.Quality) + L.T("\nUltimo inicio: ") + state.AnchorUtc.ToLocalTime().ToString(L.ShortDateTimePattern,L.Culture);
        statusLabel.Text = StatusText;
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
        if (awaitingRouter || restartWorker.IsBusy) return;
        if (state.AnchorUtc == DateTime.MinValue) return;
        int band = MonitorLogic.AlertBand((state.AnchorUtc.AddHours(state.PeriodHours) - DateTime.UtcNow).TotalMinutes);
        if (band < 0) return;
        string key = MonitorLogic.AlertKey(state, band);
        if (state.AlertKey == key) return;
        state.AlertKey = key; SaveState();
        Notify(band == 0 ? L.T("Se alcanzo la hora estimada") : L.T("Posible reconexion en ") + L.Minutes(band), band == 0 ? L.T("Todavia no se detecto otro cambio de IP. La estimacion puede haber variado.") : L.T("Evita iniciar una carrera larga cerca del cambio estimado."));
    }
    void Notify(string title, string body) { tray.ShowBalloonTip(10000, title, body, ToolTipIcon.Info); }
    void StartupChanged(object sender, EventArgs e) { SetStartup(); }
    bool IsStartup() {
        try {
            using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run")) return key != null && key.GetValue("DynamicIPMonitor", "") as string == "\"" + Application.ExecutablePath + "\" --tray";
        } catch (UnauthorizedAccessException) { return false; }
          catch (System.Security.SecurityException) { return false; }
    }
    void SetStartup() {
        try {
            using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run")) {
                if (startup.Checked) key.SetValue("DynamicIPMonitor", "\"" + Application.ExecutablePath + "\" --tray");
                else key.DeleteValue("DynamicIPMonitor", false);
            }
        } catch (Exception ex) {
            startup.CheckedChanged -= StartupChanged;
            startup.Checked = IsStartup();
            startup.CheckedChanged += StartupChanged;
            MessageBox.Show(L.T("No se pudo cambiar el inicio automatico: ") + ex.Message);
        }
    }
    [STAThread]
    public static void Main(string[] args) {
        try { L.SetLanguage(AppSettings.Load().Language); } catch { L.SetLanguage("auto"); }
        bool first;
        using (var mutex = new Mutex(true, "Local\\DynamicIPMonitor-User", out first)) {
            if (!first) { MessageBox.Show(L.T("El monitor ya esta abierto. Busca el icono en la bandeja junto al reloj.")); return; }
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false); Application.Run(new MonitorWindow(Array.IndexOf(args,"--tray") >= 0));
        }
    }
}
