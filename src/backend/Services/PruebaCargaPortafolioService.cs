using System.Diagnostics;
using SimuladorBackend.Models;

namespace SimuladorBackend.Services;

public sealed class PruebaCargaPortafolioService
{
    private readonly Portafolio _portafolio;

    public PruebaCargaPortafolioService(Portafolio portafolio)
    {
        _portafolio = portafolio;
    }

    public async Task<PruebaCargaPortafolioResultado> EjecutarAsync(
        PruebaCargaPortafolioRequest request,
        Func<PruebaCargaPortafolioProgreso, Task>? onProgreso = null,
        CancellationToken ct = default)
    {
        int operaciones = Math.Clamp(request.Operaciones, 2, 200_000);

        int concurrencia = Math.Clamp(request.Concurrencia, 1, Environment.ProcessorCount * 4);
        int trabajoCriticoMs = Math.Clamp(request.TrabajoCriticoMs, 0, 20);
        const decimal monto = 100m;

        decimal saldoInicial = _portafolio.Saldo;
        long esperaInicial = _portafolio.TiempoEsperaLockMs;
        int adquisicionesIniciales = _portafolio.AdquisicionesLock;
        int completadas = 0;
        int ganadas = 0;
        int perdidas = 0;
        int bloqueProgreso = Math.Max(1, Math.Min(operaciones / 100, 1_000));

        var sw = Stopwatch.StartNew();

        if (onProgreso is not null)
        {
            await onProgreso(CrearProgreso(
                "iniciada",
                operaciones,
                0,
                concurrencia,
                trabajoCriticoMs,
                monto,
                saldoInicial,
                saldoInicial,
                saldoInicial,
                0,
                0,
                0,
                0,
                0,
                0));
        }

        int siguienteOperacion = -1;
        var tareas = Enumerable.Range(0, concurrencia).Select(_ => Task.Run(async () =>
        {
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                int i = Interlocked.Increment(ref siguienteOperacion);
                if (i >= operaciones) break;

                var apuesta = new Apuesta
                {
                    Estrategia = "CargaPortafolio",
                    Volumen = (int)monto,
                };

                bool gano = Random.Shared.Next(2) == 0;
                int ganadasActuales;
                int perdidasActuales;

                if (gano)
                {
                    _portafolio.Sumar(monto, apuesta, trabajoCriticoMs);
                    ganadasActuales = Interlocked.Increment(ref ganadas);
                    perdidasActuales = Volatile.Read(ref perdidas);
                }
                else
                {
                    _portafolio.Restar(monto, apuesta, trabajoCriticoMs);
                    perdidasActuales = Interlocked.Increment(ref perdidas);
                    ganadasActuales = Volatile.Read(ref ganadas);
                }

                int actual = Interlocked.Increment(ref completadas);
                if (onProgreso is not null && (actual % bloqueProgreso == 0 || actual == operaciones))
                {
                    long esperaActual = Math.Max(0, _portafolio.TiempoEsperaLockMs - esperaInicial);
                    int adquisicionesActuales = Math.Max(0, _portafolio.AdquisicionesLock - adquisicionesIniciales);
                    decimal saldoEsperadoActual = CalcularSaldoEsperado(saldoInicial, monto, ganadasActuales, perdidasActuales);
                    double lockPct = sw.ElapsedMilliseconds > 0
                        ? esperaActual * 100.0 / sw.ElapsedMilliseconds
                        : 0;

                    await onProgreso(CrearProgreso(
                        "progreso",
                        operaciones,
                        actual,
                        concurrencia,
                        trabajoCriticoMs,
                        monto,
                        saldoInicial,
                        saldoEsperadoActual,
                        _portafolio.Saldo,
                        ganadasActuales,
                        perdidasActuales,
                        sw.ElapsedMilliseconds,
                        esperaActual,
                        lockPct,
                        adquisicionesActuales));
                }
            }
        }, ct)).ToArray();

        await Task.WhenAll(tareas);
        sw.Stop();

        long esperaFinal = Math.Max(0, _portafolio.TiempoEsperaLockMs - esperaInicial);
        int adquisicionesFinales = Math.Max(0, _portafolio.AdquisicionesLock - adquisicionesIniciales);
        double porcentajeLock = sw.ElapsedMilliseconds > 0
            ? esperaFinal * 100.0 / sw.ElapsedMilliseconds
            : 0;
        decimal saldoObtenido = _portafolio.Saldo;
        int totalGanadas = Volatile.Read(ref ganadas);
        int totalPerdidas = Volatile.Read(ref perdidas);
        decimal saldoEsperado = CalcularSaldoEsperado(saldoInicial, monto, totalGanadas, totalPerdidas);
        bool consistente = saldoObtenido == saldoEsperado && totalGanadas + totalPerdidas == operaciones && adquisicionesFinales >= operaciones;

        var resultado = new PruebaCargaPortafolioResultado(
            "completada",
            operaciones,
            operaciones,
            concurrencia,
            trabajoCriticoMs,
            monto,
            saldoInicial,
            saldoEsperado,
            saldoObtenido,
            totalGanadas,
            totalPerdidas,
            sw.ElapsedMilliseconds,
            esperaFinal,
            porcentajeLock,
            adquisicionesFinales,
            consistente);

        if (onProgreso is not null)
            await onProgreso(resultado.ToProgreso());

        return resultado;
    }

    private static decimal CalcularSaldoEsperado(decimal saldoInicial, decimal monto, int ganadas, int perdidas) =>
        saldoInicial + ganadas * monto - perdidas * monto;

    private static PruebaCargaPortafolioProgreso CrearProgreso(
        string estado,
        int operaciones,
        int completadas,
        int concurrencia,
        int trabajoCriticoMs,
        decimal montoOperacion,
        decimal saldoInicial,
        decimal saldoEsperado,
        decimal saldoObtenido,
        int ganadas,
        int perdidas,
        long tiempoTotalMs,
        long tiempoEsperaLockMs,
        double porcentajeLock,
        int adquisicionesLock)
    {
        return new PruebaCargaPortafolioProgreso(
            estado,
            operaciones,
            completadas,
            concurrencia,
            trabajoCriticoMs,
            montoOperacion,
            saldoInicial,
            saldoEsperado,
            saldoObtenido,
            ganadas,
            perdidas,
            tiempoTotalMs,
            tiempoEsperaLockMs,
            porcentajeLock,
            adquisicionesLock,
            saldoObtenido == saldoEsperado && completadas == operaciones && ganadas + perdidas == operaciones);
    }
}

public sealed record PruebaCargaPortafolioRequest(
    int Operaciones,
    int Concurrencia,
    int TrabajoCriticoMs);

public sealed record PruebaCargaPortafolioProgreso(
    string Estado,
    int Operaciones,
    int Completadas,
    int Concurrencia,
    int TrabajoCriticoMs,
    decimal MontoOperacion,
    decimal SaldoInicial,
    decimal SaldoEsperado,
    decimal SaldoObtenido,
    int Ganadas,
    int Perdidas,
    long TiempoTotalMs,
    long TiempoEsperaLockMs,
    double PorcentajeLock,
    int AdquisicionesLock,
    bool Consistente);

public sealed record PruebaCargaPortafolioResultado(
    string Estado,
    int Operaciones,
    int Completadas,
    int Concurrencia,
    int TrabajoCriticoMs,
    decimal MontoOperacion,
    decimal SaldoInicial,
    decimal SaldoEsperado,
    decimal SaldoObtenido,
    int Ganadas,
    int Perdidas,
    long TiempoTotalMs,
    long TiempoEsperaLockMs,
    double PorcentajeLock,
    int AdquisicionesLock,
    bool Consistente)
{
    public PruebaCargaPortafolioProgreso ToProgreso() => new(
        Estado,
        Operaciones,
        Completadas,
        Concurrencia,
        TrabajoCriticoMs,
        MontoOperacion,
        SaldoInicial,
        SaldoEsperado,
        SaldoObtenido,
        Ganadas,
        Perdidas,
        TiempoTotalMs,
        TiempoEsperaLockMs,
        PorcentajeLock,
        AdquisicionesLock,
        Consistente);
}
