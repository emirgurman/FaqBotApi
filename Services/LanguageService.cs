using Microsoft.Data.SqlClient;
using FaqBotApi.Data;
using FaqBotApi.Models;

namespace FaqBotApi.Services
{
    /// <summary>
    /// Dil kayıtlarının veritabanı işlemlerini yöneten servis.
    /// Desteklenen dillerin listelenmesi, eklenmesi ve güncellenmesi işlemlerini sağlar.
    /// </summary>
    public class LanguageService
    {
        private readonly DatabaseHelper _db;
        private readonly ILogger<LanguageService> _logger;

        public LanguageService(DatabaseHelper db, ILogger<LanguageService> logger)
        {
            _db = db;
            _logger = logger;
        }

        /// <summary>
        /// Tüm aktif dilleri listeler.
        /// Streamlit arayüzündeki dil seçim kutusunu doldurmak için kullanılır.
        /// </summary>
        public async Task<List<Language>> GetAllAsync()
        {
            var languages = new List<Language>();

            try
            {
                using var conn = _db.GetConnection();
                await conn.OpenAsync();

                var query = "SELECT Id, Code, Name, IsActive FROM Languages WHERE IsActive = 1 ORDER BY Name";

                using var cmd = new SqlCommand(query, conn);
                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    languages.Add(MapLanguage(reader));
                }
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "Dil listesi alınırken SQL hatası oluştu.");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dil listesi alınırken beklenmeyen hata oluştu.");
                throw;
            }

            return languages;
        }

        /// <summary>
        /// ID'ye göre tek bir dil kaydı getirir.
        /// </summary>
        public async Task<Language?> GetByIdAsync(int id)
        {
            try
            {
                using var conn = _db.GetConnection();
                await conn.OpenAsync();

                var query = "SELECT Id, Code, Name, IsActive FROM Languages WHERE Id = @Id";

                using var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@Id", id);

                using var reader = await cmd.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                    return MapLanguage(reader);

                return null;
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "Dil ID:{Id} getirilirken SQL hatası oluştu.", id);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dil ID:{Id} getirilirken beklenmeyen hata oluştu.", id);
                throw;
            }
        }

        /// <summary>
        /// Yeni bir dil kaydı oluşturur.
        /// Aynı dil kodu zaten varsa hata fırlatır.
        /// </summary>
        public async Task<int> CreateAsync(LanguageRequest request)
        {
            try
            {
                using var conn = _db.GetConnection();
                await conn.OpenAsync();

                // Aynı kod ile kayıt var mı kontrol et
                var checkQuery = "SELECT COUNT(1) FROM Languages WHERE Code = @Code";
                using var checkCmd = new SqlCommand(checkQuery, conn);
                checkCmd.Parameters.AddWithValue("@Code", request.Code.ToLower());
                var exists = (int)(await checkCmd.ExecuteScalarAsync() ?? 0);

                if (exists > 0)
                    throw new InvalidOperationException($"'{request.Code}' dil kodu zaten mevcut.");

                var query = @"
                    INSERT INTO Languages (Code, Name, IsActive)
                    VALUES (@Code, @Name, 1);
                    SELECT SCOPE_IDENTITY();";

                using var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@Code", request.Code.ToLower());
                cmd.Parameters.AddWithValue("@Name", request.Name);

                var result = await cmd.ExecuteScalarAsync();
                return Convert.ToInt32(result);
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "Dil oluşturulurken SQL hatası oluştu.");
                throw;
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dil oluşturulurken beklenmeyen hata oluştu.");
                throw;
            }
        }

        /// <summary>
        /// Mevcut bir dil kaydını günceller.
        /// Başarılıysa true, kayıt bulunamazsa false döner.
        /// </summary>
        public async Task<bool> UpdateAsync(int id, LanguageRequest request)
        {
            try
            {
                using var conn = _db.GetConnection();
                await conn.OpenAsync();

                var query = "UPDATE Languages SET Code = @Code, Name = @Name WHERE Id = @Id";

                using var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@Id", id);
                cmd.Parameters.AddWithValue("@Code", request.Code.ToLower());
                cmd.Parameters.AddWithValue("@Name", request.Name);

                var affected = await cmd.ExecuteNonQueryAsync();
                return affected > 0;
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "Dil ID:{Id} güncellenirken SQL hatası oluştu.", id);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dil ID:{Id} güncellenirken beklenmeyen hata oluştu.", id);
                throw;
            }
        }

        /// <summary>
        /// Dil kaydını pasife alır (soft delete).
        /// İlişkili FAQ kayıtları silinmez, sadece dil pasife alınır.
        /// </summary>
        public async Task<bool> DeleteAsync(int id)
        {
            try
            {
                using var conn = _db.GetConnection();
                await conn.OpenAsync();

                var query = "UPDATE Languages SET IsActive = 0 WHERE Id = @Id";

                using var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@Id", id);

                var affected = await cmd.ExecuteNonQueryAsync();
                return affected > 0;
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "Dil ID:{Id} silinirken SQL hatası oluştu.", id);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dil ID:{Id} silinirken beklenmeyen hata oluştu.", id);
                throw;
            }
        }

        /// <summary>
        /// SqlDataReader'dan Language nesnesine dönüşüm yapar.
        /// </summary>
        private static Language MapLanguage(SqlDataReader reader) => new()
        {
            Id = reader.GetInt32(reader.GetOrdinal("Id")),
            Code = reader.GetString(reader.GetOrdinal("Code")),
            Name = reader.GetString(reader.GetOrdinal("Name")),
            IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive"))
        };
    }
}