using FaqBotApi.Models;
using FaqBotApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace FaqBotApi.Controllers
{
    /// <summary>
    /// FAQ kategorilerinin yönetimini sağlayan controller.
    /// Kategori listeleme, ekleme, güncelleme, silme ve istatistik işlemlerini içerir.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class CategoryController : ControllerBase
    {
        private readonly CategoryService _categoryService;
        private readonly ILogger<CategoryController> _logger;

        public CategoryController(CategoryService categoryService, ILogger<CategoryController> logger)
        {
            _categoryService = categoryService;
            _logger = logger;
        }

        /// <summary>
        /// Tüm aktif kategorileri listeler.
        /// </summary>
        /// <returns>Kategori listesi</returns>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<List<Category>>), 200)]
        [ProducesResponseType(500)]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                var categories = await _categoryService.GetAllAsync();
                return Ok(ApiResponse<List<Category>>.Ok(categories, $"{categories.Count} kategori bulundu."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kategori listesi alınırken hata oluştu.");
                return StatusCode(500, ApiResponse<List<Category>>.Fail("Sunucu hatası oluştu."));
            }
        }

        /// <summary>
        /// ID'ye göre tek bir kategori getirir.
        /// </summary>
        /// <param name="id">Kategori ID'si</param>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(ApiResponse<Category>), 200)]
        [ProducesResponseType(404)]
        [ProducesResponseType(500)]
        public async Task<IActionResult> GetById(int id)
        {
            try
            {
                var category = await _categoryService.GetByIdAsync(id);

                if (category == null)
                    return NotFound(ApiResponse<Category>.Fail($"ID:{id} ile kategori bulunamadı."));

                return Ok(ApiResponse<Category>.Ok(category));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kategori ID:{Id} getirilirken hata oluştu.", id);
                return StatusCode(500, ApiResponse<Category>.Fail("Sunucu hatası oluştu."));
            }
        }

        /// <summary>
        /// Her kategorideki aktif FAQ sayısını döner.
        /// Yönetim paneli istatistik ekranı için kullanılır.
        /// </summary>
        [HttpGet("stats")]
        [ProducesResponseType(typeof(ApiResponse<List<CategoryStats>>), 200)]
        [ProducesResponseType(500)]
        public async Task<IActionResult> GetStats()
        {
            try
            {
                var stats = await _categoryService.GetStatsAsync();
                return Ok(ApiResponse<List<CategoryStats>>.Ok(stats));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kategori istatistikleri alınırken hata oluştu.");
                return StatusCode(500, ApiResponse<List<CategoryStats>>.Fail("Sunucu hatası oluştu."));
            }
        }

        /// <summary>
        /// Yeni bir kategori oluşturur.
        /// </summary>
        /// <param name="request">Kategori adı ve açıklaması</param>
        /// <returns>Oluşturulan kaydın ID'si</returns>
        [HttpPost]
        [ProducesResponseType(typeof(ApiResponse<int>), 201)]
        [ProducesResponseType(400)]
        [ProducesResponseType(409)]
        [ProducesResponseType(500)]
        public async Task<IActionResult> Create([FromBody] CategoryRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Name))
                    return BadRequest(ApiResponse<int>.Fail("Kategori adı zorunludur."));

                var newId = await _categoryService.CreateAsync(request);
                return CreatedAtAction(nameof(GetById), new { id = newId },
                    ApiResponse<int>.Ok(newId, "Kategori başarıyla oluşturuldu."));
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ApiResponse<int>.Fail(ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kategori oluşturulurken hata oluştu.");
                return StatusCode(500, ApiResponse<int>.Fail("Sunucu hatası oluştu."));
            }
        }

        /// <summary>
        /// Mevcut bir kategoriyi günceller.
        /// </summary>
        /// <param name="id">Güncellenecek kategori ID'si</param>
        /// <param name="request">Yeni kategori bilgileri</param>
        [HttpPut("{id}")]
        [ProducesResponseType(typeof(ApiResponse<bool>), 200)]
        [ProducesResponseType(404)]
        [ProducesResponseType(500)]
        public async Task<IActionResult> Update(int id, [FromBody] CategoryRequest request)
        {
            try
            {
                var updated = await _categoryService.UpdateAsync(id, request);

                if (!updated)
                    return NotFound(ApiResponse<bool>.Fail($"ID:{id} ile kategori bulunamadı."));

                return Ok(ApiResponse<bool>.Ok(true, "Kategori başarıyla güncellendi."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kategori ID:{Id} güncellenirken hata oluştu.", id);
                return StatusCode(500, ApiResponse<bool>.Fail("Sunucu hatası oluştu."));
            }
        }

        /// <summary>
        /// Kategoriyi pasife alır. İçinde aktif FAQ olan kategori silinemez.
        /// </summary>
        /// <param name="id">Silinecek kategori ID'si</param>
        [HttpDelete("{id}")]
        [ProducesResponseType(typeof(ApiResponse<bool>), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        [ProducesResponseType(500)]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var deleted = await _categoryService.DeleteAsync(id);

                if (!deleted)
                    return NotFound(ApiResponse<bool>.Fail($"ID:{id} ile kategori bulunamadı."));

                return Ok(ApiResponse<bool>.Ok(true, "Kategori başarıyla silindi."));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponse<bool>.Fail(ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kategori ID:{Id} silinirken hata oluştu.", id);
                return StatusCode(500, ApiResponse<bool>.Fail("Sunucu hatası oluştu."));
            }
        }
    }
}