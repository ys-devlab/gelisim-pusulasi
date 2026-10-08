using System;
using System.Data.SqlClient;

namespace WindowsFormsApp1
{
    /// <summary>
    /// Atama (RaterAssignments) ile değerlendirme formu kabukları (ResponseHeaders) arasındaki
    /// tutarlılığı garanti eder. Her atama için bir ResponseHeaders satırı (form) olmalıdır;
    /// uygulama "değerlendirilecek çalışanlar" listesini ResponseHeaders'tan okuduğu için,
    /// formu olmayan atamalar listede görünmez.
    ///
    /// Bu yardımcı idempotent'tir: yalnızca EKSİK formları 'Draft' olarak üretir, mevcutlara
    /// dokunmaz. Liste/HR ekranı okunmadan hemen önce çağrılarak sistem kendi kendini onarır;
    /// böylece ileride eklenen yeni atamalar için manuel SQL düzeltmesi gerekmez.
    /// </summary>
    public static class ResponseHeaderSync
    {
        // İki aşamalı, idempotent senkron:
        //  1) Her atama için eksik form kabuğunu (ResponseHeaders) üret.
        //  2) Her formun ratee'sinin UNVANINA bağlı yetkinlik sorularını (ResponseItems) üret.
        //     Soru kümesi: TitleCompetencyMappings(Title=ratee.Unvan) -> Competencies -> Questions.
        //     Cevap (NumericAnswer) NULL (cevaplanmamış) bırakılır.
        // Eşleştirme (Rater, Ratee, RaterType) üzerinden yapılır; CycleID değeri atamadan kopyalanır.
        private const string SyncSql = @"
INSERT INTO dbo.ResponseHeaders (CycleID, RateePersonelCode, RaterPersonelCode, RaterTypeID, Status)
SELECT ra.CycleID, ra.RateePersonelCode, ra.RaterPersonelCode, ra.RaterTypeID, 'Draft'
FROM dbo.RaterAssignments ra
WHERE (@rater IS NULL OR ra.RaterPersonelCode = @rater)
  AND NOT EXISTS (
        SELECT 1 FROM dbo.ResponseHeaders rh
        WHERE rh.RateePersonelCode = ra.RateePersonelCode
          AND rh.RaterPersonelCode = ra.RaterPersonelCode
          AND rh.RaterTypeID       = ra.RaterTypeID
  );

INSERT INTO dbo.ResponseItems (ResponseHeaderID, QuestionID, NumericAnswer)
SELECT DISTINCT rh.ResponseHeaderID, q.QuestionID, NULL
FROM dbo.ResponseHeaders rh
JOIN dbo.Employees_Tablo e            ON e.PersonelCode = rh.RateePersonelCode
JOIN dbo.TitleCompetencyMappings tcm  ON tcm.Title      = e.Unvan
JOIN dbo.Questions q                  ON q.CompetencyID = tcm.CompetencyID
WHERE (@rater IS NULL OR rh.RaterPersonelCode = @rater)
  AND NOT EXISTS (
        SELECT 1 FROM dbo.ResponseItems ri
        WHERE ri.ResponseHeaderID = rh.ResponseHeaderID
          AND ri.QuestionID       = q.QuestionID
  );";

        /// <summary>Belirli bir değerlendiriciye ait eksik formları üretir (değerlendirme ekranı için).</summary>
        public static void EnsureForRater(string raterPersonelCode)
        {
            if (string.IsNullOrWhiteSpace(raterPersonelCode)) return;
            Run(raterPersonelCode.Trim());
        }

        /// <summary>Tüm atamalar için eksik formları üretir (HR/genel görünüm için).</summary>
        public static void EnsureAll()
        {
            Run(null);
        }

        private static void Run(string raterOrNull)
        {
            try
            {
                SqlHelper.ExecuteNonQuery(SyncSql, new[]
                {
                    new SqlParameter("@rater", (object)raterOrNull ?? DBNull.Value)
                });
            }
            catch
            {
                // Best-effort: senkron başarısız olsa bile mevcut veriyle liste yine yüklenir.
            }
        }
    }
}
