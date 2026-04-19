using System.Diagnostics;
using Microsoft.Extensions.Options;
using SimuladorBackend.Models;
using SimuladorBackend.Options;

namespace SimuladorBackend.Models;

public class Portafolio
{
    private readonly object  _lock         = new();
    private readonly decimal _saldoInicial;
    private          decimal _saldo;
    private long _tiempoEsperaLockMs;
    private int _adquisicionesLock;

    public decimal Saldo { get; private set; }

    public Portafolio() : this(Microsoft.Extensions.Options.Options.Create(new SimuladorOptions()))
    {
    }

    public Portafolio(IOptions<SimuladorOptions> options)
    {
        _saldoInicial = options.Value.SaldoInicialPortafolio;
        _saldo        = _saldoInicial;
        Saldo         = _saldoInicial;
    }
    public long TiempoEsperaLockMs => _tiempoEsperaLockMs;
    public int AdquisicionesLock => _adquisicionesLock;

    public void Sumar(decimal monto, Apuesta apuesta, int trabajoCriticoMs = 0)
    {
        var sw = Stopwatch.StartNew();
        lock (_lock)
        {
            sw.Stop();
            Interlocked.Add(ref _tiempoEsperaLockMs, sw.ElapsedMilliseconds);
            Interlocked.Increment(ref _adquisicionesLock);
            if (trabajoCriticoMs > 0) Thread.Sleep(trabajoCriticoMs);
            _saldo += monto;
            Saldo = _saldo;
        }
    }

    public void Restar(decimal monto, Apuesta apuesta, int trabajoCriticoMs = 0)
    {
        var sw = Stopwatch.StartNew();
        lock (_lock)
        {
            sw.Stop();
            Interlocked.Add(ref _tiempoEsperaLockMs, sw.ElapsedMilliseconds);
            Interlocked.Increment(ref _adquisicionesLock);
            if (trabajoCriticoMs > 0) Thread.Sleep(trabajoCriticoMs);
            _saldo -= monto;
            Saldo = _saldo;
        }
    }

    public void Reiniciar()
    {
        lock (_lock)
        {
            _saldo = _saldoInicial;
            Saldo = _saldo;
            _tiempoEsperaLockMs = 0;
            _adquisicionesLock = 0;
        }
    }

    public double ObtenerPorcentajeLock(long tiempoTotalMs) =>
        tiempoTotalMs > 0 ? (_tiempoEsperaLockMs * 100.0 / tiempoTotalMs) : 0;
}
