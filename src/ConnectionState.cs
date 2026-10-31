using System;
using System.Globalization;
public class ConnectionState {
    public string Ip = "";
    public DateTime AnchorUtc;
    public DateTime LastSuccessUtc;
    public string Quality = "Referencia manual aproximada";
    public string AlertKey = "";
    public double PeriodHours = 12;
    public bool RouterVerified;
}

public static class MonitorLogic {
    public static bool ValidState(ConnectionState state) {
        if (state == null || Double.IsNaN(state.PeriodHours) || Double.IsInfinity(state.PeriodHours) || state.PeriodHours < 1 || state.PeriodHours > 168) return false;
        if (!String.IsNullOrEmpty(state.Ip) && !NetworkAddress.IsPublicV4(state.Ip)) return false;
        return ValidDate(state.AnchorUtc) && ValidDate(state.LastSuccessUtc);
    }
    static bool ValidDate(DateTime value) { return value == DateTime.MinValue || (value >= new DateTime(1900,1,1) && value <= DateTime.UtcNow.AddMinutes(1)); }
    public static string ObserveRouter(ConnectionState s, ConnectionReading reading) {
        bool hadRouter = s.RouterVerified;
        DateTime oldAnchor = s.AnchorUtc;
        bool sameIp = s.Ip == reading.Ip;
        string result = Observe(s, reading.Ip, reading.ReadUtc);
        bool newSession = hadRouter && (reading.SessionStartUtc - oldAnchor).TotalSeconds > 90;
        if (!hadRouter || !sameIp || Math.Abs((reading.SessionStartUtc - oldAnchor).TotalSeconds) > 90) {
            s.AnchorUtc = reading.SessionStartUtc;
            s.AlertKey = "";
        } else {
            // Keep a stable anchor so sub-second reading jitter does not repeat alerts.
            s.AnchorUtc = oldAnchor;
        }
        s.RouterVerified = true;
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
    public static string AlertKey(ConnectionState state, int band) {
        return String.Format(CultureInfo.InvariantCulture,"{0}:{1}:{2}",state.AnchorUtc.Ticks,state.PeriodHours,band);
    }
}
