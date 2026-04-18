using Microsoft.AspNetCore.SignalR;
using SimuladorBackend.Hubs;
using SimuladorBackend.Models;

namespace SimuladorBackend.Services;

/// <summary>
/// Motor principal del simulador.
///
/// Corre dos bucles concurrentes via Task.WhenAll:
///   1. LoopDePreciosTick  — obtiene precio XAU/USD cada N segundos y emite velas
///   2. LoopDeEspeculacion — ciclo automático: calcular estrategias → esperar 60s → validar → repetir
/// </summary>
public sealed class MercadoCentral : BackgroundService
{
    private readonly FuenteDeDatos              _fuenteDeDatos;
    private readonly MetricasEngine             _metricasEngine;
    private readonly Portafolio                 _portafolio;
    private readonly SimuladorService           _simuladorService;
    private readonly PortafolioService          _portafolioService;
    private readonly IHubContext<SimuladorHub>  _hub;
    private readonly ILogger<MercadoCentral>    _logger;

    // ── Estado mutable ────────────────────────────────────────────────────────
    private volatile bool          _activo;
    private volatile int           _nucleos            = 1;
    private volatile int           _intervaloSegundos  = 2;
    private          ModoEjecucion _modoEspeculacion   = ModoEjecucion.Paralelo;

    private List<Agente> _agentes     = [];
    private readonly object _agentesLock = new();

    private Apuesta[]? _apuestasPrevias;
    private int        _numeroTick;
    private const decimal MargenAceptable = 5m;

    // Precio actual y historial
    private decimal _ultimoPrecio;
    private readonly object        _precioLock     = new();
    private readonly List<decimal> _historial      = [];
    private readonly object        _historialLock  = new();
    private const int              MaxHistorial    = 100;
    private const int              MinHistorialEspeculacion = 5;

    public MercadoCentral(
        FuenteDeDatos fuenteDeDatos,
        MetricasEngine metricasEngine,
        Portafolio portafolio,
        SimuladorService simuladorService,
        PortafolioService portafolioService,
        IHubContext<SimuladorHub> hub,
        ILogger<MercadoCentral> logger)
    {
        _fuenteDeDatos     = fuenteDeDatos;
        _metricasEngine    = metricasEngine;
        _portafolio        = portafolio;
        _simuladorService  = simuladorService;
        _portafolioService = portafolioService;
        _hub               = hub;
        _logger            = logger;
    }

    // ── Control público ───────────────────────────────────────────────────────

    public void Iniciar(int nucleos, int intervaloSegundos)
    {
        _nucleos           = Math.Max(1, nucleos);
        _intervaloSegundos = Math.Max(1, intervaloSegundos);
        _modoEspeculacion  = ResolverModo(_nucleos);
        _portafolioService.Reiniciar();
        lock (_agentesLock)
            _agentes = Enumerable.Range(0, _nucleos).Select(i => new Agente(i)).ToList();
        _activo = true;
        _logger.LogInformation("Simulación iniciada: {N} núcleos, {I}s, modo={M}", _nucleos, _intervaloSegundos, _modoEspeculacion);
    }

    public void Pausar()
    {
        _activo = false;
        _ = _metricasEngine.ExportarCsvAsync();
        _logger.LogInformation("Simulación pausada.");
    }

    public void Configurar(int nucleos, int intervaloSegundos)
    {
        _nucleos           = Math.Max(1, nucleos);
        _intervaloSegundos = Math.Max(1, intervaloSegundos);
        _modoEspeculacion  = ResolverModo(_nucleos);
        lock (_agentesLock)
            _agentes = Enumerable.Range(0, _nucleos).Select(i => new Agente(i)).ToList();
    }

    public bool EstaActivo => _activo;

    public ModoEjecucion ModoEspeculacion => _modoEspeculacion;

    private static ModoEjecucion ResolverModo(int nucleos) =>
        nucleos <= 1 ? ModoEjecucion.Secuencial : ModoEjecucion.Paralelo;

    // ── ExecuteAsync: dos bucles concurrentes ─────────────────────────────────

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        _logger.LogInformation("MercadoCentral arrancó (2 bucles concurrentes).");
        await Task.WhenAll(
            LoopDePreciosTick(ct),
            LoopDeEspeculacion(ct));
    }

    // ── Bucle 1: precio del oro cada N segundos ───────────────────────────────

    private async Task LoopDePreciosTick(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await ProcesarUnTick(ct);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en tick #{T}", _numeroTick);
            }

            try { await Task.Delay(_intervaloSegundos * 1_000, ct); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task ProcesarUnTick(CancellationToken ct)
    {
        _numeroTick++;

        var (precio, fuente) = await _fuenteDeDatos.ObtenerPrecio();

        // Actualizar precio actual (usado por el loop de especulación)
        lock (_precioLock) _ultimoPrecio = precio;
        AgregarAlHistorial(precio);

        var tick = new Tick
        {
            Precio     = precio,
            Timestamp  = DateTime.UtcNow,
            Fuente     = fuente,
            NumeroTick = _numeroTick,
        };

        await _hub.Clients.All.SendAsync("NuevoPrecio", new
        {
            precio    = (double)tick.Precio,
            timestamp = tick.Timestamp,
            modo      = tick.Fuente,
            tick      = tick.NumeroTick,
        }, ct);

        if (!_activo) return;

        if (_apuestasPrevias is { Length: > 0 })
            await EvaluarApuestas(_apuestasPrevias, tick, ct);

        IReadOnlyList<Agente> snapshot;
        lock (_agentesLock) { snapshot = [.._agentes]; }

        var historial = ObtenerHistorial();
        var resultado = await _metricasEngine.MedirCiclo(tick, historial, snapshot, _portafolio, _nucleos, ct);

        await _hub.Clients.All.SendAsync("NuevaMetrica", new
        {
            speedup            = resultado.Metrica.Speedup,
            eficiencia         = resultado.Metrica.Eficiencia,
            throughput         = resultado.Metrica.DecisionesPorSegundo,
            cuellobotella      = resultado.Metrica.PorcentajeLock,
            tiempoParaleloMs   = resultado.Metrica.TiempoParaleloMs,
            tiempoSecuencialMs = resultado.Metrica.TiempoSecuencialMs,
            nucleos            = resultado.Metrica.Nucleos,
        }, ct);

        await EmitirConsoleLog("info", "metricas",
            $"Tick #{tick.NumeroTick}: secuencial {resultado.Metrica.TiempoSecuencialMs} ms | paralelo {resultado.Metrica.TiempoParaleloMs} ms | speedup {resultado.Metrica.Speedup:F2}x | eficiencia {resultado.Metrica.Eficiencia * 100:F2}%",
            ct,
            tick: tick.NumeroTick,
            nucleos: _nucleos,
            modo: _modoEspeculacion,
            speedup: resultado.Metrica.Speedup,
            eficiencia: resultado.Metrica.Eficiencia,
            tiempoSecuencialMs: resultado.Metrica.TiempoSecuencialMs,
            tiempoParaleloMs: resultado.Metrica.TiempoParaleloMs);

        _apuestasPrevias = resultado.ApuestasActuales;
    }

    // ── Bucle 2: ciclo especulativo automático ────────────────────────────────

    private async Task LoopDeEspeculacion(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            // Esperar a que la simulación esté activa y haya historial suficiente
            if (!_activo || ObtenerHistorial().Count < MinHistorialEspeculacion)
            {
                try { await Task.Delay(1_000, ct); } catch (OperationCanceledException) { break; }
                continue;
            }

            try
            {
                await EjecutarCicloEspeculativo(ct);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ciclo especulativo — reintentando en 5s");
                try { await Task.Delay(5_000, ct); } catch (OperationCanceledException) { break; }
            }
        }
    }

    private async Task EjecutarCicloEspeculativo(CancellationToken ct)
    {
        decimal precioAlApostar;
        lock (_precioLock) { precioAlApostar = _ultimoPrecio; }
        var historial = ObtenerHistorial();
        var modo      = _modoEspeculacion;

        _logger.LogInformation("Ciclo especulativo iniciado — precio={P} modo={M}", precioAlApostar, modo);

        await EmitirConsoleLog("info", "especulacion",
            $"Cálculo especulativo iniciado: modo {modo}, núcleos {_nucleos}, thread {Environment.CurrentManagedThreadId}",
            ct,
            nucleos: _nucleos,
            modo: modo);

        // 1. Avisar que inició el cálculo
        await _hub.Clients.All.SendAsync("CalculoIniciado",
            new { modo = modo.ToString(), timestamp = DateTime.UtcNow }, ct);

        // 2. Calcular las 3 estrategias (10s cada una)
        var resultado = await _simuladorService.EjecutarAsync(precioAlApostar, modo, historial, ct);

        // 3. Emitir las 3 predicciones calculadas
        var todasDto = new[] { resultado.EstrategiaSeleccionada }
            .Concat(resultado.EstrategiasDescartadas)
            .Select(MapDto)
            .ToList();

        await _hub.Clients.All.SendAsync("PrediccionesCalculadas", new
        {
            estrategias = todasDto,
            tiempoMs    = resultado.TiempoEjecucionMs,
            modo        = resultado.Modo.ToString(),
            tick        = resultado.TickNumero,
        }, ct);

        await EmitirConsoleLog("info", "benchmark",
            $"Estrategias calculadas en {resultado.TiempoEjecucionMs} ms usando modo {resultado.Modo}",
            ct,
            tick: resultado.TickNumero,
            nucleos: _nucleos,
            modo: resultado.Modo,
            tiempoParaleloMs: resultado.TiempoEjecucionMs);

        // 4. Emitir la estrategia elegida (las otras 2 se desvanecen en el frontend)
        await _hub.Clients.All.SendAsync("EstrategiaSeleccionada", new
        {
            seleccionada = MapDto(resultado.EstrategiaSeleccionada),
            descartadas  = resultado.EstrategiasDescartadas.Select(MapDto).ToList(),
            tick         = resultado.TickNumero,
        }, ct);

        await EmitirConsoleLog("info", "apuesta",
            $"Estrategia seleccionada: {resultado.EstrategiaSeleccionada.Nombre} ({resultado.EstrategiaSeleccionada.Direccion}) a {FormatoPrecio(resultado.EstrategiaSeleccionada)}",
            ct,
            tick: resultado.TickNumero,
            nucleos: _nucleos,
            modo: resultado.Modo);

        // 5. Esperar 60 segundos (cierre de vela especulativa)
        _logger.LogInformation("Esperando 60s para validar la apuesta...");
        await EmitirConsoleLog("info", "evaluacion", "Esperando 60s para validar la apuesta con precio real.", ct,
            tick: resultado.TickNumero,
            nucleos: _nucleos,
            modo: resultado.Modo);
        await Task.Delay(60_000, ct);

        // 6. Validar con el precio real actual
        decimal precioFinal;
        lock (_precioLock) { precioFinal = _ultimoPrecio; }

        var apuesta   = resultado.EstrategiaSeleccionada;
        const decimal monto = 100m;

        bool gano = EvaluarApuestaEspeculativa(apuesta, precioFinal);

        _portafolioService.RegistrarResultado(
            apuesta.Nombre, gano, monto,
            precioAlApostar,
            apuesta.PrecioEsperado);

        _logger.LogInformation(
            "Apuesta {N}: {R} (precio inicial={Pi} final={Pf})",
            apuesta.Nombre, gano ? "GANADA" : "PERDIDA", precioAlApostar, precioFinal);

        await EmitirConsoleLog(gano ? "success" : "warning", "resultado",
            $"Apuesta {apuesta.Nombre}: $100 | {DescribirCondicion(apuesta, precioFinal)} | {(gano ? "GANADA" : "PERDIDA")} | entrada ${precioAlApostar:N2} | saldo ${_portafolioService.Balance:N2}",
            ct,
            tick: resultado.TickNumero,
            nucleos: _nucleos,
            modo: resultado.Modo);

        await _hub.Clients.All.SendAsync("PortafolioActualizado", new
        {
            balance      = _portafolioService.Balance,
            ultimoEvento = _portafolioService.ObtenerHistorial().LastOrDefault(),
        }, ct);
    }

    private static object MapDto(ApuestaEspeculativa a) => new
    {
        nombre           = a.Nombre,
        precioEsperado   = a.PrecioEsperado,
        precioMin        = a.PrecioMin,
        precioMax        = a.PrecioMax,
        direccion        = a.Direccion.ToString(),
        tiempoExpiracion = a.TiempoExpiracion.TotalMinutes >= 1
            ? $"{(int)a.TiempoExpiracion.TotalMinutes}m"
            : $"{(int)a.TiempoExpiracion.TotalSeconds}s",
    };

    public Task EmitirEstadoConsolaInicial(ISingleClientProxy cliente, CancellationToken ct = default) =>
        cliente.SendAsync("ConsoleLog", new
        {
            timestamp = DateTime.UtcNow,
            level = "info",
            fase = "sistema",
            mensaje = $"Procesadores disponibles: {Environment.ProcessorCount}. Saldo real: ${_portafolio.Saldo:N2}",
            nucleos = _nucleos,
            modo = _modoEspeculacion.ToString(),
            threadId = Environment.CurrentManagedThreadId,
        }, ct);

    public Task EmitirConsoleLog(string level, string fase, string mensaje, CancellationToken ct = default,
        int? tick = null,
        int? nucleos = null,
        ModoEjecucion? modo = null,
        int? agente = null,
        double? speedup = null,
        double? eficiencia = null,
        double? tiempoSecuencialMs = null,
        double? tiempoParaleloMs = null)
    {
        return _hub.Clients.All.SendAsync("ConsoleLog", new
        {
            timestamp = DateTime.UtcNow,
            level,
            fase,
            mensaje,
            tick,
            nucleos,
            modo = modo?.ToString(),
            threadId = Environment.CurrentManagedThreadId,
            agente,
            speedup,
            eficiencia,
            tiempoSecuencialMs,
            tiempoParaleloMs,
        }, ct);
    }

    private static string FormatoPrecio(ApuestaEspeculativa apuesta) =>
        apuesta.EsRango
            ? $"${apuesta.PrecioMin:N2} - ${apuesta.PrecioMax:N2}"
            : $"${apuesta.PrecioEsperado:N2}";

    public static bool EvaluarApuestaEspeculativa(ApuestaEspeculativa apuesta, decimal precioFinal) =>
        apuesta.Direccion switch
        {
            DireccionApuesta.Alcista => precioFinal >= apuesta.PrecioEsperado,
            DireccionApuesta.Bajista => precioFinal <= apuesta.PrecioEsperado,
            DireccionApuesta.Neutro when apuesta.EsRango =>
                precioFinal >= apuesta.PrecioMin!.Value && precioFinal <= apuesta.PrecioMax!.Value,
            _ => Math.Abs(precioFinal - apuesta.PrecioEsperado) <= MargenAceptable,
        };

    private static string DescribirCondicion(ApuestaEspeculativa apuesta, decimal precioFinal) =>
        apuesta.Direccion switch
        {
            DireccionApuesta.Alcista =>
                $"barrera alcista ${apuesta.PrecioEsperado:N2}, final ${precioFinal:N2}",
            DireccionApuesta.Bajista =>
                $"barrera bajista ${apuesta.PrecioEsperado:N2}, final ${precioFinal:N2}",
            DireccionApuesta.Neutro when apuesta.EsRango =>
                $"rango ${apuesta.PrecioMin:N2} - ${apuesta.PrecioMax:N2}, final ${precioFinal:N2}",
            _ =>
                $"objetivo ${apuesta.PrecioEsperado:N2}, final ${precioFinal:N2}",
        };

    // ── Evaluación de apuestas del loop de agentes ────────────────────────────

    private async Task EvaluarApuestas(Apuesta[] apuestas, Tick tickActual, CancellationToken ct)
    {
        foreach (var apuesta in apuestas)
        {
            decimal diferencia = Math.Abs(tickActual.Precio - apuesta.PrecioEsperado);
            bool esGanadora    = diferencia <= MargenAceptable;
            decimal ganancia   = diferencia * apuesta.Volumen;

            apuesta.GananciaReal = esGanadora ? ganancia : -ganancia;
            apuesta.EsGanadora   = esGanadora;

            await _hub.Clients.All.SendAsync("ResultadoTick", new
            {
                agente          = apuesta.AgenteId,
                estrategia      = apuesta.Estrategia,
                precioEsperado  = (double)apuesta.PrecioEsperado,
                precioReal      = (double)tickActual.Precio,
                ganancia        = (double)apuesta.GananciaReal,
                esGanadora      = apuesta.EsGanadora,
                portafolioTotal = (double)_portafolio.Saldo,
            }, ct);
        }
    }

    // ── Historial ─────────────────────────────────────────────────────────────

    private void AgregarAlHistorial(decimal precio)
    {
        lock (_historialLock)
        {
            _historial.Add(precio);
            if (_historial.Count > MaxHistorial) _historial.RemoveAt(0);
        }
    }

    private IReadOnlyList<decimal> ObtenerHistorial()
    {
        lock (_historialLock) return [.._historial];
    }
}
