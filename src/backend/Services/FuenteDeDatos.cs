using System.Text.Json;

namespace SimuladorBackend.Services;

public class FuenteDeDatos
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<FuenteDeDatos> _logger;
    private readonly string? _apiKey;
    private readonly List<decimal> _datosCsv = [];
    private int _tickCsvIndex;

    // Caché del último precio exitoso de Swissquote
    private decimal _ultimoPrecioSwissquote;
    private DateTime _ultimaActualizacionSwissquote = DateTime.MinValue;
    private static readonly TimeSpan TtlCacheSwissquote = TimeSpan.FromSeconds(15);

    public FuenteDeDatos(IHttpClientFactory httpClientFactory, IConfiguration config, ILogger<FuenteDeDatos> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _apiKey = config["METALS_API_KEY"];
        CargarOGenerarCsv();
    }

    public async Task<(decimal precio, string fuente)> ObtenerPrecio()
    {
        // Nivel 1: Swissquote (gratuito, sin API key, siempre primero)
        try
        {
            decimal precio = await ObtenerDeSwissquote();
            _ultimoPrecioSwissquote = precio;
            _ultimaActualizacionSwissquote = DateTime.UtcNow;
            return (precio, "Swissquote");
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Swissquote falló: {Error}", ex.Message);

            // Si tenemos un precio en caché reciente, usarlo para evitar parpadeo
            if (_ultimoPrecioSwissquote > 0 &&
                DateTime.UtcNow - _ultimaActualizacionSwissquote < TtlCacheSwissquote)
            {
                _logger.LogInformation("Usando precio Swissquote en caché: {Precio}", _ultimoPrecioSwissquote);
                return (_ultimoPrecioSwissquote, "Swissquote");
            }
        }

        // Nivel 2: Metals-API (solo si hay API key configurada)
        if (!string.IsNullOrWhiteSpace(_apiKey))
        {
            try
            {
                decimal precio = await ObtenerDeMetalsApi();
                return (precio, "API");
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Metals-API falló: {Error}", ex.Message);
            }
        }

        // Nivel 3: CSV histórico (fallback final)
        return (ObtenerDeCsv(), "CSV");
    }

    // ── Fuentes de datos ──────────────────────────────────────────────────────

    private async Task<decimal> ObtenerDeSwissquote()
    {
        var client = _httpClientFactory.CreateClient("Swissquote");
        client.Timeout = TimeSpan.FromSeconds(5);
        client.DefaultRequestHeaders.Clear();
        client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (compatible; SimuladorOro/1.0)");
        client.DefaultRequestHeaders.Add("Accept", "application/json");

        const string url = "https://forex-data-feed.swissquote.com/public-quotes/bboquotes/instrument/XAU/USD";
        using var response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync();
        using var doc    = await JsonDocument.ParseAsync(stream);

        // Estructura: array de plataformas → primera → spreadProfilePrices → perfil "standard"
        var perfiles = doc.RootElement[0].GetProperty("spreadProfilePrices");

        foreach (var perfil in perfiles.EnumerateArray())
        {
            if (perfil.GetProperty("spreadProfile").GetString() == "standard")
            {
                double bid = perfil.GetProperty("bid").GetDouble();
                double ask = perfil.GetProperty("ask").GetDouble();
                decimal mid = Math.Round((decimal)((bid + ask) / 2.0), 2);
                _logger.LogInformation("Swissquote OK: bid={Bid} ask={Ask} mid={Mid}", bid, ask, mid);
                return mid > 0 ? mid : throw new InvalidDataException("Precio Swissquote inválido");
            }
        }

        throw new InvalidDataException("Perfil 'standard' no encontrado en respuesta Swissquote");
    }

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
            : throw new InvalidDataException("XAU rate inválido");
    }

    // ── CSV ───────────────────────────────────────────────────────────────────

    private decimal ObtenerDeCsv()
    {
        if (_datosCsv.Count == 0) return 3100m;
        decimal precio = _datosCsv[_tickCsvIndex % _datosCsv.Count];
        _tickCsvIndex++;
        return precio;
    }

    private void CargarOGenerarCsv()
    {
        const string ruta = "metrics/gold_historical.csv";

        if (!File.Exists(ruta))
            GenerarCsvSimulado(ruta);

        foreach (string linea in File.ReadLines(ruta).Skip(1))
        {
            var partes = linea.Split(',');
            if (partes.Length >= 5 && decimal.TryParse(partes[4],
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out decimal close))
            {
                _datosCsv.Add(close);
            }
        }
    }

    private static void GenerarCsvSimulado(string ruta)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ruta)!);

        var rng  = new Random(42);
        var sb   = new System.Text.StringBuilder();
        sb.AppendLine("timestamp,open,high,low,close,volume");

        decimal precio = 3100m;
        var fecha = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        for (int i = 0; i < 500; i++)
        {
            decimal variacion = (decimal)(rng.NextDouble() * 100 - 50);
            decimal open  = precio;
            decimal close = Math.Max(2800m, Math.Min(3500m, precio + variacion));
            decimal high  = Math.Max(open, close) + (decimal)(rng.NextDouble() * 10);
            decimal low   = Math.Min(open, close) - (decimal)(rng.NextDouble() * 10);
            int volume    = rng.Next(1000, 5000);

            sb.AppendLine(FormattableString.Invariant(
                $"{fecha:yyyy-MM-ddTHH:mm:ssZ},{open:F2},{high:F2},{low:F2},{close:F2},{volume}"));

            precio = close;
            fecha  = fecha.AddMinutes(5);
        }

        File.WriteAllText(ruta, sb.ToString());
    }
}
