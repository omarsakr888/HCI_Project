using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;
using TUIO;

namespace HCI_Project
{
    public partial class Screen_3_Dashboard : Form, TuioListener
    {
        // ---------- TUIO calibration ----------
        // Marker ID used to control the menu
        private const int ACTIVE_MARKER_ID = 3;

        // Angle offset added to raw marker angle so that 0 rad (marker's
        // default orientation) points to the top segment (Chest Day).
        // If the marker's zero orientation already points to the top,
        // leave this at π/2. Adjust if needed.
        private static readonly float ANGLE_OFFSET = (float)(Math.PI / 2f);

        // ---------- Visual constants ----------
        private static readonly Color Bg = Color.FromArgb(10, 12, 14);
        private static readonly Color Teal = Color.FromArgb(0, 212, 170);
        private static readonly Color TealDim = Color.FromArgb(0, 120, 96);
        private static readonly Color SegmentDim = Color.FromArgb(22, 28, 32);
        private static readonly Color TextMain = Color.FromArgb(236, 236, 236);
        private static readonly Color TextMuted = Color.FromArgb(110, 120, 126);

        private readonly string _userName;

        private TuioClient _tuioClient;

        private readonly Dictionary<long, MarkerState> _markers = new Dictionary<long, MarkerState>();

        // Smoothed angle after applying offset – used only for segment detection
        private float _smoothedAngle = (float)(Math.PI / 2f);   // initially points to top
        private int _activeSegment = -1;                         // no selection initially
        private int _dwellSegment = -1;
        private double _dwellSeconds;
        private float _dwellRingProgress;
        private string _dwellConfirmText = string.Empty;

        private const double DwellConfirmSeconds = 1.5;

        private static readonly string[] SegmentLabels = { "Chest Day", "Back Day", "Legs Day" };

        public Screen_3_Dashboard(string userName)
        {
            _userName = string.IsNullOrWhiteSpace(userName) ? "Athlete" : userName.Trim();
            InitializeComponent();
            DoubleBuffered = true;
            // _activeSegment stays -1 (no pre‑selection)
        }

        private void Screen_3_Dashboard_Load(object sender, EventArgs e)
        {
            Bounds = Screen.PrimaryScreen.Bounds;
            try
            {
                _tuioClient = new TuioClient(3333);
                _tuioClient.addTuioListener(this);
                _tuioClient.connect();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[Screen_3_Dashboard] TUIO: " + ex.Message);
            }

            timerUi.Start();
        }

        private void Screen_3_Dashboard_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                Close();
            }
        }

        private void timerUi_Tick(object sender, EventArgs e)
        {
            // Get angle from marker ID 3 only (or null if absent)
            float? rawAngle = GetTargetAngleFromMarker(ACTIVE_MARKER_ID);
            if (rawAngle.HasValue)
            {
                // Remove any reversal – use raw angle directly
                // Apply offset so that 0 rad (marker default) aligns with top
                float target = (rawAngle.Value + ANGLE_OFFSET) % (2 * (float)Math.PI);
                if (target < 0) target += 2 * (float)Math.PI;

                float delta = NormalizeAngleDelta(target - _smoothedAngle);
                _smoothedAngle += delta * 0.28f;

                _activeSegment = AngleToSegment(_smoothedAngle);

                // Dwell tracking
                if (_activeSegment == _dwellSegment)
                {
                    _dwellSeconds += timerUi.Interval / 1000.0;
                    _dwellRingProgress = (float)Math.Min(1.0, _dwellSeconds / DwellConfirmSeconds);
                    if (_dwellSeconds >= DwellConfirmSeconds && _dwellRingProgress >= 1f)
                    {
                        _dwellConfirmText = "Confirmed: " + SegmentLabels[_activeSegment];
                    }
                }
                else
                {
                    _dwellSegment = _activeSegment;
                    _dwellSeconds = 0;
                    _dwellRingProgress = 0;
                    _dwellConfirmText = string.Empty;
                }
            }
            else
            {
                // No marker – no segment selected, reset dwell
                _activeSegment = -1;
                _dwellSegment = -1;
                _dwellSeconds = 0;
                _dwellRingProgress = 0;
                _dwellConfirmText = string.Empty;
            }

            Invalidate();
        }

        /// <summary>
        /// Returns the angle of the marker with the exact SymbolID, or null.
        /// </summary>
        private float? GetTargetAngleFromMarker(int symbolId)
        {
            foreach (var kv in _markers)
            {
                var m = kv.Value;
                if (m.SymbolId == symbolId)
                    return m.Angle;
            }
            return null;
        }

        private static float NormalizeAngleDelta(float d)
        {
            while (d > Math.PI) d -= (float)(2 * Math.PI);
            while (d < -Math.PI) d += (float)(2 * Math.PI);
            return d;
        }

    
        private static int AngleToSegment(float angleRad)
        {
            double visualAngle = angleRad - Math.PI;
            double deg = (visualAngle * 180.0 / Math.PI) % 360.0;
            if (deg < 0) deg += 360.0;

            // Slice 0 (Chest Day): 210° to 330°
            if (deg >= 210.0 && deg < 330.0)
                return 0;

            // Slice 1 (Back Day): 330° to 360° OR 0° to 90°
            if (deg >= 330.0 || deg < 90.0)
                return 1;

            // Slice 2 (Legs Day): 90° to 210°
            return 2;
        }

        private void Screen_3_Dashboard_Paint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.Clear(Bg);

            var w = ClientSize.Width;
            var h = ClientSize.Height;

            DrawGreetingBar(g, w);
            DrawRadialMenu(g, w, h);
            DrawInstruction(g, w, h);
            // DrawStatStrip(g, w, h);   // removed for new user – no history
        }

        private void DrawGreetingBar(Graphics g, int w)
        {
            const int barH = 56;
            using (var b = new SolidBrush(Color.FromArgb(18, 22, 26)))
            {
                g.FillRectangle(b, 0, 0, w, barH);
            }

            var avatar = new Rectangle(24, 10, 36, 36);
            using (var avBr = new SolidBrush(Color.FromArgb(30, 40, 44)))
            using (var pen = new Pen(Teal, 1.6f))
            {
                g.FillEllipse(avBr, avatar);
                g.DrawEllipse(pen, avatar);
            }

            using (var f = new Font("Segoe UI", 11f, FontStyle.Bold))
            using (var br = new SolidBrush(TextMain))
            {
                var initials = BuildInitials(_userName);
                var sz = g.MeasureString(initials, f);
                g.DrawString(initials, f, br, avatar.X + (avatar.Width - sz.Width) / 2f, avatar.Y + (avatar.Height - sz.Height) / 2f - 1f);
            }

            using (var f = new Font("Segoe UI", 14f, FontStyle.Regular))
            using (var br = new SolidBrush(TextMain))
            {
                g.DrawString("Hello, " + _userName, f, br, 74, 14);
            }
        }

        private static string BuildInitials(string name)
        {
            var parts = name.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                return (parts[0].Substring(0, 1) + parts[1].Substring(0, 1)).ToUpperInvariant();
            }

            return name.Length >= 2 ? name.Substring(0, 2).ToUpperInvariant() : name.ToUpperInvariant();
        }

        private void DrawRadialMenu(Graphics g, int w, int h)
        {
            var cx = w / 2f;
            var cy = h / 2f - 10f;
            var outerR = Math.Min(w, h) * 0.26f;
            var innerR = outerR * 0.42f;
            var menuRectF = new RectangleF(cx - outerR, cy - outerR, outerR * 2f, outerR * 2f);
            var menuRect = Rectangle.Round(menuRectF);

            for (var i = 0; i < 3; i++)
            {
                var start = (210f + i * 120f) % 360f;
                var active = i == _activeSegment;
                var fill = active ? Color.FromArgb(42, 70, 68) : SegmentDim;

                using (var path = new GraphicsPath())
                {
                    path.AddPie(menuRect, start, 120f);
                    using (var b = new SolidBrush(fill))
                    {
                        g.FillPath(b, path);
                    }

                    using (var p = new Pen(active ? Teal : Color.FromArgb(55, 70, 74), active ? 2.2f : 1.2f))
                    {
                        g.DrawPath(p, path);
                    }
                }

                var midDeg = start + 60f;
                var midRad = midDeg * Math.PI / 180.0;
                var lx = cx + (outerR * 0.72f) * (float)Math.Cos(midRad);
                var ly = cy + (outerR * 0.72f) * (float)Math.Sin(midRad);
                DrawSegmentIcon(g, i, lx, ly, active);
                using (var font = new Font("Segoe UI", 13f, FontStyle.Bold))
                using (var br = new SolidBrush(active ? Color.White : TextMuted))
                {
                    var label = SegmentLabels[i];
                    var sz = g.MeasureString(label, font);
                    g.DrawString(label, font, br, lx - sz.Width / 2f, ly + 28f);
                }
            }

            using (var hole = new SolidBrush(Bg))
            {
                g.FillEllipse(hole, cx - innerR, cy - innerR, innerR * 2f, innerR * 2f);
            }

            using (var ring = new Pen(Color.FromArgb(50, 60, 64), 1.4f))
            {
                g.DrawEllipse(ring, cx - innerR, cy - innerR, innerR * 2f, innerR * 2f);
            }

            DrawDwellProgress(g, cx, cy, outerR, innerR);

            // Calculate the visual angle based on _smoothedAngle.
            // Subtracting Math.PI correctly maps the TUIO clockwise rotation
            // to WinForms screen coordinates (0 is right, 90 is bottom).
            float visualAngle = _smoothedAngle - (float)Math.PI;
            var dotR = outerR - 8f;

            // Note the '+' for Math.Sin! This is required because WinForms has a Y-down coordinate system.
            var dotX = cx + dotR * (float)Math.Cos(visualAngle);
            var dotY = cy + dotR * (float)Math.Sin(visualAngle);

            for (var k = 3; k >= 1; k--)
            {
                var alpha = (int)(40 + k * 35);
                var rad = 6f + k * 2.2f;
                using (var b = new SolidBrush(Color.FromArgb(alpha, Teal)))
                {
                    g.FillEllipse(b, dotX - rad, dotY - rad, rad * 2f, rad * 2f);
                }
            }

            using (var b = new SolidBrush(Color.White))
            {
                g.FillEllipse(b, dotX - 5f, dotY - 5f, 10f, 10f);
            }
        }

        private void DrawDwellProgress(Graphics g, float cx, float cy, float outerR, float innerR)
        {
            if (_dwellRingProgress <= 0f || _activeSegment < 0)
                return;

            var start = (210f + _activeSegment * 120f) % 360f;
            var sweep = 120f * _dwellRingProgress;
            var midR = innerR + (outerR - innerR) * 0.55f;
            var rect = Rectangle.Round(new RectangleF(cx - midR, cy - midR, midR * 2f, midR * 2f));
            using (var p = new Pen(Color.FromArgb(220, Teal), 3.2f))
            {
                p.StartCap = LineCap.Round;
                p.EndCap = LineCap.Round;
                g.DrawArc(p, rect, start, sweep);
            }
        }

        private static void DrawSegmentIcon(Graphics g, int segmentIndex, float cx, float cy, bool active)
        {
            var col = active ? Teal : TealDim;
            using (var pen = new Pen(col, active ? 2.2f : 1.5f))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                switch (segmentIndex)
                {
                    case 0:
                        g.DrawLine(pen, cx - 18, cy + 10, cx - 18, cy - 8);
                        g.DrawLine(pen, cx + 18, cy + 10, cx + 18, cy - 8);
                        g.DrawLine(pen, cx - 18, cy - 2, cx + 18, cy - 2);
                        g.DrawLine(pen, cx, cy - 8, cx, cy - 22);
                        break;
                    case 1:
                        g.DrawLine(pen, cx - 16, cy - 18, cx + 16, cy - 18);
                        g.DrawLine(pen, cx, cy - 18, cx, cy + 18);
                        g.DrawArc(pen, cx - 10, cy - 6, 20, 20, 0f, 180f);
                        break;
                    default:
                        g.DrawLine(pen, cx - 14, cy + 14, cx + 14, cy + 14);
                        g.DrawLine(pen, cx - 14, cy + 14, cx - 14, cy - 10);
                        g.DrawLine(pen, cx + 14, cy + 14, cx + 14, cy - 10);
                        g.DrawLine(pen, cx - 14, cy - 10, cx + 14, cy - 10);
                        break;
                }
            }
        }

        private void DrawInstruction(Graphics g, int w, int h)
        {
            var y = h * 0.72f;
            using (var f = new Font("Segoe UI", 11f, FontStyle.Regular))
            using (var br = new SolidBrush(TextMuted))
            {
                var t = "Rotate object to select · Hold to confirm";
                var sz = g.MeasureString(t, f);
                g.DrawString(t, f, br, (w - sz.Width) / 2f, y);
            }

            if (!string.IsNullOrEmpty(_dwellConfirmText))
            {
                using (var f = new Font("Segoe UI", 10f, FontStyle.Bold))
                using (var br = new SolidBrush(Teal))
                {
                    var sz = g.MeasureString(_dwellConfirmText, f);
                    g.DrawString(_dwellConfirmText, f, br, (w - sz.Width) / 2f, y + 26f);
                }
            }
        }

        private void DrawStatStrip(Graphics g, int w, int h)
        {
            var y = h - 100;
            var cardW = (w - 80) / 3f;
            var x0 = 40f;
            var stats = new[]
            {
                "Last session: Chest · 3 days ago",
                "Best form score: 87%",
                "Total reps logged: 142",
            };

            for (var i = 0; i < 3; i++)
            {
                var x = x0 + i * (cardW + 10f);
                var rect = new RectangleF(x, y, cardW, 56f);
                using (var path = RoundedRect(Rectangle.Round(rect), 10))
                using (var b = new SolidBrush(Color.FromArgb(20, 26, 30)))
                using (var p = new Pen(Color.FromArgb(48, 58, 62), 1f))
                {
                    g.FillPath(b, path);
                    g.DrawPath(p, path);
                }

                using (var f = new Font("Segoe UI", 9.5f, FontStyle.Regular))
                using (var br = new SolidBrush(TextMain))
                {
                    var sz = g.MeasureString(stats[i], f);
                    g.DrawString(stats[i], f, br, x + (cardW - sz.Width) / 2f, y + 18f);
                }
            }
        }

        private static GraphicsPath RoundedRect(Rectangle r, int radius)
        {
            var path = new GraphicsPath();
            var d = radius * 2;
            path.AddArc(r.Left, r.Top, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private void SafeTuio(Action a)
        {
            try
            {
                if (IsDisposed) return;
                BeginInvoke(a);
            }
            catch { }
        }

        private sealed class MarkerState
        {
            public int SymbolId;
            public float Angle;
        }

        // --- TUIO event handlers with diagnostic logging ---
        public void addTuioObject(TuioObject obj)
        {
            Debug.WriteLine($"[Dashboard] ADD  ID={obj.SymbolID}, angle={obj.Angle * 180.0 / Math.PI:F1}°");
            if (obj.SymbolID != ACTIVE_MARKER_ID) return;
            var sid = obj.SessionID;
            var sym = obj.SymbolID;
            var ang = obj.Angle;
            SafeTuio(() =>
            {
                _markers[sid] = new MarkerState { SymbolId = sym, Angle = ang };
            });
        }

        public void updateTuioObject(TuioObject obj)
        {
            Debug.WriteLine($"[Dashboard] UPD  ID={obj.SymbolID}, angle={obj.Angle * 180.0 / Math.PI:F1}°");
            if (obj.SymbolID != ACTIVE_MARKER_ID) return;
            var sid = obj.SessionID;
            var sym = obj.SymbolID;
            var ang = obj.Angle;
            SafeTuio(() =>
            {
                _markers[sid] = new MarkerState { SymbolId = sym, Angle = ang };
            });
        }

        public void removeTuioObject(TuioObject obj)
        {
            Debug.WriteLine($"[Dashboard] REM  ID={obj.SymbolID}");
            var sid = obj.SessionID;
            SafeTuio(() => { _markers.Remove(sid); });
        }

        public void addTuioCursor(TuioCursor tcur) { }
        public void updateTuioCursor(TuioCursor tcur) { }
        public void removeTuioCursor(TuioCursor tcur) { }
        public void addTuioBlob(TuioBlob tblb) { }
        public void updateTuioBlob(TuioBlob tblb) { }
        public void removeTuioBlob(TuioBlob tblb) { }
        public void refresh(TuioTime ftime) { }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            timerUi.Stop();
            try { _tuioClient?.disconnect(); }
            catch (Exception ex) { Debug.WriteLine("[Screen_3_Dashboard] TUIO disconnect: " + ex.Message); }
            _tuioClient = null;
            base.OnFormClosing(e);
        }
    }
}