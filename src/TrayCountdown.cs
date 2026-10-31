using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;

public class TrayCountdownState {
    public string Top, Bottom, Band;
    public Color Background, Foreground;
    public string Key { get { return Band + ":" + Top + ":" + Bottom; } }
}

public static class TrayCountdown {
    [DllImport("user32.dll", SetLastError = true)]
    static extern bool DestroyIcon(IntPtr handle);

    public static TrayCountdownState Describe(TimeSpan? remaining) {
        if (!remaining.HasValue) return new TrayCountdownState { Top="?", Bottom="", Band="unknown", Background=Color.FromArgb(78,88,105), Foreground=Color.FromArgb(156,168,188) };
        double seconds = remaining.Value.TotalSeconds;
        string band = seconds < 3600 ? "red" : seconds < 10800 ? "yellow" : "green";
        var state = new TrayCountdownState {
            Band = band,
            Background = band == "red" ? Color.FromArgb(186,37,55) : band == "yellow" ? Color.FromArgb(156,94,0) : Color.FromArgb(18,117,78),
            Foreground = band == "red" ? Color.FromArgb(255,109,124) : band == "yellow" ? Color.FromArgb(255,194,80) : Color.FromArgb(113,223,183)
        };
        if (seconds <= 0) { state.Top="!"; state.Bottom=""; }
        else if (seconds < 60) { state.Top=Math.Max(1,(int)Math.Floor(seconds)).ToString(); state.Bottom="s"; }
        else if (seconds < 3600) { state.Top=((int)remaining.Value.TotalMinutes).ToString(); state.Bottom="m"; }
        else { state.Top=((int)remaining.Value.TotalHours).ToString(); state.Bottom=""; }
        return state;
    }

    public static Bitmap Draw(TrayCountdownState state) {
        var bitmap = new Bitmap(32,32);
        using (Graphics graphics = Graphics.FromImage(bitmap)) {
            graphics.Clear(Color.Transparent);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            using (var background = new SolidBrush(state.Background))
            using (var white = new SolidBrush(Color.White))
            using (var shape = new GraphicsPath())
            using (var format = new StringFormat(StringFormat.GenericTypographic)) {
                shape.AddArc(0,0,6,6,180,90); shape.AddArc(26,0,6,6,270,90);
                shape.AddArc(26,26,6,6,0,90); shape.AddArc(0,26,6,6,90,90); shape.CloseFigure();
                graphics.FillPath(background,shape);
                format.Alignment=StringAlignment.Center; format.LineAlignment=StringAlignment.Center;
                format.FormatFlags=StringFormatFlags.NoWrap;
                float fontSize = state.Top.Length >= 3 ? 17 : state.Top.Length == 2 ? 24 : 27;
                using (var font = new Font("Segoe UI",fontSize,FontStyle.Bold,GraphicsUnit.Pixel))
                    graphics.DrawString(state.Top,font,white,state.Bottom.Length == 0 ? new RectangleF(0,-1,32,32) : new RectangleF(0,-3,32,29),format);
                if (state.Bottom.Length > 0)
                    using (var unitFont = new Font("Segoe UI",9,FontStyle.Bold,GraphicsUnit.Pixel))
                        graphics.DrawString(state.Bottom,unitFont,white,new RectangleF(0,23,32,9),format);
            }
        }
        return bitmap;
    }

    public static Icon Create(TrayCountdownState state) {
        using (var bitmap = Draw(state)) {
            IntPtr handle = bitmap.GetHicon();
            try { using (var borrowed = Icon.FromHandle(handle)) return (Icon)borrowed.Clone(); }
            finally { DestroyIcon(handle); }
        }
    }

}
