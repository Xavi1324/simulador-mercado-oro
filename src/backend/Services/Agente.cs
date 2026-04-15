using SimuladorBackend.Models;

namespace SimuladorBackend.Services;

public class Agente
{
    private readonly int _id;
    private readonly Random _rng;
    private int _aciertos;
    private int _fallos;

    public int Id => _id;
    public int Aciertos => _aciertos;
    public int Fallos => _fallos;

    public Agente(int id)
    {
        _id  = id;
        _rng = new Random(id * 42 + 7);
    }

    /// <summary>
    /// MODO PARALELO — Las 3 estrategias se ejecutan en paralelo (Task.WhenAll).
    /// Una estrategia al azar es elegida como apuesta final.
    /// </summary>
    public async Task<Apuesta> ProcesarTick(Tick tick, IReadOnlyList<decimal> historial, CancellationToken ct)
    {
        var estrategias = FabricaEstrategias.CrearTodas();
        var nombres = estrategias.Select(e => e.Nombre).ToArray();

        var precios = await Task.WhenAll(
            estrategias.Select(e => Task.Run(() => e.EspecularPrecio(historial), ct))
        );

        int indice = _rng.Next(0, 3);
        return new Apuesta
        {
            AgenteId       = _id,
            Estrategia     = nombres[indice],
            PrecioEntrada  = tick.Precio,
            PrecioEsperado = precios[indice],
            Volumen        = 10,
            TickNumero     = tick.NumeroTick,
        };
    }

    /// <summary>
    /// MODO SECUENCIAL — baseline para medir Speedup. Misma lógica, sin paralelismo.
    /// </summary>
    public Apuesta ProcesarTickSecuencial(Tick tick, IReadOnlyList<decimal> historial)
    {
        var estrategias = FabricaEstrategias.CrearTodas();
        var nombres = estrategias.Select(e => e.Nombre).ToArray();

        var precios = estrategias.Select(e => e.EspecularPrecio(historial)).ToArray();

        int indice = _rng.Next(0, 3);
        return new Apuesta
        {
            AgenteId       = _id,
            Estrategia     = nombres[indice],
            PrecioEntrada  = tick.Precio,
            PrecioEsperado = precios[indice],
            Volumen        = 10,
            TickNumero     = tick.NumeroTick,
        };
    }

    public void RegistrarResultado(Apuesta apuesta)
    {
        if (apuesta.EsGanadora)
            Interlocked.Increment(ref _aciertos);
        else
            Interlocked.Increment(ref _fallos);
    }
}
