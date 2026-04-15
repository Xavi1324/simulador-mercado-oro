namespace SimuladorBackend.Models;

public class Apuesta
{
    public int AgenteId { get; set; }
    public string Estrategia { get; set; } = "";
    public decimal PrecioEntrada { get; set; }
    public decimal PrecioEsperado { get; set; }
    public int Volumen { get; set; } = 10;
    public decimal GananciaReal { get; set; }
    public bool EsGanadora { get; set; }
    public int TickNumero { get; set; }
}
