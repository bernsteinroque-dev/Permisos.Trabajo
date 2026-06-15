using Microsoft.EntityFrameworkCore;
using PTS_Synthon.Data;
using PTS_Synthon.Services;
using QuestPDF.Infrastructure;

QuestPDF.Settings.License = LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthorization();

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddControllers()
    .AddJsonOptions(options => {
        options.JsonSerializerOptions.PropertyNamingPolicy = null;
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    });

builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddHostedService<VencimientoBackgroundService>();

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<AppDbContext>>();
    try
    {
        db.Database.EnsureCreated();

        // Create indexes idempotently (safe to run on existing DB)
        var indexSql = """
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Permisos_FechaCreacion' AND object_id = OBJECT_ID('Permisos'))
                CREATE INDEX IX_Permisos_FechaCreacion ON Permisos (FechaCreacion DESC);
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Permisos_Estado_Fecha' AND object_id = OBJECT_ID('Permisos'))
                CREATE INDEX IX_Permisos_Estado_Fecha ON Permisos (Estado, FechaCreacion DESC);
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Permisos_Tipo' AND object_id = OBJECT_ID('Permisos'))
                CREATE INDEX IX_Permisos_Tipo ON Permisos (Tipo);
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Proveedores_RazonSocial' AND object_id = OBJECT_ID('Proveedores'))
                CREATE INDEX IX_Proveedores_RazonSocial ON Proveedores (RazonSocial);
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Proveedores_Estado' AND object_id = OBJECT_ID('Proveedores'))
                CREATE INDEX IX_Proveedores_Estado ON Proveedores (Estado);
            """;
        db.Database.ExecuteSqlRaw(indexSql);

        logger.LogInformation("Database schema and indexes verified successfully.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "CRITICAL: Database initialization failed.");
    }
}

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseSession();
app.UseAuthorization();

app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();
