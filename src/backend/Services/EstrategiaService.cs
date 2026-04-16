using SimuladorBackend.Models;

namespace SimuladorBackend.Services;

/// <summary>
/// Servicio de estrategias con carga simulada de 10 s por estrategia.
/// Esto permite demostrar:
///   Secuencial → 3 × 10 s = ~30 s
///   Paralelo   → Task.WhenAll → ~10 s
/// </summary>
public sealed class EstrategiaService
{
    private readonly Random _rng = new();

    // ── Agresiva: espera precio alto (Alcista) ────────────────────────────────
    public async Task<ApuestaDemo> CalcularAgresiva(decimal precioActual, CancellationToken ct)
    {
        await Task.Delay(10_000, ct);

        decimal delta = (decimal)(_rng.NextDouble() * 2 + 1); // 1..3
        return new ApuestaDemo
        {
            Nombre          = "Agresiva",
            PrecioEsperado  = Math.Round(precioActual + delta, 2),
            Direccion       = DireccionApuesta.Alcista,
            TiempoExpiracion = TimeSpan.FromMinutes(1),
            MomentoCreacion = DateTime.UtcNow,
        };
    }

    // ── Conservadora: espera precio estable (Neutro) ──────────────────────────
    public async Task<ApuestaDemo> CalcularConservadora(decimal precioActual, CancellationToken ct)
    {
        await Task.Delay(10_000, ct);

        decimal delta = (decimal)(_rng.NextDouble() * 0.5 - 0.25); // -0.25..0.25
        return new ApuestaDemo
        {
            Nombre          = "Conservadora",
            PrecioEsperado  = Math.Round(precioActual + delta, 2),
            Direccion       = DireccionApuesta.Neutro,
            TiempoExpiracion = TimeSpan.FromMinutes(1),
            MomentoCreacion = DateTime.UtcNow,
        };
    }

    // ── Tendencia: espera precio bajo (Bajista) ───────────────────────────────
    public async Task<ApuestaDemo> CalcularTendencia(decimal precioActual, CancellationToken ct)
    {
        await Task.Delay(10_000, ct);

        decimal delta = (decimal)(_rng.NextDouble() * 2 + 1); // 1..3
        return new ApuestaDemo
        {
            Nombre          = "Tendencia",
            PrecioEsperado  = Math.Round(precioActual - delta, 2),
            Direccion       = DireccionApuesta.Bajista,
            TiempoExpiracion = TimeSpan.FromMinutes(1),
            MomentoCreacion = DateTime.UtcNow,
        };
    }
}

/// <summary>
/// Modelo de apuesta para la demo de descomposición especulativa.
/// Separado del Apuesta del simulador de fondo para no romper el loop existente.
/// </summary>
public sealed class ApuestaDemo
{
    public string           Nombre           { get; init; } = string.Empty;
    public decimal          PrecioEsperado   { get; init; }
    public DireccionApuesta Direccion        { get; init; }
    public TimeSpan         TiempoExpiracion { get; init; }
    public DateTime         MomentoCreacion  { get; init; }
    public DateTime         MomentoExpiracion => MomentoCreacion.Add(TiempoExpiracion);
}

/// <summary>Dirección de la apuesta especulativa.</summary>
public enum DireccionApuesta { Alcista, Bajista, Neutro }

/// <summary>Modo de ejecución del ciclo especulativo.</summary>
public enum ModoEjecucion { Secuencial, Paralelo }
