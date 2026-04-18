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
    private readonly SimuladorOptions _opciones;

    public SimuladorHub(
        MercadoCentral mercadoCentral,
        MetricasEngine metricasEngine,
        FuenteDeDatos fuenteDeDatos,
        Portafolio portafolio,
        IOptions<SimuladorOptions> opciones)
    {
        _mercadoCentral = mercadoCentral;
        _metricasEngine = metricasEngine;
        _fuenteDeDatos  = fuenteDeDatos;
        _portafolio     = portafolio;
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
            nucleos,
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
            nucleos                = 1,
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

    // ── Ciclo especulativo: automático en MercadoCentral ─────────────────────
    // No hay métodos manuales de especulación aquí. El loop corre solo.

    public override async Task OnConnectedAsync()
    {
        var ultimasMetricas = _metricasEngine.ObtenerUltimasMetricas(10);

        await Clients.Caller.SendAsync("EstadoInicial", new
        {
            simulacionActiva       = _mercadoCentral.EstaActivo,
            nucleos                = 1,
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
