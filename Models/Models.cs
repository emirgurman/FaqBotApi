namespace FaqBotApi.Models
{
    /// <summary>
    /// Veritabanındaki Languages tablosunu temsil eder.
    /// </summary>
    public class Language
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;   // tr, en, de
        public string Name { get; set; } = string.Empty;   // Türkçe, English
        public bool IsActive { get; set; }
    }

    /// <summary>
    /// Veritabanındaki Categories tablosunu temsil eder.
    /// </summary>
    public class Category
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsActive { get; set; }
    }

    /// <summary>
    /// Veritabanındaki Faqs tablosunu temsil eder.
    /// </summary>
    public class Faq
    {
        public int Id { get; set; }
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public int LanguageId { get; set; }
        public string LanguageCode { get; set; } = string.Empty;
        public string Question { get; set; } = string.Empty;
        public string Answer { get; set; } = string.Empty;
        public string? Keywords { get; set; }
        public int ViewCount { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    /// <summary>
    /// Yeni FAQ eklerken veya güncellerken kullanılan DTO.
    /// </summary>
    public class FaqRequest
    {
        public int CategoryId { get; set; }
        public int LanguageId { get; set; }
        public string Question { get; set; } = string.Empty;
        public string Answer { get; set; } = string.Empty;
        public string? Keywords { get; set; }
    }

    /// <summary>
    /// Kullanıcının bota gönderdiği mesajı temsil eder.
    /// </summary>
    public class ChatRequest
    {
        public string Message { get; set; } = string.Empty;
        public string SessionId { get; set; } = string.Empty;
        public string? LanguageCode { get; set; }   // boş gelirse otomatik tespit edilir
    }

    /// <summary>
    /// Botun kullanıcıya döndürdüğü yanıtı temsil eder.
    /// </summary>
    public class ChatResponse
    {
        public string Response { get; set; } = string.Empty;
        public string DetectedLanguage { get; set; } = string.Empty;
        public List<Faq> RelatedFaqs { get; set; } = new();
        public bool UsedAI { get; set; }
    }

    /// <summary>
    /// Veritabanındaki ChatHistory tablosunu temsil eder.
    /// </summary>
    public class ChatHistory
    {
        public int Id { get; set; }
        public string SessionId { get; set; } = string.Empty;
        public string UserMessage { get; set; } = string.Empty;
        public string BotResponse { get; set; } = string.Empty;
        public string DetectedLanguage { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// API'nin standart yanıt sarmalayıcısı.
    /// Tüm endpoint'ler bu format üzerinden yanıt döner.
    /// </summary>
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public T? Data { get; set; }

        public static ApiResponse<T> Ok(T data, string message = "İşlem başarılı.") =>
            new() { Success = true, Message = message, Data = data };

        public static ApiResponse<T> Fail(string message) =>
            new() { Success = false, Message = message, Data = default };
    }
}

namespace FaqBotApi.Models
{
    /// <summary>
    /// Yeni dil eklerken veya güncellerken kullanılan DTO.
    /// </summary>
    public class LanguageRequest
    {
        public string Code { get; set; } = string.Empty;   // tr, en, de
        public string Name { get; set; } = string.Empty;   // Türkçe, English
    }

    /// <summary>
    /// Yeni kategori eklerken veya güncellerken kullanılan DTO.
    /// </summary>
    public class CategoryRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    /// <summary>
    /// Kategori bazında FAQ sayısını dönen istatistik modeli.
    /// Yönetim paneli istatistik ekranında kullanılır.
    /// </summary>
    public class CategoryStats
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int FaqCount { get; set; }
    }
}