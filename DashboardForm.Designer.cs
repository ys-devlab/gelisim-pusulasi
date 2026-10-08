namespace WindowsFormsApp1
{
    partial class DashboardForm
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

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.SuspendLayout();
            // 
            // DashboardForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1280, 800); // Boyutu Dashboard koduna uygun hale getirdik
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None; // Kenarlıkları kaldırdık
            this.Name = "DashboardForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "LiftUp 360 Dashboard";
            this.Load += new System.EventHandler(this.DashboardForm_Load);
            this.ResumeLayout(false);
        }

        #endregion
    }
}