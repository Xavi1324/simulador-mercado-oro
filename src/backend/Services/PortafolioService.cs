using SimuladorBackend.Models;

namespace SimuladorBackend.Services;

/// <summary>
/// Servicio de portafolio para la demo de descomposición especulativa.
/// Mantiene un balance separado del portafolio del loop de fondo,
/// protegido con lock para escrituras concurrentes.
/// </summary>
public sealed class PortafolioService
{
    private readonly object      _lock     = new();
    private          decimal     _balance  = 1_000m;
    private readonly List<string> _historial = [];

    public decimal Balance
    {
        get { lock (_lock) return _balance; }
    }

    /// <summary>
    /// Registra el resultado de una apuesta y actualiza el balance.
    /// El lock garantiza que escrituras concurrentes no corrompan el saldo.
    /// </summary>
    public void RegistrarResultado(string nombreApuesta, bool gano, decimal monto)
    {
        lock (_lock)
        {
            _balance += gano ? monto : -monto;
            string estado = gano ? "GANADA" : "PERDIDA";
            _historial.Add(
                $"[{DateTime.UtcNow:HH:mm:ss}] {nombreApuesta} - {estado} - Balance: ${_balance:F2}");
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
            _balance = 1_000m;
            _historial.Clear();
        }
    }
}
