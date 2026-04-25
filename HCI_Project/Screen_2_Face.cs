using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace HCI_Project
{
    public partial class Screen_2_Face : Form
    {
        private const string FaceHost = "127.0.0.1";
        private const int FacePort = 5000;

        private static readonly Color Bg = Color.FromArgb(12, 16, 19);
        private static readonly Color Panel = Color.FromArgb(14, 21, 24);
        private static readonly Color Teal = Color.FromArgb(0, 212, 170);
        private static readonly Color TextMain = Color.FromArgb(236, 236, 236);
        private static readonly Color TextMuted = Color.FromArgb(120, 130, 136);

        private readonly FaceMode _mode;

        private Rectangle _leftZone;
        private Rectangle _rightZone;
        private Rectangle _cameraRect;
        private Rectangle _faceRect;
        private Rectangle _nameBadgeRect;
        private Rectangle _rightCardRect;
        private Rectangle _phoneChipRect;
        private Rectangle _progressTrack;
        private Rectangle _manualTextRect;
        private Rectangle _manualButtonRect;

        private float _scanPhase;
        private float _progressPhase;
        private int _tickCount;
        private bool _faceMatched;
        private bool _showFallback;

        private volatile bool _stopFaceClient;
        private Thread _faceClientThread;

        private string _detectedPhoneLabel = "Scanning...";
        private string _athleteLabel = "Unknown";
        private string _avatarLetters = "";

        private string _leftCaption = "BIOMETRIC SCAN ACTIVE";
        private string _leftHeadline = "Stand in front of the camera";

        private string _statusMessage = "Matching face signature...";
        private float _progressValue = 0.14f;

        private readonly object _cameraLock = new object();
        private Bitmap _cameraFrame;

        private bool _handledTerminalFaceEvent;

        public Screen_2_Face()
            : this(FaceMode.Login)
        {
        }

        public Screen_2_Face(FaceMode mode)
        {
            _mode = mode;
            InitializeComponent();
            DoubleBuffered = true;
            ApplyModeCopy();
        }

        private void ApplyModeCopy()
        {
            if (_mode == FaceMode.Login)
            {
                _leftCaption = "BIOMETRIC SCAN ACTIVE";
                _leftHeadline = "Welcome back";
                _statusMessage = "Looking for your Face ID\n Conneting Camera..";
            }
            else
            {
                _leftCaption = "WELCOME TO FORMGUARD";
                _leftHeadline = "Let's get your Face ID";
                _statusMessage = "Position your face in the frame";
            }
        }

        private void LoginForm_Load(object sender, EventArgs e)
        {
            Bounds = Screen.PrimaryScreen.Bounds;
            LayoutControls();
            TryLaunchPythonFaceServer();
            StartFaceBackendClient();
            timerUi.Start();
        }

        private void TryLaunchPythonFaceServer()
        {
            PythonServerManager.StartIfNeeded();
        }

        private void LoginForm_Shown(object sender, EventArgs e)
        {
            _faceMatched = false;
            _showFallback = false;
            _tickCount = 0;
            timerUi.Start();
        }

        private void LoginForm_SizeChanged(object sender, EventArgs e)
        {
            LayoutControls();
            Invalidate();
        }

        private void LayoutControls()
        {
            var w = ClientSize.Width;
            var h = ClientSize.Height;
            const int margin = 40;
            var top = 78;
            var bottom = h - 40;
            var contentH = Math.Max(320, bottom - top);
            var split = (int)(w * 0.58f);

            _leftZone = new Rectangle(margin, top, split - margin - 10, contentH);
            _rightZone = new Rectangle(split + 10, top, w - split - margin - 10, contentH);

            _cameraRect = new Rectangle(
                _leftZone.Left + 20,
                _leftZone.Top + 130,
                _leftZone.Width - 40,
                _leftZone.Height - 170);

            var faceW = (int)(_cameraRect.Width * 0.46f);
            var faceH = (int)(_cameraRect.Height * 0.52f);
            _faceRect = new Rectangle(
                _cameraRect.Left + (_cameraRect.Width - faceW) / 2,
                _cameraRect.Top + (_cameraRect.Height - faceH) / 2 - 20,
                faceW,
                faceH);

            _nameBadgeRect = new Rectangle(
                _faceRect.Left + (_faceRect.Width - 170) / 2,
                _faceRect.Bottom + 24,
                170,
                38);

            _rightCardRect = new Rectangle(_rightZone.Left + 20, _rightZone.Top + 18, _rightZone.Width - 40, _rightZone.Height - 40);
            _phoneChipRect = new Rectangle(_rightCardRect.Left + 20, _rightCardRect.Top + 24, _rightCardRect.Width - 40, 60);
            _progressTrack = new Rectangle(_rightCardRect.Left + 82, _rightCardRect.Top + 435, _rightCardRect.Width - 164, 7);

            _manualTextRect = new Rectangle(_rightCardRect.Left + 20, _rightCardRect.Top + 560, _rightCardRect.Width - 40, 36);
            _manualButtonRect = new Rectangle(_rightCardRect.Left + 20, _rightCardRect.Top + 620, _rightCardRect.Width - 40, 42);

            txtManualName.Location = _manualTextRect.Location;
            txtManualName.Size = _manualTextRect.Size;

            btnSecureSession.Location = _manualButtonRect.Location;
            btnSecureSession.Size = _manualButtonRect.Size;
        }

        private void timerUi_Tick(object sender, EventArgs e)
        {
            _tickCount++;
            _scanPhase += 0.032f;
            _progressPhase += 0.019f;
            if (_scanPhase > Math.PI * 2) _scanPhase -= (float)(Math.PI * 2);
            if (_progressPhase > Math.PI * 2) _progressPhase -= (float)(Math.PI * 2);

            txtManualName.Visible = _showFallback;
            btnSecureSession.Visible = _showFallback;

            Invalidate();
        }

        private void LoginForm_Paint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.Clear(Bg);

            DrawHeader(g);
            DrawLeftZone(g);
            DrawRightZone(g);
        }

        private void DrawHeader(Graphics g)
        {
            using (var b = new SolidBrush(Color.FromArgb(16, 22, 26)))
            {
                g.FillRectangle(b, new Rectangle(0, 0, ClientSize.Width, 56));
            }

            using (var font = new Font("Segoe UI", 12f, FontStyle.Bold))
            using (var brush = new SolidBrush(Color.FromArgb(220, 220, 220)))
            {
                g.DrawString("FORMGUARD", font, brush, 34, 16);
            }

            using (var iconFont = new Font("Segoe UI Symbol", 14f))
            using (var iconBrush = new SolidBrush(Teal))
            {
                var x = ClientSize.Width - 112;
                g.DrawString("\u2699", iconFont, iconBrush, x, 16);
                g.DrawString("\u003F", iconFont, iconBrush, x + 34, 16);
                g.DrawString("\u23FB", iconFont, iconBrush, x + 66, 16);
            }
        }

        private void DrawLeftZone(Graphics g)
        {
            using (var subtitleFont = new Font("Consolas", 10f, FontStyle.Bold))
            using (var titleFont = new Font("Segoe UI", 30f, FontStyle.Regular))
            using (var teal = new SolidBrush(Teal))
            using (var white = new SolidBrush(TextMain))
            {
                g.DrawString(_leftCaption, subtitleFont, teal, _leftZone.Left + 10, _leftZone.Top + 16);
                g.DrawString(_leftHeadline, titleFont, white, _leftZone.Left + 10, _leftZone.Top + 34);
            }

            using (var panelBrush = new SolidBrush(Panel))
            using (var path = RoundedRect(_cameraRect, 24))
            {
                g.FillPath(panelBrush, path);
            }

            var lensR = Math.Min(_faceRect.Width, _faceRect.Height) / 2f + 56f;
            var cx = _faceRect.X + _faceRect.Width / 2f;
            var cy = _faceRect.Y + _faceRect.Height / 2f;

            DrawCameraFeedInLens(g, cx, cy, lensR);

            DrawViewfinderCorners(g, _faceRect, Teal, 30, 2.8f);

            var scanY = _faceRect.Top + (int)((_faceRect.Height - 2) * (0.5f + 0.5f * Math.Sin(_scanPhase)));
            using (var scanPen = new Pen(Color.FromArgb(160, Teal), 2f))
            {
                g.DrawLine(scanPen, _faceRect.Left + 24, scanY, _faceRect.Right - 24, scanY);
            }

            if (_faceMatched)
            {
                using (var path = RoundedRect(_nameBadgeRect, 18))
                using (var b = new SolidBrush(Teal))
                using (var font = new Font("Segoe UI", 11f, FontStyle.Bold))
                using (var txt = new SolidBrush(Color.FromArgb(10, 24, 26)))
                {
                    g.FillPath(b, path);
                    var t = _athleteLabel;
                    var sz = g.MeasureString(t, font);
                    g.DrawString(t, font, txt, _nameBadgeRect.X + (_nameBadgeRect.Width - sz.Width) / 2f, _nameBadgeRect.Y + 8);
                }
            }
        }

        private void DrawCameraFeedInLens(Graphics g, float cx, float cy, float lensR)
        {
            using (var clipPath = new GraphicsPath())
            {
                clipPath.AddEllipse(cx - lensR, cy - lensR, lensR * 2f, lensR * 2f);
                var oldClip = g.Clip;
                try
                {
                    g.SetClip(clipPath, CombineMode.Replace);

                    Bitmap local;
                    lock (_cameraLock)
                    {
                        local = _cameraFrame;
                    }

                    if (local != null)
                    {
                        var destW = lensR * 2f;
                        var destH = lensR * 2f;
                        var scale = Math.Max(destW / local.Width, destH / local.Height);
                        var drawW = local.Width * scale;
                        var drawH = local.Height * scale;
                        var dx = cx - drawW / 2f;
                        var dy = cy - drawH / 2f;
                        g.DrawImage(local, dx, dy, drawW, drawH);
                    }
                    else
                    {
                        using (var lensBrush = new SolidBrush(Color.FromArgb(22, 30, 34)))
                        {
                            g.FillEllipse(lensBrush, cx - lensR, cy - lensR, lensR * 2f, lensR * 2f);
                        }
                    }
                }
                finally
                {
                    g.SetClip(oldClip, CombineMode.Replace);
                    oldClip.Dispose();
                }
            }

            using (var ringPen = new Pen(Color.FromArgb(60, Teal), 2f))
            {
                g.DrawEllipse(ringPen, cx - lensR, cy - lensR, lensR * 2f, lensR * 2f);
            }
        }

        private void DrawRightZone(Graphics g)
        {
            using (var panelBrush = new SolidBrush(Color.FromArgb(10, 16, 20)))
            using (var panelPath = RoundedRect(_rightCardRect, 22))
            {
                g.FillPath(panelBrush, panelPath);
            }

            using (var chipBrush = new SolidBrush(Color.FromArgb(22, 30, 34)))
            using (var chipPath = RoundedRect(_phoneChipRect, 28))
            using (var iconFont = new Font("Segoe UI Symbol", 14f))
            using (var txtFont = new Font("Segoe UI", 11f, FontStyle.Regular))
            using (var txtBold = new Font("Segoe UI", 12f, FontStyle.Bold))
            using (var iconBrush = new SolidBrush(Teal))
            using (var muted = new SolidBrush(TextMuted))
            using (var white = new SolidBrush(TextMain))
            {
                g.FillPath(chipBrush, chipPath);
                g.DrawString("\u2630", iconFont, iconBrush, _phoneChipRect.Left + 16, _phoneChipRect.Top + 17);
                g.DrawString("Link Status: Bluetooth Feed", txtFont, muted, _phoneChipRect.Left + 50, _phoneChipRect.Top + 11);
                g.DrawString(_detectedPhoneLabel, txtBold, white, _phoneChipRect.Left + 50, _phoneChipRect.Top + 31);
            }

            var avatar = new Rectangle(_rightCardRect.Left + (_rightCardRect.Width - 110) / 2, _rightCardRect.Top + 170, 110, 110);
            using (var avatarBrush = new SolidBrush(Color.FromArgb(26, 34, 38)))
            using (var pen = new Pen(Color.FromArgb(70, Teal), 1.8f))
            using (var txtBrush = new SolidBrush(TextMain))
            using (var font = new Font("Segoe UI", 28f, FontStyle.Bold))
            {
                g.FillEllipse(avatarBrush, avatar);
                g.DrawEllipse(pen, avatar);
                var t = _avatarLetters;
                var sz = g.MeasureString(t, font);
                g.DrawString(t, font, txtBrush, avatar.X + (avatar.Width - sz.Width) / 2f, avatar.Y + (avatar.Height - sz.Height) / 2f - 2);
            }

            using (var textFont = new Font("Segoe UI", 24f, FontStyle.Regular))
            using (var tealBrush = new SolidBrush(Teal))
            {
                var msg = _statusMessage;
                var sz = g.MeasureString(msg, textFont);
                g.DrawString(msg, textFont, tealBrush, _rightCardRect.Left + (_rightCardRect.Width - sz.Width) / 2f, _rightCardRect.Top + 315);
            }

            using (var trackBrush = new SolidBrush(Color.FromArgb(38, 52, 58)))
            using (var fillBrush = new SolidBrush(Teal))
            using (var trackPath = RoundedRect(_progressTrack, 4))
            {
                g.FillPath(trackBrush, trackPath);
                var progress = Math.Max(0f, Math.Min(1f, _progressValue));
                if (!_faceMatched && progress < 0.99f)
                {
                    progress = Math.Max(progress, 0.12f + 0.38f * (0.5f + 0.5f * (float)Math.Sin(_progressPhase)));
                }

                var fill = new Rectangle(_progressTrack.X, _progressTrack.Y, (int)(_progressTrack.Width * progress), _progressTrack.Height);
                using (var fillPath = RoundedRect(fill, 4))
                {
                    g.FillPath(fillBrush, fillPath);
                }
            }

            using (var sepPen = new Pen(Color.FromArgb(34, 46, 50)))
            using (var capFont = new Font("Consolas", 10f))
            using (var mutedBrush = new SolidBrush(TextMuted))
            using (var white = new SolidBrush(Color.FromArgb(180, 180, 180)))
            {
                var y = _rightCardRect.Top + 510;
                g.DrawLine(sepPen, _rightCardRect.Left + 20, y, _rightCardRect.Left + 120, y);
                g.DrawLine(sepPen, _rightCardRect.Right - 120, y, _rightCardRect.Right - 20, y);
                //g.DrawString("SYSTEM FALLBACK", capFont, mutedBrush, _rightCardRect.Left + (_rightCardRect.Width - 120) / 2f, y - 9);
                //using (var fallbackFont = new Font("Segoe UI", 10f))
                //{
                //    g.DrawString("Face not recognised", fallbackFont, white, _rightCardRect.Left + 20, y + 24);
                //}
            }
        }

        private static void DrawViewfinderCorners(Graphics g, Rectangle rect, Color color, int len, float thickness)
        {
            using (var p = new Pen(color, thickness))
            {
                g.DrawLine(p, rect.Left, rect.Top, rect.Left + len, rect.Top);
                g.DrawLine(p, rect.Left, rect.Top, rect.Left, rect.Top + len);

                g.DrawLine(p, rect.Right - len, rect.Top, rect.Right, rect.Top);
                g.DrawLine(p, rect.Right, rect.Top, rect.Right, rect.Top + len);

                g.DrawLine(p, rect.Left, rect.Bottom, rect.Left + len, rect.Bottom);
                g.DrawLine(p, rect.Left, rect.Bottom - len, rect.Left, rect.Bottom);

                g.DrawLine(p, rect.Right - len, rect.Bottom, rect.Right, rect.Bottom);
                g.DrawLine(p, rect.Right, rect.Bottom - len, rect.Right, rect.Bottom);
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

        private void btnSecureSession_Click(object sender, EventArgs e)
        {
            _showFallback = false;
            txtManualName.Clear();
            Invalidate();
        }

        private void LoginForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                Close();
            }
            else if (e.KeyCode == Keys.F9)
            {
                _showFallback = !_showFallback;
                if (_showFallback)
                {
                    _faceMatched = false;
                }

                Invalidate();
            }
        }

        private void StartFaceBackendClient()
        {
            _stopFaceClient = false;
            _handledTerminalFaceEvent = false;
            _faceClientThread = new Thread(FaceClientLoop)
            {
                IsBackground = true,
                Name = "Screen2FaceTcpClient",
            };
            _faceClientThread.Start();
        }

        private void FaceClientLoop()
        {
            var action = _mode == FaceMode.Login ? "verify" : "register";
            var requestJson = $"{{\"action\":\"{action}\"}}\n";
            Debug.WriteLine("[Screen_2_Face] Sending action: " + action);

            while (!_stopFaceClient)
            {
                try
                {
                    using (var client = new TcpClient())
                    {
                        client.Connect(FaceHost, FacePort);
                        using (var stream = client.GetStream())
                        {
                            // Send action
                            byte[] reqBytes = Encoding.UTF8.GetBytes(requestJson);
                            stream.Write(reqBytes, 0, reqBytes.Length);
                            stream.Flush();

                            using (var reader = new StreamReader(stream, Encoding.UTF8, false, 1024 * 1024))
                            {
                                while (!_stopFaceClient)
                                {
                                    var line = reader.ReadLine();
                                    if (line == null) break;

                                    ProcessServerLine(line);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("[Screen_2_Face] backend error: " + ex.Message);
                    SafeInvoke(() =>
                    {
                        _statusMessage = "Waiting, Connecting Camera For Scanning.";
                        _progressValue = 0.08f;
                        Invalidate();
                    });
                }

                if (_stopFaceClient) break;
                Thread.Sleep(900);
            }
        }

        private void ProcessServerLine(string line)
        {
            if (string.IsNullOrEmpty(line)) return;

            // Manual JSON parsing to avoid extra dependencies
            if (TryExtractJsonStringValue(line, "frame", out string b64))
            {
                try
                {
                    byte[] bytes = Convert.FromBase64String(b64);
                    using (var ms = new MemoryStream(bytes, false))
                    using (var tmp = Image.FromStream(ms))
                    {
                        var fresh = new Bitmap(tmp);
                        lock (_cameraLock)
                        {
                            var old = _cameraFrame;
                            _cameraFrame = fresh;
                            old?.Dispose();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("[Screen_2_Face] frame decode error: " + ex.Message);
                }

                SafeInvoke(Invalidate);
            }
            else if (line.Contains("\"status\":"))
            {
                if (_handledTerminalFaceEvent) return;
                _handledTerminalFaceEvent = true;
                _stopFaceClient = true;

                bool success = TryExtractJsonStringValue(line, "status", out string statusValue) &&
                               string.Equals(statusValue, "success", StringComparison.OrdinalIgnoreCase);
                string name = null;
                string error = null;

                TryExtractJsonStringValue(line, "name", out name);
                TryExtractJsonStringValue(line, "error", out error);

                if (success)
                {
                    SafeInvoke(() =>
                    {
                        _faceMatched = true;
                        _athleteLabel = name ?? "User";
                        _avatarLetters = BuildInitials(_athleteLabel);
                        _progressValue = 1f;

                        if (_mode == FaceMode.Login)
                        {
                            _leftHeadline = "Welcome back, " + _athleteLabel;
                            _statusMessage = "Signed in - welcome back";
                        }
                        else
                        {
                           
                            _statusMessage = "Face ID Saved..Registration complete for " + _athleteLabel + ".";
                            _leftHeadline = "Welcome, " + _athleteLabel;
                        }

                        Invalidate();

                        // Navigate to dashboard after a short delay (no message box)
                        System.Windows.Forms.Timer delayNav = new System.Windows.Forms.Timer { Interval = 1500 };
                        delayNav.Tick += (s, args) =>
                        {
                            delayNav.Stop();
                            delayNav.Dispose();
                            using (var dash = new Screen_3_Dashboard(_athleteLabel))
                            {
                                dash.ShowDialog(this);
                            }
                            Close();
                        };
                        delayNav.Start();
                    });
                }
                else
                {
                    SafeInvoke(() =>
                    {
                        _faceMatched = false;
                        _progressValue = 0.2f;
                        if (_mode == FaceMode.Login)
                        {
                            _statusMessage = "User not found";
                            _leftHeadline = "Welcome back";
                        }
                        else
                        {
                            if (error != null && error.IndexOf("exists", StringComparison.OrdinalIgnoreCase) >= 0)
                                _statusMessage = "User already exists";
                            else
                                _statusMessage = "Registration failed: " + (error ?? "Unknown error");
                        }
                        Invalidate();
                    });
                }
            }
        }

        private static string ResolveFaceServerScriptPath(string startupPath)
        {
            string current = startupPath;
            for (int i = 0; i < 8 && !string.IsNullOrEmpty(current); i++)
            {
                string candidate = Path.Combine(current, "face_recognition", "face_server.py");
                Debug.WriteLine("[Screen_2_Face] probe script path: " + candidate);
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                var parent = Directory.GetParent(current);
                current = parent?.FullName;
            }

            return null;
        }

        private static bool TryExtractJsonStringValue(string line, string key, out string value)
        {
            value = null;
            if (string.IsNullOrEmpty(line) || string.IsNullOrEmpty(key)) return false;

            int keyIndex = line.IndexOf("\"" + key + "\"", StringComparison.Ordinal);
            if (keyIndex < 0) return false;

            int colonIndex = line.IndexOf(':', keyIndex);
            if (colonIndex < 0) return false;

            int i = colonIndex + 1;
            while (i < line.Length && char.IsWhiteSpace(line[i])) i++;
            if (i >= line.Length || line[i] != '"') return false;
            i++;

            var sb = new StringBuilder();
            bool escape = false;
            for (; i < line.Length; i++)
            {
                char c = line[i];
                if (escape)
                {
                    switch (c)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        default: sb.Append(c); break;
                    }
                    escape = false;
                }
                else if (c == '\\')
                {
                    escape = true;
                }
                else if (c == '"')
                {
                    value = sb.ToString();
                    return true;
                }
                else
                {
                    sb.Append(c);
                }
            }

            return false;
        }
        private static string BuildInitials(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "FG";
            var parts = name.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2) return (parts[0].Substring(0, 1) + parts[1].Substring(0, 1)).ToUpperInvariant();
            return name.Length >= 2 ? name.Substring(0, 2).ToUpperInvariant() : name.ToUpperInvariant();
        }

        private void SafeInvoke(Action action)
        {
            try
            {
                if (IsDisposed) return;
                if (InvokeRequired) BeginInvoke(action);
                else action();
            }
            catch (ObjectDisposedException) { }
            catch (InvalidOperationException) { }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _stopFaceClient = true;
            try { _faceClientThread?.Join(1500); } catch { }

            lock (_cameraLock)
            {
                _cameraFrame?.Dispose();
                _cameraFrame = null;
            }
            base.OnFormClosing(e);
        }
    }
}
