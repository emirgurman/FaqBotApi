using Anthropic.SDK;
using Anthropic.SDK.Constants;
using Anthropic.SDK.Messaging;
using FaqBotApi.Data;
using FaqBotApi.Models;
using Microsoft.Data.SqlClient;
using System.Net.Sockets;

namespace FaqBotApi.Services
{
    /// <summary>
    /// Claude API ile iletişimi ve sohbet geçmişi yönetimini sağlayan servis.
    /// Kullanıcı sorusuna önce veritabanından eşleşme arar;
    /// bulamazsa Claude API'ye yönlendirir.
    /// </summary>
    public class ClaudeService
    {
        private readonly AnthropicClient _claude;
        private readonly FaqService _faqService;
        private readonly DatabaseHelper _db;
        private readonly ILogger<ClaudeService> _logger;

        public ClaudeService(
            IConfiguration config,
            FaqService faqService,
            DatabaseHelper db,
            ILogger<ClaudeService> logger)
        {
            var apiKey = config["Anthropic:ApiKey"]
                ?? throw new ArgumentNullException("Anthropic API Key bulunamadı.");

            _claude = new AnthropicClient(apiKey);
            _faqService = faqService;
            _db = db;
            _logger = logger;
        }

        /// <summary>
        /// Kullanıcının sorusunu işler.
        /// Önce veritabanında eşleşen FAQ arar.
        /// Bulamazsa Claude API'ye gönderir ve cevabı döner.
        /// Tüm konuşma geçmişi ChatHistory tablosuna kaydedilir.
        /// </summary>
        public async Task<ChatResponse> AskAsync(ChatRequest request)
        {
            try
            {
                // 1. Dil tespiti — istemci göndermemişse varsayılan "tr"
                var detectedLang = request.LanguageCode ?? "tr";

                // 2. Önce veritabanında ara
                var matchedFaqs = await _faqService.SearchAsync(request.Message, detectedLang);

                string botResponse;
                bool usedAI = false;

                if (matchedFaqs.Count > 0)
                {
                    // Veritabanında eşleşme bulundu — doğrudan döndür
                    botResponse = matchedFaqs.First().Answer;
                }
                else
                {
                    // Eşleşme yok — Claude API'ye sor
                    botResponse = await AskClaudeAsync(request.Message, detectedLang);
                    usedAI = true;
                }

                // 3. Konuşmayı kaydet
                await SaveChatHistoryAsync(request.SessionId, request.Message, botResponse, detectedLang);

                return new ChatResponse
                {
                    Response = botResponse,
                    DetectedLanguage = detectedLang,
                    RelatedFaqs = matchedFaqs.Take(3).ToList(),
                    UsedAI = usedAI
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Soru işlenirken hata oluştu. Mesaj: {Message}", request.Message);
                throw;
            }
        }

        /// <summary>
        /// Claude API'ye bağlanarak kullanıcının sorusunu iletir ve yanıt alır.
        /// Sistem promptu ile botun çok dilli FAQ asistanı olduğu tanımlanır.
        /// </summary>
        private async Task<string> AskClaudeAsync(string userMessage, string languageCode)
        {
            try
            {
                var systemPrompt = $@"Sen çok dilli bir FAQ (Sık Sorulan Sorular) asistanısın.
Kullanıcının dilini otomatik olarak algılayarak aynı dilde yanıt ver.
Tespit edilen dil kodu: {languageCode}
Kısa, net ve yardımcı yanıtlar ver.
E�er soruyu bilmiyorsan dürüstçe belirt ve ilgili departmanla iletişime geçmelerini öner.";

                // Hata 1 düzeltme: Content, List<ContentBase> bekliyor — TextContent kullan
                var messages = new List<Message>
                {
                    new()
                    {
                        Role = RoleType.User,
                        Content = new List<ContentBase>
                        {
                            new TextContent { Text = userMessage }
                        }
                    }
                };

                // Hata 2 düzeltme: System string olarak geçiliyor, SystemMessage listesi değil
                var response = await _claude.Messages.GetClaudeMessageAsync(
                    new MessageParameters
                    {
                        //AnthropicModels.Claude46Sonnetclaude-sonnet-4-6
                        //AnthropicModels.Claude46Opusclaude-opus-4-6
                        //AnthropicModels.Claude45Haikuclaude-haiku-4-5
                        //AnthropicModels.Claude37Sonnetclaude-sonnet-3-7
                        //AnthropicModels.Claude35Sonneteski versiyonda vardı, 5.x'te kaldırıldı

                        Model = AnthropicModels.Claude46Sonnet, 
                        MaxTokens = 1024,
                        System = new List<SystemMessage> { new(systemPrompt) },
                        Messages = messages
                    });

                // Content[0] TextContent tipinde, ToString() yerine Text property kullan
                return response.Content.OfType<TextContent>().FirstOrDefault()?.Text
                    ?? "Yanıt alınamadı.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Claude API isteği sırasında hata oluştu.");
                throw;
            }
        }

        /// <summary>
        /// Kullanıcı sorusunu ve bot yanıtını ChatHistory tablosuna kaydeder.
        /// Konuşma geçmişi analiz ve raporlama için saklanır.
        /// </summary>
        private async Task SaveChatHistoryAsync(
            string sessionId,
            string userMessage,
            string botResponse,
            string detectedLanguage)
        {
            try
            {
                using var conn = _db.GetConnection();
                await conn.OpenAsync();

                var query = @"
                    INSERT INTO ChatHistory (SessionId, UserMessage, BotResponse, DetectedLanguage, CreatedAt)
                    VALUES (@SessionId, @UserMessage, @BotResponse, @DetectedLanguage, GETDATE())";

                using var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@SessionId", sessionId);
                cmd.Parameters.AddWithValue("@UserMessage", userMessage);
                cmd.Parameters.AddWithValue("@BotResponse", botResponse);
                cmd.Parameters.AddWithValue("@DetectedLanguage", detectedLanguage);

                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                // Geçmiş kaydı kritik değil — sadece logla, fırlatma
                _logger.LogWarning(ex, "Sohbet geçmişi kaydedilemedi. SessionId: {SessionId}", sessionId);
            }
        }

        /// <summary>
        /// Belirli bir oturuma ait sohbet geçmişini döndürür.
        /// Streamlit tarafında konuşma akışını göstermek için kullanılır.
        /// </summary>
        public async Task<List<ChatHistory>> GetHistoryAsync(string sessionId)
        {
            var history = new List<ChatHistory>();

            try
            {
                using var conn = _db.GetConnection();
                await conn.OpenAsync();

                var query = @"
                    SELECT Id, SessionId, UserMessage, BotResponse, DetectedLanguage, CreatedAt
                    FROM ChatHistory
                    WHERE SessionId = @SessionId
                    ORDER BY CreatedAt ASC";

                using var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@SessionId", sessionId);

                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    history.Add(new ChatHistory
                    {
                        Id = reader.GetInt32(0),
                        SessionId = reader.GetString(1),
                        UserMessage = reader.GetString(2),
                        BotResponse = reader.GetString(3),
                        DetectedLanguage = reader.GetString(4),
                        CreatedAt = reader.GetDateTime(5)
                    });
                }
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "Sohbet geçmişi alınırken SQL hatası oluştu. SessionId: {SessionId}", sessionId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Sohbet geçmişi alınırken beklenmeyen hata oluştu.");
                throw;
            }

            return history;
        }
    }
}