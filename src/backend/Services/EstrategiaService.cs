using SimuladorBackend.Models;

namespace SimuladorBackend.Services;

/// <summary>
/// Calcula las 3 estrategias especulativas mediante simulación Monte Carlo real.
///
/// Cada estrategia corre su fórmula N veces sobre muestras bootstrap del historial
/// de precios (con ruido gaussiano), devolviendo la media de todas las estimaciones.
/// Esto es trabajo genuinamente CPU-bound que demuestra la ventaja del paralelismo:
///
///   Secuencial → Agresiva + Conservadora + Tendencia  (tiempo total = suma)
///   Paralelo   → Task.WhenAll(Agresiva, Conservadora, Tendencia) (tiempo = máximo)
///
/// Ajustar <see cref="Iteraciones"/> según el hardware:
///   - Demasiado rápido (&lt;500 ms) → subir a 8_000_000
///   - Demasiado lento  (&gt;5 s)    → bajar a 1_000_000
/// </summary>
public sealed class EstrategiaService
{
    // ── Configuración ─────────────────────────────────────────────────────────

    /// <summary>Iteraciones Monte Carlo por estrategia. Objetivo: ~1-3 s por estrategia.</summary>
    private const int Iteraciones = 4_000_000;

    // ── Métodos públicos ──────────────────────────────────────────────────────

    public Task<ApuestaDemo> CalcularAgresiva(
        decimal precioActual,
        IReadOnlyList<decimal> historial,
        CancellationToken ct)
    {
        var arr = HistorialDouble(historial, precioActual);
        double p0 = (double)precioActual;

        return Task.Run(() =>
        {
            double estimado = MonteCarloAgresiva(arr, p0, Iteraciones, ct);
            decimal esperado = Math.Round((decimal)estimado, 2);
            return new ApuestaDemo
            {
                Nombre           = "Agresiva",
                PrecioEsperado   = esperado,
                Direccion        = Direccion(esperado, precioActual),
                TiempoExpiracion = TimeSpan.FromMinutes(1),
                MomentoCreacion  = DateTime.UtcNow,
            };
        }, ct);
    }

    public Task<ApuestaDemo> CalcularConservadora(
        decimal precioActual,
        IReadOnlyList<decimal> historial,
        CancellationToken ct)
    {
        var arr = HistorialDouble(historial, precioActual);
        double p0 = (double)precioActual;

        return Task.Run(() =>
        {
            double estimado = MonteCarloConservadora(arr, p0, Iteraciones, ct);
            decimal esperado = Math.Round((decimal)estimado, 2);
            return new ApuestaDemo
            {
                Nombre           = "Conservadora",
                PrecioEsperado   = esperado,
                Direccion        = Direccion(esperado, precioActual),
                TiempoExpiracion = TimeSpan.FromMinutes(1),
                MomentoCreacion  = DateTime.UtcNow,
            };
        }, ct);
    }

    public Task<ApuestaDemo> CalcularTendencia(
        decimal precioActual,
        IReadOnlyList<decimal> historial,
        CancellationToken ct)
    {
        var arr = HistorialDouble(historial, precioActual);
        double p0 = (double)precioActual;

        return Task.Run(() =>
        {
            double estimado = MonteCarloTendencia(arr, p0, Iteraciones, ct);
            decimal esperado = Math.Round((decimal)estimado, 2);
            return new ApuestaDemo
            {
                Nombre           = "Tendencia",
                PrecioEsperado   = esperado,
                Direccion        = Direccion(esperado, precioActual),
                TiempoExpiracion = TimeSpan.FromMinutes(1),
                MomentoCreacion  = DateTime.UtcNow,
            };
        }, ct);
    }

    // ── Monte Carlo — Estrategia Agresiva ─────────────────────────────────────
    // Desviación estándar de 5 muestras bootstrap × 2.5 → movimiento brusco

    private static double MonteCarloAgresiva(double[] arr, double precioActual, int n, CancellationToken ct)
    {
        var rng   = new Random(NuevaSemilla());
        int len   = arr.Length;
        int vent  = Math.Min(5, len);
        double sigma = precioActual * 0.0005; // ruido ≈ 0.05% del precio
        double suma = 0;

        for (int i = 0; i < n; i++)
        {
            if ((i & 0xFFFF) == 0 && ct.IsCancellationRequested) break;

            // Bootstrap: samplear 'vent' elementos con reemplazo
            double s = 0, s2 = 0;
            for (int j = 0; j < vent; j++)
            {
                double v = arr[rng.Next(len)];
                s  += v;
                s2 += v * v;
            }
            double media  = s / vent;
            double varianza = s2 / vent - media * media;
            double stdDev = varianza > 0 ? Math.Sqrt(varianza) : 0;

            double ultimo    = arr[^1] + Gaussiano(rng) * sigma;
            double direccion = ultimo >= media ? 1.0 : -1.0;
            suma += ultimo + stdDev * 2.5 * direccion;
        }

        return suma / n;
    }

    // ── Monte Carlo — Estrategia Conservadora ─────────────────────────────────
    // Promedio móvil 20 muestras bootstrap → regresión 30% hacia la media

    private static double MonteCarloConservadora(double[] arr, double precioActual, int n, CancellationToken ct)
    {
        var rng  = new Random(NuevaSemilla());
        int len  = arr.Length;
        int vent = Math.Min(20, len);
        double sigma = precioActual * 0.0005;
        double suma = 0;

        for (int i = 0; i < n; i++)
        {
            if ((i & 0xFFFF) == 0 && ct.IsCancellationRequested) break;

            double s = 0;
            for (int j = 0; j < vent; j++)
                s += arr[rng.Next(len)];
            double media  = s / vent;
            double ultimo = arr[^1] + Gaussiano(rng) * sigma;
            suma += ultimo + (media - ultimo) * 0.30;
        }

        return suma / n;
    }

    // ── Monte Carlo — Estrategia Tendencia ────────────────────────────────────
    // Regresión lineal sobre 10 muestras bootstrap → continuar tendencia

    private static double MonteCarloTendencia(double[] arr, double precioActual, int n, CancellationToken ct)
    {
        var rng  = new Random(NuevaSemilla());
        int len  = arr.Length;
        int vent = Math.Min(10, len);
        double sigma = precioActual * 0.0005;
        double suma = 0;

        for (int i = 0; i < n; i++)
        {
            if ((i & 0xFFFF) == 0 && ct.IsCancellationRequested) break;

            // Samplear 'vent' puntos en orden temporal (bootstrap ordenado)
            double sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0;
            for (int j = 0; j < vent; j++)
            {
                double v = arr[rng.Next(len)];
                sumX  += j;
                sumY  += v;
                sumXY += j * v;
                sumX2 += j * j;
            }

            double denom    = vent * sumX2 - sumX * sumX;
            double pendiente = Math.Abs(denom) > 1e-10
                ? (vent * sumXY - sumX * sumY) / denom
                : 0;

            double ultimo = arr[^1] + Gaussiano(rng) * sigma;
            suma += ultimo + pendiente;
        }

        return suma / n;
    }

    // ── Utilidades ────────────────────────────────────────────────────────────

    /// <summary>Semilla única por hilo para evitar correlación entre estrategias paralelas.</summary>
    private static int NuevaSemilla() =>
        Environment.TickCount ^ Thread.CurrentThread.ManagedThreadId ^ Guid.NewGuid().GetHashCode();

    /// <summary>Transformación Box-Muller: genera número gaussiano estándar.</summary>
    private static double Gaussiano(Random rng)
    {
        double u1 = 1.0 - rng.NextDouble();
        double u2 = 1.0 - rng.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
    }

    /// <summary>Convierte el historial a double[] y rellena si hay menos de 5 precios.</summary>
    private static double[] HistorialDouble(IReadOnlyList<decimal> historial, decimal precioActual)
    {
        const int min = 5;
        if (historial.Count >= min)
            return historial.Select(p => (double)p).ToArray();

        int faltantes = min - historial.Count;
        return Enumerable.Repeat((double)precioActual, faltantes)
                         .Concat(historial.Select(p => (double)p))
                         .ToArray();
    }

    private static DireccionApuesta Direccion(decimal esperado, decimal actual) =>
        esperado > actual ? DireccionApuesta.Alcista :
        esperado < actual ? DireccionApuesta.Bajista :
                            DireccionApuesta.Neutro;
}

// ── Modelos compartidos ───────────────────────────────────────────────────────

public sealed class ApuestaDemo
{
    public string           Nombre           { get; init; } = string.Empty;
    public decimal          PrecioEsperado   { get; init; }
    public DireccionApuesta Direccion        { get; init; }
    public TimeSpan         TiempoExpiracion { get; init; }
    public DateTime         MomentoCreacion  { get; init; }
    public DateTime         MomentoExpiracion => MomentoCreacion.Add(TiempoExpiracion);
}

public enum DireccionApuesta { Alcista, Bajista, Neutro }
public enum ModoEjecucion    { Secuencial, Paralelo }
