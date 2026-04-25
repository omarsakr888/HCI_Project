namespace HCI_Project
{
    partial class Screen_2_Face
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.timerUi = new System.Windows.Forms.Timer(this.components);
            this.txtManualName = new System.Windows.Forms.TextBox();
            this.btnSecureSession = new System.Windows.Forms.Button();
            this.lblHint = new System.Windows.Forms.Label();
            this.SuspendLayout();
            //
            // timerUi
            //
            this.timerUi.Interval = 16;
            this.timerUi.Tick += new System.EventHandler(this.timerUi_Tick);
            //
            // txtManualName
            //
            this.txtManualName.BackColor = System.Drawing.Color.FromArgb(20, 26, 29);
            this.txtManualName.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.txtManualName.Font = new System.Drawing.Font("Segoe UI", 11F);
            this.txtManualName.ForeColor = System.Drawing.Color.FromArgb(220, 220, 220);
            this.txtManualName.Location = new System.Drawing.Point(620, 570);
            this.txtManualName.Name = "txtManualName";
            this.txtManualName.Size = new System.Drawing.Size(280, 27);
            this.txtManualName.TabIndex = 0;
            this.txtManualName.Visible = false;
            //
            // btnSecureSession
            //
            this.btnSecureSession.BackColor = System.Drawing.Color.FromArgb(0, 212, 170);
            this.btnSecureSession.FlatAppearance.BorderSize = 0;
            this.btnSecureSession.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnSecureSession.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.btnSecureSession.ForeColor = System.Drawing.Color.FromArgb(8, 20, 22);
            this.btnSecureSession.Location = new System.Drawing.Point(620, 620);
            this.btnSecureSession.Name = "btnSecureSession";
            this.btnSecureSession.Size = new System.Drawing.Size(280, 42);
            this.btnSecureSession.TabIndex = 1;
            this.btnSecureSession.Text = "ESTABLISH SECURE SESSION";
            this.btnSecureSession.UseVisualStyleBackColor = false;
            this.btnSecureSession.Visible = false;
            this.btnSecureSession.Click += new System.EventHandler(this.btnSecureSession_Click);
            //
            // lblHint
            //
            this.lblHint.AutoSize = true;
            this.lblHint.BackColor = System.Drawing.Color.FromArgb(180, 8, 10, 12);
            this.lblHint.Font = new System.Drawing.Font("Consolas", 8.25F);
            this.lblHint.ForeColor = System.Drawing.Color.FromArgb(100, 100, 100);
            this.lblHint.Location = new System.Drawing.Point(12, 9);
            this.lblHint.Name = "lblHint";
            this.lblHint.Padding = new System.Windows.Forms.Padding(6, 4, 6, 4);
            this.lblHint.Size = new System.Drawing.Size(265, 21);
            this.lblHint.TabIndex = 2;
            this.lblHint.Text = "F9: toggle fallback   ESC: close";
            //
            // LoginForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.FromArgb(12, 16, 19);
            this.ClientSize = new System.Drawing.Size(1200, 760);
            this.Controls.Add(this.lblHint);
            this.Controls.Add(this.btnSecureSession);
            this.Controls.Add(this.txtManualName);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.KeyPreview = true;
            this.Name = "LoginForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.Manual;
            this.Text = "FormGuard · Face Login";
            this.WindowState = System.Windows.Forms.FormWindowState.Maximized;
            this.Load += new System.EventHandler(this.LoginForm_Load);
            this.Shown += new System.EventHandler(this.LoginForm_Shown);
            this.SizeChanged += new System.EventHandler(this.LoginForm_SizeChanged);
            this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.LoginForm_KeyDown);
            this.Paint += new System.Windows.Forms.PaintEventHandler(this.LoginForm_Paint);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private System.Windows.Forms.Timer timerUi;
        private System.Windows.Forms.TextBox txtManualName;
        private System.Windows.Forms.Button btnSecureSession;
        private System.Windows.Forms.Label lblHint;
    }
}
