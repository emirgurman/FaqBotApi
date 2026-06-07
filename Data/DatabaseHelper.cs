using Microsoft.Data.SqlClient;

namespace FaqBotApi.Data
{
    /// <summary>
    /// Veritabanı bağlantısını merkezi olarak yöneten yardımcı sınıf.
    /// Tüm SQL operasyonları bu sınıf üzerinden gerçekleştirilir.
    /// </summary>
    public class DatabaseHelper
    {
        private readonly string _connectionString;

        public DatabaseHelper(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new ArgumentNullException("DefaultConnection string bulunamadı.");
        }

        /// <summary>
        /// Yeni bir SQL bağlantısı döndürür.
        /// Her servis metodu kendi bağlantısını açıp kapatır (using bloğu ile).
        /// </summary>
        public SqlConnection GetConnection()
        {
            return new SqlConnection(_connectionString);
        }
    }
}