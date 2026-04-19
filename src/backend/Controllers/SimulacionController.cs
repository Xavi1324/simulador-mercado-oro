using Microsoft.AspNetCore.Mvc;
using SimuladorBackend.Services;

namespace SimuladorBackend.Controllers;

[ApiController]
[Route("api")]
public class SimulacionController : ControllerBase
{
    private readonly MercadoCentral _mercadoCentral;
    private readonly MetricasEngine _metricasEngine;
    private readonly PruebaCargaPortafolioService _pruebaCargaPortafolio;

    public SimulacionController(
        MercadoCentral mercadoCentral,
        MetricasEngine metricasEngine,
        PruebaCargaPortafolioService pruebaCargaPortafolio)
    {
        _mercadoCentral = mercadoCentral;
        _metricasEngine = metricasEngine;
        _pruebaCargaPortafolio = pruebaCargaPortafolio;
    }

    [HttpGet("sistema/nucleos")]
    public IActionResult ObtenerNucleos()
    {
        return Ok(new
        {
            nucleosDisponibles = Environment.ProcessorCount,
            maquina            = Environment.MachineName,
            os                 = Environment.OSVersion.ToString(),
        });
    }

    [HttpPost("simulacion/iniciar")]
    public IActionResult Iniciar([FromBody] ConfiguracionRequest req)
    {
        _mercadoCentral.Iniciar(req.Nucleos, req.IntervaloSegundos);
        return Ok(new { activo = true });
    }

    [HttpPost("simulacion/pausar")]
    public IActionResult Pausar()
    {
        _mercadoCentral.Pausar();
        return Ok(new { activo = false });
    }

    [HttpPost("simulacion/configurar")]
    public IActionResult Configurar([FromBody] ConfiguracionRequest req)
    {
        _mercadoCentral.Configurar(req.Nucleos, req.IntervaloSegundos);
        return Ok(new { actualizado = true });
    }

    [HttpGet("metricas/ultimas")]
    public IActionResult ObtenerUltimasMetricas([FromQuery] int n = 20)
    {
        var metricas = _metricasEngine.ObtenerUltimasMetricas(n);
        return Ok(metricas);
    }

    [HttpGet("metricas/exportar")]
    public async Task<IActionResult> ExportarCsv()
    {
        string archivo = await _metricasEngine.ExportarCsvAsync();
        if (string.IsNullOrEmpty(archivo))
            return NotFound(new { error = "No hay métricas para exportar." });

        byte[] contenido = await System.IO.File.ReadAllBytesAsync(archivo);
        return File(contenido, "text/csv", Path.GetFileName(archivo));
    }

    [HttpPost("portafolio/prueba-carga")]
    public async Task<IActionResult> EjecutarPruebaCargaPortafolio([FromBody] PruebaCargaPortafolioRequest req, CancellationToken ct)
    {
        var resultado = await _pruebaCargaPortafolio.EjecutarAsync(req, ct: ct);
        return Ok(resultado);
    }
}

public record ConfiguracionRequest(int Nucleos, int IntervaloSegundos);
