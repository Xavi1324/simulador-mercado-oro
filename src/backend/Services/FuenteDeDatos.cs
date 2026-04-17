using System.Text.Json;

namespace SimuladorBackend.Services;

/// <summary>
/// Única fuente de verdad para el precio XAU/USD.
/// Cadena de fallback: Swissquote → Metals-API → último precio real conocido.
/// NUNCA genera precios inventados. Si nunca se obtuvo un precio real, lanza excepción.
/// </summary>
public sealed class FuenteDeDatos
{
    private readonly IHttpClientFactory        _httpClientFactory;
    private readonly ILogger<FuenteDeDatos>    _logger;
    private readonly string?                   _apiKey;

    // Último precio real obtenido de cualquier fuente externa
    private decimal  _ultimoPrecioReal;
    private string   _ultimaFuenteReal = string.Empty;

    public FuenteDeDatos(IHttpClientFactory httpClientFactory, IConfiguration config, ILogger<FuenteDeDatos> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger            = logger;
        _apiKey            = config["METALS_API_KEY"];
    }

    public async Task<(decimal precio, string fuente)> ObtenerPrecio()
    {
        // Nivel 1: Swissquote (gratuito, sin API key)
        try
        {
            decimal precio = await ObtenerDeSwissquote();
            _ultimoPrecioReal  = precio;
            _ultimaFuenteReal  = "Swissquote";
            return (precio, "Swissquote");
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Swissquote falló: {Error}", ex.Message);
        }

        // Nivel 2: Metals-API (requiere API key)
        if (!string.IsNullOrWhiteSpace(_apiKey))
        {
            try
            {
                decimal precio = await ObtenerDeMetalsApi();
                _ultimoPrecioReal = precio;
                _ultimaFuenteReal = "API";
                return (precio, "API");
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Metals-API falló: {Error}", ex.Message);
            }
        }

        // Nivel 3: último precio real conocido (sin inventar nada)
        if (_ultimoPrecioReal > 0)
        {
            _logger.LogWarning(
                "Todas las fuentes fallaron. Usando último precio real ({Fuente}): {Precio}",
                _ultimaFuenteReal, _ultimoPrecioReal);
            return (_ultimoPrecioReal, _ultimaFuenteReal);
        }

        // Sin precio real disponible — lanzar para que el tick se omita y reintente
        throw new InvalidOperationException(
            "No se pudo obtener el precio XAU/USD de ninguna fuente real. " +
            "Verifica la conectividad con Swissquote.");
    }

    // ── Swissquote ────────────────────────────────────────────────────────────

    private async Task<decimal> ObtenerDeSwissquote()
    {
        var client = _httpClientFactory.CreateClient("Swissquote");
        client.Timeout = TimeSpan.FromSeconds(5);
        client.DefaultRequestHeaders.Clear();
        client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (compatible; SimuladorOro/1.0)");
        client.DefaultRequestHeaders.Add("Accept", "application/json");

        const string url =
            "https://forex-data-feed.swissquote.com/public-quotes/bboquotes/instrument/XAU/USD";

        using var response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync();
        using var doc    = await JsonDocument.ParseAsync(stream);

        // La respuesta es un array de servidores. Cada servidor tiene spreadProfilePrices[].
        // Iteramos todos los servidores buscando el perfil "standard".
        // Si ninguno lo tiene, tomamos el primer bid/ask disponible de cualquier servidor.
        var root = doc.RootElement;
        var servidores = root.ValueKind == JsonValueKind.Array
            ? root.EnumerateArray().ToList()
            : root.TryGetProperty("value", out var v) ? v.EnumerateArray().ToList() : [];

        double? primerBid = null, primerAsk = null;

        foreach (var servidor in servidores)
        {
            if (!servidor.TryGetProperty("spreadProfilePrices", out var perfiles)) continue;

            foreach (var perfil in perfiles.EnumerateArray())
            {
                double bid = perfil.GetProperty("bid").GetDouble();
                double ask = perfil.GetProperty("ask").GetDouble();

                // Guardar el primer par bid/ask válido como fallback
                if (primerBid is null && bid > 0 && ask > 0)
                {
                    primerBid = bid;
                    primerAsk = ask;
                }

                // Preferir el perfil "standard" si existe en algún servidor
                if (perfil.GetProperty("spreadProfile").GetString() == "standard" && bid > 0 && ask > 0)
                {
                    decimal mid = Math.Round((decimal)((bid + ask) / 2.0), 2);
                    _logger.LogInformation("Swissquote OK (standard): bid={Bid} ask={Ask} mid={Mid}", bid, ask, mid);
                    return mid;
                }
            }
        }

        // Sin perfil "standard" → usar el primer bid/ask encontrado
        if (primerBid is not null && primerAsk is not null)
        {
            decimal mid = Math.Round((decimal)((primerBid.Value + primerAsk.Value) / 2.0), 2);
            _logger.LogInformation("Swissquote OK (primer perfil disponible): bid={Bid} ask={Ask} mid={Mid}", primerBid, primerAsk, mid);
            return mid > 0 ? mid : throw new InvalidDataException("Precio Swissquote inválido (≤0)");
        }

        throw new InvalidDataException("No se encontró ningún bid/ask válido en la respuesta de Swissquote.");
    }

    // ── Metals-API ────────────────────────────────────────────────────────────

    private async Task<decimal> ObtenerDeMetalsApi()
    {
        var client = _httpClientFactory.CreateClient("MetalsApi");
        client.Timeout = TimeSpan.FromSeconds(3);

        string url = $"https://metals-api.com/api/latest?access_key={_apiKey}&base=USD&symbols=XAU";
        using var response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync();
        using var doc    = await JsonDocument.ParseAsync(stream);

        double xauRate = doc.RootElement
            .GetProperty("rates")
            .GetProperty("XAU")
            .GetDouble();

        return xauRate > 0
            ? Math.Round((decimal)(1.0 / xauRate), 2)
            : throw new InvalidDataException("XAU rate inválido de Metals-API.");
    }
}
