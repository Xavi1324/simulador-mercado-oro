using System.Text;
using Microsoft.AspNetCore.Hosting;
using SimuladorBackend.Models;

namespace SimuladorBackend.Services;

public class MetricasEngine
{
    private readonly List<MetricasCiclo> _historial = [];
    private readonly object _historialLock = new();
    private readonly string _metricsDir;
    private readonly Portafolio _portafolio;
    private long _ultimoTiempoLockMs;

    public MetricasEngine(IWebHostEnvironment env, Portafolio portafolio)
    {
        // ContentRootPath = .../simulador-mercado-oro/src/backend
        // Subiendo dos niveles llegamos a .../simulador-mercado-oro/metrics
        _metricsDir = Path.GetFullPath(
            Path.Combine(env.ContentRootPath, "..", "..", "metrics"));
        _portafolio = portafolio;
    }

    public MetricasCiclo RegistrarMetrica(
        int nucleos,
        int tickNumero,
        long tiempoSecuencialMs,
        long tiempoParaleloMs,
        decimal precioOro,
        decimal saldoPortafolio)
    {
        long tSeq = Math.Max(tiempoSecuencialMs, 1);
        long tPar = Math.Max(tiempoParaleloMs, 1);
        double speedup    = (double)tSeq / tPar;
        double eficiencia = nucleos > 0 ? Math.Min(1.0, speedup / nucleos) : Math.Min(1.0, speedup);
        // 3 estrategias por ciclo independientemente del número de núcleos
        double throughput = 3.0 / (tPar / 1000.0 + 0.001);

        // Delta de tiempo esperando el lock en este ciclo vs el total paralelo
        long lockActual   = _portafolio.TiempoEsperaLockMs;
        long deltaLock    = lockActual - _ultimoTiempoLockMs;
        _ultimoTiempoLockMs = lockActual;
        double porcentajeLock = deltaLock > 0 ? deltaLock * 100.0 / tPar : 0;

        var metrica = new MetricasCiclo
        {
            Nucleos              = nucleos,
            TickNumero           = tickNumero,
            TiempoSecuencialMs   = tSeq,
            TiempoParaleloMs     = tPar,
            Speedup              = speedup,
            Eficiencia           = eficiencia,
            DecisionesPorSegundo = throughput,
            PorcentajeLock       = porcentajeLock,
            PrecioOro            = precioOro,
            SaldoPortafolio      = saldoPortafolio,
            Timestamp            = DateTime.UtcNow,
        };

        lock (_historialLock)
            _historial.Add(metrica);

        return metrica;
    }

    public IReadOnlyList<MetricasCiclo> ObtenerUltimasMetricas(int n)
    {
        lock (_historialLock)
            return _historial.TakeLast(n).ToList();
    }

    public async Task<string> ExportarCsvAsync()
    {
        List<MetricasCiclo> copia;
        lock (_historialLock)
            copia = [.._historial];

        if (copia.Count == 0) return string.Empty;

        Directory.CreateDirectory(_metricsDir);
        string archivo = Path.Combine(_metricsDir,
            $"resultados_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv");

        var sb = new StringBuilder();
        sb.AppendLine("Nucleos,TickNumero,TiempoSecuencialMs,TiempoParaleloMs,Speedup,Eficiencia,DecisionesPorSegundo,PorcentajeLock,PrecioOro,SaldoPortafolio,Timestamp");

        foreach (var m in copia)
        {
            sb.AppendLine(string.Create(
                System.Globalization.CultureInfo.InvariantCulture,
                $"{m.Nucleos},{m.TickNumero},{m.TiempoSecuencialMs},{m.TiempoParaleloMs},{m.Speedup:F4},{m.Eficiencia:F4},{m.DecisionesPorSegundo:F2},{m.PorcentajeLock:F2},{m.PrecioOro:F2},{m.SaldoPortafolio:F2},{m.Timestamp:O}"));
        }

        await File.WriteAllTextAsync(archivo, sb.ToString());
        return archivo;
    }
}
