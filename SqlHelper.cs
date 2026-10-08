using System;
using System.Data;
using System.Data.SqlClient;
using System.Configuration; // App.config okumak için

namespace WindowsFormsApp1
{
    public static class SqlHelper
    {
        // Bağlantı cümlesini App.config dosyasından okur
        private static string connectionString = ConfigurationManager.ConnectionStrings["LiftUpConnection"].ConnectionString;

        // 1. Tablo Çekmek İçin (SELECT * FROM...)
        public static DataTable GetDataTable(string query, SqlParameter[] parameters = null)
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    if (parameters != null) cmd.Parameters.AddRange(parameters);

                    DataTable dt = new DataTable();
                    try
                    {
                        conn.Open();
                        using (SqlDataAdapter da = new SqlDataAdapter(cmd))
                        {
                            da.Fill(dt);
                        }
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("Veri Çekme Hatası: " + ex.Message);
                    }
                    return dt;
                }
            }
        }

        // 2. Ekleme/Silme/Güncelleme İçin (INSERT, UPDATE, DELETE)
        public static void ExecuteNonQuery(string query, SqlParameter[] parameters = null)
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    if (parameters != null) cmd.Parameters.AddRange(parameters);

                    try
                    {
                        conn.Open();
                        cmd.ExecuteNonQuery();
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("İşlem Hatası: " + ex.Message);
                    }
                }
            }
        }

        public static object ExecuteScalar(string query, SqlParameter[] parameters = null)
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    if (parameters != null) cmd.Parameters.AddRange(parameters);
                    try
                    {
                        conn.Open();
                        object result = cmd.ExecuteScalar();
                        // Eğer sonuç null ise 0 dön (hata almamak için)
                        return (result == null || result == DBNull.Value) ? 0 : result;
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("Tekil Veri Okuma Hatası: " + ex.Message);
                    }
                }
            }
        }
    }
}