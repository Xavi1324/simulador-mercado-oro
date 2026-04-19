using SimuladorBackend.Models;

namespace SimuladorBackend.Services;

/// <summary>
/// Calcula las 3 estrategias especulativas mediante simulación Monte Carlo real.
///
/// Cada estrategia corre su fórmula N veces sobre muestras bootstrap del historial
/// de precios (con ruido gaussiano), devolviendo la media de todas las estimaciones.
/// Esto es trabajo genuinamente CPU-bound que demuestra la ventaja del paralelismo:
///
///   Secuencial → Agresiva + Conservadora + Tendencia con 1 partición por estrategia
///   Paralelo   → Task.WhenAll(Agresiva, Conservadora, Tendencia), y cada estrategia
///                divide sus iteraciones entre los núcleos seleccionados.
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

    public async Task<ApuestaEspeculativa> CalcularAgresiva(
        decimal precioActual,
        IReadOnlyList<decimal> historial,
        int nucleos,
        CancellationToken ct)
    {
        var arr = HistorialDouble(historial, precioActual);
        double p0 = (double)precioActual;

        double estimado = await MonteCarloParticionado(arr, p0, Iteraciones, nucleos, MonteCarloAgresivaSuma, ct);
        // Agresiva → siempre Alcista: precio esperado > precio actual
        double delta = Math.Abs(estimado - p0);
        if (delta < 0.50) delta = 0.50; // mínimo $0.50 de movimiento al alza
        decimal esperado = Math.Round((decimal)(p0 + delta), 2);
        return new ApuestaEspeculativa
        {
            Nombre           = "Agresiva",
            PrecioEsperado   = esperado,
            Direccion        = DireccionApuesta.Alcista,
            TiempoExpiracion = TimeSpan.FromMinutes(1),
            MomentoCreacion  = DateTime.UtcNow,
        };
    }

    public async Task<ApuestaEspeculativa> CalcularConservadora(
        decimal precioActual,
        IReadOnlyList<decimal> historial,
        int nucleos,
        CancellationToken ct)
    {
        var arr = HistorialDouble(historial, precioActual);
        double p0 = (double)precioActual;

        double estimado  = await MonteCarloParticionado(arr, p0, Iteraciones, nucleos, MonteCarloConservadoraSuma, ct);
        // Conservadora → rango ±0.3% alrededor del precio actual
        double maxDelta  = p0 * 0.003;
        double delta     = Math.Max(-maxDelta, Math.Min(maxDelta, estimado - p0));
        decimal centro   = Math.Round((decimal)(p0 + delta), 2);
        decimal minRango = Math.Round((decimal)(p0 - maxDelta), 2);
        decimal maxRango = Math.Round((decimal)(p0 + maxDelta), 2);
        return new ApuestaEspeculativa
        {
            Nombre           = "Conservadora",
            PrecioEsperado   = centro,
            PrecioMin        = minRango,
            PrecioMax        = maxRango,
            Direccion        = DireccionApuesta.Neutro,
            TiempoExpiracion = TimeSpan.FromMinutes(1),
            MomentoCreacion  = DateTime.UtcNow,
        };
    }

    public async Task<ApuestaEspeculativa> CalcularTendencia(
        decimal precioActual,
        IReadOnlyList<decimal> historial,
        int nucleos,
        CancellationToken ct)
    {
        var arr = HistorialDouble(historial, precioActual);
        double p0 = (double)precioActual;

        double estimado = await MonteCarloParticionado(arr, p0, Iteraciones, nucleos, MonteCarloTendenciaSuma, ct);
        // Tendencia → siempre Bajista: precio esperado < precio actual
        double delta = Math.Abs(estimado - p0);
        if (delta < 0.50) delta = 0.50; // mínimo $0.50 de movimiento a la baja
        decimal esperado = Math.Round((decimal)(p0 - delta), 2);
        return new ApuestaEspeculativa
        {
            Nombre           = "Tendencia",
            PrecioEsperado   = esperado,
            Direccion        = DireccionApuesta.Bajista,
            TiempoExpiracion = TimeSpan.FromMinutes(1),
            MomentoCreacion  = DateTime.UtcNow,
        };
    }

    // ── Monte Carlo — Estrategia Agresiva ─────────────────────────────────────
    // Desviación estándar de 5 muestras bootstrap × 2.5 → movimiento brusco

    private static async Task<double> MonteCarloParticionado(
        double[] arr,
        double precioActual,
        int iteraciones,
        int nucleos,
        Func<double[], double, int, CancellationToken, double> calcularSuma,
        CancellationToken ct)
    {
        int particiones = Math.Clamp(nucleos, 1, Math.Max(1, Environment.ProcessorCount));
        if (particiones > iteraciones) particiones = iteraciones;

        int basePorParticion = iteraciones / particiones;
        int residuo = iteraciones % particiones;

        var tareas = Enumerable.Range(0, particiones)
            .Select(i =>
            {
                int iteracionesParticion = basePorParticion + (i < residuo ? 1 : 0);
                return Task.Run(() => calcularSuma(arr, precioActual, iteracionesParticion, ct), ct);
            })
            .ToArray();

        double[] sumas = await Task.WhenAll(tareas);
        return sumas.Sum() / iteraciones;
    }

    private static double MonteCarloAgresivaSuma(double[] arr, double precioActual, int n, CancellationToken ct)
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

        return suma;
    }

    // ── Monte Carlo — Estrategia Conservadora ─────────────────────────────────
    // Promedio móvil 20 muestras bootstrap → regresión 30% hacia la media

    private static double MonteCarloConservadoraSuma(double[] arr, double precioActual, int n, CancellationToken ct)
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

        return suma;
    }

    // ── Monte Carlo — Estrategia Tendencia ────────────────────────────────────
    // Regresión lineal sobre 10 muestras bootstrap → continuar tendencia

    private static double MonteCarloTendenciaSuma(double[] arr, double precioActual, int n, CancellationToken ct)
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

        return suma;
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

public sealed class ApuestaEspeculativa
{
    public string           Nombre           { get; init; } = string.Empty;
    public decimal          PrecioEsperado   { get; init; }  // punto central (Agresiva / Tendencia)
    public decimal?         PrecioMin        { get; init; }  // extremo bajo del rango (Conservadora)
    public decimal?         PrecioMax        { get; init; }  // extremo alto del rango (Conservadora)
    public DireccionApuesta Direccion        { get; init; }
    public TimeSpan         TiempoExpiracion { get; init; }
    public DateTime         MomentoCreacion  { get; init; }
    public DateTime         MomentoExpiracion => MomentoCreacion.Add(TiempoExpiracion);

    /// <summary>True cuando la estrategia predice un rango en lugar de un punto exacto.</summary>
    public bool EsRango => PrecioMin.HasValue && PrecioMax.HasValue;
}

public enum DireccionApuesta { Alcista, Bajista, Neutro }
public enum ModoEjecucion    { Secuencial, Paralelo }
