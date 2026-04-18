using SimuladorBackend.Hubs;
using SimuladorBackend.Models;
using SimuladorBackend.Options;
using SimuladorBackend.Services;

var builder = WebApplication.CreateBuilder(args);

// ── Configuración ─────────────────────────────────────────────────────────
builder.Services.Configure<SimuladorOptions>(
    builder.Configuration.GetSection(SimuladorOptions.SectionName));

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

// ── Demo: Descomposición Especulativa ─────────────────────────────────────────
builder.Services.AddSingleton<EstrategiaService>();
builder.Services.AddSingleton<SimuladorService>();
builder.Services.AddSingleton<PortafolioService>();

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
