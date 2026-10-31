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
        else if (seconds < 60) { state.Top="<1"; state.Bottom="m"; }
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
            using (var shape = new GraphicsPath()) {
                shape.AddArc(0,0,6,6,180,90); shape.AddArc(26,0,6,6,270,90);
                shape.AddArc(26,26,6,6,0,90); shape.AddArc(0,26,6,6,90,90); shape.CloseFigure();
                graphics.FillPath(background,shape);
                // Number and unit share one baseline, e.g. 45m. Fit the complete
                // glyph bounds so neither the unit nor three-digit hours clip.
                using (var family = new FontFamily("Segoe UI"))
                using (var text = new GraphicsPath()) {
                    text.AddString(state.Top + state.Bottom,family,(int)FontStyle.Bold,28,PointF.Empty,StringFormat.GenericTypographic);
                    RectangleF bounds = text.GetBounds();
                    float scale = Math.Min(1,Math.Min(30 / bounds.Width,26 / bounds.Height));
                    using (var transform = new Matrix(scale,0,0,scale,(32-bounds.Width*scale)/2-bounds.X*scale,(32-bounds.Height*scale)/2-bounds.Y*scale)) text.Transform(transform);
                    graphics.FillPath(white,text);
                }
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
