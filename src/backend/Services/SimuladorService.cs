using System.Diagnostics;

namespace SimuladorBackend.Services;

/// <summary>
/// Orquesta la ejecución de estrategias en modo Secuencial o Paralelo.
///
/// Secuencial: Agresiva → Conservadora → Tendencia  (~30 s)
/// Paralelo:   Task.WhenAll(Agresiva, Conservadora, Tendencia)  (~10 s)
///
/// La diferencia de tiempo es la demostración visual del paralelismo.
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
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        List<ApuestaDemo> todas;

        if (modo == ModoEjecucion.Secuencial)
        {
            // Una tras otra — demuestra el cuello de botella secuencial
            var a = await _estrategias.CalcularAgresiva(precioActual, ct);
            var c = await _estrategias.CalcularConservadora(precioActual, ct);
            var t = await _estrategias.CalcularTendencia(precioActual, ct);
            todas = [a, c, t];
        }
        else
        {
            // Paralelo con Task.WhenAll — descomposición especulativa real
            var tA = _estrategias.CalcularAgresiva(precioActual, ct);
            var tC = _estrategias.CalcularConservadora(precioActual, ct);
            var tT = _estrategias.CalcularTendencia(precioActual, ct);
            var resultados = await Task.WhenAll(tA, tC, tT);
            todas = [.. resultados];
        }

        sw.Stop();

        var seleccionada = SeleccionarMasCercana(todas, precioActual);
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

    // La estrategia cuyo precio esperado más se acerca al precio actual gana
    private static ApuestaDemo SeleccionarMasCercana(List<ApuestaDemo> candidatas, decimal precioActual)
        => candidatas.OrderBy(x => Math.Abs(x.PrecioEsperado - precioActual)).First();
}

/// <summary>Resultado de un ciclo completo de especulación.</summary>
public sealed class ResultadoEspeculacion
{
    public required ApuestaDemo       EstrategiaSeleccionada { get; init; }
    public required List<ApuestaDemo> EstrategiasDescartadas { get; init; }
    public required long              TiempoEjecucionMs      { get; init; }
    public required ModoEjecucion     Modo                   { get; init; }
    public required int               TickNumero             { get; init; }
}
