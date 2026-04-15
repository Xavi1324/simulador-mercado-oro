namespace SimuladorBackend.Models;

public class Tick
{
    public decimal Precio { get; set; }
    public DateTime Timestamp { get; set; }
    public string Fuente { get; set; } = "CSV"; // "API" | "CSV"
    public int NumeroTick { get; set; }
}
