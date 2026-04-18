using System.Diagnostics;
using System.Text;
using SimuladorBackend.Models;

namespace SimuladorBackend.Services;

public record ResultadoCiclo(MetricasCiclo Metrica, Apuesta[] ApuestasActuales);

public class MetricasEngine
{
    private readonly List<MetricasCiclo> _historial = [];
    private readonly object _historialLock = new();

    public async Task<ResultadoCiclo> MedirCiclo(
        Tick tick,
        IReadOnlyList<decimal> historial,
        IReadOnlyList<Agente> agentes,
        Portafolio portafolio,
        int nucleos,
        CancellationToken ct)
    {
        if (agentes.Count == 0)
        {
            var metricaVacia = new MetricasCiclo
            {
                Nucleos              = nucleos,
                TickNumero           = tick.NumeroTick,
                TiempoSecuencialMs   = 0,
                TiempoParaleloMs     = 0,
                Speedup              = 1.0,
                Eficiencia           = 1.0,
                DecisionesPorSegundo = 0,
                PorcentajeLock       = 0,
                PrecioOro            = tick.Precio,
                SaldoPortafolio      = portafolio.Saldo,
                Timestamp            = DateTime.UtcNow,
            };
            return new ResultadoCiclo(metricaVacia, []);
        }

        // 1. Baseline secuencial — no usar CancellationToken, es síncrono
        var swSeq = Stopwatch.StartNew();
        var apuestasSecuenciales = agentes
            .Select(agente => agente.ProcesarTickSecuencial(tick, historial))
            .ToArray();
        swSeq.Stop();

        Apuesta[] apuestasActuales;
        long tSeq = Math.Max(swSeq.ElapsedMilliseconds, 1);
        long tPar;

        if (nucleos <= 1)
        {
            apuestasActuales = apuestasSecuenciales;
            tPar = tSeq;
        }
        else
        {
            // 2. Modo paralelo real — MIMD
            var swPar = Stopwatch.StartNew();
            apuestasActuales = await Task.WhenAll(
                agentes.Select(a => a.ProcesarTick(tick, historial, ct))
            );
            swPar.Stop();
            tPar = Math.Max(swPar.ElapsedMilliseconds, 1);
        }

        double speedup    = (double)tSeq / tPar;
        double eficiencia = nucleos > 0 ? speedup / nucleos : speedup;
        double throughput = nucleos * 3.0 / (tPar / 1000.0 + 0.001);

        var metrica = new MetricasCiclo
        {
            Nucleos              = nucleos,
            TickNumero           = tick.NumeroTick,
            TiempoSecuencialMs   = tSeq,
            TiempoParaleloMs     = tPar,
            Speedup              = speedup,
            Eficiencia           = eficiencia,
            DecisionesPorSegundo = throughput,
            PorcentajeLock       = portafolio.ObtenerPorcentajeLock(tPar),
            PrecioOro            = tick.Precio,
            SaldoPortafolio      = portafolio.Saldo,
            Timestamp            = DateTime.UtcNow,
        };

        lock (_historialLock)
            _historial.Add(metrica);

        return new ResultadoCiclo(metrica, apuestasActuales);
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

        Directory.CreateDirectory("metrics");
        string archivo = $"metrics/resultados_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";

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
