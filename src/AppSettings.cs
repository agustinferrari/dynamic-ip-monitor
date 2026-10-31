using System;
using System.IO;
using System.Text;
using System.Net;
using System.Drawing;
using System.Windows.Forms;
using System.Security.Cryptography;
using System.Web.Script.Serialization;

public static class AppFiles {
    public static readonly string DataDir = GetDataDir();
    static string GetDataDir() {
        string chosen = Environment.GetEnvironmentVariable("MONITORIP_DATA_DIR");
        string dir = String.IsNullOrEmpty(chosen) ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DynamicIPMonitor") : Path.GetFullPath(chosen);
        Directory.CreateDirectory(dir); return dir;
    }
}

public class AppSettings {
    public bool RouterEnabled;
    public string RouterUrl = "http://192.168.1.1/";
    public string Username = "";
    public string ProtectedPassword = "";
    public bool SetupComplete;
    public string Language = "auto";
    public static readonly string FilePath = Path.Combine(AppFiles.DataDir, "config.json");
    public static AppSettings Load() {
        if (!File.Exists(FilePath)) return new AppSettings();
        var settings = new JavaScriptSerializer().Deserialize<AppSettings>(File.ReadAllText(FilePath));
        if (settings == null) throw new IOException(L.T("Configuracion invalida"));
        settings.RouterUrl = ValidateUrl(settings.RouterUrl);
        return settings;
    }
    public void Save() { File.WriteAllText(FilePath, new JavaScriptSerializer().Serialize(this)); }
    public string Password() {
        if (String.IsNullOrEmpty(ProtectedPassword)) return "";
        return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(ProtectedPassword), null, DataProtectionScope.CurrentUser));
    }
    public void SetPassword(string password) {
        ProtectedPassword = String.IsNullOrEmpty(password) ? "" : Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(password), null, DataProtectionScope.CurrentUser));
    }
    public static string ValidateUrl(string value) {
        Uri uri; IPAddress address;
        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out uri) || (uri.Scheme != "http" && uri.Scheme != "https") || !String.IsNullOrEmpty(uri.UserInfo) || uri.AbsolutePath != "/" || !String.IsNullOrEmpty(uri.Query) || !String.IsNullOrEmpty(uri.Fragment) || !IPAddress.TryParse(uri.Host, out address) || address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork) throw new ArgumentException(L.T("Usa una URL local como http://192.168.1.1/"));
        byte[] b = address.GetAddressBytes();
        if (!(b[0] == 10 || b[0] == 127 || (b[0] == 192 && b[1] == 168) || (b[0] == 172 && b[1] >= 16 && b[1] <= 31))) throw new ArgumentException(L.T("La direccion del router debe ser una IPv4 privada de tu red local."));
        return uri.AbsoluteUri;
    }
}

public class SettingsWindow : Form {
    CheckBox enabled;
    TextBox url, username, password;
    CheckBox clearPassword;
    ComboBox language;
    readonly AppSettings current;
    public AppSettings Result;
    public SettingsWindow(AppSettings settings) {
        current = settings;
        Text = L.T("Configurar Dynamic IP Monitor"); ClientSize = new Size(560,437);
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent; Font = new Font("Segoe UI",10);
        AddLabel(L.T("Elegí cómo obtener la referencia de tu conexión."),18,18,520,26);
        enabled = new CheckBox { Text=L.T("Autorizar lectura e inicio de sesión en mi router ZTE F6600"), Location=new Point(18,54), AutoSize=true, Checked=settings.RouterEnabled };
        Controls.Add(enabled);
        AddLabel(L.T("Router (URL local)"),18,94,170,26);
        url = AddText(settings.RouterUrl,190,91);
        AddLabel(L.T("Usuario (opcional)"),18,133,170,26);
        username = AddText(settings.Username,190,130);
        AddLabel(L.T("Contraseña (opcional)"),18,172,170,26);
        password = AddText("",190,169); password.UseSystemPasswordChar=true;
        clearPassword = new CheckBox { Text=L.T("Borrar contraseña guardada"), Location=new Point(190,207), AutoSize=true };
        Controls.Add(clearPassword);
        AddLabel(L.T("Si dejás usuario y contraseña vacíos, usa los valores precargados\npor el panel, si existen. Una clave propia se protege con Windows.\nSolo IP pública también funciona: desmarcá la lectura del router.\nEl ciclo de 12 h es estimado; podés ajustarlo en el contador."),18,246,525,89);
        var save = new Button { Text=L.T("Guardar"), Location=new Point(320,348), Size=new Size(105,32) };
        var cancel = new Button { Text=L.T("Cancelar"), Location=new Point(435,348), Size=new Size(105,32), DialogResult=DialogResult.Cancel };
        Controls.Add(save); Controls.Add(cancel); AcceptButton=save; CancelButton=cancel;
        foreach (Control control in Controls) if (control.Top >= 54) control.Top += 42;
        AddLabel(L.T("Idioma"),18,54,170,26);
        language = new ComboBox { Location=new Point(190,51), Width=350, DropDownStyle=ComboBoxStyle.DropDownList };
        language.Items.AddRange(new object[] { L.T("Automático (Windows)"), "Español", "English" });
        language.SelectedIndex = settings.Language == "es" ? 1 : settings.Language == "en" ? 2 : 0;
        Controls.Add(language);
        L.Capture(this); L.Apply(this);
        language.SelectedIndexChanged += delegate {
            L.SetLanguage(language.SelectedIndex == 1 ? "es" : language.SelectedIndex == 2 ? "en" : "auto");
            L.Apply(this); string autoLabel=L.T("Automático (Windows)");
            if ((string)language.Items[0] != autoLabel) language.Items[0]=autoLabel;
        };
        FormClosed += delegate { if (Result == null) L.SetLanguage(current.Language); };
        save.Click += delegate {
            try {
                var next = new AppSettings { RouterEnabled=enabled.Checked, RouterUrl=AppSettings.ValidateUrl(url.Text), Username=username.Text.Trim(), ProtectedPassword=current.ProtectedPassword, SetupComplete=true, Language=language.SelectedIndex == 1 ? "es" : language.SelectedIndex == 2 ? "en" : "auto" };
                if (clearPassword.Checked) next.SetPassword("");
                if (password.Text.Length > 0) next.SetPassword(password.Text);
                next.Save(); Result=next; DialogResult=DialogResult.OK; Close();
            } catch (Exception ex) { MessageBox.Show(this, ex.Message,L.T("No se pudo guardar")); }
        };
    }
    void AddLabel(string text,int x,int y,int w,int h) { Controls.Add(new Label { Text=text, Location=new Point(x,y), Size=new Size(w,h) }); }
    TextBox AddText(string text,int x,int y) { var t=new TextBox { Text=text, Location=new Point(x,y), Width=350 }; Controls.Add(t); return t; }
}
