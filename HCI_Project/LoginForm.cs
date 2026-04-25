using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;
using TUIO;

namespace HCI_Project
{
    public class LoginForm : Form, TuioListener
    {
        private static readonly Color Bg = Color.FromArgb(13, 15, 15);
        private static readonly Color Teal = Color.FromArgb(73, 244, 200);
        private static readonly Color Glass = Color.FromArgb(40, 18, 26, 26);
        private static readonly string AssetsBasePath = Path.Combine(Application.StartupPath, "Assets");

        private readonly TableLayoutPanel rootLayout;
        private readonly Panel leftColumn;
        private readonly Panel centerColumn;
        private readonly Panel rightColumn;

        private readonly PictureBox logoBox;
        private readonly Label titleLabel;
        private readonly Label subtitleLabel;
        private readonly Panel loginPanel;
        private readonly Panel signUpPanel;
        private readonly Label kioskLabel;
        private readonly Label versionLabel;
        private readonly VerticalTextPanel centerTextPanel;

        private Image backgroundImage;
        private Image trainerImage;

        private TuioClient _tuioClient;
        private DateTime _lastTuioMarkerUtc = DateTime.MinValue;
#if DEBUG
        private bool _screen3TestingShortcutOpened;
#endif

        public LoginForm()
        {
            rootLayout = new TableLayoutPanel();
            leftColumn = new Panel();
            centerColumn = new Panel();
            rightColumn = new Panel();

            logoBox = new PictureBox();
            titleLabel = new Label();
            subtitleLabel = new Label();
            loginPanel = new Panel();
            signUpPanel = new Panel();
            kioskLabel = new Label();
            versionLabel = new Label();
            centerTextPanel = new VerticalTextPanel();

            InitializeComponent();
        }

        private void InitializeComponent()
        {
            SuspendLayout();

            AutoScaleMode = AutoScaleMode.None;
            FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Maximized;
            BackColor = Bg;
            KeyPreview = true;
            DoubleBuffered = true;
            Text = "FormGuard Login";
            StartPosition = FormStartPosition.Manual;

            Load += LoginForm_Load;
            KeyDown += LoginForm_KeyDown;
            FormClosed += LoginForm_FormClosed;
            Paint += LoginForm_Paint;

            rootLayout.Dock = DockStyle.Fill;
            rootLayout.BackColor = Color.Transparent;
            rootLayout.Margin = new Padding(0);
            rootLayout.Padding = new Padding(0);
            rootLayout.ColumnCount = 3;
            rootLayout.RowCount = 1;
            rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40f));
            rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20f));
            rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40f));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            BuildLeftColumn();
            BuildCenterColumn();
            BuildRightColumn();

            rootLayout.Controls.Add(leftColumn, 0, 0);
            rootLayout.Controls.Add(centerColumn, 1, 0);
            rootLayout.Controls.Add(rightColumn, 2, 0);
            Controls.Add(rootLayout);

            ResumeLayout(false);
        }

        private void BuildLeftColumn()
        {
            leftColumn.Dock = DockStyle.Fill;
            leftColumn.BackColor = Color.FromArgb(130, 9, 11, 11);
            leftColumn.Padding = new Padding(42, 30, 28, 24);

            logoBox.Size = new Size(66, 66);
            logoBox.Location = new Point(0, 0);
            logoBox.BackColor = Color.Transparent;
            logoBox.SizeMode = PictureBoxSizeMode.Zoom;

            titleLabel.AutoSize = true;
            titleLabel.Location = new Point(0, 70);
            titleLabel.ForeColor = Teal;
            titleLabel.BackColor = Color.Transparent;
            titleLabel.Font = new Font("Segoe UI Black", 27f, FontStyle.Bold | FontStyle.Italic, GraphicsUnit.Point);
            titleLabel.Text = "FormGuard";

            subtitleLabel.AutoSize = true;
            subtitleLabel.Location = new Point(2, 122);
            subtitleLabel.ForeColor = Color.FromArgb(120, 220, 198);
            subtitleLabel.BackColor = Color.Transparent;
            subtitleLabel.Font = new Font("Consolas", 10f, FontStyle.Regular, GraphicsUnit.Point);
            subtitleLabel.Text = "TUIO Experimental Telemetry Interface";

            ConfigureActionPanel(loginPanel, "INTERFACE NODE 01", "LOGIN", "➜", "SECURE_LINK: ACTIVE", "ID: FG_01_AUTH");
            ConfigureActionPanel(signUpPanel, "INTERFACE NODE 02", "SIGN UP", "◉", "ONBOARD_MODE: READY", "PROTOCOL: JOINT_NET");

            loginPanel.Location = new Point(0, 220);
            signUpPanel.Location = new Point(0, 420);

            kioskLabel.AutoSize = true;
            kioskLabel.ForeColor = Color.FromArgb(125, 156, 156);
            kioskLabel.BackColor = Color.Transparent;
            kioskLabel.Font = new Font("Consolas", 9f, FontStyle.Regular, GraphicsUnit.Point);
            kioskLabel.Text = "Kiosk: Online";

            versionLabel.AutoSize = true;
            versionLabel.ForeColor = Color.FromArgb(102, 130, 130);
            versionLabel.BackColor = Color.Transparent;
            versionLabel.Font = new Font("Consolas", 9f, FontStyle.Regular, GraphicsUnit.Point);
            versionLabel.Text = "v2.4.0-TUIO";

            leftColumn.Controls.Add(logoBox);
            leftColumn.Controls.Add(titleLabel);
            leftColumn.Controls.Add(subtitleLabel);
            leftColumn.Controls.Add(loginPanel);
            leftColumn.Controls.Add(signUpPanel);
            leftColumn.Controls.Add(kioskLabel);
            leftColumn.Controls.Add(versionLabel);
            leftColumn.Resize += LeftColumn_Resize;
        }

        private void BuildCenterColumn()
        {
            centerColumn.Dock = DockStyle.Fill;
            centerColumn.BackColor = Color.FromArgb(70, 8, 10, 10);

            centerTextPanel.Dock = DockStyle.Fill;
            centerTextPanel.Text = "BELIEVE IN YOURSELF";
            centerTextPanel.ForeColor = Color.FromArgb(110, 235, 196);
            centerTextPanel.Font = new Font("Consolas", 12f, FontStyle.Bold, GraphicsUnit.Point);
            centerTextPanel.BackColor = Color.Transparent;

            centerColumn.Controls.Add(centerTextPanel);
        }

        private void BuildRightColumn()
        {
            rightColumn.Dock = DockStyle.Fill;
            rightColumn.Margin = new Padding(0);
            rightColumn.BackColor = Bg;
            rightColumn.Paint += RightColumn_Paint;
        }

        private void ConfigureActionPanel(Panel panel, string nodeText, string actionText, string icon, string footerLeft, string footerRight)
        {
            panel.Size = new Size(420, 145);
            panel.BackColor = Glass;
            panel.Cursor = Cursors.Default;
            panel.Padding = new Padding(18, 14, 18, 14);

            var nodeLabel = new Label
            {
                AutoSize = true,
                BackColor = Color.Transparent,
                ForeColor = Color.FromArgb(126, 176, 176),
                Font = new Font("Consolas", 9f, FontStyle.Regular, GraphicsUnit.Point),
                Text = nodeText,
                Location = new Point(10, 12)
            };

            var actionLabel = new Label
            {
                AutoSize = true,
                BackColor = Color.Transparent,
                ForeColor = Color.WhiteSmoke,
                Font = new Font("Segoe UI Black", 24f, FontStyle.Bold, GraphicsUnit.Point),
                Text = actionText,
                Location = new Point(8, 34)
            };

            var iconPanel = new Panel
            {
                Size = new Size(66, 66),
                BackColor = Color.FromArgb(35, 7, 20, 20),
                Tag = icon
            };
            iconPanel.Paint += IconPanel_Paint;
            iconPanel.Location = new Point(panel.Width - iconPanel.Width - 22, 34);
            iconPanel.Anchor = AnchorStyles.Top | AnchorStyles.Right;

            var footerLeftLabel = new Label
            {
                AutoSize = true,
                BackColor = Color.Transparent,
                ForeColor = Color.FromArgb(92, 118, 118),
                Font = new Font("Consolas", 7.8f, FontStyle.Regular, GraphicsUnit.Point),
                Text = footerLeft,
                Location = new Point(10, panel.Height - 20)
            };

            var footerRightLabel = new Label
            {
                AutoSize = true,
                BackColor = Color.Transparent,
                ForeColor = Color.FromArgb(92, 118, 118),
                Font = new Font("Consolas", 7.8f, FontStyle.Regular, GraphicsUnit.Point),
                Text = footerRight,
                Location = new Point(panel.Width - 150, panel.Height - 20),
                Anchor = AnchorStyles.Right | AnchorStyles.Bottom
            };

            panel.Controls.Add(nodeLabel);
            panel.Controls.Add(actionLabel);
            panel.Controls.Add(iconPanel);
            panel.Controls.Add(footerLeftLabel);
            panel.Controls.Add(footerRightLabel);
        }

        private void LeftColumn_Resize(object sender, EventArgs e)
        {
            var panelWidth = Math.Max(260, leftColumn.ClientSize.Width - 20);
            loginPanel.Width = panelWidth;
            signUpPanel.Width = panelWidth;

            loginPanel.Location = new Point(0, Math.Max(220, leftColumn.ClientSize.Height / 2 - 20));
            signUpPanel.Location = new Point(0, loginPanel.Bottom + 18);

            kioskLabel.Location = new Point(0, leftColumn.ClientSize.Height - 28);
            versionLabel.Location = new Point(140, leftColumn.ClientSize.Height - 28);
        }

        private void LoginForm_Load(object sender, EventArgs e)
        {
            // Keep gym ambience in background and trainer dominant on right panel.
            backgroundImage = LoadImageHardcoded("Gym.jpg", "background.jpg", "gym.jpg");
            logoBox.Image = LoadImageHardcoded("gym logo.jpg", "logo.png", "gym logo.png");
            trainerImage = LoadImageHardcoded("Fitness Trainer.jpeg", "Fitness Trainer.png", "trainer.jpg", "fitness trainer.jpg");
            LeftColumn_Resize(this, EventArgs.Empty);

            try
            {
                _tuioClient = new TuioClient(3333);
                _tuioClient.addTuioListener(this);
                _tuioClient.connect();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[LoginForm] TUIO connect failed: " + ex.Message);
            }
        }

        private void LoginForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            try
            {
                _tuioClient?.disconnect();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[LoginForm] TUIO disconnect: " + ex.Message);
            }

            _tuioClient = null;
            backgroundImage?.Dispose();
            trainerImage?.Dispose();
            logoBox.Image?.Dispose();
        }

        private Image LoadImageHardcoded(params string[] fileNames)
        {
            try
            {
                if (!Directory.Exists(AssetsBasePath))
                {
                    Debug.WriteLine("[LoginForm] Assets folder missing: " + AssetsBasePath);
                    return null;
                }

                foreach (var fileName in fileNames)
                {
                    var fullPath = Path.Combine(AssetsBasePath, fileName);
                    if (!File.Exists(fullPath))
                    {
                        continue;
                    }

                    return Image.FromFile(fullPath);
                }

                Debug.WriteLine("[LoginForm] Missing images in Assets path: " + string.Join(", ", fileNames));
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[LoginForm] Failed loading image set: " + ex.Message);
                return null;
            }
        }

        private static Image MakeNearWhiteTransparent(Image source)
        {
            if (source == null)
            {
                return null;
            }

            var bitmap = new Bitmap(source.Width, source.Height);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.DrawImage(source, 0, 0, source.Width, source.Height);
            }

            // First apply classic color-key transparency based on top-left pixel.
            bitmap.MakeTransparent(bitmap.GetPixel(0, 0));

            for (var y = 0; y < bitmap.Height; y++)
            {
                for (var x = 0; x < bitmap.Width; x++)
                {
                    var c = bitmap.GetPixel(x, y);
                    if (c.R >= 240 && c.G >= 240 && c.B >= 240)
                    {
                        bitmap.SetPixel(x, y, Color.FromArgb(0, c.R, c.G, c.B));
                    }
                }
            }

            source.Dispose();
            return bitmap;
        }

        private void LoginForm_Paint(object sender, PaintEventArgs e)
        {
            if (backgroundImage != null)
            {
                e.Graphics.DrawImage(backgroundImage, ClientRectangle);
            }
            else
            {
                using (var brush = new SolidBrush(Bg))
                {
                    e.Graphics.FillRectangle(brush, ClientRectangle);
                }
            }

            using (var scanPen = new Pen(Color.FromArgb(16, 180, 200, 200), 1f))
            {
                for (var y = 0; y < Height; y += 3)
                {
                    e.Graphics.DrawLine(scanPen, 0, y, Width, y);
                }
            }

            using (var bottomFade = new LinearGradientBrush(
                new Rectangle(0, Height - 180, Width, 180),
                Color.FromArgb(0, 13, 15, 15),
                Color.FromArgb(200, 13, 15, 15),
                LinearGradientMode.Vertical))
            {
                e.Graphics.FillRectangle(bottomFade, new Rectangle(0, Height - 180, Width, 180));
            }
        }

        private void RightColumn_Paint(object sender, PaintEventArgs e)
        {
            var rect = rightColumn.ClientRectangle;
            e.Graphics.SmoothingMode = SmoothingMode.HighQuality;

            if (trainerImage != null)
            {
                e.Graphics.DrawImage(trainerImage, rect);
            }
            else
            {
                using (var fallback = new SolidBrush(Color.FromArgb(28, 30, 30)))
                {
                    e.Graphics.FillRectangle(fallback, rect);
                }
            }

            using (var overlay = new LinearGradientBrush(
                rect,
                Color.FromArgb(230, 13, 15, 15),
                Color.FromArgb(0, 13, 15, 15),
                LinearGradientMode.Horizontal))
            {
                e.Graphics.FillRectangle(overlay, rect);
            }
        }

        private void IconPanel_Paint(object sender, PaintEventArgs e)
        {
            var panel = (Panel)sender;
            var icon = panel.Tag as string ?? "+";
            var rect = new Rectangle(1, 1, panel.Width - 3, panel.Height - 3);

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var pen = new Pen(Color.FromArgb(90, Teal), 2f))
            {
                pen.DashStyle = DashStyle.Dash;
                e.Graphics.DrawRectangle(pen, rect);
            }

            using (var brush = new SolidBrush(Teal))
            using (var font = new Font("Segoe UI Symbol", 19f, FontStyle.Bold, GraphicsUnit.Point))
            {
                var size = e.Graphics.MeasureString(icon, font);
                e.Graphics.DrawString(icon, font, brush, (panel.Width - size.Width) / 2f, (panel.Height - size.Height) / 2f);
            }
        }

        private void LoginForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.L)
            {
                StartLoginFlow();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.S)
            {
                StartSignUpFlow();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Escape)
            {
                Close();
                e.Handled = true;
            }
        }

        private void StopTuioClient()
        {
            try
            {
                _tuioClient?.disconnect();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[LoginForm] TUIO disconnect: " + ex.Message);
            }

            _tuioClient = null;
        }

        private void StartLoginFlow()
        {
            StopTuioClient();
            Hide();
            IdleStandbyForm.BleDetected = false;   // reset the flag

            using (var ble = new IdleStandbyForm())
            {
                ble.ShowDialog(this);              // wait here until BLE dialog closes
            }

            if (IdleStandbyForm.BleDetected)      // did we get a phone?
            {
                using (var s2 = new Screen_2_Face(FaceMode.Login))
                {
                    s2.ShowDialog(this);
                }
            }

            Close();   // close LoginForm (and the whole app)
        }

        private void StartSignUpFlow()
        {
            Hide();
            using (var s2 = new Screen_2_Face(FaceMode.Signup))
            {
                s2.ShowDialog(this);
            }

            Close();
        }

#if DEBUG
        private void StartScreen3TestingShortcut()
        {
            if (_screen3TestingShortcutOpened)
            {
                return;
            }

            _screen3TestingShortcutOpened = true;

            StopTuioClient();
            Hide();

            var dashboard = new Screen_3_Dashboard("Testing");
            dashboard.FormClosed += (_, __) => Close();
            dashboard.Show();
        }
#endif

        public void addTuioObject(TuioObject obj)
        {
            var symbolId = obj.SymbolID;
            BeginInvoke((Action)(() =>
            {
                if ((DateTime.UtcNow - _lastTuioMarkerUtc).TotalSeconds < 1.2) return;
                _lastTuioMarkerUtc = DateTime.UtcNow;

                if (symbolId == 0)
                {
                    // BLE login flow
                    StartLoginFlow();
                }
                else if (symbolId == 1)
                {
                    // Direct face login
                    StartFaceLogin();
                }
                else if (symbolId == 2)
                {
                    // Face signup
                    StartSignUpFlow();
                }
#if DEBUG
                else if (symbolId == 20)
                {
                    StartScreen3TestingShortcut();
                }
#endif
            }));
        }

        // *** ADD this new method anywhere in LoginForm class ***
        private void StartFaceLogin()
        {
            Hide();
            using (var s2 = new Screen_2_Face(FaceMode.Login))
            {
                s2.ShowDialog(this);
            }
            Close();
        }
  

        public void updateTuioObject(TuioObject tobj)
        {
        }

        public void removeTuioObject(TuioObject tobj)
        {
        }

        public void addTuioCursor(TuioCursor tcur)
        {
        }

        public void updateTuioCursor(TuioCursor tcur)
        {
        }

        public void removeTuioCursor(TuioCursor tcur)
        {
        }

        public void addTuioBlob(TuioBlob tblb)
        {
        }

        public void updateTuioBlob(TuioBlob tblb)
        {
        }

        public void removeTuioBlob(TuioBlob tblb)
        {
        }

        public void refresh(TuioTime ftime)
        {
        }
    }

    internal class VerticalTextPanel : Panel
    {
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.HighQuality;
            e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var content = (Text ?? string.Empty).Replace(" ", string.Empty).ToUpperInvariant();
            if (content.Length == 0)
            {
                return;
            }

            using (var linePen = new Pen(Color.FromArgb(80, 73, 244, 200), 1f))
            {
                e.Graphics.DrawLine(linePen, Width / 2f, 30, Width / 2f, 200);
                e.Graphics.DrawLine(linePen, Width / 2f, Height - 200, Width / 2f, Height - 30);
            }

            var charHeight = Font.GetHeight(e.Graphics) + 4f;
            var totalHeight = charHeight * content.Length;
            var y = (Height - totalHeight) / 2f;

            using (var brush = new SolidBrush(ForeColor))
            {
                foreach (var ch in content)
                {
                    var s = ch.ToString();
                    var sz = e.Graphics.MeasureString(s, Font);
                    e.Graphics.DrawString(s, Font, brush, (Width - sz.Width) / 2f, y);
                    y += charHeight;
                }
            }
        }
    }

    // Compatibility alias for existing references.
    public class Login : LoginForm
    {
    }
}
