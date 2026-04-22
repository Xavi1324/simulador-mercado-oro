using SimuladorBackend.Models;

namespace SimuladorBackend.Services;

/// <summary>
/// Registra los resultados especulativos en el portafolio real compartido.
/// </summary>
public sealed class PortafolioService
{
    private readonly object       _lock      = new();
    private readonly Portafolio   _portafolio;
    private readonly List<string> _historial = [];

    public PortafolioService(Portafolio portafolio)
    {
        _portafolio = portafolio;
    }

    public decimal Balance => _portafolio.Saldo;

    /// <summary>
    /// Registra el resultado de una apuesta y actualiza el saldo real.
    /// </summary>
    public void RegistrarResultado(
        string nombreApuesta, bool gano, decimal monto,
        decimal precioEntrada, decimal precioEsperado)
    {
        var apuesta = new Apuesta
        {
            Estrategia     = nombreApuesta,
            PrecioEntrada  = precioEntrada,
            PrecioEsperado = precioEsperado,
            Volumen        = (int)monto,
            GananciaReal   = gano ? monto : -monto,
            EsGanadora     = gano,
        };

        if (gano) _portafolio.Sumar(monto, apuesta);
        else      _portafolio.Restar(monto, apuesta);

        lock (_lock)
        {
            string emoji = gano ? "✅" : "❌";
            _historial.Add(
                $"[{DateTime.UtcNow:HH:mm:ss}] {nombreApuesta}: " +
                $"${precioEntrada:N2} — ${precioEsperado:N2} {emoji}");
        }
    }

    public IReadOnlyList<string> ObtenerHistorial()
    {
        lock (_lock) return [.. _historial];
    }

    public void Reiniciar()
    {
        lock (_lock)
        {
            _portafolio.Reiniciar();
            _historial.Clear();
        }
    }
}
