using FaqBotApi.Data;
using FaqBotApi.Middleware;
using FaqBotApi.Services;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

// ─────────────────────────────────────────────
// SERVİSLER
// ─────────────────────────────────────────────

builder.Services.AddControllers();

// Swagger / OpenAPI — XML yorumlarıyla tam dokümantasyon
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "FAQ Bot API",
        Version = "v1",
        Description = "Çok dilli FAQ Bot — ASP.NET Core 8 + Claude AI + MS SQL Server"
    });

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        options.IncludeXmlComments(xmlPath);
});

// CORS — Streamlit (localhost:8501) erişimine izin ver
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowStreamlit", policy =>
    {
        policy
            .WithOrigins("http://localhost:8501")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// Uygulama servisleri — DI container'a kaydet
builder.Services.AddSingleton<DatabaseHelper>();
builder.Services.AddScoped<FaqService>();
builder.Services.AddScoped<ClaudeService>();
builder.Services.AddScoped<LanguageService>();
builder.Services.AddScoped<CategoryService>();

builder.Services.AddLogging();

var app = builder.Build();

// ─────────────────────────────────────────────
// MİDDLEWARE PIPELINE
// ─────────────────────────────────────────────

// Global hata yakalayıcı — pipeline'ın en başına eklenir
app.UseMiddleware<ExceptionMiddleware>();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "FAQ Bot API v1");
    options.RoutePrefix = "swagger";
});

app.UseHttpsRedirection();
app.UseCors("AllowStreamlit");
app.UseAuthorization();
app.MapControllers();

app.Run();