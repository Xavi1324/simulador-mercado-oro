using SimuladorBackend.Hubs;
using SimuladorBackend.Models;
using SimuladorBackend.Services;

var builder = WebApplication.CreateBuilder(args);

// ── Servicios ─────────────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddHttpClient("Swissquote");
builder.Services.AddHttpClient("MetalsApi");

builder.Services.AddSingleton<Portafolio>();
builder.Services.AddSingleton<FuenteDeDatos>();
builder.Services.AddSingleton<MetricasEngine>();
builder.Services.AddSingleton<MercadoCentral>();
builder.Services.AddHostedService(p => p.GetRequiredService<MercadoCentral>());

// CORS — obligatorio para SignalR con Next.js (AllowCredentials requiere origen explícito)
builder.Services.AddCors(options =>
    options.AddPolicy("FrontendPolicy", policy =>
        policy.WithOrigins("http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials()));

// ── Build ─────────────────────────────────────────────────────────────────
var app = builder.Build();

app.UseCors("FrontendPolicy");

app.MapControllers();
app.MapHub<SimuladorHub>("/hubs/simulador");

app.Run();
