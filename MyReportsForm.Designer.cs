namespace WindowsFormsApp1
{
    partial class MyReportsForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null)) components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
			this.SuspendLayout();
			// 
			// MyReportsForm
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(1733, 1009);
			this.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
			this.Name = "MyReportsForm";
			this.Text = "Raporlarım";
			this.Load += new System.EventHandler(this.MyReportsForm_Load);
			this.ResumeLayout(false);

        }
    }
}
