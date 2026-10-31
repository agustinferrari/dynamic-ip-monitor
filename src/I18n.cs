using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Windows.Forms;

// Spanish source strings are stable keys. Add every new UI message in both languages.
public static class L {
    static readonly CultureInfo WindowsLanguage = CultureInfo.CurrentUICulture;
    public static string Current = "es";
    public static CultureInfo Culture { get { return CultureInfo.GetCultureInfo(Current == "es" ? "es-UY" : "en-US"); } }
    public static string DateTimePattern { get { return Current == "es" ? "dd/MM/yyyy HH:mm:ss" : "MM/dd/yyyy HH:mm:ss"; } }
    public static string ShortDateTimePattern { get { return Current == "es" ? "dd/MM HH:mm:ss" : "MM/dd HH:mm:ss"; } }
    public static string TargetPattern { get { return Current == "es" ? "dddd dd/MM, HH:mm:ss" : "dddd MM/dd, HH:mm:ss"; } }
    public static string Resolve(string language, CultureInfo system) { return language == "es" || language == "en" ? language : system.TwoLetterISOLanguageName == "es" ? "es" : "en"; }
    public static void SetLanguage(string language) {
        Current = Resolve(language,WindowsLanguage);
        Thread.CurrentThread.CurrentCulture = Culture;
        Thread.CurrentThread.CurrentUICulture = Culture;
    }
    internal static readonly string[,] Entries = {
        {"Habilita la lectura del router en Configurar\npara poder reiniciarlo desde aqui.","Enable router reading in Settings\nto restart it from here."},
        {"Próximo cambio estimado","Estimated next reconnect"},
        {"Seguir en bandeja","Keep in system tray"},
        {"Actualizar ahora","Refresh now"},
        {"Actualizando lectura...","Refreshing connection..."},
        {"Avisos a los 30, 15, 5 y 1 min.\nWindows puede silenciarlos al jugar.","Alerts at 30, 15, 5 and 1 min.\nWindows may silence them while gaming."},
        {"Control del router","Router control"},
        {"Reiniciar corta Internet en todos los dispositivos.\nTe pediremos confirmación antes de continuar.","Restart disconnects all devices from Internet.\nWe will ask for confirmation before continuing."},
        {"Mostrar ajustes avanzados","Show advanced settings"},
        {"Ocultar ajustes avanzados","Hide advanced settings"},
        {"Bandeja: horas completas; en rojo, minutos (45m); último minuto, <1m.\nVerde: 3 h o mas. Amarillo: 1 a 3 h. Rojo: menos de 1 h.","Tray: whole hours; minutes in red (45m); final minute: <1m.\nGreen: 3 h or more. Yellow: 1 to 3 h. Red: less than 1 h."},
        {"Internet disponible. Esperando datos del router; se reintenta cada 30 segundos.","Internet available. Waiting for router data; retrying every 30 seconds."},
        {"Reiniciar router","Restart router"},
        {"Se cortara Internet para todos los dispositivos durante varios minutos.\nEl contador se ajustara al leer la nueva sesion.\n\nReiniciar este router?","Internet will disconnect for all devices for several minutes.\nThe countdown will adjust when the new session is read.\n\nRestart this router?"},
        {"No se pudo reiniciar el router. Revisa la conexion y la configuracion.","Could not restart the router. Check the connection and settings."},
        {"Reinicio aceptado. Esperando que vuelva Internet...","Restart accepted. Waiting for Internet to return..."},
        {"Respuesta no confirmada. Esperando al router; no se repetira el reinicio.","Response unconfirmed. Waiting for the router; restart will not be repeated."},
        {"Enviando solicitud de reinicio...","Sending restart request..."},
        {"RECONECTANDO...","RECONNECTING..."},
        {"Esperando una nueva lectura del router.","Waiting for a new router reading."},
        {"Dynamic IP Monitor - reconectando","Dynamic IP Monitor - reconnecting"},
        {"El router rechazo el reinicio.","The router rejected the restart."},
        {"El firmware no permite preparar el reinicio.","This firmware cannot prepare the restart request."},
        {"Automático (Windows)","Automatic (Windows)"},
        {"Idioma","Language"},
        {"Configurar Dynamic IP Monitor","Configure Dynamic IP Monitor"},
        {"Elegí cómo obtener la referencia de tu conexión.","Choose how to obtain your connection's reference time."},
        {"Autorizar lectura e inicio de sesión en mi router ZTE F6600","Allow sign-in and status reading on my ZTE F6600 router"},
        {"Router (URL local)","Router (local URL)"},
        {"Usuario (opcional)","Username (optional)"},
        {"Contraseña (opcional)","Password (optional)"},
        {"Borrar contraseña guardada","Remove saved password"},
        {"Si dejás usuario y contraseña vacíos, usa los valores precargados\npor el panel, si existen. Una clave propia se protege con Windows.\nSolo IP pública también funciona: desmarcá la lectura del router.\nEl ciclo de 12 h es estimado; podés ajustarlo en el contador.","Leave credentials blank to use values prefilled by the router,\nif available. Your own password is protected by Windows.\nFor public IP only, leave router reading unchecked.\nThe 12-hour cycle is an estimate; adjust it in the main window."},
        {"Guardar","Save"}, {"Cancelar","Cancel"},
        {"No se pudo guardar","Could not save"},
        {"Usa una URL local como http://192.168.1.1/","Use a local URL such as http://192.168.1.1/"},
        {"La direccion del router debe ser una IPv4 privada de tu red local.","The router address must be a private IPv4 address on your local network."},
        {"Esperando la primera lectura...","Waiting for the first reading..."},
        {"No se pudo leer la configuracion. Revisala desde Configurar.","Could not read configuration. Check it in Settings."},
        {"Dynamic IP Monitor - aviso de reconexion","Dynamic IP Monitor - reconnection alert"},
        {"PROXIMO CAMBIO ESTIMADO","NEXT ESTIMATED CHANGE"},
        {"Inicio de sesion (hora local):","Session start (local time):"},
        {"Usar esta hora","Use this time"},
        {"El ultimo inicio no puede estar en el futuro.","The last session start cannot be in the future."},
        {"Referencia manual (el router la reemplaza si esta habilitado)","Manual reference (replaced by router reading if enabled)"},
        {"Ciclo estimado (horas):","Estimated cycle (hours):"},
        {"Siempre visible","Always on top"},
        {"Iniciar con Windows","Start with Windows"},
        {"Probar aviso","Test alert"},
        {"Prueba de aviso","Test notification"},
        {"Los avisos aparecen aqui. Windows puede silenciarlos al jugar o en No molestar.","Notifications appear here. Windows may silence them while gaming or in Do not disturb."},
        {"Ocultar a bandeja","Hide to tray"},
        {"Configurar","Settings"}, {"Abrir mis datos","Open my data"},
        {"Abrir contador","Open countdown"},
        {"Salir y detener monitoreo","Exit and stop monitoring"},
        {"Hay una lectura en curso. Volve a intentar en unos segundos.","A reading is in progress. Try again in a few seconds."},
        {"No se pudo leer estado.json. Hace falta obtener una nueva referencia.","Could not read estado.json. A new reference is needed."},
        {"Sin referencia: configura el router o indica una hora","No reference: configure the router or enter a time"},
        {"No se pudo guardar el estado: ","Could not save state: "},
        {"IP no publica","Non-public IP address"},
        {"Las fuentes no coinciden; se reintentara","Sources disagree; will retry"},
        {"Sin lectura de IP. Se reintentara; el contador sigue siendo estimado.","No IP reading. Will retry; the countdown is still an estimate."},
        {"Router ZTE: ","ZTE router: "},
        {"Router sin lectura; respaldo por IP publica: ","No router reading; public IP fallback: "},
        {"Lectura por IP publica: ","Public IP reading: "},
        {"Ultimo inicio leido del router; sin confirmacion actual","Last start read from router; not currently confirmed"},
        {" (no se pudo guardar historial)"," (could not save history)"},
        {"Nueva sesion de Internet detectada","New Internet session detected"},
        {"Cambio de IP detectado","IP change detected"},
        {"Inicio de sesion obtenido del router. Contador ajustado automaticamente.","Session start obtained from router. Countdown adjusted automatically."},
        {"La IP cambio durante una pausa. Contador reiniciado desde ahora, con hora incierta.","IP changed during a monitoring gap. Countdown restarted from now; time is uncertain."},
        {"Contador reiniciado. Proximo cambio estimado en ","Countdown restarted. Next change estimated in "},
        {" horas."," hours."},
        {" (no se pudo guardar historial del router)"," (could not save router history)"},
        {"SIN REFERENCIA","NO REFERENCE"},
        {"Configura el router o indica la hora del ultimo cambio.","Configure the router or enter the last change time."},
        {"IP publica: ","Public IP: "}, {"pendiente","pending"},
        {"\nSin referencia de inicio de sesion.","\nNo session start reference."},
        {"Dynamic IP Monitor - esperando referencia","Dynamic IP Monitor - awaiting reference"},
        {"HORA ESTIMADA PASADA","ESTIMATED TIME PASSED"},
        {" (hora local, estimada)"," (local time, estimated)"},
        {"\nUltimo inicio: ","\nLast start: "},
        {"Dynamic IP Monitor - faltan ","Dynamic IP Monitor - remaining "},
        {"Dynamic IP Monitor - estimacion vencida","Dynamic IP Monitor - estimate expired"},
        {"Se alcanzo la hora estimada","Estimated time reached"},
        {"Posible reconexion en ","Possible reconnection in "},
        {"Todavia no se detecto otro cambio de IP. La estimacion puede haber variado.","No further IP change detected yet. The estimate may have shifted."},
        {"Evita iniciar una carrera larga cerca del cambio estimado.","Avoid starting a long race near the estimated change."},
        {"No se pudo cambiar el inicio automatico: ","Could not change automatic startup: "},
        {"El monitor ya esta abierto. Busca el icono en la bandeja junto al reloj.","The monitor is already running. Look for its icon in the system tray."},
        {"Referencia manual aproximada","Approximate manual reference"},
        {"Inicio de sesion confirmado por el router ZTE","Session start confirmed by the ZTE router"},
        {"Cambio durante una pausa: hora incierta","Change during a monitoring gap: time uncertain"},
        {"Cambio observado entre dos lecturas","Change observed between two readings"},
        {"Hubo una pausa sin monitoreo: estimacion incierta","Monitoring gap: estimate uncertain"},
        {"Referencia manual (el router la reemplazara al leer)","Manual reference (replaced by next router reading)"},
        {"El router requiere credenciales no disponibles","Router credentials are not available"},
        {"Login del router en espera","Router sign-in on cooldown"},
        {"El router no acepto el inicio de sesion","The router rejected sign-in"},
        {"Lectura de router desactivada","Router reading disabled"},
        {"No se encontro una sesion PPPoE IPv4 conectada","No connected IPv4 PPPoE session found"}
    };
    static readonly Dictionary<string,string> English = Build(false);
    static readonly Dictionary<string,string> Spanish = Build(true);
    static Dictionary<string,string> Build(bool reverse) {
        var result = new Dictionary<string,string>(StringComparer.Ordinal);
        for (int i=0;i<Entries.GetLength(0);i++) result.Add(Entries[i,reverse ? 1 : 0],Entries[i,reverse ? 0 : 1]);
        return result;
    }
    public static string Source(string text) { if (text == null) return ""; string source; return Spanish.TryGetValue(text,out source) ? source : text; }
    public static string T(string text) { string translated; string source=Source(text); return Current == "en" && English.TryGetValue(source,out translated) ? translated : source; }
    public static string Minutes(int count) { return count.ToString(Culture) + (Current == "es" ? count == 1 ? " minuto" : " minutos" : count == 1 ? " minute" : " minutes"); }
    public static void Capture(Control root) {
        if (root is Form || root is Label || root is Button || root is CheckBox) root.Tag=Source(root.Text);
        foreach (Control child in root.Controls) Capture(child);
    }
    public static void Apply(Control root) {
        if (root.Tag is string) root.Text=T((string)root.Tag);
        foreach (Control child in root.Controls) Apply(child);
    }
}
