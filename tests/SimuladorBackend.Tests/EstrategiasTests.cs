using SimuladorBackend.Services;
using Xunit;

namespace SimuladorBackend.Tests;

public class EstrategiasTests
{
    [Fact]
    public void MetricasEngine_RegistraMetricasDelCalculoReal()
    {
        var engine = new MetricasEngine();

        var metrica = engine.RegistrarMetrica(
            nucleos: 4,
            tickNumero: 7,
            tiempoSecuencialMs: 400,
            tiempoParaleloMs: 100,
            precioOro: 3000m,
            saldoPortafolio: 1000m);

        Assert.Equal(4.0, metrica.Speedup, precision: 2);
        Assert.Equal(1.0, metrica.Eficiencia, precision: 2);
        Assert.Equal(0, metrica.PorcentajeLock);
        Assert.Single(engine.ObtenerUltimasMetricas(10));
    }

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

    // ── Monte Carlo particionado ──────────────────────────────────────────

    [Fact]
    public async Task EstrategiaService_Agresiva_UnNucleo_DevuelveApuestaAlcista()
    {
        var service = new EstrategiaService();
        var historial = Enumerable.Range(0, 20).Select(i => 3000m + i).ToList();

        var apuesta = await service.CalcularAgresiva(3020m, historial, nucleos: 1, CancellationToken.None);

        Assert.Equal("Agresiva", apuesta.Nombre);
        Assert.Equal(DireccionApuesta.Alcista, apuesta.Direccion);
        Assert.True(apuesta.PrecioEsperado > 3020m);
    }

    [Fact]
    public async Task EstrategiaService_Agresiva_VariosNucleos_DevuelveApuestaAlcista()
    {
        var service = new EstrategiaService();
        var historial = Enumerable.Range(0, 20).Select(i => 3000m + i).ToList();

        var apuesta = await service.CalcularAgresiva(3020m, historial, nucleos: 4, CancellationToken.None);

        Assert.Equal("Agresiva", apuesta.Nombre);
        Assert.Equal(DireccionApuesta.Alcista, apuesta.Direccion);
        Assert.True(apuesta.PrecioEsperado > 3020m);
    }

    [Fact]
    public async Task EstrategiaService_Conservadora_VariosNucleos_DevuelveRangoValido()
    {
        var service = new EstrategiaService();
        var historial = Enumerable.Range(0, 20).Select(i => 3000m + i).ToList();

        var apuesta = await service.CalcularConservadora(3020m, historial, nucleos: 4, CancellationToken.None);

        Assert.Equal("Conservadora", apuesta.Nombre);
        Assert.Equal(DireccionApuesta.Neutro, apuesta.Direccion);
        Assert.NotNull(apuesta.PrecioMin);
        Assert.NotNull(apuesta.PrecioMax);
        Assert.True(apuesta.PrecioMin <= apuesta.PrecioEsperado);
        Assert.True(apuesta.PrecioEsperado <= apuesta.PrecioMax);
    }

    [Fact]
    public async Task EstrategiaService_Tendencia_VariosNucleos_DevuelveApuestaBajista()
    {
        var service = new EstrategiaService();
        var historial = Enumerable.Range(0, 20).Select(i => 3000m + i).ToList();

        var apuesta = await service.CalcularTendencia(3020m, historial, nucleos: 4, CancellationToken.None);

        Assert.Equal("Tendencia", apuesta.Nombre);
        Assert.Equal(DireccionApuesta.Bajista, apuesta.Direccion);
        Assert.True(apuesta.PrecioEsperado < 3020m);
    }

    // ── Validación de apuesta especulativa ─────────────────────────────────

    [Fact]
    public void ApuestaAlcista_GanaCuandoSuperaBarrera()
    {
        var apuesta = new ApuestaEspeculativa
        {
            Nombre = "Agresiva",
            PrecioEsperado = 4000m,
            Direccion = DireccionApuesta.Alcista,
        };

        Assert.True(MercadoCentral.EvaluarApuestaEspeculativa(apuesta, 4500m));
    }

    [Fact]
    public void ApuestaAlcista_GanaCuandoIgualaBarrera()
    {
        var apuesta = new ApuestaEspeculativa
        {
            Nombre = "Agresiva",
            PrecioEsperado = 4000m,
            Direccion = DireccionApuesta.Alcista,
        };

        Assert.True(MercadoCentral.EvaluarApuestaEspeculativa(apuesta, 4000m));
    }

    [Fact]
    public void ApuestaAlcista_PierdeCuandoNoAlcanzaBarrera()
    {
        var apuesta = new ApuestaEspeculativa
        {
            Nombre = "Agresiva",
            PrecioEsperado = 4000m,
            Direccion = DireccionApuesta.Alcista,
        };

        Assert.False(MercadoCentral.EvaluarApuestaEspeculativa(apuesta, 3900m));
    }

    [Fact]
    public void ApuestaBajista_GanaCuandoRompeBarreraHaciaAbajo()
    {
        var apuesta = new ApuestaEspeculativa
        {
            Nombre = "Tendencia",
            PrecioEsperado = 2000m,
            Direccion = DireccionApuesta.Bajista,
        };

        Assert.True(MercadoCentral.EvaluarApuestaEspeculativa(apuesta, 1800m));
    }

    [Fact]
    public void ApuestaBajista_GanaCuandoIgualaBarrera()
    {
        var apuesta = new ApuestaEspeculativa
        {
            Nombre = "Tendencia",
            PrecioEsperado = 2000m,
            Direccion = DireccionApuesta.Bajista,
        };

        Assert.True(MercadoCentral.EvaluarApuestaEspeculativa(apuesta, 2000m));
    }

    [Fact]
    public void ApuestaBajista_PierdeCuandoQuedaSobreBarrera()
    {
        var apuesta = new ApuestaEspeculativa
        {
            Nombre = "Tendencia",
            PrecioEsperado = 2000m,
            Direccion = DireccionApuesta.Bajista,
        };

        Assert.False(MercadoCentral.EvaluarApuestaEspeculativa(apuesta, 2100m));
    }

    [Theory]
    [InlineData(2950)]
    [InlineData(3000)]
    [InlineData(3050)]
    public void ApuestaConservadora_GanaDentroDelRangoIncluyendoBordes(decimal precioFinal)
    {
        var apuesta = new ApuestaEspeculativa
        {
            Nombre = "Conservadora",
            PrecioEsperado = 3000m,
            PrecioMin = 2950m,
            PrecioMax = 3050m,
            Direccion = DireccionApuesta.Neutro,
        };

        Assert.True(MercadoCentral.EvaluarApuestaEspeculativa(apuesta, precioFinal));
    }

    [Theory]
    [InlineData(2949.99)]
    [InlineData(3050.01)]
    public void ApuestaConservadora_PierdeFueraDelRango(decimal precioFinal)
    {
        var apuesta = new ApuestaEspeculativa
        {
            Nombre = "Conservadora",
            PrecioEsperado = 3000m,
            PrecioMin = 2950m,
            PrecioMax = 3050m,
            Direccion = DireccionApuesta.Neutro,
        };

        Assert.False(MercadoCentral.EvaluarApuestaEspeculativa(apuesta, precioFinal));
    }
}
