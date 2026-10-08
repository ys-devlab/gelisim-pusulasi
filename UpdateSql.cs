using System;
using System.IO;
using System.Text.RegularExpressions;

class Program {
    static void Main() {
        try {
            string path = @"c:\WindowsFormsApp1\ReportForm.cs";
            string content = File.ReadAllText(path);
            
            string pattern = @"string sql = @""(.*?)    -- 4\. DONUT GRAFİĞİ \(AĞIRLIKLAR\) - HATA DÜZELTİLDİ(.*?)FROM GroupedWeightsD;(\r\n|\n)?(\s*)"";";
            
            string newSql = @"string sql = @""
    WITH RateeFlags AS (
        SELECT CASE WHEN EXISTS (
            SELECT 1 FROM ResponseHeaders RH
            JOIN RaterTypes RT ON RH.RaterTypeID = RT.RaterTypeID
            WHERE RH.RateePersonelCode = @code AND RH.Status = 'Completed' AND RT.RaterTypeCode = 'Subordinate'
        ) THEN 1 ELSE 0 END AS HasSub
    ),
    -- 1. ANA YETKİNLİKLER
    GroupAverages AS (
        SELECT C.CompetencyName, 
               CASE WHEN RT.RaterTypeCode LIKE 'Manager%' THEN 'Manager'
                    WHEN RT.RaterTypeCode = 'Subordinate' THEN 'Subordinate'
                    WHEN RT.RaterTypeCode = 'Peer' THEN 'Peer'
                    WHEN RT.RaterTypeCode = 'JointWorker' THEN 'JointWorker'
                    WHEN RT.RaterTypeCode = 'Self' THEN 'Self'
                    ELSE 'Other' END AS RolGroup,
               E.Yaka,
               AVG(CAST(RI.NumericAnswer AS FLOAT)) AS GrpAvg
        FROM ResponseItems RI
        JOIN ResponseHeaders RH ON RI.ResponseHeaderID = RH.ResponseHeaderID
        JOIN Employees_Tablo E ON RH.RateePersonelCode = E.PersonelCode
        JOIN Questions Q ON RI.QuestionID = Q.QuestionID
        JOIN Competencies C ON Q.CompetencyID = C.CompetencyID
        JOIN RaterTypes RT ON RH.RaterTypeID = RT.RaterTypeID
        WHERE RH.RateePersonelCode = @code AND RH.Status = 'Completed' AND RI.NumericAnswer > 0
        GROUP BY C.CompetencyName, 
                 CASE WHEN RT.RaterTypeCode LIKE 'Manager%' THEN 'Manager'
                      WHEN RT.RaterTypeCode = 'Subordinate' THEN 'Subordinate'
                      WHEN RT.RaterTypeCode = 'Peer' THEN 'Peer'
                      WHEN RT.RaterTypeCode = 'JointWorker' THEN 'JointWorker'
                      WHEN RT.RaterTypeCode = 'Self' THEN 'Self'
                      ELSE 'Other' END, E.Yaka
    ),
    WeightLogic AS (
        SELECT B.*,
            CASE 
                WHEN B.Yaka = 'Mavi' AND B.RolGroup = 'Manager' THEN 50.0
                WHEN B.Yaka = 'Beyaz' AND B.RolGroup = 'Manager' THEN CASE WHEN (SELECT HasSub FROM RateeFlags) = 1 THEN 25.0 ELSE 50.0 END
                WHEN B.RolGroup IN ('Subordinate', 'Peer', 'JointWorker') THEN 25.0
                ELSE 0.0
            END AS Weight
        FROM GroupAverages B
    ),
    MainScores AS (
        SELECT CompetencyName,
            CAST(ISNULL(MAX(CASE WHEN RolGroup = 'Self' THEN GrpAvg END), 0) AS DECIMAL(5,2)) AS SelfScore,
            CAST(ISNULL(MAX(CASE WHEN RolGroup = 'Manager' THEN GrpAvg END), 0) AS DECIMAL(5,2)) AS ManagerScore,
            CAST(ISNULL(SUM(CASE WHEN RolGroup <> 'Self' THEN GrpAvg * Weight END) / NULLIF(SUM(CASE WHEN RolGroup <> 'Self' THEN Weight END), 0), 0) AS DECIMAL(5,2)) AS WeightedScore,
            CAST(ISNULL(SUM(CASE WHEN RolGroup <> 'Self' THEN GrpAvg * Weight END) / NULLIF(SUM(CASE WHEN RolGroup <> 'Self' THEN Weight END), 0), 0) - ISNULL(MAX(CASE WHEN RolGroup = 'Self' THEN GrpAvg END), 0) AS DECIMAL(5,2)) AS JohariGap
        FROM WeightLogic
        GROUP BY CompetencyName
    ),
    TitleAvg AS (
        SELECT C.CompetencyName, AVG(CAST(RI.NumericAnswer AS FLOAT)) AS CompanyTitleAvg
        FROM ResponseItems RI
        JOIN ResponseHeaders RH ON RI.ResponseHeaderID = RH.ResponseHeaderID
        JOIN Employees_Tablo E ON RH.RateePersonelCode = E.PersonelCode
        JOIN Questions Q ON RI.QuestionID = Q.QuestionID
        JOIN Competencies C ON Q.CompetencyID = C.CompetencyID
        WHERE RH.Status = 'Completed' AND RI.NumericAnswer > 0 
          AND E.Unvan = @title
        GROUP BY C.CompetencyName
    )
    SELECT M.CompetencyName, M.SelfScore, M.ManagerScore, M.WeightedScore, M.JohariGap,
           CAST(ISNULL(T.CompanyTitleAvg, 0) AS DECIMAL(5,2)) AS CompanyTitleAvg
    FROM MainScores M
    LEFT JOIN TitleAvg T ON M.CompetencyName = T.CompetencyName
    ORDER BY M.CompetencyName;

    -- 2. 21 SORULUK DETAY
    WITH GroupAveragesQ AS (
        SELECT C.CompetencyName, Q.QuestionText,
               CASE WHEN RT.RaterTypeCode LIKE 'Manager%' THEN 'Manager'
                    WHEN RT.RaterTypeCode = 'Subordinate' THEN 'Subordinate'
                    WHEN RT.RaterTypeCode = 'Peer' THEN 'Peer'
                    WHEN RT.RaterTypeCode = 'JointWorker' THEN 'JointWorker'
                    WHEN RT.RaterTypeCode = 'Self' THEN 'Self'
                    ELSE 'Other' END AS RolGroup,
               E.Yaka,
               AVG(CAST(RI.NumericAnswer AS FLOAT)) AS GrpAvg
        FROM ResponseItems RI
        JOIN ResponseHeaders RH ON RI.ResponseHeaderID = RH.ResponseHeaderID
        JOIN Employees_Tablo E ON RH.RateePersonelCode = E.PersonelCode
        JOIN Questions Q ON RI.QuestionID = Q.QuestionID
        JOIN Competencies C ON Q.CompetencyID = C.CompetencyID
        JOIN RaterTypes RT ON RH.RaterTypeID = RT.RaterTypeID
        WHERE RH.RateePersonelCode = @code AND RH.Status = 'Completed' AND RI.NumericAnswer > 0
        GROUP BY C.CompetencyName, Q.QuestionText,
                 CASE WHEN RT.RaterTypeCode LIKE 'Manager%' THEN 'Manager'
                      WHEN RT.RaterTypeCode = 'Subordinate' THEN 'Subordinate'
                      WHEN RT.RaterTypeCode = 'Peer' THEN 'Peer'
                      WHEN RT.RaterTypeCode = 'JointWorker' THEN 'JointWorker'
                      WHEN RT.RaterTypeCode = 'Self' THEN 'Self'
                      ELSE 'Other' END, E.Yaka
    ),
    RateeFlagsQ AS (
        SELECT CASE WHEN EXISTS (
            SELECT 1 FROM ResponseHeaders RH
            JOIN RaterTypes RT ON RH.RaterTypeID = RT.RaterTypeID
            WHERE RH.RateePersonelCode = @code AND RH.Status = 'Completed' AND RT.RaterTypeCode = 'Subordinate'
        ) THEN 1 ELSE 0 END AS HasSub
    ),
    WeightLogicQ AS (
        SELECT B.*,
            CASE 
                WHEN B.Yaka = 'Mavi' AND B.RolGroup = 'Manager' THEN 50.0
                WHEN B.Yaka = 'Beyaz' AND B.RolGroup = 'Manager' THEN CASE WHEN (SELECT HasSub FROM RateeFlagsQ) = 1 THEN 25.0 ELSE 50.0 END
                WHEN B.RolGroup IN ('Subordinate', 'Peer', 'JointWorker') THEN 25.0
                ELSE 0.0
            END AS Weight
        FROM GroupAveragesQ B
    ),
    MainQ AS (
        SELECT CompetencyName, QuestionText,
            CAST(ISNULL(MAX(CASE WHEN RolGroup = 'Self' THEN GrpAvg END), 0) AS DECIMAL(5,2)) AS SelfScore,
            CAST(ISNULL(MAX(CASE WHEN RolGroup = 'Manager' THEN GrpAvg END), 0) AS DECIMAL(5,2)) AS ManagerScore,
            CAST(ISNULL(SUM(CASE WHEN RolGroup <> 'Self' THEN GrpAvg * Weight END) / NULLIF(SUM(CASE WHEN RolGroup <> 'Self' THEN Weight END), 0), 0) AS DECIMAL(5,2)) AS WeightedScore
        FROM WeightLogicQ
        GROUP BY CompetencyName, QuestionText
    ),
    TitleAvgQ AS (
        SELECT C.CompetencyName, Q.QuestionText, AVG(CAST(RI.NumericAnswer AS FLOAT)) AS CompanyTitleAvg
        FROM ResponseItems RI
        JOIN ResponseHeaders RH ON RI.ResponseHeaderID = RH.ResponseHeaderID
        JOIN Employees_Tablo E ON RH.RateePersonelCode = E.PersonelCode
        JOIN Questions Q ON RI.QuestionID = Q.QuestionID
        JOIN Competencies C ON Q.CompetencyID = C.CompetencyID
        WHERE RH.Status = 'Completed' AND RI.NumericAnswer > 0 
          AND E.Unvan = @title
        GROUP BY C.CompetencyName, Q.QuestionText
    )
    SELECT M.CompetencyName, M.QuestionText, M.SelfScore, M.ManagerScore, M.WeightedScore,
           CAST(ISNULL(T.CompanyTitleAvg, 0) AS DECIMAL(5,2)) AS CompanyTitleAvg
    FROM MainQ M
    LEFT JOIN TitleAvgQ T ON M.CompetencyName = T.CompetencyName AND M.QuestionText = T.QuestionText
    ORDER BY M.CompetencyName, M.QuestionText;

    -- 3. GELİŞİM PLANI (EĞİTİMLER)
    WITH GroupAveragesT AS (
        SELECT C.CompetencyName, 
               CASE WHEN RT.RaterTypeCode LIKE 'Manager%' THEN 'Manager'
                    WHEN RT.RaterTypeCode = 'Subordinate' THEN 'Subordinate'
                    WHEN RT.RaterTypeCode = 'Peer' THEN 'Peer'
                    WHEN RT.RaterTypeCode = 'JointWorker' THEN 'JointWorker'
                    WHEN RT.RaterTypeCode = 'Self' THEN 'Self'
                    ELSE 'Other' END AS RolGroup,
               E.Yaka, AVG(CAST(RI.NumericAnswer AS FLOAT)) AS GrpAvg
        FROM ResponseItems RI
        JOIN ResponseHeaders RH ON RI.ResponseHeaderID = RH.ResponseHeaderID
        JOIN Employees_Tablo E ON RH.RateePersonelCode = E.PersonelCode
        JOIN Questions Q ON RI.QuestionID = Q.QuestionID
        JOIN Competencies C ON Q.CompetencyID = C.CompetencyID
        JOIN RaterTypes RT ON RH.RaterTypeID = RT.RaterTypeID
        WHERE RH.RateePersonelCode = @code AND RH.Status = 'Completed' AND RI.NumericAnswer > 0
        GROUP BY C.CompetencyName, 
                 CASE WHEN RT.RaterTypeCode LIKE 'Manager%' THEN 'Manager'
                      WHEN RT.RaterTypeCode = 'Subordinate' THEN 'Subordinate'
                      WHEN RT.RaterTypeCode = 'Peer' THEN 'Peer'
                      WHEN RT.RaterTypeCode = 'JointWorker' THEN 'JointWorker'
                      WHEN RT.RaterTypeCode = 'Self' THEN 'Self'
                      ELSE 'Other' END, E.Yaka
    ),
    RateeFlagsT AS (
        SELECT CASE WHEN EXISTS (
            SELECT 1 FROM ResponseHeaders RH
            JOIN RaterTypes RT ON RH.RaterTypeID = RT.RaterTypeID
            WHERE RH.RateePersonelCode = @code AND RH.Status = 'Completed' AND RT.RaterTypeCode = 'Subordinate'
        ) THEN 1 ELSE 0 END AS HasSub
    ),
    WeightLogicT AS (
        SELECT B.*,
            CASE 
                WHEN B.Yaka = 'Mavi' AND B.RolGroup = 'Manager' THEN 50.0
                WHEN B.Yaka = 'Beyaz' AND B.RolGroup = 'Manager' THEN CASE WHEN (SELECT HasSub FROM RateeFlagsT) = 1 THEN 25.0 ELSE 50.0 END
                WHEN B.RolGroup IN ('Subordinate', 'Peer', 'JointWorker') THEN 25.0
                ELSE 0.0
            END AS Weight
        FROM GroupAveragesT B
    ),
    FinalScoresT AS (
        SELECT CompetencyName,
            CAST(ISNULL(SUM(CASE WHEN RolGroup <> 'Self' THEN GrpAvg * Weight END) / NULLIF(SUM(CASE WHEN RolGroup <> 'Self' THEN Weight END), 0), 0) AS DECIMAL(5,2)) AS WeightedScore
        FROM WeightLogicT GROUP BY CompetencyName
    )
    SELECT F.CompetencyName, F.WeightedScore,
        CASE WHEN F.WeightedScore <= 2.99 THEN 'Düşük' WHEN F.WeightedScore <= 4.00 THEN 'Orta' ELSE 'Yüksek' END AS Seviye,
        ISNULL(T.TrainingName, 'Gelişim planı atanmamış.') AS TrainingName,
        ISNULL(T.TrainingDescription, 'Detay bulunmuyor.') AS TrainingDescription,
        ISNULL(T.TrainingType, '') AS TrainingType,
        ISNULL(T.TrainingLink, '') AS TrainingLink 
    FROM FinalScoresT F
    LEFT JOIN Trainings T ON F.CompetencyName = T.CompetencyName 
        AND T.Level = (CASE WHEN F.WeightedScore <= 2.99 THEN 'Düşük' WHEN F.WeightedScore <= 4.00 THEN 'Orta' ELSE 'Yüksek' END);

    -- 4. DONUT GRAFİĞİ (AĞIRLIKLAR)
    WITH RateeDataD AS (
        SELECT Yaka, (SELECT CASE WHEN EXISTS (
            SELECT 1 FROM ResponseHeaders RH
            JOIN RaterTypes RT ON RH.RaterTypeID = RT.RaterTypeID
            WHERE RH.RateePersonelCode = @code AND RH.Status = 'Completed' AND RT.RaterTypeCode = 'Subordinate'
        ) THEN 1 ELSE 0 END) AS HasSub
        FROM Employees_Tablo WHERE PersonelCode = @code
    ),
    BaseWeightsD AS (
        SELECT 
            CASE WHEN RT.RaterTypeCode LIKE 'Manager%' THEN 'Yönetici'
                 WHEN RT.RaterTypeCode = 'Subordinate' THEN 'Astlar'
                 WHEN RT.RaterTypeCode = 'Peer' THEN 'Ekip Arkadaşları'
                 WHEN RT.RaterTypeCode = 'JointWorker' THEN 'Ortak İş Yürütülenler'
                 ELSE 'Diğer' END AS Rol,
            COUNT(DISTINCT RA.RaterPersonelCode) AS KisiSayisi,
            CASE 
                WHEN MAX(D.Yaka) = 'Mavi' AND MAX(CASE WHEN RT.RaterTypeCode LIKE 'Manager%' THEN 1 ELSE 0 END) = 1 THEN 50.0
                WHEN MAX(D.Yaka) = 'Beyaz' AND MAX(CASE WHEN RT.RaterTypeCode LIKE 'Manager%' THEN 1 ELSE 0 END) = 1 THEN CASE WHEN MAX(D.HasSub) = 1 THEN 25.0 ELSE 50.0 END
                WHEN MAX(CASE WHEN RT.RaterTypeCode IN ('Subordinate', 'Peer', 'JointWorker') THEN 1 ELSE 0 END) = 1 THEN 25.0
                ELSE 0.0
            END AS RawWeight
        FROM RaterAssignments RA
        JOIN RaterTypes RT ON RA.RaterTypeID = RT.RaterTypeID
        CROSS JOIN RateeDataD D
        WHERE RA.RateePersonelCode = @code AND RT.RaterTypeCode <> 'Self'
          AND EXISTS (SELECT 1 FROM ResponseHeaders RH WHERE RH.RateePersonelCode = RA.RateePersonelCode AND RH.RaterPersonelCode = RA.RaterPersonelCode AND RH.Status = 'Completed')
        GROUP BY 
            CASE WHEN RT.RaterTypeCode LIKE 'Manager%' THEN 'Yönetici'
                 WHEN RT.RaterTypeCode = 'Subordinate' THEN 'Astlar'
                 WHEN RT.RaterTypeCode = 'Peer' THEN 'Ekip Arkadaşları'
                 WHEN RT.RaterTypeCode = 'JointWorker' THEN 'Ortak İş Yürütülenler'
                 ELSE 'Diğer' END
    ),
    TotalWeightD AS (SELECT SUM(RawWeight) as TotalW FROM BaseWeightsD)
    SELECT Rol, KisiSayisi, 
           CAST(ROUND((RawWeight * 100.0) / NULLIF((SELECT TotalW FROM TotalWeightD), 0), 0) AS INT) AS EtkiYuzdesi
    FROM BaseWeightsD
    WHERE RawWeight > 0;"";";

            if (!Regex.IsMatch(content, pattern, RegexOptions.Singleline)) {
                Console.WriteLine("Regex failed to match. Saving original content to check pattern.");
                File.WriteAllText(@"c:\WindowsFormsApp1\debug.txt", content);
                return;
            }

            string replaced = Regex.Replace(content, pattern, newSql, RegexOptions.Singleline);
            File.WriteAllText(path, replaced);
            Console.WriteLine("Success");
        } catch (Exception ex) {
            Console.WriteLine(ex.ToString());
        }
    }
}
