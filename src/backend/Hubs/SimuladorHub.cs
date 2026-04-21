using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using SimuladorBackend.Models;
using SimuladorBackend.Options;
using SimuladorBackend.Services;

namespace SimuladorBackend.Hubs;

public class SimuladorHub : Hub
{
    private readonly MercadoCentral  _mercadoCentral;
    private readonly MetricasEngine  _metricasEngine;
    private readonly FuenteDeDatos   _fuenteDeDatos;
    private readonly Portafolio      _portafolio;
    private readonly PruebaCargaPortafolioService _pruebaCargaPortafolio;
    private readonly SimuladorOptions _opciones;

    public SimuladorHub(
        MercadoCentral mercadoCentral,
        MetricasEngine metricasEngine,
        FuenteDeDatos fuenteDeDatos,
        Portafolio portafolio,
        PruebaCargaPortafolioService pruebaCargaPortafolio,
        IOptions<SimuladorOptions> opciones)
    {
        _mercadoCentral = mercadoCentral;
        _metricasEngine = metricasEngine;
        _fuenteDeDatos  = fuenteDeDatos;
        _portafolio     = portafolio;
        _pruebaCargaPortafolio = pruebaCargaPortafolio;
        _opciones       = opciones.Value;
    }

    public async Task IniciarSimulacion(int nucleos, int intervaloSegundos)
    {
        _mercadoCentral.Iniciar(nucleos, intervaloSegundos);
        var ultimasMetricas = _metricasEngine.ObtenerUltimasMetricas(10);

        await _mercadoCentral.EmitirConsoleLog("info", "inicio",
            $"Se utilizarán {Math.Max(1, nucleos)} procesadores. Modo: {_mercadoCentral.ModoEspeculacion}. Intervalo: {Math.Max(1, intervaloSegundos)}s.");

        await Clients.Caller.SendAsync("EstadoInicial", new
        {
            simulacionActiva        = true,
            nucleos                 = _mercadoCentral.NucleosActuales,
            intervaloSegundos       = _mercadoCentral.IntervaloSegundosActual,
            nucleosDisponibles      = Environment.ProcessorCount,
            ultimasMetricas,
            saldoInicialPortafolio  = _opciones.SaldoInicialPortafolio,
            saldoPortafolio         = _portafolio.Saldo,
            modoEspeculacion        = _mercadoCentral.ModoEspeculacion.ToString(),
        });
    }

    public async Task PausarSimulacion()
    {
        _mercadoCentral.Pausar();
        await _mercadoCentral.EmitirConsoleLog("warning", "pausa", "Simulación pausada. Exportando métricas si hay datos disponibles.");
        await Clients.Caller.SendAsync("EstadoInicial", new
        {
            simulacionActiva       = false,
            nucleos                = _mercadoCentral.NucleosActuales,
            intervaloSegundos      = _mercadoCentral.IntervaloSegundosActual,
            nucleosDisponibles     = Environment.ProcessorCount,
            ultimasMetricas        = _metricasEngine.ObtenerUltimasMetricas(10),
            saldoInicialPortafolio = _opciones.SaldoInicialPortafolio,
            saldoPortafolio        = _portafolio.Saldo,
            modoEspeculacion       = _mercadoCentral.ModoEspeculacion.ToString(),
        });
    }

    public async Task Configurar(int nucleos, int intervaloSegundos)
    {
        _mercadoCentral.Configurar(nucleos, intervaloSegundos);
        await _mercadoCentral.EmitirConsoleLog("info", "configuracion",
            $"Configuración actualizada: {Math.Max(1, nucleos)} procesadores, intervalo {Math.Max(1, intervaloSegundos)}s, modo {_mercadoCentral.ModoEspeculacion}.");
    }

    public async Task CambiarFuente(string fuente)
    {
        _fuenteDeDatos.SetModo(fuente == "CSV");
        await Clients.All.SendAsync("ModoFuenteChanged", fuente);
        await _mercadoCentral.EmitirConsoleLog("info", "fuente", $"Fuente de datos cambiada a {fuente}.");
    }

    public async Task EjecutarPruebaCargaPortafolio(int operaciones, int concurrencia, int trabajoCriticoMs)
    {
        var req = new PruebaCargaPortafolioRequest(operaciones, concurrencia, trabajoCriticoMs);

        await _mercadoCentral.EmitirConsoleLog("info", "carga-portafolio",
            $"Prueba de carga iniciada: {operaciones} operaciones, concurrencia {concurrencia}, trabajo crítico {trabajoCriticoMs} ms.",
            nucleos: concurrencia);

        PruebaCargaPortafolioResultado? resultadoFinal = null;

        try
        {
            resultadoFinal = await _pruebaCargaPortafolio.EjecutarAsync(req, async progreso =>
            {
                await Clients.All.SendAsync("PruebaCargaPortafolio", progreso);

                if (progreso.Estado == "progreso" || progreso.Estado == "completada")
                {
                    await Clients.All.SendAsync("NuevaMetrica", new
                    {
                        speedup = 1.0,
                        eficiencia = progreso.Estado == "completada" && !progreso.Consistente ? 0.0 : 1.0,
                        throughput = progreso.TiempoTotalMs > 0
                            ? progreso.Completadas / (progreso.TiempoTotalMs / 1000.0)
                            : 0,
                        cuellobotella = progreso.PorcentajeLock,
                        tiempoParaleloMs = progreso.TiempoTotalMs,
                        tiempoSecuencialMs = progreso.TiempoEsperaLockMs,
                        nucleos = progreso.Concurrencia,
                    });

                    await Clients.All.SendAsync("PortafolioActualizado", new
                    {
                        balance = _portafolio.Saldo,
                        ultimoEvento = $"Carga de portafolio: {progreso.Ganadas} ganancias, {progreso.Perdidas} pérdidas.",
                    });

                    await _mercadoCentral.EmitirConsoleLog(
                        progreso.Estado == "completada" && !progreso.Consistente ? "warning" : "info",
                        "carga-portafolio",
                        $"Progreso {progreso.Completadas}/{progreso.Operaciones} | ganadas {progreso.Ganadas} | pérdidas {progreso.Perdidas} | espera lock {progreso.TiempoEsperaLockMs} ms | presión {progreso.PorcentajeLock:F2}% | saldo ${progreso.SaldoObtenido:N2}",
                        nucleos: progreso.Concurrencia,
                        tiempoSecuencialMs: progreso.TiempoEsperaLockMs,
                        tiempoParaleloMs: progreso.TiempoTotalMs);
                }
            }, Context.ConnectionAborted);

            await Clients.All.SendAsync("PortafolioActualizado", new
            {
                balance = _portafolio.Saldo,
                ultimoEvento = resultadoFinal.Consistente
                    ? "Prueba de carga completada sin corrupción del saldo."
                    : "Prueba de carga detectó inconsistencia en el saldo.",
            });

            await Clients.All.SendAsync("NuevaMetrica", new
            {
                speedup = 1.0,
                eficiencia = resultadoFinal.Consistente ? 1.0 : 0.0,
                throughput = resultadoFinal.TiempoTotalMs > 0
                    ? resultadoFinal.Completadas / (resultadoFinal.TiempoTotalMs / 1000.0)
                    : 0,
                cuellobotella = 0.0,
                tiempoParaleloMs = resultadoFinal.TiempoTotalMs,
                tiempoSecuencialMs = 0,
                nucleos = resultadoFinal.Concurrencia,
            });

            await _mercadoCentral.EmitirConsoleLog(
                resultadoFinal.Consistente ? "success" : "error",
                "carga-portafolio",
                $"Prueba completada: ganadas {resultadoFinal.Ganadas}, pérdidas {resultadoFinal.Perdidas}, esperado ${resultadoFinal.SaldoEsperado:N2}, obtenido ${resultadoFinal.SaldoObtenido:N2}, adquisiciones lock {resultadoFinal.AdquisicionesLock}.",
                nucleos: resultadoFinal.Concurrencia,
                tiempoSecuencialMs: resultadoFinal.TiempoEsperaLockMs,
                tiempoParaleloMs: resultadoFinal.TiempoTotalMs);
        }
        catch (Exception ex)
        {
            await Clients.All.SendAsync("PruebaCargaPortafolio", new
            {
                estado = "fallida",
                operaciones,
                completadas = 0,
                concurrencia,
                trabajoCriticoMs,
                montoOperacion = 100m,
                saldoInicial = _portafolio.Saldo,
                saldoEsperado = _portafolio.Saldo,
                saldoObtenido = _portafolio.Saldo,
                ganadas = 0,
                perdidas = 0,
                tiempoTotalMs = 0,
                tiempoEsperaLockMs = 0,
                porcentajeLock = 0,
                adquisicionesLock = 0,
                consistente = false,
            });

            await _mercadoCentral.EmitirConsoleLog("error", "carga-portafolio",
                $"Prueba de carga falló: {ex.Message}", nucleos: concurrencia);
            throw;
        }
    }

    // ── Ciclo especulativo: automático en MercadoCentral ─────────────────────
    // No hay métodos manuales de especulación aquí. El loop corre solo.

    public override async Task OnConnectedAsync()
    {
        var ultimasMetricas = _metricasEngine.ObtenerUltimasMetricas(10);

        await Clients.Caller.SendAsync("EstadoInicial", new
        {
            simulacionActiva       = _mercadoCentral.EstaActivo,
            nucleos                = _mercadoCentral.NucleosActuales,
            intervaloSegundos      = _mercadoCentral.IntervaloSegundosActual,
            nucleosDisponibles     = Environment.ProcessorCount,
            ultimasMetricas,
            saldoInicialPortafolio = _opciones.SaldoInicialPortafolio,
            saldoPortafolio        = _portafolio.Saldo,
            modoEspeculacion       = _mercadoCentral.ModoEspeculacion.ToString(),
        });

        await _mercadoCentral.EmitirEstadoConsolaInicial(Clients.Caller);

        await base.OnConnectedAsync();
    }
}
