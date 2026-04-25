namespace HCI_Project
{
    partial class IdleStandbyForm
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
            this.timerPulse = new System.Windows.Forms.Timer(this.components);
            this.timerNavigate = new System.Windows.Forms.Timer(this.components);
            this.lblDemoHint = new System.Windows.Forms.Label();
            this.SuspendLayout();
            //
            // timerPulse
            //
            this.timerPulse.Interval = 16;
            this.timerPulse.Tick += new System.EventHandler(this.timerPulse_Tick);
            //
            // timerNavigate
            //
            this.timerNavigate.Interval = 16;
            this.timerNavigate.Tick += new System.EventHandler(this.timerNavigate_Tick);
            //
            // lblDemoHint
            //
            this.lblDemoHint.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.lblDemoHint.AutoSize = true;
            this.lblDemoHint.BackColor = System.Drawing.Color.FromArgb(200, 10, 10, 10);
            this.lblDemoHint.Font = new System.Drawing.Font("Consolas", 8.25F);
            this.lblDemoHint.ForeColor = System.Drawing.Color.FromArgb(90, 90, 90);
            this.lblDemoHint.Location = new System.Drawing.Point(420, 12);
            this.lblDemoHint.Name = "lblDemoHint";
            this.lblDemoHint.Padding = new System.Windows.Forms.Padding(6, 4, 6, 4);
            this.lblDemoHint.Size = new System.Drawing.Size(349, 21);
            this.lblDemoHint.TabIndex = 0;
            this.lblDemoHint.Text = "F11: demo highlight   F10: simulate PHONE_NEAR";
            this.lblDemoHint.Visible = false;
            //
            // IdleStandbyForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.FromArgb(15, 15, 15);
            this.ClientSize = new System.Drawing.Size(784, 441);
            this.Controls.Add(this.lblDemoHint);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Name = "IdleStandbyForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.Manual;
            this.Text = "FormGuard";
            this.WindowState = System.Windows.Forms.FormWindowState.Maximized;
            this.Load += new System.EventHandler(this.IdleStandbyForm_Load);
            this.Shown += new System.EventHandler(this.IdleStandbyForm_Shown);
            this.SizeChanged += new System.EventHandler(this.IdleStandbyForm_SizeChanged);
            this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.IdleStandbyForm_KeyDown);
            this.Paint += new System.Windows.Forms.PaintEventHandler(this.IdleStandbyForm_Paint);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private System.Windows.Forms.Timer timerPulse;
        private System.Windows.Forms.Timer timerNavigate;
        private System.Windows.Forms.Label lblDemoHint;
    }
}
