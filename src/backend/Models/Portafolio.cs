using System.Diagnostics;
using SimuladorBackend.Models;

namespace SimuladorBackend.Models;

public class Portafolio
{
    private readonly object _lock = new();
    private decimal _saldo = 10_000m;
    private long _tiempoEsperaLockMs;
    private int _adquisicionesLock;

    public decimal Saldo { get; private set; } = 10_000m;
    public long TiempoEsperaLockMs => _tiempoEsperaLockMs;
    public int AdquisicionesLock => _adquisicionesLock;

    public void Sumar(decimal monto, Apuesta apuesta)
    {
        var sw = Stopwatch.StartNew();
        lock (_lock)
        {
            sw.Stop();
            Interlocked.Add(ref _tiempoEsperaLockMs, sw.ElapsedMilliseconds);
            Interlocked.Increment(ref _adquisicionesLock);
            _saldo += monto;
            Saldo = _saldo;
        }
    }

    public void Restar(decimal monto, Apuesta apuesta)
    {
        var sw = Stopwatch.StartNew();
        lock (_lock)
        {
            sw.Stop();
            Interlocked.Add(ref _tiempoEsperaLockMs, sw.ElapsedMilliseconds);
            Interlocked.Increment(ref _adquisicionesLock);
            _saldo -= monto;
            Saldo = _saldo;
        }
    }

    public void Reiniciar()
    {
        lock (_lock)
        {
            _saldo = 10_000m;
            Saldo = _saldo;
            _tiempoEsperaLockMs = 0;
            _adquisicionesLock = 0;
        }
    }

    public double ObtenerPorcentajeLock(long tiempoTotalMs) =>
        tiempoTotalMs > 0 ? (_tiempoEsperaLockMs * 100.0 / tiempoTotalMs) : 0;
}
