using Microsoft.AspNetCore.SignalR;
using SimuladorBackend.Services;

namespace SimuladorBackend.Hubs;

public class SimuladorHub : Hub
{
    private readonly MercadoCentral _mercadoCentral;
    private readonly MetricasEngine _metricasEngine;

    public SimuladorHub(MercadoCentral mercadoCentral, MetricasEngine metricasEngine)
    {
        _mercadoCentral = mercadoCentral;
        _metricasEngine = metricasEngine;
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
