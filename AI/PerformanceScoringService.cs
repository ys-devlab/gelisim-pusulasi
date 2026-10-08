using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;

namespace WindowsFormsApp1.AI
{
    public sealed class PerformanceScoringService
    {
        public double ComputeFinalScore5(
            DataTable dtAnaYetkinlikler,
            IReadOnlyDictionary<string, double> competencyWeights01,
            double fallbackOverallScore)
        {
            if (dtAnaYetkinlikler == null || dtAnaYetkinlikler.Rows.Count == 0) return Clamp5(fallbackOverallScore);
            if (!dtAnaYetkinlikler.Columns.Contains("CompetencyName")) return Clamp5(fallbackOverallScore);
            if (!dtAnaYetkinlikler.Columns.Contains("WeightedScore")) return Clamp5(fallbackOverallScore);

            double sumWeighted = 0.0;
            double sumW = 0.0;

            foreach (DataRow r in dtAnaYetkinlikler.Rows)
            {
                string comp = (r["CompetencyName"] == DBNull.Value) ? "" : (r["CompetencyName"].ToString() ?? "");
                comp = comp.Trim();
                if (string.IsNullOrWhiteSpace(comp)) continue;

                double baseScore = ToDoubleSafe(r["WeightedScore"]);
                if (baseScore <= 0) continue;

                double w01 = 1.0;
                if (competencyWeights01 != null && competencyWeights01.TryGetValue(comp, out double v))
                {
                    w01 = Clamp01(v);
                }

                // If model returned 0 for everything, we will fallback later.
                sumWeighted += baseScore * w01;
                sumW += w01;
            }

            if (sumW <= 0.0000001) return Clamp5(fallbackOverallScore);
            return Clamp5(sumWeighted / sumW);
        }

        private static double ToDoubleSafe(object o)
        {
            try
            {
                if (o == null || o == DBNull.Value) return 0;
                if (o is double d) return d;
                if (o is float f) return f;
                if (o is decimal m) return (double)m;
                if (o is int i) return i;
                if (o is long l) return l;

                string s = o.ToString();
                if (string.IsNullOrWhiteSpace(s)) return 0;
                if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out double inv)) return inv;
                if (double.TryParse(s, NumberStyles.Any, new CultureInfo("tr-TR"), out double tr)) return tr;
                return 0;
            }
            catch { return 0; }
        }

        private static double Clamp01(double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) return 0;
            if (v < 0) return 0;
            if (v > 1) return 1;
            return v;
        }

        private static double Clamp5(double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) return 0;
            if (v < 0) return 0;
            if (v > 5) return 5;
            return v;
        }
    }
}

