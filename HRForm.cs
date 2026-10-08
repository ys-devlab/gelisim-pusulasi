using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using System.IO;
using iTextSharp.text;
using iTextSharp.text.pdf;

namespace WindowsFormsApp1
{
    public partial class HRForm : Form
    {
        private DataGridView dgv;

        public HRForm()
        {
            InitializeComponent();
            this.WindowState = FormWindowState.Maximized;
            this.Text = "İK Yönetim Paneli";
            BuildLayout();
            LoadData(); // Butonu ve veriyi yükleyen ana metod
        }

        private void BuildLayout()
        {
            this.Controls.Clear();
            dgv = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Color.White,
                AllowUserToAddRows = false,
                RowHeadersVisible = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                RowTemplate = { Height = 45 }
            };
            this.Controls.Add(dgv);
            dgv.CellContentClick += Dgv_CellContentClick;
        }

        private void LoadData()
        {
            try
            {
                // Veriyi çek
                DataTable dt = SqlHelper.GetDataTable(
                    "SELECT RH.ResponseHeaderID AS ID, RH.RaterPersonelCode AS YoneticiKodu," +
                    " E.AdSoyad AS [Değerlendirilen Personel], E.Unvan AS [Unvan]," +
                    " E.Birim AS [Birim], RH.Status AS [Genel Durum]" +
                    " FROM ResponseHeaders RH" +
                    " JOIN Employees_Tablo E ON RH.RateePersonelCode = E.PersonelCode" +
                    " WHERE ISNULL(E.Yaka, '') <> 'Mavi'");
                dgv.DataSource = dt;

                // 1. Gereksiz sütunları gizle
                string[] hideCols = { "PersonelCode", "Durum", "Toplam", "Tamamlanan", "ID" };
                foreach (string col in hideCols)
                {
                    if (dgv.Columns.Contains(col)) dgv.Columns[col].Visible = false;
                }

                // 2. Butonu her zaman temizleyip yeniden ekle
                if (dgv.Columns.Contains("btnRapor")) dgv.Columns.Remove("btnRapor");

                DataGridViewButtonColumn btn = new DataGridViewButtonColumn
                {
                    Name = "btnRapor",
                    HeaderText = "Rapor İşlemi",
                    Text = "📄 PDF İNDİR",
                    UseColumnTextForButtonValue = true,
                    FlatStyle = FlatStyle.Flat,
                    Width = 120
                };
                dgv.Columns.Add(btn);
                btn.DisplayIndex = 0; // Butonu en başa (sol) çeker

                dgv.CellFormatting += Dgv_CellFormatting;
            }
            catch (Exception ex) { MessageBox.Show("Veri yükleme hatası: " + ex.Message); }
        }

        private void Dgv_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (dgv.Columns[e.ColumnIndex].Name == "btnRapor" && e.RowIndex >= 0)
            {
                var val = dgv.Rows[e.RowIndex].Cells["Durum"].Value;
                if (val != null && val.ToString() == "HAZIR")
                {
                    e.CellStyle.BackColor = Color.FromArgb(46, 204, 113);
                    e.CellStyle.ForeColor = Color.White;
                }
                else
                {
                    e.CellStyle.BackColor = Color.FromArgb(189, 195, 199);
                    e.CellStyle.ForeColor = Color.DimGray;
                }
            }
        }

        private void Dgv_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && dgv.Columns[e.ColumnIndex].Name == "btnRapor")
            {
                string durum = dgv.Rows[e.RowIndex].Cells["Durum"].Value?.ToString();
                if (durum == "HAZIR")
                {
                    string pAd = dgv.Rows[e.RowIndex].Cells["Değerlendirilen"].Value.ToString();
                    GeneratePDFReport(pAd);
                }
                else
                {
                    MessageBox.Show("Süreç henüz tamamlanmamış.", "Bilgi");
                }
            }
        }

        private void GeneratePDFReport(string pAd)
        {
            SaveFileDialog sfd = new SaveFileDialog { Filter = "PDF Dosyası|*.pdf", FileName = "Rapor_" + pAd.Replace(" ", "_") };
            if (sfd.ShowDialog() == DialogResult.OK)
            {
                // Çakışmayı önlemek için türlerin tam adını kullanıyoruz
                iTextSharp.text.Document doc = new iTextSharp.text.Document(iTextSharp.text.PageSize.A4);
                PdfWriter.GetInstance(doc, new FileStream(sfd.FileName, FileMode.Create));
                doc.Open();
                doc.Add(new iTextSharp.text.Paragraph("LIFTUP 360 PERFORMANS ANALIZ RAPORU"));
                doc.Add(new iTextSharp.text.Paragraph("Personel: " + pAd));
                doc.Close();
                MessageBox.Show("Rapor kaydedildi.");
            }
        }

        private void HRForm_Load(object sender, EventArgs e)
        {

        }
    }
}