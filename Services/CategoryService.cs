using Microsoft.Data.SqlClient;
using FaqBotApi.Data;
using FaqBotApi.Models;

namespace FaqBotApi.Services
{
    /// <summary>
    /// Kategori kayıtlarının veritabanı işlemlerini yöneten servis.
    /// FAQ'ların gruplandırılması için kategori yönetimini sağlar.
    /// </summary>
    public class CategoryService
    {
        private readonly DatabaseHelper _db;
        private readonly ILogger<CategoryService> _logger;

        public CategoryService(DatabaseHelper db, ILogger<CategoryService> logger)
        {
            _db = db;
            _logger = logger;
        }

        /// <summary>
        /// Tüm aktif kategorileri listeler.
        /// Streamlit arayüzündeki kategori seçim kutusunu doldurmak için kullanılır.
        /// </summary>
        public async Task<List<Category>> GetAllAsync()
        {
            var categories = new List<Category>();

            try
            {
                using var conn = _db.GetConnection();
                await conn.OpenAsync();

                var query = @"
                    SELECT Id, Name, Description, IsActive 
                    FROM Categories 
                    WHERE IsActive = 1 
                    ORDER BY Name";

                using var cmd = new SqlCommand(query, conn);
                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    categories.Add(MapCategory(reader));
                }
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "Kategori listesi alınırken SQL hatası oluştu.");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kategori listesi alınırken beklenmeyen hata oluştu.");
                throw;
            }

            return categories;
        }

        /// <summary>
        /// ID'ye göre tek bir kategori kaydı getirir.
        /// </summary>
        public async Task<Category?> GetByIdAsync(int id)
        {
            try
            {
                using var conn = _db.GetConnection();
                await conn.OpenAsync();

                var query = "SELECT Id, Name, Description, IsActive FROM Categories WHERE Id = @Id";

                using var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@Id", id);

                using var reader = await cmd.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                    return MapCategory(reader);

                return null;
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "Kategori ID:{Id} getirilirken SQL hatası oluştu.", id);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kategori ID:{Id} getirilirken beklenmeyen hata oluştu.", id);
                throw;
            }
        }

        /// <summary>
        /// Her kategoriye ait kaç adet aktif FAQ bulunduğunu döner.
        /// Yönetim panelinde istatistik göstermek için kullanılır.
        /// </summary>
        public async Task<List<CategoryStats>> GetStatsAsync()
        {
            var stats = new List<CategoryStats>();

            try
            {
                using var conn = _db.GetConnection();
                await conn.OpenAsync();

                var query = @"
                    SELECT c.Id, c.Name, COUNT(f.Id) AS FaqCount
                    FROM Categories c
                    LEFT JOIN Faqs f ON f.CategoryId = c.Id AND f.IsActive = 1
                    WHERE c.IsActive = 1
                    GROUP BY c.Id, c.Name
                    ORDER BY FaqCount DESC";

                using var cmd = new SqlCommand(query, conn);
                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    stats.Add(new CategoryStats
                    {
                        Id = reader.GetInt32(0),
                        Name = reader.GetString(1),
                        FaqCount = reader.GetInt32(2)
                    });
                }
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "Kategori istatistikleri alınırken SQL hatası oluştu.");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kategori istatistikleri alınırken beklenmeyen hata oluştu.");
                throw;
            }

            return stats;
        }

        /// <summary>
        /// Yeni bir kategori kaydı oluşturur.
        /// Aynı isimde kategori varsa hata fırlatır.
        /// </summary>
        public async Task<int> CreateAsync(CategoryRequest request)
        {
            try
            {
                using var conn = _db.GetConnection();
                await conn.OpenAsync();

                // Aynı isimde kategori var mı kontrol et
                var checkQuery = "SELECT COUNT(1) FROM Categories WHERE Name = @Name";
                using var checkCmd = new SqlCommand(checkQuery, conn);
                checkCmd.Parameters.AddWithValue("@Name", request.Name);
                var exists = (int)(await checkCmd.ExecuteScalarAsync() ?? 0);

                if (exists > 0)
                    throw new InvalidOperationException($"'{request.Name}' kategorisi zaten mevcut.");

                var query = @"
                    INSERT INTO Categories (Name, Description, IsActive)
                    VALUES (@Name, @Description, 1);
                    SELECT SCOPE_IDENTITY();";

                using var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@Name", request.Name);
                cmd.Parameters.AddWithValue("@Description", (object?)request.Description ?? DBNull.Value);

                var result = await cmd.ExecuteScalarAsync();
                return Convert.ToInt32(result);
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "Kategori oluşturulurken SQL hatası oluştu.");
                throw;
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kategori oluşturulurken beklenmeyen hata oluştu.");
                throw;
            }
        }

        /// <summary>
        /// Mevcut bir kategori kaydını günceller.
        /// Başarılıysa true, kayıt bulunamazsa false döner.
        /// </summary>
        public async Task<bool> UpdateAsync(int id, CategoryRequest request)
        {
            try
            {
                using var conn = _db.GetConnection();
                await conn.OpenAsync();

                var query = @"
                    UPDATE Categories 
                    SET Name = @Name, Description = @Description 
                    WHERE Id = @Id AND IsActive = 1";

                using var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@Id", id);
                cmd.Parameters.AddWithValue("@Name", request.Name);
                cmd.Parameters.AddWithValue("@Description", (object?)request.Description ?? DBNull.Value);

                var affected = await cmd.ExecuteNonQueryAsync();
                return affected > 0;
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "Kategori ID:{Id} güncellenirken SQL hatası oluştu.", id);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kategori ID:{Id} güncellenirken beklenmeyen hata oluştu.", id);
                throw;
            }
        }

        /// <summary>
        /// Kategoriyi pasife alır (soft delete).
        /// İçinde FAQ olan kategoriler silinemez, önce FAQ'ların taşınması gerekir.
        /// </summary>
        public async Task<bool> DeleteAsync(int id)
        {
            try
            {
                using var conn = _db.GetConnection();
                await conn.OpenAsync();

                // Kategoriye bağlı aktif FAQ var mı kontrol et
                var checkQuery = "SELECT COUNT(1) FROM Faqs WHERE CategoryId = @Id AND IsActive = 1";
                using var checkCmd = new SqlCommand(checkQuery, conn);
                checkCmd.Parameters.AddWithValue("@Id", id);
                var faqCount = (int)(await checkCmd.ExecuteScalarAsync() ?? 0);

                if (faqCount > 0)
                    throw new InvalidOperationException($"Bu kategoriye ait {faqCount} aktif FAQ var. Önce FAQ'ları taşıyın veya silin.");

                var query = "UPDATE Categories SET IsActive = 0 WHERE Id = @Id";
                using var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@Id", id);

                var affected = await cmd.ExecuteNonQueryAsync();
                return affected > 0;
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "Kategori ID:{Id} silinirken SQL hatası oluştu.", id);
                throw;
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kategori ID:{Id} silinirken beklenmeyen hata oluştu.", id);
                throw;
            }
        }

        /// <summary>
        /// SqlDataReader'dan Category nesnesine dönüşüm yapar.
        /// </summary>
        private static Category MapCategory(SqlDataReader reader) => new()
        {
            Id = reader.GetInt32(reader.GetOrdinal("Id")),
            Name = reader.GetString(reader.GetOrdinal("Name")),
            Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? null : reader.GetString(reader.GetOrdinal("Description")),
            IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive"))
        };
    }
}