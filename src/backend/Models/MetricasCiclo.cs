namespace SimuladorBackend.Models;

public class MetricasCiclo
{
    public int Nucleos { get; set; }
    public int TickNumero { get; set; }
    public long TiempoSecuencialMs { get; set; }
    public long TiempoParaleloMs { get; set; }
    public double Speedup { get; set; }
    public double Eficiencia { get; set; }
    public double DecisionesPorSegundo { get; set; }
    public double PorcentajeLock { get; set; }
    public decimal PrecioOro { get; set; }
    public decimal SaldoPortafolio { get; set; }
    public DateTime Timestamp { get; set; }
}
