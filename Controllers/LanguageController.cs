using FaqBotApi.Models;
using FaqBotApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace FaqBotApi.Controllers
{
    /// <summary>
    /// Desteklenen dillerin yönetimini sağlayan controller.
    /// Streamlit arayüzü dil listesini bu endpoint üzerinden çeker.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class LanguageController : ControllerBase
    {
        private readonly LanguageService _languageService;
        private readonly ILogger<LanguageController> _logger;

        public LanguageController(LanguageService languageService, ILogger<LanguageController> logger)
        {
            _languageService = languageService;
            _logger = logger;
        }

        /// <summary>
        /// Tüm aktif dilleri listeler.
        /// </summary>
        /// <returns>Dil listesi</returns>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<List<Language>>), 200)]
        [ProducesResponseType(500)]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                var languages = await _languageService.GetAllAsync();
                return Ok(ApiResponse<List<Language>>.Ok(languages, $"{languages.Count} dil bulundu."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dil listesi alınırken hata oluştu.");
                return StatusCode(500, ApiResponse<List<Language>>.Fail("Sunucu hatası oluştu."));
            }
        }

        /// <summary>
        /// ID'ye göre tek bir dil kaydı getirir.
        /// </summary>
        /// <param name="id">Dil kayıt ID'si</param>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(ApiResponse<Language>), 200)]
        [ProducesResponseType(404)]
        [ProducesResponseType(500)]
        public async Task<IActionResult> GetById(int id)
        {
            try
            {
                var language = await _languageService.GetByIdAsync(id);

                if (language == null)
                    return NotFound(ApiResponse<Language>.Fail($"ID:{id} ile dil bulunamadı."));

                return Ok(ApiResponse<Language>.Ok(language));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dil ID:{Id} getirilirken hata oluştu.", id);
                return StatusCode(500, ApiResponse<Language>.Fail("Sunucu hatası oluştu."));
            }
        }

        /// <summary>
        /// Yeni bir dil ekler.
        /// </summary>
        /// <param name="request">Dil kodu (tr, en, de) ve dil adı</param>
        /// <returns>Oluşturulan kaydın ID'si</returns>
        [HttpPost]
        [ProducesResponseType(typeof(ApiResponse<int>), 201)]
        [ProducesResponseType(400)]
        [ProducesResponseType(409)]
        [ProducesResponseType(500)]
        public async Task<IActionResult> Create([FromBody] LanguageRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name))
                    return BadRequest(ApiResponse<int>.Fail("Dil kodu ve adı zorunludur."));

                var newId = await _languageService.CreateAsync(request);
                return CreatedAtAction(nameof(GetById), new { id = newId },
                    ApiResponse<int>.Ok(newId, "Dil başarıyla eklendi."));
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ApiResponse<int>.Fail(ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dil oluşturulurken hata oluştu.");
                return StatusCode(500, ApiResponse<int>.Fail("Sunucu hatası oluştu."));
            }
        }

        /// <summary>
        /// Mevcut bir dil kaydını günceller.
        /// </summary>
        /// <param name="id">Güncellenecek dil ID'si</param>
        /// <param name="request">Yeni dil bilgileri</param>
        [HttpPut("{id}")]
        [ProducesResponseType(typeof(ApiResponse<bool>), 200)]
        [ProducesResponseType(404)]
        [ProducesResponseType(500)]
        public async Task<IActionResult> Update(int id, [FromBody] LanguageRequest request)
        {
            try
            {
                var updated = await _languageService.UpdateAsync(id, request);

                if (!updated)
                    return NotFound(ApiResponse<bool>.Fail($"ID:{id} ile dil bulunamadı."));

                return Ok(ApiResponse<bool>.Ok(true, "Dil başarıyla güncellendi."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dil ID:{Id} güncellenirken hata oluştu.", id);
                return StatusCode(500, ApiResponse<bool>.Fail("Sunucu hatası oluştu."));
            }
        }

        /// <summary>
        /// Dil kaydını pasife alır (soft delete).
        /// </summary>
        /// <param name="id">Silinecek dil ID'si</param>
        [HttpDelete("{id}")]
        [ProducesResponseType(typeof(ApiResponse<bool>), 200)]
        [ProducesResponseType(404)]
        [ProducesResponseType(500)]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var deleted = await _languageService.DeleteAsync(id);

                if (!deleted)
                    return NotFound(ApiResponse<bool>.Fail($"ID:{id} ile dil bulunamadı."));

                return Ok(ApiResponse<bool>.Ok(true, "Dil başarıyla silindi."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dil ID:{Id} silinirken hata oluştu.", id);
                return StatusCode(500, ApiResponse<bool>.Fail("Sunucu hatası oluştu."));
            }
        }
    }
}