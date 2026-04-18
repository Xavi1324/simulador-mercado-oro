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
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        List<ApuestaEspeculativa> todas;

        if (modo == ModoEjecucion.Secuencial)
        {
            // Una tras otra — demuestra el cuello de botella secuencial (~30 s)
            var a = await _estrategias.CalcularAgresiva    (precioActual, historial, ct);
            var c = await _estrategias.CalcularConservadora(precioActual, historial, ct);
            var t = await _estrategias.CalcularTendencia   (precioActual, historial, ct);
            todas = [a, c, t];
        }
        else
        {
            // Paralelo con Task.WhenAll — descomposición especulativa (~10 s)
            var tA = _estrategias.CalcularAgresiva    (precioActual, historial, ct);
            var tC = _estrategias.CalcularConservadora(precioActual, historial, ct);
            var tT = _estrategias.CalcularTendencia   (precioActual, historial, ct);
            todas = [.. await Task.WhenAll(tA, tC, tT)];
        }

        sw.Stop();

        // Selección ALEATORIA — no siempre gana la misma estrategia
        var seleccionada = SeleccionarAleatoria(todas);
        var descartadas  = todas.Where(x => x.Nombre != seleccionada.Nombre).ToList();

        return new ResultadoEspeculacion
        {
            EstrategiaSeleccionada = seleccionada,
            EstrategiasDescartadas = descartadas,
            TiempoEjecucionMs      = sw.ElapsedMilliseconds,
            Modo                   = modo,
            TickNumero             = Interlocked.Increment(ref _tickCounter),
        };
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
    public required ModoEjecucion             Modo                   { get; init; }
    public required int                       TickNumero             { get; init; }
}
