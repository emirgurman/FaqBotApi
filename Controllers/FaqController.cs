using FaqBotApi.Models;
using FaqBotApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace FaqBotApi.Controllers
{
    /// <summary>
    /// FAQ kayıtlarının CRUD işlemlerini yöneten controller.
    /// Listeleme, arama, ekleme, güncelleme ve silme işlemlerini sağlar.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class FaqController : ControllerBase
    {
        private readonly FaqService _faqService;
        private readonly ILogger<FaqController> _logger;

        public FaqController(FaqService faqService, ILogger<FaqController> logger)
        {
            _faqService = faqService;
            _logger = logger;
        }

        /// <summary>
        /// Tüm aktif FAQ kayıtlarını listeler.
        /// </summary>
        /// <param name="languageCode">Dil filtresi (tr, en, de). Boş bırakılırsa tüm diller.</param>
        /// <param name="categoryId">Kategori filtresi. Boş bırakılırsa tüm kategoriler.</param>
        /// <returns>FAQ listesi</returns>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<List<Faq>>), 200)]
        [ProducesResponseType(500)]
        public async Task<IActionResult> GetAll(
            [FromQuery] string? languageCode = null,
            [FromQuery] int? categoryId = null)
        {
            try
            {
                var faqs = await _faqService.GetAllAsync(languageCode, categoryId);
                return Ok(ApiResponse<List<Faq>>.Ok(faqs, $"{faqs.Count} kayıt bulundu."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FAQ listesi alınırken hata oluştu.");
                return StatusCode(500, ApiResponse<List<Faq>>.Fail("Sunucu hatası oluştu."));
            }
        }

        /// <summary>
        /// ID'ye göre tek bir FAQ kaydı getirir.
        /// </summary>
        /// <param name="id">FAQ kayıt ID'si</param>
        /// <returns>FAQ kaydı</returns>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(ApiResponse<Faq>), 200)]
        [ProducesResponseType(404)]
        [ProducesResponseType(500)]
        public async Task<IActionResult> GetById(int id)
        {
            try
            {
                var faq = await _faqService.GetByIdAsync(id);

                if (faq == null)
                    return NotFound(ApiResponse<Faq>.Fail($"ID:{id} ile FAQ bulunamadı."));

                return Ok(ApiResponse<Faq>.Ok(faq));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FAQ ID:{Id} getirilirken hata oluştu.", id);
                return StatusCode(500, ApiResponse<Faq>.Fail("Sunucu hatası oluştu."));
            }
        }

        /// <summary>
        /// Soru metni, cevap ve anahtar kelimelerde arama yapar.
        /// </summary>
        /// <param name="q">Arama metni (zorunlu)</param>
        /// <param name="languageCode">Dil filtresi (tr, en, de). Boş bırakılırsa tüm diller.</param>
        /// <returns>Eşleşen FAQ listesi</returns>
        [HttpGet("search")]
        [ProducesResponseType(typeof(ApiResponse<List<Faq>>), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(500)]
        public async Task<IActionResult> Search(
            [FromQuery] string q,
            [FromQuery] string? languageCode = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(q))
                    return BadRequest(ApiResponse<List<Faq>>.Fail("Arama metni boş olamaz."));

                var results = await _faqService.SearchAsync(q, languageCode);
                return Ok(ApiResponse<List<Faq>>.Ok(results, $"{results.Count} sonuç bulundu."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Arama sırasında hata oluştu. Arama: {Query}", q);
                return StatusCode(500, ApiResponse<List<Faq>>.Fail("Sunucu hatası oluştu."));
            }
        }

        /// <summary>
        /// Yeni bir FAQ kaydı oluşturur.
        /// </summary>
        /// <param name="request">FAQ bilgileri</param>
        /// <returns>Oluşturulan kaydın ID'si</returns>
        [HttpPost]
        [ProducesResponseType(typeof(ApiResponse<int>), 201)]
        [ProducesResponseType(400)]
        [ProducesResponseType(500)]
        public async Task<IActionResult> Create([FromBody] FaqRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Question) || string.IsNullOrWhiteSpace(request.Answer))
                    return BadRequest(ApiResponse<int>.Fail("Soru ve cevap alanları zorunludur."));

                var newId = await _faqService.CreateAsync(request);
                return CreatedAtAction(nameof(GetById), new { id = newId },
                    ApiResponse<int>.Ok(newId, "FAQ başarıyla oluşturuldu."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FAQ oluşturulurken hata oluştu.");
                return StatusCode(500, ApiResponse<int>.Fail("Sunucu hatası oluştu."));
            }
        }

        /// <summary>
        /// Mevcut bir FAQ kaydını günceller.
        /// </summary>
        /// <param name="id">Güncellenecek FAQ ID'si</param>
        /// <param name="request">Yeni FAQ bilgileri</param>
        [HttpPut("{id}")]
        [ProducesResponseType(typeof(ApiResponse<bool>), 200)]
        [ProducesResponseType(404)]
        [ProducesResponseType(500)]
        public async Task<IActionResult> Update(int id, [FromBody] FaqRequest request)
        {
            try
            {
                var updated = await _faqService.UpdateAsync(id, request);

                if (!updated)
                    return NotFound(ApiResponse<bool>.Fail($"ID:{id} ile FAQ bulunamadı."));

                return Ok(ApiResponse<bool>.Ok(true, "FAQ başarıyla güncellendi."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FAQ ID:{Id} güncellenirken hata oluştu.", id);
                return StatusCode(500, ApiResponse<bool>.Fail("Sunucu hatası oluştu."));
            }
        }

        /// <summary>
        /// FAQ kaydını pasife alır (soft delete). Fiziksel olarak silmez.
        /// </summary>
        /// <param name="id">Silinecek FAQ ID'si</param>
        [HttpDelete("{id}")]
        [ProducesResponseType(typeof(ApiResponse<bool>), 200)]
        [ProducesResponseType(404)]
        [ProducesResponseType(500)]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var deleted = await _faqService.DeleteAsync(id);

                if (!deleted)
                    return NotFound(ApiResponse<bool>.Fail($"ID:{id} ile FAQ bulunamadı."));

                return Ok(ApiResponse<bool>.Ok(true, "FAQ başarıyla silindi."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FAQ ID:{Id} silinirken hata oluştu.", id);
                return StatusCode(500, ApiResponse<bool>.Fail("Sunucu hatası oluştu."));
            }
        }
    }
}