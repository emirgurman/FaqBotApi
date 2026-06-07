using Microsoft.Data.SqlClient;
using FaqBotApi.Data;
using FaqBotApi.Models;

namespace FaqBotApi.Services
{
    /// <summary>
    /// FAQ verilerinin veritabanı işlemlerini yöneten servis.
    /// Entity Framework kullanılmaz; tüm sorgular Pure SQL ile yazılmıştır.
    /// </summary>
    public class FaqService
    {
        private readonly DatabaseHelper _db;
        private readonly ILogger<FaqService> _logger;

        public FaqService(DatabaseHelper db, ILogger<FaqService> logger)
        {
            _db = db;
            _logger = logger;
        }

        /// <summary>
        /// Tüm aktif FAQ kayıtlarını listeler.
        /// İsteğe bağlı olarak dil kodu ve kategori ID'sine göre filtreler.
        /// </summary>
        public async Task<List<Faq>> GetAllAsync(string? languageCode = null, int? categoryId = null)
        {
            var faqs = new List<Faq>();

            try
            {
                using var conn = _db.GetConnection();
                await conn.OpenAsync();

                var query = @"
                    SELECT f.Id, f.CategoryId, c.Name AS CategoryName,
                           f.LanguageId, l.Code AS LanguageCode,
                           f.Question, f.Answer, f.Keywords,
                           f.ViewCount, f.IsActive, f.CreatedAt, f.UpdatedAt
                    FROM Faqs f
                    INNER JOIN Categories c ON f.CategoryId = c.Id
                    INNER JOIN Languages l ON f.LanguageId = l.Id
                    WHERE f.IsActive = 1";

                if (!string.IsNullOrEmpty(languageCode))
                    query += " AND l.Code = @LanguageCode";

                if (categoryId.HasValue)
                    query += " AND f.CategoryId = @CategoryId";

                query += " ORDER BY f.CreatedAt DESC";

                using var cmd = new SqlCommand(query, conn);

                if (!string.IsNullOrEmpty(languageCode))
                    cmd.Parameters.AddWithValue("@LanguageCode", languageCode);

                if (categoryId.HasValue)
                    cmd.Parameters.AddWithValue("@CategoryId", categoryId.Value);

                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    faqs.Add(MapFaq(reader));
                }
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "FAQ listesi alınırken SQL hatası oluştu.");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FAQ listesi alınırken beklenmeyen hata oluştu.");
                throw;
            }

            return faqs;
        }

        /// <summary>
        /// ID'ye göre tek bir FAQ kaydı getirir.
        /// Kayıt bulunamazsa null döner.
        /// </summary>
        public async Task<Faq?> GetByIdAsync(int id)
        {
            try
            {
                using var conn = _db.GetConnection();
                await conn.OpenAsync();

                var query = @"
                    SELECT f.Id, f.CategoryId, c.Name AS CategoryName,
                           f.LanguageId, l.Code AS LanguageCode,
                           f.Question, f.Answer, f.Keywords,
                           f.ViewCount, f.IsActive, f.CreatedAt, f.UpdatedAt
                    FROM Faqs f
                    INNER JOIN Categories c ON f.CategoryId = c.Id
                    INNER JOIN Languages l ON f.LanguageId = l.Id
                    WHERE f.Id = @Id AND f.IsActive = 1";

                using var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@Id", id);

                using var reader = await cmd.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    // Görüntülenme sayısını artır
                    await IncrementViewCountAsync(id);
                    return MapFaq(reader);
                }

                return null;
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "FAQ ID:{Id} getirilirken SQL hatası oluştu.", id);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FAQ ID:{Id} getirilirken beklenmeyen hata oluştu.", id);
                throw;
            }
        }

        /// <summary>
        /// Kullanıcının arama metnine göre FAQ kayıtlarını arar.
        /// Soru metni ve anahtar kelimeler üzerinde arama yapar.
        /// </summary>
        public async Task<List<Faq>> SearchAsync(string searchText, string? languageCode = null)
        {
            var faqs = new List<Faq>();

            try
            {
                using var conn = _db.GetConnection();
                await conn.OpenAsync();

                var query = @"
                    SELECT f.Id, f.CategoryId, c.Name AS CategoryName,
                           f.LanguageId, l.Code AS LanguageCode,
                           f.Question, f.Answer, f.Keywords,
                           f.ViewCount, f.IsActive, f.CreatedAt, f.UpdatedAt
                    FROM Faqs f
                    INNER JOIN Categories c ON f.CategoryId = c.Id
                    INNER JOIN Languages l ON f.LanguageId = l.Id
                    WHERE f.IsActive = 1
                    AND (f.Question LIKE @Search 
                         OR f.Answer LIKE @Search 
                         OR f.Keywords LIKE @Search)";

                if (!string.IsNullOrEmpty(languageCode))
                    query += " AND l.Code = @LanguageCode";

                query += " ORDER BY f.ViewCount DESC";

                using var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@Search", $"%{searchText}%");

                if (!string.IsNullOrEmpty(languageCode))
                    cmd.Parameters.AddWithValue("@LanguageCode", languageCode);

                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    faqs.Add(MapFaq(reader));
                }
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "FAQ arama sırasında SQL hatası oluştu. Arama: {Search}", searchText);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FAQ arama sırasında beklenmeyen hata oluştu.");
                throw;
            }

            return faqs;
        }

        /// <summary>
        /// Yeni bir FAQ kaydı oluşturur ve oluşturulan kaydın ID'sini döner.
        /// </summary>
        public async Task<int> CreateAsync(FaqRequest request)
        {
            try
            {
                using var conn = _db.GetConnection();
                await conn.OpenAsync();

                var query = @"
                    INSERT INTO Faqs (CategoryId, LanguageId, Question, Answer, Keywords, CreatedAt, UpdatedAt, IsActive, ViewCount)
                    VALUES (@CategoryId, @LanguageId, @Question, @Answer, @Keywords, GETDATE(), GETDATE(), 1, 0);
                    SELECT SCOPE_IDENTITY();";

                using var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@CategoryId", request.CategoryId);
                cmd.Parameters.AddWithValue("@LanguageId", request.LanguageId);
                cmd.Parameters.AddWithValue("@Question", request.Question);
                cmd.Parameters.AddWithValue("@Answer", request.Answer);
                cmd.Parameters.AddWithValue("@Keywords", (object?)request.Keywords ?? DBNull.Value);

                var result = await cmd.ExecuteScalarAsync();
                return Convert.ToInt32(result);
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "FAQ oluşturulurken SQL hatası oluştu.");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FAQ oluşturulurken beklenmeyen hata oluştu.");
                throw;
            }
        }

        /// <summary>
        /// Mevcut bir FAQ kaydını günceller.
        /// Başarılıysa true, kayıt bulunamazsa false döner.
        /// </summary>
        public async Task<bool> UpdateAsync(int id, FaqRequest request)
        {
            try
            {
                using var conn = _db.GetConnection();
                await conn.OpenAsync();

                var query = @"
                    UPDATE Faqs 
                    SET CategoryId = @CategoryId,
                        LanguageId = @LanguageId,
                        Question = @Question,
                        Answer = @Answer,
                        Keywords = @Keywords,
                        UpdatedAt = GETDATE()
                    WHERE Id = @Id AND IsActive = 1";

                using var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@Id", id);
                cmd.Parameters.AddWithValue("@CategoryId", request.CategoryId);
                cmd.Parameters.AddWithValue("@LanguageId", request.LanguageId);
                cmd.Parameters.AddWithValue("@Question", request.Question);
                cmd.Parameters.AddWithValue("@Answer", request.Answer);
                cmd.Parameters.AddWithValue("@Keywords", (object?)request.Keywords ?? DBNull.Value);

                var affected = await cmd.ExecuteNonQueryAsync();
                return affected > 0;
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "FAQ ID:{Id} güncellenirken SQL hatası oluştu.", id);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FAQ ID:{Id} güncellenirken beklenmeyen hata oluştu.", id);
                throw;
            }
        }

        /// <summary>
        /// FAQ kaydını fiziksel olarak silmez; IsActive = 0 yaparak pasife alır (soft delete).
        /// </summary>
        public async Task<bool> DeleteAsync(int id)
        {
            try
            {
                using var conn = _db.GetConnection();
                await conn.OpenAsync();

                var query = "UPDATE Faqs SET IsActive = 0, UpdatedAt = GETDATE() WHERE Id = @Id";

                using var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@Id", id);

                var affected = await cmd.ExecuteNonQueryAsync();
                return affected > 0;
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "FAQ ID:{Id} silinirken SQL hatası oluştu.", id);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FAQ ID:{Id} silinirken beklenmeyen hata oluştu.", id);
                throw;
            }
        }

        /// <summary>
        /// FAQ kaydının görüntülenme sayacını 1 artırır.
        /// Popüler soruları takip etmek için kullanılır.
        /// </summary>
        private async Task IncrementViewCountAsync(int id)
        {
            try
            {
                using var conn = _db.GetConnection();
                await conn.OpenAsync();

                var query = "UPDATE Faqs SET ViewCount = ViewCount + 1 WHERE Id = @Id";
                using var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@Id", id);
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                // Görüntülenme sayacı kritik değil, sadece logla
                _logger.LogWarning(ex, "FAQ ID:{Id} görüntülenme sayacı güncellenemedi.", id);
            }
        }

        /// <summary>
        /// SqlDataReader'dan Faq nesnesine dönüşüm yapar.
        /// Tekrarlanan mapping kodunu tek noktada toplar.
        /// </summary>
        private static Faq MapFaq(SqlDataReader reader) => new()
        {
            Id = reader.GetInt32(reader.GetOrdinal("Id")),
            CategoryId = reader.GetInt32(reader.GetOrdinal("CategoryId")),
            CategoryName = reader.GetString(reader.GetOrdinal("CategoryName")),
            LanguageId = reader.GetInt32(reader.GetOrdinal("LanguageId")),
            LanguageCode = reader.GetString(reader.GetOrdinal("LanguageCode")),
            Question = reader.GetString(reader.GetOrdinal("Question")),
            Answer = reader.GetString(reader.GetOrdinal("Answer")),
            Keywords = reader.IsDBNull(reader.GetOrdinal("Keywords")) ? null : reader.GetString(reader.GetOrdinal("Keywords")),
            ViewCount = reader.GetInt32(reader.GetOrdinal("ViewCount")),
            IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive")),
            CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
            UpdatedAt = reader.GetDateTime(reader.GetOrdinal("UpdatedAt"))
        };
    }
}