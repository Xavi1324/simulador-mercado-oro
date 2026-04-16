using Microsoft.AspNetCore.SignalR;
using SimuladorBackend.Services;

namespace SimuladorBackend.Hubs;

public class SimuladorHub : Hub
{
    private readonly MercadoCentral  _mercadoCentral;
    private readonly MetricasEngine  _metricasEngine;
    private readonly SimuladorService _simuladorService;
    private readonly PortafolioService _portafolioService;

    public SimuladorHub(
        MercadoCentral mercadoCentral,
        MetricasEngine metricasEngine,
        SimuladorService simuladorService,
        PortafolioService portafolioService)
    {
        _mercadoCentral  = mercadoCentral;
        _metricasEngine  = metricasEngine;
        _simuladorService  = simuladorService;
        _portafolioService = portafolioService;
    }

    public async Task IniciarSimulacion(int nucleos, int intervaloSegundos)
    {
        _mercadoCentral.Iniciar(nucleos, intervaloSegundos);
        var ultimasMetricas = _metricasEngine.ObtenerUltimasMetricas(10);

        await Clients.Caller.SendAsync("EstadoInicial", new
        {
            simulacionActiva   = true,
            nucleos,
            nucleosDisponibles = Environment.ProcessorCount,
            ultimasMetricas,
        });
    }

    public async Task PausarSimulacion()
    {
        _mercadoCentral.Pausar();
        await Clients.Caller.SendAsync("EstadoInicial", new
        {
            simulacionActiva   = false,
            nucleos            = 1,
            nucleosDisponibles = Environment.ProcessorCount,
            ultimasMetricas   = _metricasEngine.ObtenerUltimasMetricas(10),
        });
    }

    public Task Configurar(int nucleos, int intervaloSegundos)
    {
        _mercadoCentral.Configurar(nucleos, intervaloSegundos);
        return Task.CompletedTask;
    }

    // ── Demo: Descomposición Especulativa ─────────────────────────────────────

    /// <summary>
    /// Ejecuta un ciclo completo de especulación en el modo indicado.
    /// Emite 3 eventos SignalR mientras corre:
    ///   1. "CalculoIniciado"       — avisa que empezó
    ///   2. "PrediccionesCalculadas" — las 3 estrategias listas + tiempo total
    ///   3. "EstrategiaSeleccionada" — elegida + descartadas (para fade-out visual)
    /// </summary>
    public async Task EjecutarEspeculacion(decimal precioActual, string modo)
    {
        var modoEnum = Enum.Parse<ModoEjecucion>(modo, ignoreCase: true);

        // 1. Avisa que el cálculo empezó
        await Clients.Caller.SendAsync("CalculoIniciado", new
        {
            modo,
            timestamp = DateTime.UtcNow,
        });

        // 2. Ejecuta secuencial (~30 s) o paralelo (~10 s)
        using var cts    = CancellationTokenSource.CreateLinkedTokenSource(Context.ConnectionAborted);
        var resultado    = await _simuladorService.EjecutarAsync(precioActual, modoEnum, cts.Token);

        var todasLasEstrategias = new[] { resultado.EstrategiaSeleccionada }
            .Concat(resultado.EstrategiasDescartadas)
            .Select(a => new
            {
                nombre          = a.Nombre,
                precioEsperado  = a.PrecioEsperado,
                direccion       = a.Direccion.ToString(),
                tiempoExpiracion = FormatearTiempo(a.TiempoExpiracion),
            })
            .ToList();

        // 3. Envía las 3 predicciones calculadas
        await Clients.Caller.SendAsync("PrediccionesCalculadas", new
        {
            estrategias = todasLasEstrategias,
            tiempoMs    = resultado.TiempoEjecucionMs,
            modo        = resultado.Modo.ToString(),
            tick        = resultado.TickNumero,
        });

        // 4. Envía la estrategia ganadora (las otras 2 se desvanecen en el frontend)
        await Clients.Caller.SendAsync("EstrategiaSeleccionada", new
        {
            seleccionada = new
            {
                nombre          = resultado.EstrategiaSeleccionada.Nombre,
                precioEsperado  = resultado.EstrategiaSeleccionada.PrecioEsperado,
                direccion       = resultado.EstrategiaSeleccionada.Direccion.ToString(),
                tiempoExpiracion = FormatearTiempo(resultado.EstrategiaSeleccionada.TiempoExpiracion),
            },
            descartadas = resultado.EstrategiasDescartadas.Select(a => new
            {
                nombre          = a.Nombre,
                precioEsperado  = a.PrecioEsperado,
                direccion       = a.Direccion.ToString(),
                tiempoExpiracion = FormatearTiempo(a.TiempoExpiracion),
            }).ToList(),
            tick = resultado.TickNumero,
        });
    }

    /// <summary>
    /// Registra el resultado de una apuesta (ganó/perdió) y emite el balance actualizado.
    /// </summary>
    public async Task RegistrarResultadoApuesta(string nombreApuesta, bool gano, decimal monto)
    {
        _portafolioService.RegistrarResultado(nombreApuesta, gano, monto);

        await Clients.All.SendAsync("PortafolioActualizado", new
        {
            balance      = _portafolioService.Balance,
            ultimoEvento = _portafolioService.ObtenerHistorial().LastOrDefault(),
        });
    }

    private static string FormatearTiempo(TimeSpan ts) =>
        ts.TotalMinutes >= 1 ? $"{(int)ts.TotalMinutes}m" : $"{(int)ts.TotalSeconds}s";

    // ── Simulación de fondo (existente, sin cambios) ──────────────────────────

    public override async Task OnConnectedAsync()
    {
        var ultimasMetricas = _metricasEngine.ObtenerUltimasMetricas(10);

        await Clients.Caller.SendAsync("EstadoInicial", new
        {
            simulacionActiva   = _mercadoCentral.EstaActivo,
            nucleos            = 1,
            nucleosDisponibles = Environment.ProcessorCount,
            ultimasMetricas,
        });

        await base.OnConnectedAsync();
    }
}
