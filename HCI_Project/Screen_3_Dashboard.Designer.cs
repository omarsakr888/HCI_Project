namespace HCI_Project
{
    partial class Screen_3_Dashboard
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
            this.SuspendLayout();
            this.timerUi.Interval = 16;
            this.timerUi.Tick += new System.EventHandler(this.timerUi_Tick);
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.FromArgb(10, 12, 14);
            this.ClientSize = new System.Drawing.Size(1200, 760);
            this.DoubleBuffered = true;
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.KeyPreview = true;
            this.Name = "Screen_3_Dashboard";
            this.StartPosition = System.Windows.Forms.FormStartPosition.Manual;
            this.Text = "FormGuard · Dashboard";
            this.WindowState = System.Windows.Forms.FormWindowState.Maximized;
            this.Load += new System.EventHandler(this.Screen_3_Dashboard_Load);
            this.Paint += new System.Windows.Forms.PaintEventHandler(this.Screen_3_Dashboard_Paint);
            this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.Screen_3_Dashboard_KeyDown);
            this.ResumeLayout(false);
        }

        private System.Windows.Forms.Timer timerUi;
    }
}
