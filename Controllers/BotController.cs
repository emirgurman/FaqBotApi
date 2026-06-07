using FaqBotApi.Models;
using FaqBotApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace FaqBotApi.Controllers
{
    
    /// <summary>
    /// Kullanıcı - Bot etkileşimini yöneten controller.
    /// Claude API entegrasyonu ve sohbet geçmişi işlemlerini sağlar.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class BotController : ControllerBase
    {
        private readonly ClaudeService _claudeService;
        private readonly ILogger<BotController> _logger;

        public BotController(ClaudeService claudeService, ILogger<BotController> logger)
        {
            _claudeService = claudeService;
            _logger = logger;
        }

        /// <summary>
        /// Kullanıcının sorusunu alır ve yanıt üretir.
        /// Önce FAQ veritabanında arama yapar; bulamazsa Claude API kullanır.
        /// </summary>
        /// <param name="request">Kullanıcı mesajı, oturum ID ve dil bilgisi</param>
        /// <returns>Bot yanıtı, tespit edilen dil ve ilgili FAQ'lar</returns>
        [HttpPost("ask")]
        [ProducesResponseType(typeof(ApiResponse<ChatResponse>), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(500)]
        public async Task<IActionResult> Ask([FromBody] ChatRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Message))
                    return BadRequest(ApiResponse<ChatResponse>.Fail("Mesaj boş olamaz."));

                if (string.IsNullOrWhiteSpace(request.SessionId))
                    request.SessionId = Guid.NewGuid().ToString();

                var response = await _claudeService.AskAsync(request);
                return Ok(ApiResponse<ChatResponse>.Ok(response));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Bot sorusu işlenirken hata oluştu.");
                return StatusCode(500, ApiResponse<ChatResponse>.Fail("Sunucu hatası oluştu."));
            }
        }

        /// <summary>
        /// Belirli bir oturuma ait tüm sohbet geçmişini döndürür.
        /// Streamlit arayüzünde konuşma akışını göstermek için kullanılır.
        /// </summary>
        /// <param name="sessionId">Oturum kimliği</param>
        /// <returns>Kronolojik sohbet geçmişi</returns>
        [HttpGet("history/{sessionId}")]
        [ProducesResponseType(typeof(ApiResponse<List<ChatHistory>>), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(500)]
        public async Task<IActionResult> GetHistory(string sessionId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(sessionId))
                    return BadRequest(ApiResponse<List<ChatHistory>>.Fail("SessionId boş olamaz."));

                var history = await _claudeService.GetHistoryAsync(sessionId);
                return Ok(ApiResponse<List<ChatHistory>>.Ok(history, $"{history.Count} mesaj bulundu."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Sohbet geçmişi alınırken hata oluştu. SessionId: {SessionId}", sessionId);
                return StatusCode(500, ApiResponse<List<ChatHistory>>.Fail("Sunucu hatası oluştu."));
            }
        }
    }
}