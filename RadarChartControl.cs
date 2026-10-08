using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace WindowsFormsApp1
{
    /// <summary>
    /// İki dönemi kıyaslayan KÖŞELİ (N-gen) radar/spider grafiği.
    /// 7 yetkinlik için HEPTAGON (7gen) çizer; tüm halkalar düz kenarlı çokgendir —
    /// daire/yuvarlak ASLA kullanılmaz (madde 7 kuralı).
    /// Eski dönem soluk/saydam, yeni (aktif) dönem canlı renkle üst üste (overlay) çizilir.
    /// </summary>
    public sealed class RadarChartControl : Panel
    {
        private List<string> _axes = new List<string>();
        private double[] _oldVals;
        private double[] _newVals;
        private string _oldLabel = "Eski Dönem";
        private string _newLabel = "Aktif Dönem";
        private const double MaxVal = 5.0;

        private static readonly Color OldLine = Color.FromArgb(170, 120, 144, 156); // soluk gri-mavi
        private static readonly Color OldFill = Color.FromArgb(60, 120, 144, 156);
        private static readonly Color NewLine = Color.FromArgb(255, 0, 120, 215);    // canlı mavi
        private static readonly Color NewFill = Color.FromArgb(95, 0, 120, 215);
        private static readonly Color GridCol = Color.FromArgb(224, 228, 232);
        private static readonly Color AxisCol = Color.FromArgb(205, 210, 216);
        private static readonly Color TextCol = Color.FromArgb(60, 70, 80);

        public RadarChartControl()
        {
            DoubleBuffered = true;
            BackColor = Color.White;
            ResizeRedraw = true;
        }

        public void SetData(List<string> axes, double[] oldVals, double[] newVals, string oldLabel, string newLabel)
        {
            _axes = axes ?? new List<string>();
            _oldVals = oldVals;
            _newVals = newVals;
            _oldLabel = string.IsNullOrWhiteSpace(oldLabel) ? "Eski Dönem" : oldLabel;
            _newLabel = string.IsNullOrWhiteSpace(newLabel) ? "Aktif Dönem" : newLabel;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int n = _axes.Count;
            if (n < 3)
            {
                TextRenderer.DrawText(g, "Karşılaştırma grafiği için yeterli\ndeğerlendirme verisi bulunmuyor.",
                    Font, ClientRectangle, TextCol,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                return;
            }

            int legendH = 26;
            int padX = 78, padY = 56;
            float size = Math.Min(Width - padX * 2, Height - legendH - padY * 2);
            if (size < 40) return;

            PointF center = new PointF(Width / 2f, legendH + (Height - legendH) / 2f);
            float radius = size / 2f;

            // Açılar: tepe noktası -90°, saat yönünde eşit aralık
            double[] ang = new double[n];
            for (int i = 0; i < n; i++) ang[i] = -Math.PI / 2 + i * 2 * Math.PI / n;

            // 1) Köşeli grid (5 seviye) — DÜZ KENARLI ÇOKGEN (heptagon vb.), daire değil
            int levels = 5;
            using (var gridPen = new Pen(GridCol, 1f))
            {
                for (int lv = 1; lv <= levels; lv++)
                {
                    float r = radius * lv / levels;
                    g.DrawPolygon(gridPen, Polygon(center, r, ang, n));
                }
            }

            // 2) Eksen çizgileri + yetkinlik etiketleri
            using (var axisPen = new Pen(AxisCol, 1f))
            using (var font = new Font("Segoe UI", 8f))
            using (var br = new SolidBrush(TextCol))
            {
                for (int i = 0; i < n; i++)
                {
                    PointF outer = Pt(center, radius, ang[i]);
                    g.DrawLine(axisPen, center, outer);

                    string lbl = _axes[i] ?? "";
                    if (lbl.Length > 20) lbl = lbl.Substring(0, 19) + "…";
                    float lx = center.X + (float)((radius + 12) * Math.Cos(ang[i]));
                    float ly = center.Y + (float)((radius + 12) * Math.Sin(ang[i]));
                    SizeF sz = g.MeasureString(lbl, font);
                    double cx = Math.Cos(ang[i]);
                    float tx = lx;
                    if (cx < -0.2) tx = lx - sz.Width;
                    else if (cx < 0.2) tx = lx - sz.Width / 2f;
                    g.DrawString(lbl, font, br, tx, ly - sz.Height / 2f);
                }
            }

            // 3) Veri çokgenleri — önce eski (soluk), sonra yeni (canlı) overlay
            DrawSeries(g, center, radius, ang, _oldVals, OldFill, OldLine, n);
            DrawSeries(g, center, radius, ang, _newVals, NewFill, NewLine, n);

            // 4) Legend
            using (var lf = new Font("Segoe UI", 8.5f, FontStyle.Bold))
            {
                int x = 12;
                // Eski dönem yoksa (tek dönem) yalnız aktif legend gösterilir.
                if (_oldVals != null)
                    x = DrawLegend(g, x, 6, OldLine, _oldLabel, lf) + 16;
                DrawLegend(g, x, 6, NewLine, _newLabel, lf);
            }
        }

        private void DrawSeries(Graphics g, PointF center, float radius, double[] ang, double[] vals, Color fill, Color line, int n)
        {
            if (vals == null || vals.Length != n) return;
            PointF[] pts = new PointF[n];
            for (int i = 0; i < n; i++)
            {
                double v = Math.Max(0, Math.Min(MaxVal, vals[i]));
                float r = (float)(radius * v / MaxVal);
                pts[i] = Pt(center, r, ang[i]);
            }
            using (var fb = new SolidBrush(fill)) g.FillPolygon(fb, pts);
            using (var lp = new Pen(line, 2f)) g.DrawPolygon(lp, pts);
            using (var pb = new SolidBrush(line))
                foreach (var p in pts) g.FillEllipse(pb, p.X - 2.6f, p.Y - 2.6f, 5.2f, 5.2f);
        }

        private static PointF[] Polygon(PointF center, float r, double[] ang, int n)
        {
            PointF[] pts = new PointF[n];
            for (int i = 0; i < n; i++) pts[i] = Pt(center, r, ang[i]);
            return pts;
        }

        private static PointF Pt(PointF c, float r, double a)
        {
            return new PointF(c.X + (float)(r * Math.Cos(a)), c.Y + (float)(r * Math.Sin(a)));
        }

        private int DrawLegend(Graphics g, int x, int y, Color c, string text, Font f)
        {
            using (var b = new SolidBrush(c)) g.FillRectangle(b, x, y + 2, 14, 11);
            using (var tb = new SolidBrush(TextCol)) g.DrawString(text, f, tb, x + 19, y);
            return x + 19 + (int)g.MeasureString(text, f).Width;
        }
    }
}
