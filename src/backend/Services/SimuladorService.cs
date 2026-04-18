using System.Diagnostics;

namespace SimuladorBackend.Services;

/// <summary>
/// Orquesta la ejecución de estrategias en modo Secuencial o Paralelo.
///
/// Secuencial: Agresiva → Conservadora → Tendencia  (~30 s)
/// Paralelo:   Task.WhenAll(Agresiva, Conservadora, Tendencia)  (~10 s)
///
/// La selección de la estrategia ganadora es ALEATORIA para reflejar
/// que la especulación puede acertar o fallar — no siempre gana la misma.
/// </summary>
public sealed class SimuladorService
{
    private readonly EstrategiaService _estrategias;
    private int _tickCounter;

    public SimuladorService(EstrategiaService estrategias)
        => _estrategias = estrategias;

    public async Task<ResultadoEspeculacion> EjecutarAsync(
        decimal precioActual,
        ModoEjecucion modo,
        IReadOnlyList<decimal> historial,
        int nucleos,
        CancellationToken ct = default)
    {
        List<ApuestaEspeculativa> todas;
        long tiempoSecuencialMs;
        long tiempoParaleloMs;

        if (modo == ModoEjecucion.Secuencial)
        {
            var swSeq = Stopwatch.StartNew();
            todas = await CalcularSecuencial(precioActual, historial, ct);
            swSeq.Stop();

            tiempoSecuencialMs = Math.Max(swSeq.ElapsedMilliseconds, 1);
            tiempoParaleloMs   = tiempoSecuencialMs;
        }
        else
        {
            // Baseline real secuencial para comparar contra el trabajo paralelo real.
            var swSeq = Stopwatch.StartNew();
            await CalcularSecuencial(precioActual, historial, ct);
            swSeq.Stop();

            var swPar = Stopwatch.StartNew();
            todas = await CalcularParalelo(precioActual, historial, ct);
            swPar.Stop();

            tiempoSecuencialMs = Math.Max(swSeq.ElapsedMilliseconds, 1);
            tiempoParaleloMs   = Math.Max(swPar.ElapsedMilliseconds, 1);
        }

        double speedup    = (double)tiempoSecuencialMs / tiempoParaleloMs;
        double eficiencia = nucleos > 0 ? speedup / nucleos : speedup;

        // Selección ALEATORIA — no siempre gana la misma estrategia
        var seleccionada = SeleccionarAleatoria(todas);
        var descartadas  = todas.Where(x => x.Nombre != seleccionada.Nombre).ToList();

        return new ResultadoEspeculacion
        {
            EstrategiaSeleccionada = seleccionada,
            EstrategiasDescartadas = descartadas,
            TiempoEjecucionMs      = modo == ModoEjecucion.Secuencial ? tiempoSecuencialMs : tiempoParaleloMs,
            TiempoSecuencialMs     = tiempoSecuencialMs,
            TiempoParaleloMs       = tiempoParaleloMs,
            Speedup                = speedup,
            Eficiencia             = eficiencia,
            Modo                   = modo,
            TickNumero             = Interlocked.Increment(ref _tickCounter),
        };
    }

    private async Task<List<ApuestaEspeculativa>> CalcularSecuencial(
        decimal precioActual,
        IReadOnlyList<decimal> historial,
        CancellationToken ct)
    {
        // Una tras otra — demuestra el cuello de botella secuencial (~30 s)
        var a = await _estrategias.CalcularAgresiva(precioActual, historial, ct);
        var c = await _estrategias.CalcularConservadora(precioActual, historial, ct);
        var t = await _estrategias.CalcularTendencia(precioActual, historial, ct);
        return [a, c, t];
    }

    private async Task<List<ApuestaEspeculativa>> CalcularParalelo(
        decimal precioActual,
        IReadOnlyList<decimal> historial,
        CancellationToken ct)
    {
        // Paralelo con Task.WhenAll — descomposición especulativa (~10 s)
        var tA = _estrategias.CalcularAgresiva(precioActual, historial, ct);
        var tC = _estrategias.CalcularConservadora(precioActual, historial, ct);
        var tT = _estrategias.CalcularTendencia(precioActual, historial, ct);
        return [.. await Task.WhenAll(tA, tC, tT)];
    }

    // Selección aleatoria entre las 3 estrategias calculadas
    private static ApuestaEspeculativa SeleccionarAleatoria(List<ApuestaEspeculativa> candidatas)
        => candidatas[Random.Shared.Next(candidatas.Count)];
}

/// <summary>Resultado de un ciclo completo de especulación.</summary>
public sealed class ResultadoEspeculacion
{
    public required ApuestaEspeculativa       EstrategiaSeleccionada { get; init; }
    public required List<ApuestaEspeculativa> EstrategiasDescartadas { get; init; }
    public required long                      TiempoEjecucionMs      { get; init; }
    public required long                      TiempoSecuencialMs     { get; init; }
    public required long                      TiempoParaleloMs       { get; init; }
    public required double                    Speedup                { get; init; }
    public required double                    Eficiencia             { get; init; }
    public required ModoEjecucion             Modo                   { get; init; }
    public required int                       TickNumero             { get; init; }
}
