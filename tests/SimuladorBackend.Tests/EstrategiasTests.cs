using SimuladorBackend.Services;
using Xunit;

namespace SimuladorBackend.Tests;

public class EstrategiasTests
{
    // ── EstrategiaAgresiva ────────────────────────────────────────────────

    [Fact]
    public void Agresiva_ConHistorialDeUnTick_DevuelvePrecioSinCrash()
    {
        var estrategia = new EstrategiaAgresiva();
        var historial  = new List<decimal> { 3100m };

        decimal resultado = estrategia.EspecularPrecio(historial);

        Assert.Equal(3100m, resultado); // con ventana 1 devuelve el mismo precio
    }

    [Fact]
    public void Agresiva_ConHistorialVacio_DevuelveCero()
    {
        var estrategia = new EstrategiaAgresiva();

        decimal resultado = estrategia.EspecularPrecio([]);

        Assert.Equal(0m, resultado);
    }

    [Fact]
    public void Agresiva_ConTendenciaAlcista_EspeculaPrecioMayor()
    {
        var estrategia = new EstrategiaAgresiva();
        // Precios crecientes — el último ($3150) > media ($3130)
        var historial = new List<decimal> { 3100m, 3120m, 3130m, 3140m, 3150m };

        decimal resultado = estrategia.EspecularPrecio(historial);

        Assert.True(resultado > 3150m, $"Se esperaba precio > $3150, fue {resultado}");
    }

    // ── EstrategiaConservadora ────────────────────────────────────────────

    [Fact]
    public void Conservadora_ConHistorialDeUnTick_DevuelveMismoPrecio()
    {
        var estrategia = new EstrategiaConservadora();
        var historial  = new List<decimal> { 3100m };

        decimal resultado = estrategia.EspecularPrecio(historial);

        Assert.Equal(3100m, resultado); // media == último → sin cambio
    }

    [Fact]
    public void Conservadora_EspeculaPrecioMasCercaDeLaMedia()
    {
        var estrategia = new EstrategiaConservadora();
        // Media ≈ 3100, último = 3200 → regresión 30% hacia media
        var historial = Enumerable.Repeat(3100m, 19).Append(3200m).ToList();

        decimal media    = historial.Average();
        decimal resultado = estrategia.EspecularPrecio(historial);
        decimal diferenciaPreviaAMedia  = Math.Abs(3200m - media);
        decimal diferenciaResultadoMedia = Math.Abs(resultado - media);

        Assert.True(diferenciaResultadoMedia < diferenciaPreviaAMedia,
            $"Conservadora debería acercarse a la media. Resultado: {resultado}, media: {media}");
    }

    // ── EstrategiaTendencia ───────────────────────────────────────────────

    [Fact]
    public void Tendencia_ConHistorialVacio_DevuelveCero()
    {
        var estrategia = new EstrategiaTendencia();

        decimal resultado = estrategia.EspecularPrecio([]);

        Assert.Equal(0m, resultado);
    }

    [Fact]
    public void Tendencia_ConPendientePositiva_EspeculaPrecioMayor()
    {
        var estrategia = new EstrategiaTendencia();
        // Tendencia claramente alcista: +10 por tick
        var historial = Enumerable.Range(0, 10).Select(i => 3000m + i * 10m).ToList();

        decimal ultimo   = historial[^1]; // 3090
        decimal resultado = estrategia.EspecularPrecio(historial);

        Assert.True(resultado > ultimo, $"Tendencia positiva debería especular precio mayor. Fue {resultado}");
    }

    [Fact]
    public void Tendencia_ConPendienteNegativa_EspeculaPrecioMenor()
    {
        var estrategia = new EstrategiaTendencia();
        // Tendencia bajista: -10 por tick
        var historial = Enumerable.Range(0, 10).Select(i => 3090m - i * 10m).ToList();

        decimal ultimo   = historial[^1]; // 3000
        decimal resultado = estrategia.EspecularPrecio(historial);

        Assert.True(resultado < ultimo, $"Tendencia negativa debería especular precio menor. Fue {resultado}");
    }

    // ── FabricaEstrategias ────────────────────────────────────────────────

    [Fact]
    public void Fabrica_DevuelveTresEstrategias()
    {
        var estrategias = FabricaEstrategias.CrearTodas();

        Assert.Equal(3, estrategias.Count);
        Assert.Contains(estrategias, e => e.Nombre == "Agresiva");
        Assert.Contains(estrategias, e => e.Nombre == "Conservadora");
        Assert.Contains(estrategias, e => e.Nombre == "Tendencia");
    }
}
