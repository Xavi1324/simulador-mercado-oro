using SimuladorBackend.Models;
using SimuladorBackend.Services;
using Xunit;

namespace SimuladorBackend.Tests;

public class AgenteTests
{
    private static readonly IReadOnlyList<decimal> HistorialBasico =
        Enumerable.Range(0, 10).Select(i => 3100m + i * 5m).ToList();

    private static Tick TickBasico(int numero = 1) => new()
    {
        Precio     = 3150m,
        Timestamp  = DateTime.UtcNow,
        Fuente     = "CSV",
        NumeroTick = numero,
    };

    [Fact]
    public async Task ProcesarTick_DevuelveApuestaConAgenteIdCorrecto()
    {
        var agente   = new Agente(7);
        var apuesta  = await agente.ProcesarTick(TickBasico(), HistorialBasico, CancellationToken.None);

        Assert.Equal(7, apuesta.AgenteId);
    }

    [Fact]
    public async Task ProcesarTick_EstrategiaEsUnaDelasTres()
    {
        var agente  = new Agente(0);
        var apuesta = await agente.ProcesarTick(TickBasico(), HistorialBasico, CancellationToken.None);

        var nombresValidos = new[] { "Agresiva", "Conservadora", "Tendencia" };
        Assert.Contains(apuesta.Estrategia, nombresValidos);
    }

    [Fact]
    public async Task ProcesarTick_VolumenEsDiez()
    {
        var agente  = new Agente(1);
        var apuesta = await agente.ProcesarTick(TickBasico(), HistorialBasico, CancellationToken.None);

        Assert.Equal(10, apuesta.Volumen);
    }

    [Fact]
    public async Task ProcesarTick_TickNumeroCopiado()
    {
        var agente  = new Agente(2);
        var apuesta = await agente.ProcesarTick(TickBasico(42), HistorialBasico, CancellationToken.None);

        Assert.Equal(42, apuesta.TickNumero);
    }

    [Fact]
    public void ProcesarTickSecuencial_DevuelveApuestaDeterminista()
    {
        // Misma semilla (mismo id) → misma estrategia elegida en llamadas consecutivas
        // Nota: cada llamada avanza el Random internamente; aquí solo verificamos que
        // no lanza y que devuelve algo válido.
        var agente  = new Agente(5);
        var apuesta = agente.ProcesarTickSecuencial(TickBasico(), HistorialBasico);

        var nombresValidos = new[] { "Agresiva", "Conservadora", "Tendencia" };
        Assert.Contains(apuesta.Estrategia, nombresValidos);
        Assert.Equal(5, apuesta.AgenteId);
    }

    [Fact]
    public async Task ProcesarTick_ConHistorialVacio_NoCrash()
    {
        var agente = new Agente(3);

        var excepcion = await Record.ExceptionAsync(
            () => agente.ProcesarTick(TickBasico(), [], CancellationToken.None));

        Assert.Null(excepcion);
    }

    [Fact]
    public void RegistrarResultado_ActualizaContadoresCorrectamente()
    {
        var agente = new Agente(4);

        agente.RegistrarResultado(new Apuesta { EsGanadora = true });
        agente.RegistrarResultado(new Apuesta { EsGanadora = true });
        agente.RegistrarResultado(new Apuesta { EsGanadora = false });

        Assert.Equal(2, agente.Aciertos);
        Assert.Equal(1, agente.Fallos);
    }
}
