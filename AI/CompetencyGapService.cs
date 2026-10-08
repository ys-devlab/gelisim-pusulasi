using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;

namespace WindowsFormsApp1.AI
{
	public sealed class CompetencyGapService
	{
		public sealed class CompetencyGapItem
		{
			public string CompetencyName { get; set; }
			public double WeightedScore { get; set; }
		}

		private readonly string _connectionString;

		public CompetencyGapService(string connectionString)
		{
			_connectionString = connectionString ?? string.Empty;
		}

		public List<CompetencyGapItem> GetTopWeakCompetencies(string rateePersonelCode, int top = 3, double threshold = 3.0)
		{
			if (string.IsNullOrWhiteSpace(rateePersonelCode) || string.IsNullOrWhiteSpace(_connectionString))
			{
				return new List<CompetencyGapItem>();
			}

			const string sql = @"
WITH BaseAverages AS (
    SELECT C.CompetencyName, RT.RaterTypeCode, E.Yaka,
           AVG(CAST(RI.NumericAnswer AS FLOAT)) AS GrpAvg
    FROM ResponseItems RI
    JOIN ResponseHeaders RH ON RI.ResponseHeaderID = RH.ResponseHeaderID
    JOIN Employees_Tablo E ON RH.RateePersonelCode = E.PersonelCode
    JOIN Questions Q ON RI.QuestionID = Q.QuestionID
    JOIN Competencies C ON Q.CompetencyID = C.CompetencyID
    JOIN RaterTypes RT ON RH.RaterTypeID = RT.RaterTypeID
    WHERE RH.RateePersonelCode = @code
      AND RH.Status = 'Completed'
      AND RI.NumericAnswer > 0
      AND RH.CycleID = (SELECT TOP 1 CycleID FROM EvaluationCycles WHERE IsActive = 1)
    GROUP BY C.CompetencyName, RT.RaterTypeCode, E.Yaka
),
RateeFlags AS (
    SELECT CASE WHEN COUNT(*) > 0 THEN 1 ELSE 0 END AS HasSub
    FROM RaterAssignments RA
    JOIN RaterTypes RT ON RA.RaterTypeID = RT.RaterTypeID
    WHERE RA.RateePersonelCode = @code
      AND RT.RaterTypeCode = 'Subordinate'
),
WeightLogic AS (
    SELECT B.*,
           CASE
               WHEN B.Yaka = 'Mavi' AND B.RaterTypeCode LIKE 'Manager%' THEN 50.0
               WHEN B.Yaka = 'Beyaz' AND B.RaterTypeCode LIKE 'Manager%' THEN CASE WHEN (SELECT HasSub FROM RateeFlags) = 1 THEN 25.0 ELSE 50.0 END
               WHEN B.RaterTypeCode IN ('Subordinate', 'Peer', 'JointWorker') THEN 25.0
               ELSE 0.0
           END AS Weight
    FROM BaseAverages B
),
FinalScores AS (
    SELECT CompetencyName,
           CAST(ISNULL(SUM(CASE WHEN RaterTypeCode <> 'Self' THEN GrpAvg * Weight END) /
                       NULLIF(SUM(CASE WHEN RaterTypeCode <> 'Self' THEN Weight END), 0), 0) AS DECIMAL(5,2)) AS WeightedScore
    FROM WeightLogic
    GROUP BY CompetencyName
)
SELECT TOP (@top)
       CompetencyName,
       CAST(WeightedScore AS FLOAT) AS WeightedScore
FROM FinalScores
WHERE WeightedScore < @threshold
ORDER BY WeightedScore ASC, CompetencyName ASC;";

			var result = new List<CompetencyGapItem>();

			using (var conn = new SqlConnection(_connectionString))
			using (var cmd = new SqlCommand(sql, conn))
			{
				cmd.Parameters.AddWithValue("@code", rateePersonelCode);
				cmd.Parameters.AddWithValue("@top", top);
				cmd.Parameters.AddWithValue("@threshold", threshold);

				conn.Open();
				using (var reader = cmd.ExecuteReader())
				{
					while (reader.Read())
					{
						var name = reader["CompetencyName"] == DBNull.Value ? string.Empty : reader["CompetencyName"].ToString();
						var score = reader["WeightedScore"] == DBNull.Value ? 0.0 : Convert.ToDouble(reader["WeightedScore"]);
						if (!string.IsNullOrWhiteSpace(name))
						{
							result.Add(new CompetencyGapItem
							{
								CompetencyName = name.Trim(),
								WeightedScore = score
							});
						}
					}
				}
			}

			return result.Take(Math.Max(1, top)).ToList();
		}
	}
}
