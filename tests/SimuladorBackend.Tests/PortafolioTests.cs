using SimuladorBackend.Models;
using SimuladorBackend.Services;
using Xunit;

namespace SimuladorBackend.Tests;

public class PortafolioTests
{
    private static Apuesta ApuestaFalsa() => new() { AgenteId = 0, Estrategia = "Test" };

    [Fact]
    public void SaldoInicial_Es1000()
    {
        var portafolio = new Portafolio();
        Assert.Equal(1_000m, portafolio.Saldo);
    }

    [Fact]
    public void Sumar_IncrementaSaldo()
    {
        var portafolio = new Portafolio();
        portafolio.Sumar(500m, ApuestaFalsa());
        Assert.Equal(1_500m, portafolio.Saldo);
    }

    [Fact]
    public void Restar_ReduceSaldo()
    {
        var portafolio = new Portafolio();
        portafolio.Restar(200m, ApuestaFalsa());
        Assert.Equal(800m, portafolio.Saldo);
    }

    [Fact]
    public void Reiniciar_VuelveA1000()
    {
        var portafolio = new Portafolio();
        portafolio.Sumar(9_000m, ApuestaFalsa());
        portafolio.Restar(3_000m, ApuestaFalsa());
        portafolio.Reiniciar();

        Assert.Equal(1_000m, portafolio.Saldo);
    }

    [Fact]
    public async Task Concurrencia_10Tasks_NoRaceCondition()
    {
        var portafolio = new Portafolio();
        // 10 Tasks suman $100 c/u → saldo final debe ser exactamente $2,000
        var tareas = Enumerable.Range(0, 10)
            .Select(_ => Task.Run(() => portafolio.Sumar(100m, ApuestaFalsa())))
            .ToArray();

        await Task.WhenAll(tareas);

        Assert.Equal(2_000m, portafolio.Saldo);
    }

    [Fact]
    public async Task Concurrencia_SumasYRestas_ResultadoConsistente()
    {
        var portafolio = new Portafolio();
        // 5 suman $200, 5 restan $200 → saldo no cambia
        var sumas   = Enumerable.Range(0, 5).Select(_ => Task.Run(() => portafolio.Sumar(200m, ApuestaFalsa())));
        var restas  = Enumerable.Range(0, 5).Select(_ => Task.Run(() => portafolio.Restar(200m, ApuestaFalsa())));

        await Task.WhenAll(sumas.Concat(restas));

        Assert.Equal(1_000m, portafolio.Saldo);
    }

    [Fact]
    public void ObtenerPorcentajeLock_TiempoCero_DevuelveCero()
    {
        var portafolio = new Portafolio();
        Assert.Equal(0.0, portafolio.ObtenerPorcentajeLock(0));
    }

    [Fact]
    public void AdquisicionesLock_SeIncrementaConCadaOperacion()
    {
        var portafolio = new Portafolio();
        portafolio.Sumar(100m, ApuestaFalsa());
        portafolio.Restar(50m, ApuestaFalsa());

        Assert.Equal(2, portafolio.AdquisicionesLock);
    }

    [Fact]
    public void PortafolioService_UsaSaldoRealCompartido()
    {
        var portafolio = new Portafolio();
        var service = new PortafolioService(portafolio);

        service.RegistrarResultado("Agresiva", true, 100m, 3100m, 3110m);

        Assert.Equal(1_100m, portafolio.Saldo);
        Assert.Equal(portafolio.Saldo, service.Balance);
    }

    [Fact]
    public void PortafolioService_PerdidaReduceSaldoReal()
    {
        var portafolio = new Portafolio();
        var service = new PortafolioService(portafolio);

        service.RegistrarResultado("Tendencia", false, 100m, 3100m, 3090m);

        Assert.Equal(900m, portafolio.Saldo);
    }
}
