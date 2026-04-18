using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using SimuladorBackend.Options;
using SimuladorBackend.Services;

namespace SimuladorBackend.Hubs;

public class SimuladorHub : Hub
{
    private readonly MercadoCentral  _mercadoCentral;
    private readonly MetricasEngine  _metricasEngine;
    private readonly FuenteDeDatos   _fuenteDeDatos;
    private readonly SimuladorOptions _opciones;

    public SimuladorHub(
        MercadoCentral mercadoCentral,
        MetricasEngine metricasEngine,
        FuenteDeDatos fuenteDeDatos,
        IOptions<SimuladorOptions> opciones)
    {
        _mercadoCentral = mercadoCentral;
        _metricasEngine = metricasEngine;
        _fuenteDeDatos  = fuenteDeDatos;
        _opciones       = opciones.Value;
    }

    public async Task IniciarSimulacion(int nucleos, int intervaloSegundos, string modo = "Paralelo")
    {
        var modoEnum = Enum.TryParse<ModoEjecucion>(modo, ignoreCase: true, out var m) ? m : ModoEjecucion.Paralelo;
        _mercadoCentral.Iniciar(nucleos, intervaloSegundos, modoEnum);
        var ultimasMetricas = _metricasEngine.ObtenerUltimasMetricas(10);

        await Clients.Caller.SendAsync("EstadoInicial", new
        {
            simulacionActiva        = true,
            nucleos,
            nucleosDisponibles      = Environment.ProcessorCount,
            ultimasMetricas,
            saldoInicialPortafolio  = _opciones.SaldoInicialPortafolio,
            balanceInicialDemo      = _opciones.BalanceInicialDemo,
        });
    }

    public async Task PausarSimulacion()
    {
        _mercadoCentral.Pausar();
        await Clients.Caller.SendAsync("EstadoInicial", new
        {
            simulacionActiva       = false,
            nucleos                = 1,
            nucleosDisponibles     = Environment.ProcessorCount,
            ultimasMetricas        = _metricasEngine.ObtenerUltimasMetricas(10),
            saldoInicialPortafolio = _opciones.SaldoInicialPortafolio,
            balanceInicialDemo     = _opciones.BalanceInicialDemo,
        });
    }

    public Task Configurar(int nucleos, int intervaloSegundos)
    {
        _mercadoCentral.Configurar(nucleos, intervaloSegundos);
        return Task.CompletedTask;
    }

    public async Task CambiarFuente(string fuente)
    {
        _fuenteDeDatos.SetModo(fuente == "CSV");
        await Clients.All.SendAsync("ModoFuenteChanged", fuente);
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
            balanceInicialDemo     = _opciones.BalanceInicialDemo,
        });

        await base.OnConnectedAsync();
    }
}
