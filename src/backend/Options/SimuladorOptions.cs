namespace SimuladorBackend.Options;

public sealed class SimuladorOptions
{
    public const string SectionName = "Simulador";

    public decimal SaldoInicialPortafolio { get; init; } = 1_000m;
}
