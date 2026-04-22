namespace SimuladorBackend.Services;

public interface IEstrategia
{
    string Nombre { get; }
    decimal EspecularPrecio(IReadOnlyList<decimal> historial);
}

/// <summary>
/// Desviación estándar de los últimos 5 ticks × 2.5 — especula un movimiento brusco.
/// </summary>
public class EstrategiaAgresiva : IEstrategia
{
    public string Nombre => "Agresiva";

    public decimal EspecularPrecio(IReadOnlyList<decimal> historial)
    {
        if (historial.Count == 0) return 0m;

        int ventana = Math.Min(5, historial.Count);
        var muestra = historial.TakeLast(ventana).ToList();
        decimal ultimo = muestra[^1];

        if (ventana < 2) return ultimo;

        decimal media = muestra.Average();
        double varianza = muestra.Select(p => Math.Pow((double)(p - media), 2)).Average();
        decimal stdDev = (decimal)Math.Sqrt(varianza);

        decimal direccion = ultimo >= media ? 1m : -1m;
        return ultimo + stdDev * 2.5m * direccion;
    }
}

/// <summary>
/// Promedio móvil de los últimos 20 ticks, especula regresión del 30% hacia la media.
/// </summary>
public class EstrategiaConservadora : IEstrategia
{
    public string Nombre => "Conservadora";

    public decimal EspecularPrecio(IReadOnlyList<decimal> historial)
    {
        if (historial.Count == 0) return 0m;

        int ventana = Math.Min(20, historial.Count);
        var muestra = historial.TakeLast(ventana).ToList();
        decimal ultimo = muestra[^1];
        decimal media = muestra.Average();

        return ultimo + (media - ultimo) * 0.30m;
    }
}

/// <summary>
/// Pendiente lineal (mínimos cuadrados) de los últimos 10 ticks — la tendencia continúa.
/// </summary>
public class EstrategiaTendencia : IEstrategia
{
    public string Nombre => "Tendencia";

    public decimal EspecularPrecio(IReadOnlyList<decimal> historial)
    {
        if (historial.Count == 0) return 0m;

        int ventana = Math.Min(10, historial.Count);
        var muestra = historial.TakeLast(ventana).ToList();
        decimal ultimo = muestra[^1];

        if (ventana < 2) return ultimo;

        // Regresión lineal simple: y = a + b*x
        double n = ventana;
        double sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0;
        for (int i = 0; i < ventana; i++)
        {
            sumX  += i;
            sumY  += (double)muestra[i];
            sumXY += i * (double)muestra[i];
            sumX2 += i * i;
        }

        double denominador = n * sumX2 - sumX * sumX;
        if (Math.Abs(denominador) < 1e-10) return ultimo;

        double pendiente = (n * sumXY - sumX * sumY) / denominador;
        return ultimo + (decimal)pendiente;
    }
}

public static class FabricaEstrategias
{
    public static IReadOnlyList<IEstrategia> CrearTodas() =>
    [
        new EstrategiaAgresiva(),
        new EstrategiaConservadora(),
        new EstrategiaTendencia(),
    ];
}
