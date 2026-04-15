using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using SimuladorBackend.Hubs;
using SimuladorBackend.Models;

namespace SimuladorBackend.Services;

public class MercadoCentral : BackgroundService
{
    private readonly FuenteDeDatos _fuenteDeDatos;
    private readonly MetricasEngine _metricasEngine;
    private readonly Portafolio _portafolio;
    private readonly IHubContext<SimuladorHub> _hub;
    private readonly ILogger<MercadoCentral> _logger;

    private volatile bool _activo;
    private volatile int _nucleos = 1;
    private volatile int _intervaloSegundos = 2;

    private List<Agente> _agentes = [];
    private readonly object _agentesLock = new();

    private Apuesta[]? _apuestasPrevias;
    private int _numeroTick;
    private readonly decimal _margenAceptable = 5m; // diferencia ≤ $5 → ganadora

    // Historial de precios para las estrategias
    private readonly List<decimal> _historialOrdenado = [];
    private readonly object _historialLock = new();
    private const int MaxHistorial = 100;

    public MercadoCentral(
        FuenteDeDatos fuenteDeDatos,
        MetricasEngine metricasEngine,
        Portafolio portafolio,
        IHubContext<SimuladorHub> hub,
        ILogger<MercadoCentral> logger)
    {
        _fuenteDeDatos  = fuenteDeDatos;
        _metricasEngine = metricasEngine;
        _portafolio     = portafolio;
        _hub            = hub;
        _logger         = logger;
    }

    // ── Control público ──────────────────────────────────────────────────────

    public void Iniciar(int nucleos, int intervaloSegundos)
    {
        _nucleos           = Math.Max(1, nucleos);
        _intervaloSegundos = Math.Max(1, intervaloSegundos);
        lock (_agentesLock)
            _agentes = Enumerable.Range(0, _nucleos).Select(i => new Agente(i)).ToList();
        _activo = true;
        _logger.LogInformation("Simulación iniciada: {Nucleos} núcleos, {Intervalo}s", _nucleos, _intervaloSegundos);
    }

    public void Pausar()
    {
        _activo = false;
        _ = _metricasEngine.ExportarCsvAsync(); // fire-and-forget
        _logger.LogInformation("Simulación pausada. CSV exportado.");
    }

    public void Configurar(int nucleos, int intervaloSegundos)
    {
        _nucleos           = Math.Max(1, nucleos);
        _intervaloSegundos = Math.Max(1, intervaloSegundos);
        lock (_agentesLock)
            _agentes = Enumerable.Range(0, _nucleos).Select(i => new Agente(i)).ToList();
        _logger.LogInformation("Configuración actualizada: {Nucleos} núcleos, {Intervalo}s", _nucleos, _intervaloSegundos);
    }

    public bool EstaActivo => _activo;

    // ── Loop principal ───────────────────────────────────────────────────────

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MercadoCentral arrancó.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcesarUnTick(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error procesando tick #{Tick}", _numeroTick);
            }

            await Task.Delay(_intervaloSegundos * 1000, stoppingToken);
        }
    }

    private async Task ProcesarUnTick(CancellationToken ct)
    {
        _numeroTick++;

        // 1. Obtener precio del oro (siempre, independiente de si la simulación está activa)
        var (precio, fuente) = await _fuenteDeDatos.ObtenerPrecio();
        var tick = new Tick
        {
            Precio     = precio,
            Timestamp  = DateTime.UtcNow,
            Fuente     = fuente,
            NumeroTick = _numeroTick,
        };

        // 2. Emitir NuevoPrecio por SignalR (siempre — alimenta la gráfica de velas)
        await _hub.Clients.All.SendAsync("NuevoPrecio", new
        {
            precio    = (double)tick.Precio,
            timestamp = tick.Timestamp,
            modo      = tick.Fuente,
            tick      = tick.NumeroTick,
        }, ct);

        // 3. Agregar precio al historial (siempre)
        AgregarAlHistorial(precio);

        // Solo continuar con agentes y métricas si la simulación está activa
        if (!_activo) return;

        // 4. Evaluar apuestas del tick anterior contra el precio nuevo
        if (_apuestasPrevias is { Length: > 0 })
            await EvaluarApuestas(_apuestasPrevias, tick, ct);

        // 5. Medir ciclo (baseline secuencial + paralelo)
        IReadOnlyList<Agente> agentesSnapshot;
        lock (_agentesLock) { agentesSnapshot = [.._agentes]; }

        var historial = ObtenerHistorial();
        var resultado = await _metricasEngine.MedirCiclo(tick, historial, agentesSnapshot, _portafolio, _nucleos, ct);

        // 6. Emitir métricas por SignalR
        await _hub.Clients.All.SendAsync("NuevaMetrica", new
        {
            speedup           = resultado.Metrica.Speedup,
            eficiencia        = resultado.Metrica.Eficiencia,
            throughput        = resultado.Metrica.DecisionesPorSegundo,
            cuellobotella     = resultado.Metrica.PorcentajeLock,
            tiempoParaleloMs  = resultado.Metrica.TiempoParaleloMs,
            tiempoSecuencialMs = resultado.Metrica.TiempoSecuencialMs,
            nucleos           = resultado.Metrica.Nucleos,
        }, ct);

        // 7. Guardar apuestas del ciclo actual (se evalúan en el próximo tick)
        _apuestasPrevias = resultado.ApuestasActuales;
    }

    private async Task EvaluarApuestas(Apuesta[] apuestas, Tick tickActual, CancellationToken ct)
    {
        foreach (var apuesta in apuestas)
        {
            decimal diferencia = Math.Abs(tickActual.Precio - apuesta.PrecioEsperado);
            bool esGanadora    = diferencia <= _margenAceptable;
            decimal ganancia   = diferencia * apuesta.Volumen;

            apuesta.GananciaReal  = esGanadora ? ganancia : -ganancia;
            apuesta.EsGanadora    = esGanadora;

            if (esGanadora)
                _portafolio.Sumar(ganancia, apuesta);
            else
                _portafolio.Restar(ganancia, apuesta);

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

    private void AgregarAlHistorial(decimal precio)
    {
        lock (_historialLock)
        {
            _historialOrdenado.Add(precio);
            if (_historialOrdenado.Count > MaxHistorial)
                _historialOrdenado.RemoveAt(0);
        }
    }

    private IReadOnlyList<decimal> ObtenerHistorial()
    {
        lock (_historialLock)
            return [.._historialOrdenado];
    }
}
