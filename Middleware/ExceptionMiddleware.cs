using System.Net;
using System.Text.Json;
using FaqBotApi.Models;

namespace FaqBotApi.Middleware
{
    /// <summary>
    /// Tüm API genelinde yakalanmamış istisnaları merkezi olarak ele alan middleware.
    /// Controller'lardan kaçan beklenmedik hatalar burada yakalanır,
    /// kullanıcıya standart ApiResponse formatında hata mesajı döner
    /// ve hata detayı sunucu loglarına yazılır.
    /// </summary>
    public class ExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionMiddleware> _logger;

        public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Yakalanmamış hata — Method: {Method} Path: {Path}",
                    context.Request.Method,
                    context.Request.Path);

                await HandleExceptionAsync(context, ex);
            }
        }

        /// <summary>
        /// Hata türüne göre uygun HTTP status kodu belirler
        /// ve standart ApiResponse formatında JSON yanıt döner.
        /// </summary>
        private static async Task HandleExceptionAsync(HttpContext context, Exception ex)
        {
            context.Response.ContentType = "application/json";

            // Hata türüne göre status kodu belirle
            context.Response.StatusCode = ex switch
            {
                InvalidOperationException => (int)HttpStatusCode.BadRequest,
                UnauthorizedAccessException => (int)HttpStatusCode.Unauthorized,
                KeyNotFoundException => (int)HttpStatusCode.NotFound,
                _ => (int)HttpStatusCode.InternalServerError
            };

            // Üretim ortamında iç hata detayları gizlenir
            var message = context.Response.StatusCode == (int)HttpStatusCode.InternalServerError
                ? "Beklenmeyen bir sunucu hatası oluştu. Lütfen daha sonra tekrar deneyin."
                : ex.Message;

            var response = ApiResponse<object>.Fail(message);

            var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            await context.Response.WriteAsync(json);
        }
    }
}