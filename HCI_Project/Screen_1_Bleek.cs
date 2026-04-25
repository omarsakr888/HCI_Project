using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace HCI_Project
{
    /// <summary>
    /// Screen 1: idle / standby. Flat dark UI (no bitmap assets). TCP server on port 5050.
    /// </summary>
    public partial class IdleStandbyForm : Form
    {
        private static readonly Color Bg = Color.FromArgb(15, 15, 15);
        private static readonly Color Teal = Color.FromArgb(0, 212, 170);
        private static readonly Color TextWhite = Color.FromArgb(245, 245, 245);

        private const int TopBarH = 56;
        private const int BottomBarH = 104;
        private const int SideMargin = 28;
        private const int TcpPort = 5050;
        public static bool BleDetected { get; set; }

        private double _pulsePhase;
        private int _dotPhase;
        private DateTime _flashUntilUtc = DateTime.MinValue;
        private bool _highlightAthleteNearby;
        private bool _navigateAfterHighlight;
        private int _navigateTicksRemaining;

        private TcpListener _listener;
        private Thread _acceptThread;
        private volatile bool _stopServer;
        private volatile bool _navigationArmed;

        private readonly Stopwatch _uptime = new Stopwatch();

        private Color _bleDotColor = Teal;
        private Color _serverDotColor = Teal;
        private string _bleMain = "SCANNING...";
        private string _bleSub = "PROTOCOL: BLE 5.2  SIGNAL: -- dBm";
        private string _serverMain = "SERVER READY : 5050";
        private string _waitingPrimary = "WAITING FOR ATHLETE";
        private Color _waitingColor = Color.FromArgb(130, 130, 130);

        public IdleStandbyForm()
        {
            InitializeComponent();
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            DoubleBuffered = true;
            KeyPreview = true;
        }

        private void IdleStandbyForm_Load(object sender, EventArgs e)
        {
            Bounds = Screen.PrimaryScreen.Bounds;
            ResetStatusPresentation();
            _uptime.Start();

            StartTcpServer();
            timerPulse.Start();
            PositionDemoHint();
        }

        private void IdleStandbyForm_Shown(object sender, EventArgs e)
        {
            lblDemoHint.Visible = Debugger.IsAttached;
            lblDemoHint.BringToFront();
        }

        private void IdleStandbyForm_SizeChanged(object sender, EventArgs e)
        {
            PositionDemoHint();
            Invalidate();
        }

        private void PositionDemoHint()
        {
            const int pad = 16;
            lblDemoHint.Location = new Point(ClientSize.Width - lblDemoHint.Width - pad, pad);
        }

        private void ResetStatusPresentation()
        {
            _bleDotColor = Teal;
            _serverDotColor = Teal;
            _bleMain = "SCANNING...";
            _bleSub = "PROTOCOL: BLE 5.2  SIGNAL: -- dBm";
            _serverMain = "SERVER READY : " + TcpPort;
            _waitingPrimary = "WAITING FOR ATHLETE";
            _waitingColor = Color.FromArgb(130, 130, 130);
        }

        private void TryLaunchPythonBleClient()
        {
            try
            {
                var script = System.IO.Path.Combine(Application.StartupPath, "FormGuardBle", "main.py");
                if (!System.IO.File.Exists(script))
                {
                    return;
                }

                var psi = new ProcessStartInfo
                {
                    FileName = "python",
                    Arguments = "\"" + script + "\"",
                    WorkingDirectory = System.IO.Path.GetDirectoryName(script) ?? Application.StartupPath,
                    UseShellExecute = true,
                };
                Process.Start(psi);
            }
            catch
            {
                // Manual start is fine.
            }
        }

        private void StartTcpServer()
        {
            _stopServer = false;
            _acceptThread = new Thread(AcceptLoop)
            {
                IsBackground = true,
                Name = "FormGuardTcpAccept",
            };
            _acceptThread.Start();
        }

        private void AcceptLoop()
        {
            while (!_stopServer)
            {
                TcpListener listener = null;
                try
                {
                    listener = new TcpListener(IPAddress.Loopback, TcpPort);
                    listener.Start();
                    _listener = listener;
                    SafeInvoke(() =>
                    {
                        _serverDotColor = Teal;
                        _serverMain = "SERVER READY : " + TcpPort;
                        Invalidate();
                    });

                    while (!_stopServer)
                    {
                        try
                        {
                            var client = listener.AcceptTcpClient();
                            ThreadPool.QueueUserWorkItem(_ => HandleClient(client));
                        }
                        catch (SocketException)
                        {
                            if (_stopServer)
                            {
                                break;
                            }

                            Thread.Sleep(50);
                        }
                        catch (ObjectDisposedException)
                        {
                            break;
                        }
                    }
                }
                catch (SocketException)
                {
                    SafeInvoke(() =>
                    {
                        _serverDotColor = Color.FromArgb(220, 80, 80);
                        _serverMain = "SERVER ERROR : " + TcpPort;
                        Invalidate();
                    });
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                finally
                {
                    try { listener?.Stop(); } catch { /* ignore */ }
                    if (ReferenceEquals(_listener, listener))
                    {
                        _listener = null;
                    }
                }

                if (!_stopServer)
                {
                    Thread.Sleep(800);
                }
            }
        }

        private void HandleClient(object state)
        {
            var client = (TcpClient)state;
            try
            {
                using (client)
                using (var stream = client.GetStream())
                using (var reader = new StreamReader(stream, Encoding.UTF8, false, 1024, leaveOpen: false))
                {
                    string line;
                    while (!_stopServer && (line = reader.ReadLine()) != null)
                    {
                        var cmd = line.Trim();
                        if (cmd.Length == 0)
                        {
                            continue;
                        }

                        SafeInvoke(() => OnTcpCommand(cmd));
                    }
                }
            }
            catch
            {
                // Client disconnect is normal.
            }
        }

        private void SafeInvoke(Action action)
        {
            try
            {
                if (IsDisposed)
                {
                    return;
                }

                if (InvokeRequired)
                {
                    BeginInvoke(action);
                }
                else
                {
                    action();
                }
            }
            catch (ObjectDisposedException)
            {
            }
            catch (InvalidOperationException)
            {
            }
        }

        private void OnTcpCommand(string cmd)
        {
            Debug.WriteLine("[BLE Screen] TCP received: " + cmd);
            if (cmd.StartsWith("SIGNAL:", StringComparison.OrdinalIgnoreCase))
            {
                var tail = cmd.Substring(7).Trim();
                if (tail.Length > 0)
                {
                    _bleSub = "PROTOCOL: BLE 5.2  SIGNAL: " + tail;
                }

                Invalidate();
                return;
            }

            if (cmd.Equals("SCANNING", StringComparison.OrdinalIgnoreCase))
            {
                _bleDotColor = Color.FromArgb(0, 200, 120);
                _bleMain = "SCANNING...";
                _bleSub = "PROTOCOL: BLE 5.2  SIGNAL: -- dBm";
                Invalidate();
            }
            else if (cmd.Equals("PHONE_NEAR", StringComparison.OrdinalIgnoreCase))
            {
                if (_navigationArmed)
                {
                    return;
                }

                _navigationArmed = true;
                _bleDotColor = Teal;
                _bleMain = "ATHLETE NEARBY";
                _bleSub = "PROTOCOL: BLE 5.2  SIGNAL: STRONG";
                _waitingPrimary = "ATHLETE NEARBY";
                _waitingColor = Teal;
                _flashUntilUtc = DateTime.UtcNow.AddMilliseconds(220);
                _highlightAthleteNearby = true;
                _navigateAfterHighlight = true;
                _navigateTicksRemaining = 48;
                timerNavigate.Start();
                Invalidate();
            }
            else if (cmd.Equals("DEVICE_LOST", StringComparison.OrdinalIgnoreCase))
            {
                _bleDotColor = Color.FromArgb(220, 70, 70);
                _bleMain = "DEVICE LOST";
                _bleSub = "PROTOCOL: BLE 5.2  SIGNAL: LOST";
                _waitingPrimary = "WAITING FOR ATHLETE";
                _waitingColor = Color.FromArgb(130, 130, 130);
                Invalidate();
            }
        }

        private void timerNavigate_Tick(object sender, EventArgs e)
        {
            if (_navigateTicksRemaining > 0)
            {
                _navigateTicksRemaining--;
                if (_navigateTicksRemaining == 36)
                {
                    _highlightAthleteNearby = false;
                }

                Invalidate();
            }

            if (_navigateAfterHighlight && _navigateTicksRemaining <= 0)
            {
                timerNavigate.Stop();
                _navigateAfterHighlight = false;
                GoToFaceScreen();
            }
        }

        private void GoToFaceScreen()
        {
            timerNavigate.Stop();
            BleDetected = true;                     // Signal that BLE detected
            BeginInvoke((Action)(() => this.Close()));   // Close the dialog
        }

        private void IdleStandbyForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                Close();
                return;
            }

            if (e.KeyCode == Keys.F11)
            {
                _highlightAthleteNearby = !_highlightAthleteNearby;
                _waitingPrimary = _highlightAthleteNearby ? "ATHLETE NEARBY" : "WAITING FOR ATHLETE";
                _waitingColor = _highlightAthleteNearby ? Teal : Color.FromArgb(130, 130, 130);
                Invalidate();
                e.Handled = true;
                return;
            }

            if (e.KeyCode == Keys.A)
            {
                OnTcpCommand("PHONE_NEAR");
                e.Handled = true;
            }
        }

        private void timerPulse_Tick(object sender, EventArgs e)
        {
            _pulsePhase += 0.018;
            if (_pulsePhase > Math.PI * 2)
            {
                _pulsePhase -= Math.PI * 2;
            }

            _dotPhase = (_dotPhase + 1) % 32;
            Invalidate();
        }

        private void IdleStandbyForm_Paint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            var w = ClientSize.Width;
            var h = ClientSize.Height;

            using (var b = new SolidBrush(Bg))
            {
                g.FillRectangle(b, 0, 0, w, h);
            }

            DrawHeader(g, w);
            DrawSideRails(g, w, h);
            DrawCenterCard(g, w, h);
            DrawWaitingLine(g, w, h);
            DrawBottomStatus(g, w, h);
        }

        private void DrawHeader(Graphics g, int w)
        {
            const int pad = SideMargin;

            using (var fontBrand = new Font("Segoe UI", 11f, FontStyle.Bold, GraphicsUnit.Point))
            using (var brush = new SolidBrush(Teal))
            {
                var text = "FORMGUARD";
                g.DrawString(text, fontBrand, brush, pad, (TopBarH - g.MeasureString(text, fontBrand).Height) / 2f);
            }

            using (var fontMeta = new Font("Consolas", 8.25f, FontStyle.Regular, GraphicsUnit.Point))
            using (var brush = new SolidBrush(Color.FromArgb(180, 180, 180)))
            {
                var meta = "PRECISION LAB V1.0";
                var sz = g.MeasureString(meta, fontMeta);
                var x = w - pad - sz.Width - 64f;
                g.DrawString(meta, fontMeta, brush, x, (TopBarH - sz.Height) / 2f);

                using (var iconFont = new Font("Segoe UI Symbol", 11f, FontStyle.Regular, GraphicsUnit.Point))
                using (var iconBrush = new SolidBrush(Color.FromArgb(170, 170, 170)))
                {
                    g.DrawString("\u2699", iconFont, iconBrush, w - pad - 58f, (TopBarH - 16f) / 2f);
                    g.DrawString("\u237E", iconFont, iconBrush, w - pad - 38f, (TopBarH - 16f) / 2f);
                    g.DrawString("\u25C9", iconFont, iconBrush, w - pad - 18f, (TopBarH - 16f) / 2f);
                }
            }
        }

        private void DrawSideRails(Graphics g, int w, int h)
        {
            using (var font = new Font("Consolas", 8f, FontStyle.Regular, GraphicsUnit.Point))
            using (var brush = new SolidBrush(Color.FromArgb(90, 90, 90)))
            {
                var left = "BIOMETRIC_DATA_STREAM";
                var right = "PRECISION_CORE_V1";

                var st = g.Save();
                g.TranslateTransform(SideMargin, h / 2f);
                g.RotateTransform(-90f);
                var sz = g.MeasureString(left, font);
                g.DrawString(left, font, brush, -sz.Width / 2f, 0f);
                g.Restore(st);

                st = g.Save();
                g.TranslateTransform(w - SideMargin, h / 2f);
                g.RotateTransform(90f);
                sz = g.MeasureString(right, font);
                g.DrawString(right, font, brush, -sz.Width / 2f, 0f);
                g.Restore(st);
            }
        }

        private void DrawCenterCard(Graphics g, int w, int h)
        {
            var breathe = 0.94f + 0.06f * (0.5f + 0.5f * (float)Math.Sin(_pulsePhase));
            var baseSize = Math.Min(w, h) * 0.34f * breathe;
            var cx = w / 2f;
            var cy = h / 2f - 8f;
            var half = baseSize / 2f;
            var outer = new RectangleF(cx - half, cy - half, baseSize, baseSize);
            var radius = 14f;

            var flashActive = DateTime.UtcNow < _flashUntilUtc;
            var outerPenW = flashActive ? 4.5f : (_highlightAthleteNearby ? 3.8f : 2.8f);

            using (var path = RoundedRectangle(outer, radius))
            using (var outerPen = new Pen(Teal, outerPenW))
            {
                outerPen.Alignment = PenAlignment.Center;
                g.DrawPath(outerPen, path);
            }

            using (var pathInner = RoundedRectangle(RectangleF.Inflate(outer, -7f, -7f), Math.Max(4f, radius - 4f)))
            using (var innerPen = new Pen(Color.FromArgb(140, Teal), 1.2f))
            {
                g.DrawPath(innerPen, pathInner);
            }

            if (flashActive)
            {
                using (var pathGlow = RoundedRectangle(RectangleF.Inflate(outer, -10f, -10f), radius))
                using (var glowPen = new Pen(Color.FromArgb(100, Teal), 1.5f))
                {
                    g.DrawPath(glowPen, pathGlow);
                }
            }

            var inner = Rectangle.Round(RectangleF.Inflate(outer, -22f, -22f));
            if (inner.Width < 40 || inner.Height < 40)
            {
                return;
            }

            var dumbbellCenter = new PointF(inner.X + inner.Width / 2f, inner.Y + inner.Height * 0.28f);
            DrawDumbbellIcon(g, dumbbellCenter, 22f, Teal);

            var wordY = inner.Y + inner.Height * 0.48f;
            using (var fontForm = new Font("Segoe UI", 19f, FontStyle.Bold, GraphicsUnit.Point))
            using (var fontGuard = new Font("Segoe UI", 19f, FontStyle.Bold, GraphicsUnit.Point))
            using (var white = new SolidBrush(TextWhite))
            using (var tealBrush = new SolidBrush(Teal))
            {
                var form = "FORM";
                var guard = "GUARD";
                var wForm = g.MeasureString(form, fontForm).Width;
                var wGuard = g.MeasureString(guard, fontGuard).Width;
                var total = wForm + wGuard;
                var startX = inner.X + (inner.Width - total) / 2f;
                g.DrawString(form, fontForm, white, startX, wordY);
                g.DrawString(guard, fontGuard, tealBrush, startX + wForm, wordY);
            }

            using (var subFont = new Font("Segoe UI", 8.5f, FontStyle.Bold, GraphicsUnit.Point))
            using (var subBrush = new SolidBrush(Teal))
            {
                var sub = "SYSTEM STANDBY";
                var sz = g.MeasureString(sub, subFont);
                g.DrawString(sub, subFont, subBrush, inner.X + (inner.Width - sz.Width) / 2f, wordY + 34f);
            }
        }

        private static void DrawDumbbellIcon(Graphics g, PointF center, float size, Color color)
        {
            using (var pen = new Pen(color, size * 0.12f))
            using (var fill = new SolidBrush(color))
            {
                var w = size * 0.9f;
                var h = size * 0.28f;
                var left = new RectangleF(center.X - w * 0.55f, center.Y - h / 2f, h, h);
                var right = new RectangleF(center.X + w * 0.55f - h, center.Y - h / 2f, h, h);
                g.FillEllipse(fill, left);
                g.FillEllipse(fill, right);
                g.DrawLine(pen, center.X - w * 0.35f, center.Y, center.X + w * 0.35f, center.Y);
            }
        }

        private static GraphicsPath RoundedRectangle(RectangleF bounds, float radius)
        {
            var path = new GraphicsPath();
            var d = radius * 2f;
            var x = bounds.X;
            var y = bounds.Y;
            var w = bounds.Width;
            var h = bounds.Height;
            path.AddArc(x, y, d, d, 180, 90);
            path.AddArc(x + w - d, y, d, d, 270, 90);
            path.AddArc(x + w - d, y + h - d, d, d, 0, 90);
            path.AddArc(x, y + h - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private void DrawWaitingLine(Graphics g, int w, int h)
        {
            using (var font = new Font("Consolas", 10.5f, FontStyle.Regular, GraphicsUnit.Point))
            using (var brush = new SolidBrush(_waitingColor))
            {
                var dots = new string('.', 1 + (_dotPhase / 8) % 3);
                var text = _waitingPrimary + " " + dots;
                var sz = g.MeasureString(text, font);
                var x = (w - sz.Width) / 2f;
                var y = h / 2f + Math.Min(w, h) * 0.22f;
                g.DrawString(text, font, brush, x, y);
            }
        }

        private void DrawBottomStatus(Graphics g, int w, int h)
        {
            var pad = SideMargin;
            var yMain = h - BottomBarH + 18;
            var ySub = yMain + 22;

            using (var dotFont = new Font("Segoe UI", 10f, FontStyle.Bold, GraphicsUnit.Point))
            using (var mainFont = new Font("Consolas", 9.5f, FontStyle.Bold, GraphicsUnit.Point))
            using (var subFont = new Font("Consolas", 8f, FontStyle.Regular, GraphicsUnit.Point))
            {
                using (var dotBrush = new SolidBrush(_bleDotColor))
                {
                    g.DrawString("●", dotFont, dotBrush, pad, yMain - 2f);
                }

                using (var brush = new SolidBrush(Color.FromArgb(200, 200, 200)))
                using (var subBrush = new SolidBrush(Color.FromArgb(120, 120, 120)))
                {
                    var szDot = g.MeasureString("●", dotFont);
                    g.DrawString(_bleMain.ToUpperInvariant(), mainFont, brush, pad + szDot.Width + 6f, yMain);
                    g.DrawString(_bleSub, subFont, subBrush, pad, ySub);
                }
            }

            var uptime = _uptime.Elapsed.ToString(@"hh\:mm\:ss");
            var subRight = "LATENCY: 4MS  UPTIME: " + uptime;

            using (var mainFont = new Font("Consolas", 9.5f, FontStyle.Bold, GraphicsUnit.Point))
            using (var subFont = new Font("Consolas", 8f, FontStyle.Regular, GraphicsUnit.Point))
            using (var brush = new SolidBrush(Color.FromArgb(200, 200, 200)))
            using (var subBrush = new SolidBrush(Color.FromArgb(120, 120, 120)))
            {
                var main = _serverMain.ToUpperInvariant();
                var szMain = g.MeasureString(main, mainFont);
                var szSub = g.MeasureString(subRight, subFont);
                var xMain = w - pad - szMain.Width - 18f;
                g.DrawString(main, mainFont, brush, xMain, yMain);

                using (var dotBrush = new SolidBrush(_serverDotColor))
                using (var dotFont = new Font("Segoe UI", 10f, FontStyle.Bold, GraphicsUnit.Point))
                {
                    g.DrawString("●", dotFont, dotBrush, xMain + szMain.Width + 4f, yMain - 2f);
                }

                var xSub = w - pad - szSub.Width;
                g.DrawString(subRight, subFont, subBrush, xSub, ySub);
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _stopServer = true;
            try { _listener?.Stop(); } catch { /* ignore */ }
            base.OnFormClosing(e);
        }
    }
}
