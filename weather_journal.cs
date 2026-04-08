using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.Threading.Tasks;

// ============================================================
// БЛОК 1: МОДЕЛИ ДАННЫХ - хранят информацию о погоде, городе, конфигурации
// ============================================================

/// <summary>Конфигурация приложения (сохраняется в config.cfg)</summary>
public class Config
{
    public string LastCity { get; set; } = string.Empty;        // Последний выбранный город
    public string LastDataSource { get; set; } = "VisualCrossing"; // Какой API использовался
}

/// <summary>Основные данные о погоде (сохраняются в weather_journal.json)</summary>
public class WeatherData
{
    public DateTime Date { get; set; }              // Дата наблюдения
    public string City { get; set; } = string.Empty; // Город
    public double Latitude { get; set; }             // Широта
    public double Longitude { get; set; }            // Долгота
    public string Timezone { get; set; } = string.Empty; // Часовой пояс
    public double MinTemperature { get; set; }       // Мин. температура (°C)
    public double MaxTemperature { get; set; }       // Макс. температура (°C)
    public double Precipitation { get; set; }        // Осадки (мм)
    public double Humidity { get; set; }             // Влажность (%)
    public double Pressure { get; set; }             // Давление (мм рт.ст.)
    public double WindSpeed { get; set; }            // Скорость ветра (м/с)
    public string WeatherCondition { get; set; } = string.Empty; // Описание погоды
    public DateTime LocalDateTime { get; set; }      // Время получения данных
    public string DataSource { get; set; } = string.Empty; // Источник (VisualCrossing/OpenMeteo)
}

/// <summary>Ответ от Visual Crossing API</summary>
public class VisualCrossingResponse
{
    public double latitude { get; set; }
    public double longitude { get; set; }
    public string timezone { get; set; } = string.Empty;
    public List<VisualCrossingDay> days { get; set; } = new();
}

/// <summary>Данные за один день от Visual Crossing</summary>
public class VisualCrossingDay
{
    public string datetime { get; set; } = string.Empty;
    public double tempmax { get; set; }
    public double tempmin { get; set; }
    public double precip { get; set; }
    public double humidity { get; set; }
    public double pressure { get; set; }
    public double windspeed { get; set; }
    public string conditions { get; set; } = string.Empty;
    public string description { get; set; } = string.Empty;
}

/// <summary>Ответ от Open-Meteo API (резервный)</summary>
public class OpenMeteoResponse
{
    public double latitude { get; set; }
    public double longitude { get; set; }
    public string timezone { get; set; } = string.Empty;
    public HourlyData hourly { get; set; } = new();
}

/// <summary>Почасовые данные от Open-Meteo</summary>
public class HourlyData
{
    public List<string> time { get; set; } = new();
    public List<double> temperature_2m { get; set; } = new();
    public List<int> relativehumidity_2m { get; set; } = new();
    public List<double> precipitation { get; set; } = new();
    public List<double> pressure_msl { get; set; } = new();
    public List<double> windspeed_10m { get; set; } = new();
    public List<int> weathercode { get; set; } = new();
}

/// <summary>Ответ от Geocoding API (поиск координат города)</summary>
public class GeocodingResponse
{
    public List<GeocodingResult> results { get; set; } = new();
}

/// <summary>Информация о найденном городе</summary>
public class GeocodingResult
{
    public string name { get; set; } = string.Empty;
    public double latitude { get; set; }
    public double longitude { get; set; }
    public string timezone { get; set; } = string.Empty;
    public string country { get; set; } = string.Empty;
    public string admin1 { get; set; } = string.Empty;
    public double elevation { get; set; }
    public int population { get; set; }
}

/// <summary>DTO (Data Transfer Object) для запроса погоды (передача данных между слоями)</summary>
public class WeatherRequest
{
    public string CityName { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string Timezone { get; set; } = string.Empty;
}

/// <summary>Результат запроса погоды (успех/неудача + данные)</summary>
public class WeatherResult
{
    public bool Success { get; set; }
    public WeatherData? Data { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public string DataSource { get; set; } = string.Empty;
}

/// <summary>Информация о городе после геокодинга</summary>
public class CityInfo
{
    public string Name { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string Timezone { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string AdminRegion { get; set; } = string.Empty;
    public double Elevation { get; set; }
    public int Population { get; set; }
}

/// <summary>Статистика по городу для отображения</summary>
public class CityStatistics
{
    public int TotalEntries { get; set; }
    public DateTime FirstDate { get; set; }
    public DateTime LastDate { get; set; }
    public double MinTemperature { get; set; }
    public double MaxTemperature { get; set; }
    public double MaxPrecipitation { get; set; }
    public double MaxPressure { get; set; }
    public double MaxWindSpeed { get; set; }
}

// ============================================================
// БЛОК 2: JSON CONTEXT - для высокопроизводительной сериализации
// ============================================================

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNameCaseInsensitive = true)] // настройки сериализации
[JsonSerializable(typeof(Config))]              // Конфигурация приложения
[JsonSerializable(typeof(WeatherData))]         // Запись о погоде
[JsonSerializable(typeof(List<WeatherData>))]   // Список записей (весь дневник)
[JsonSerializable(typeof(OpenMeteoResponse))]   // Ответ от Open-Meteo API
[JsonSerializable(typeof(VisualCrossingResponse))] // Ответ от Visual Crossing API
[JsonSerializable(typeof(VisualCrossingDay))]   // Дневные данные Visual Crossing
[JsonSerializable(typeof(GeocodingResponse))]   // Ответ геокодинга
[JsonSerializable(typeof(GeocodingResult))]     // Результат поиска города
public partial class WeatherJsonContext : JsonSerializerContext { } //класс объявлен как частичный; компилятор допишет вторую часть с реализацией

// ============================================================
// БЛОК 3: ИНТЕРФЕЙС API СЕРВИСОВ - для возможности подмены API
// ============================================================

/// <summary>Интерфейс для всех API погоды (паттерн Стратегия)</summary>
public interface IWeatherApiService
{
    string ServiceName { get; }
    Task<WeatherResult> GetWeatherDataAsync(WeatherRequest request);
    bool IsAvailable();
}

// ============================================================
// БЛОК 4: VISUAL CROSSING API - ОСНОВНОЙ ИСТОЧНИК ДАННЫХ
// ============================================================

/// <summary>Основной API погоды. Требует ключ в файле visualcrossing-api-key.txt</summary>
public class VisualCrossingService : IWeatherApiService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private bool _isAvailable;
    private string _availabilityReason;
    private bool _keyValidated;

    // Константы для перевода единиц измерения
    private const double HPA_TO_MMHG = 0.75006375541921;  // 1 гПа = 0.75 мм рт.ст.
    private const double KMH_TO_MS = 3.6;                  // 1 м/с = 3.6 км/ч

    public string ServiceName => "VisualCrossing";

    public VisualCrossingService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _apiKey = LoadApiKey();
        _isAvailable = !string.IsNullOrEmpty(_apiKey);
        _availabilityReason = _isAvailable ? "Ключ загружен" : "Ключ не найден";
        
        // Синхронная проверка ключа
        if (_isAvailable)
        {
            ValidateApiKeySync();
        }
        
        PrintStatus();
    }

    /// <summary>Загружает API ключ из файла</summary>
    private string LoadApiKey()
    {
        string filePath = "visualcrossing-api-key.txt";
        if (!File.Exists(filePath)) return string.Empty;
        
        try
        {
            string key = File.ReadAllText(filePath).Trim();
            return string.IsNullOrEmpty(key) || key.Length < 10 ? string.Empty : key;
        }
        catch { return string.Empty; }
    }

    /// <summary>Синхронная проверка ключа через тестовый запрос</summary>
    private void ValidateApiKeySync()
    {
        try
        {
            string url = $"https://weather.visualcrossing.com/VisualCrossingWebServices/rest/services/timeline/New%20York?unitGroup=metric&key={_apiKey}&contentType=json";
            var response = _httpClient.GetAsync(url).GetAwaiter().GetResult();
            
            if (response.IsSuccessStatusCode)
            {
                _keyValidated = true;
                _isAvailable = true;
                _availabilityReason = "Ключ действителен";
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                _keyValidated = false;
                _isAvailable = false;
                _availabilityReason = "Ключ недействителен (401)";
            }
            else
            {
                _keyValidated = false;
                _isAvailable = false;
                _availabilityReason = $"Ошибка: {response.StatusCode}";
            }
        }
        catch (Exception ex)
        {
            _keyValidated = false;
            _isAvailable = false;
            _availabilityReason = $"Ошибка: {ex.Message}";
        }
    }

    /// <summary>Выводит статус API в консоль</summary>
    private void PrintStatus()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"\n🔑 Visual Crossing API:");
        if (_isAvailable && _keyValidated)
        {
            Console.WriteLine($"   ✅ Ключ: {_apiKey.Substring(0, Math.Min(8, _apiKey.Length))}...");
            Console.WriteLine($"   ✅ {_availabilityReason}");
            Console.WriteLine($"   🌐 Сервис готов к работе (ОСНОВНОЙ)");
        }
        else
        {
            Console.WriteLine($"   ❌ {_availabilityReason}");
            Console.WriteLine($"   ⚠️ Сервис НЕДОСТУПЕН. Будет использован Open-Meteo");
            Console.WriteLine($"   💡 Создайте файл 'visualcrossing-api-key.txt' с ключом от visualcrossing.com");
        }
        Console.ResetColor();
    }

    public bool IsAvailable() => _isAvailable && _keyValidated;

    /// <summary>Переводит английское описание погоды на русский язык</summary>
    private string TranslateWeatherCondition(string englishCondition)
    {
        if (string.IsNullOrEmpty(englishCondition)) return "Неизвестно";
        
        string condition = englishCondition.ToLowerInvariant();
        
        if (condition.Contains("clear") || condition.Contains("sunny"))
            return "Ясно";
        if (condition.Contains("partially cloudy") || condition.Contains("partly cloudy"))
            return "Переменная облачность";
        if (condition.Contains("overcast"))
            return "Пасмурно";
        if (condition.Contains("fog") || condition.Contains("mist"))
            return "Туман";
        if (condition.Contains("drizzle"))
            return "Морось";
        if (condition.Contains("rain") && condition.Contains("thunder"))
            return "Гроза с дождем";
        if (condition.Contains("rain"))
            return "Дождь";
        if (condition.Contains("snow") && condition.Contains("rain"))
            return "Дождь со снегом";
        if (condition.Contains("snow"))
            return "Снег";
        if (condition.Contains("thunder"))
            return "Гроза";
        if (condition.Contains("shower"))
            return "Ливень";
        
        return char.ToUpper(englishCondition[0]) + englishCondition.Substring(1);
    }

    /// <summary>Получение данных о погоде из Visual Crossing</summary>
    public async Task<WeatherResult> GetWeatherDataAsync(WeatherRequest request)
    {
        if (!IsAvailable())
            return new WeatherResult { Success = false, ErrorMessage = _availabilityReason };

        try
        {
            string dateStr = request.Date.ToString("yyyy-MM-dd");
            string url = $"https://weather.visualcrossing.com/VisualCrossingWebServices/rest/services/timeline/" +
                        $"{request.Latitude.ToString(CultureInfo.InvariantCulture)},{request.Longitude.ToString(CultureInfo.InvariantCulture)}/" +
                        $"{dateStr}?unitGroup=metric&include=days&key={_apiKey}&contentType=json";

            var response = await _httpClient.GetAsync(url);
            
            if (!response.IsSuccessStatusCode)
                return new WeatherResult { Success = false, ErrorMessage = $"HTTP {response.StatusCode}" };

            var json = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize(json, WeatherJsonContext.Default.VisualCrossingResponse);
            
            if (data?.days == null || data.days.Count == 0)
                return new WeatherResult { Success = false, ErrorMessage = "Нет данных" };

            var day = data.days[0];
            
            // ПРЕОБРАЗОВАНИЕ ЕДИНИЦ ИЗМЕРЕНИЯ:
            // 1. Давление: Visual Crossing возвращает в гПа -> переводим в мм рт.ст.
            double pressureMmHg = day.pressure * HPA_TO_MMHG;
            
            // 2. Скорость ветра: Visual Crossing возвращает в км/ч -> переводим в м/с
            double windSpeedMs = day.windspeed / KMH_TO_MS;
            
            // 3. Осадки: уже в мм (совпадает)
            // 4. Температура: уже в °C (совпадает)
            // 5. Влажность: уже в % (совпадает)
            
            // Получаем английское описание и переводим на русский
            string englishCondition = day.conditions ?? day.description ?? "Неизвестно";
            string russianCondition = TranslateWeatherCondition(englishCondition);
            
            return new WeatherResult
            {
                Success = true,
                DataSource = ServiceName,
                Data = new WeatherData
                {
                    Date = request.Date,
                    City = request.CityName,
                    Latitude = Math.Round(data.latitude, 5),
                    Longitude = Math.Round(data.longitude, 5),
                    Timezone = data.timezone ?? request.Timezone,
                    MinTemperature = Math.Round(day.tempmin, 1),
                    MaxTemperature = Math.Round(day.tempmax, 1),
                    Precipitation = Math.Round(day.precip, 1),
                    Humidity = Math.Round(day.humidity, 1),
                    Pressure = Math.Round(pressureMmHg, 0),      // мм рт.ст., целое число
                    WindSpeed = Math.Round(windSpeedMs, 1),      // м/с, один знак после запятой
                    WeatherCondition = russianCondition,
                    LocalDateTime = DateTime.Now,
                    DataSource = ServiceName
                }
            };
        }
        catch (Exception ex)
        {
            return new WeatherResult { Success = false, ErrorMessage = ex.Message };
        }
    }
}

// ============================================================
// БЛОК 5: OPEN-METEO API - РЕЗЕРВНЫЙ ИСТОЧНИК ДАННЫХ
// ============================================================

/// <summary>Резервный API погоды. Бесплатный, не требует ключа</summary>
public class OpenMeteoService : IWeatherApiService
{
    private readonly HttpClient _httpClient;
    private bool _isAvailable = true;
    
    public string ServiceName => "OpenMeteo";

    public OpenMeteoService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("🌐 Open-Meteo API: доступен (РЕЗЕРВНЫЙ)");
        Console.ResetColor();
    }

    public bool IsAvailable() => _isAvailable;

    /// <summary>Получение данных о погоде из Open-Meteo</summary>
    public async Task<WeatherResult> GetWeatherDataAsync(WeatherRequest request)
    {
        if (!_isAvailable)
            return new WeatherResult { Success = false, ErrorMessage = "Open-Meteo недоступен" };

        try
        {
            string url = $"https://archive-api.open-meteo.com/v1/archive?" +
                        $"latitude={request.Latitude.ToString("F5", CultureInfo.InvariantCulture)}&" +
                        $"longitude={request.Longitude.ToString("F5", CultureInfo.InvariantCulture)}&" +
                        $"start_date={request.Date:yyyy-MM-dd}&end_date={request.Date:yyyy-MM-dd}&" +
                        $"hourly=temperature_2m,relativehumidity_2m,precipitation,pressure_msl,windspeed_10m,weathercode&" +
                        $"timezone={request.Timezone}";

            var response = await _httpClient.GetAsync(url);
            
            if (!response.IsSuccessStatusCode)
            {
                _isAvailable = false;
                return new WeatherResult { Success = false, ErrorMessage = $"HTTP {response.StatusCode}" };
            }

            var json = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize(json, WeatherJsonContext.Default.OpenMeteoResponse);
            
            if (data?.hourly == null || data.hourly.temperature_2m.Count == 0)
                return new WeatherResult { Success = false, ErrorMessage = "Нет данных" };

            const double HPA_TO_MMHG = 0.75006375541921;
            const double KMH_TO_MS = 3.6;

            return new WeatherResult
            {
                Success = true,
                DataSource = ServiceName,
                Data = new WeatherData
                {
                    Date = request.Date,
                    City = request.CityName,
                    Latitude = Math.Round(data.latitude, 5),
                    Longitude = Math.Round(data.longitude, 5),
                    Timezone = data.timezone,
                    MinTemperature = Math.Round(data.hourly.temperature_2m.Min(), 1),
                    MaxTemperature = Math.Round(data.hourly.temperature_2m.Max(), 1),
                    Precipitation = Math.Round(data.hourly.precipitation.Sum(), 1),
                    Humidity = Math.Round(data.hourly.relativehumidity_2m.Average(), 1),
                    Pressure = Math.Round(data.hourly.pressure_msl.Average() * HPA_TO_MMHG, 1),
                    WindSpeed = Math.Round(data.hourly.windspeed_10m.Average() / KMH_TO_MS, 1),
                    WeatherCondition = GetWeatherDescription(GetMostFrequentCode(data.hourly.weathercode)),
                    LocalDateTime = DateTime.Now,
                    DataSource = ServiceName
                }
            };
        }
        catch (Exception ex)
        {
            _isAvailable = false;
            return new WeatherResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    private static int GetMostFrequentCode(List<int> codes) =>
        codes?.GroupBy(x => x).OrderByDescending(g => g.Count()).First().Key ?? 0;

    private static string GetWeatherDescription(int code) => code switch
    {
        0 => "Ясно", 1 => "Преимущественно ясно", 2 => "Переменная облачность",
        3 => "Пасмурно", 45 or 48 => "Туман", 51 or 53 or 55 => "Морось",
        56 or 57 => "Ледяная морось", 61 or 63 or 65 => "Дождь", 66 or 67 => "Ледяной дождь",
        71 or 73 or 75 => "Снег", 77 => "Снежная крупа", 80 or 81 or 82 => "Ливень",
        85 or 86 => "Снегопад", 95 => "Гроза", 96 or 99 => "Гроза с градом",
        _ => "Неизвестно"
    };
}

// ============================================================
// БЛОК 6: WEATHER SERVICE - ОРКЕСТРАТОР API СЕРВИСОВ
// ============================================================

/// <summary>Управляет API сервисами: сначала Visual Crossing, при ошибке - Open-Meteo</summary>
public class WeatherService
{
    private readonly VisualCrossingService _primary;
    private readonly OpenMeteoService _fallback;

    public WeatherService(HttpClient httpClient)
    {
        _primary = new VisualCrossingService(httpClient);
        _fallback = new OpenMeteoService(httpClient);
    }

    /// <summary>Получает данные: сначала основной API, при ошибке - резервный</summary>
    public async Task<WeatherResult> GetWeatherDataAsync(WeatherRequest request)
    {
        // Сначала пробуем Visual Crossing (основной)
        Console.WriteLine($"   📡 Пробуем {_primary.ServiceName} (ОСНОВНОЙ)...");
        var result = await _primary.GetWeatherDataAsync(request);
        
        if (result.Success)
        {
            Console.WriteLine($"   ✅ Данные от {_primary.ServiceName}");
            return result;
        }
        
        Console.WriteLine($"   ❌ {_primary.ServiceName}: {result.ErrorMessage}");
        
        // Если основной не сработал, пробуем Open-Meteo (резервный)
        Console.WriteLine($"   📡 Пробуем {_fallback.ServiceName} (РЕЗЕРВНЫЙ)...");
        result = await _fallback.GetWeatherDataAsync(request);
        
        if (result.Success)
        {
            Console.WriteLine($"   ✅ Данные от {_fallback.ServiceName} (резервный)");
            return result;
        }
        
        Console.WriteLine($"   ❌ {_fallback.ServiceName}: {result.ErrorMessage}");
        return new WeatherResult { Success = false, ErrorMessage = "Все сервисы недоступны" };
    }
}

// ============================================================
// БЛОК 7: GEOCODING SERVICE - ПОИСК КООРДИНАТ ГОРОДА
// ============================================================

/// <summary>Находит координаты и часовой пояс по названию города</summary>
/// <remarks>Сначала использует Visual Crossing Geocoding (требует ключ), 
/// при ошибке - Open-Meteo Geocoding (бесплатный)</remarks>
public class GeocodingService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private bool _visualCrossingAvailable;

    public GeocodingService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _apiKey = LoadApiKey();
        _visualCrossingAvailable = !string.IsNullOrEmpty(_apiKey);
        
        PrintStatus();
    }

    /// <summary>Загружает API ключ Visual Crossing из файла</summary>
    private string LoadApiKey()
    {
        string filePath = "visualcrossing-api-key.txt";
        if (!File.Exists(filePath)) return string.Empty;
        
        try
        {
            string key = File.ReadAllText(filePath).Trim();
            return string.IsNullOrEmpty(key) || key.Length < 10 ? string.Empty : key;
        }
        catch { return string.Empty; }
    }

    /// <summary>Выводит статус геокодинг сервисов</summary>
    private void PrintStatus()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        if (_visualCrossingAvailable)
        {
            Console.WriteLine($"🗺️  Visual Crossing Geocoding: доступен (ОСНОВНОЙ, ключ: {_apiKey.Substring(0, Math.Min(8, _apiKey.Length))}...)");
        }
        else
        {
            Console.WriteLine($"🗺️  Visual Crossing Geocoding: НЕДОСТУПЕН (нет ключа)");
        }
        Console.WriteLine($"🗺️  Open-Meteo Geocoding: доступен (РЕЗЕРВНЫЙ)");
        Console.ResetColor();
    }

    /// <summary>Получает информацию о городе по названию</summary>
    /// <remarks>Сначала пробует Visual Crossing, при ошибке - Open-Meteo</remarks>
    public async Task<CityInfo?> GetCityCoordinatesAsync(string cityName)
    {
        // Сначала пробуем Visual Crossing Geocoding (основной)
        if (_visualCrossingAvailable)
        {
            Console.WriteLine($"   📡 Геокодинг: пробуем Visual Crossing...");
            var result = await GetFromVisualCrossingAsync(cityName);
            if (result != null)
            {
                Console.WriteLine($"   ✅ Геокодинг: Visual Crossing");
                return result;
            }
            Console.WriteLine($"   ❌ Геокодинг: Visual Crossing не ответил");
        }
        
        // Если Visual Crossing не сработал, пробуем Open-Meteo (резервный)
        Console.WriteLine($"   📡 Геокодинг: пробуем Open-Meteo...");
        var fallbackResult = await GetFromOpenMeteoAsync(cityName);
        if (fallbackResult != null)
        {
            Console.WriteLine($"   ✅ Геокодинг: Open-Meteo (резервный)");
            return fallbackResult;
        }
        
        Console.WriteLine($"   ❌ Геокодинг: все сервисы недоступны");
        return null;
    }

    /// <summary>Геокодинг через Visual Crossing API (требует ключ)</summary>
    private async Task<CityInfo?> GetFromVisualCrossingAsync(string cityName)
    {
        try
        {
            // Документация Visual Crossing Geocoding:
            // https://www.visualcrossing.com/resources/documentation/weather-api/timeline-geocoding/
            string url = $"https://weather.visualcrossing.com/VisualCrossingWebServices/rest/services/timeline/" +
                        $"{Uri.EscapeDataString(cityName)}?unitGroup=metric&key={_apiKey}&contentType=json";
            
            var response = await _httpClient.GetAsync(url);
            
            if (!response.IsSuccessStatusCode)
                return null;
            
            var json = await response.Content.ReadAsStringAsync();
            
            // Visual Crossing возвращает структуру с полями resolvedAddress, latitude, longitude, timezone
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            
            // Извлекаем координаты
            if (!root.TryGetProperty("latitude", out var latElement) ||
                !root.TryGetProperty("longitude", out var lonElement))
                return null;
            
            double latitude = latElement.GetDouble();
            double longitude = lonElement.GetDouble();
            
            // Извлекаем часовой пояс
            string timezone = root.TryGetProperty("timezone", out var tzElement) 
                ? tzElement.GetString() ?? string.Empty 
                : string.Empty;
            
            // Извлекаем адрес (город, страна)
            string resolvedAddress = root.TryGetProperty("resolvedAddress", out var addrElement)
                ? addrElement.GetString() ?? cityName
                : cityName;
            
            // Парсим адрес для получения названия города и страны
            string city = cityName;
            string country = string.Empty;
            
            // resolvedAddress обычно имеет формат "City, State, Country" или "City, Country"
            var parts = resolvedAddress.Split(',');
            if (parts.Length > 0)
                city = parts[0].Trim();
            if (parts.Length > 1)
                country = parts[^1].Trim(); // последняя часть - страна
            
            return new CityInfo
            {
                Name = city,
                Latitude = Math.Round(latitude, 5),
                Longitude = Math.Round(longitude, 5),
                Timezone = timezone,
                Country = country,
                AdminRegion = parts.Length > 2 ? parts[1].Trim() : string.Empty,
                Elevation = 0, // Visual Crossing не возвращает высоту в этом запросе
                Population = 0  // Visual Crossing не возвращает население
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Геокодинг через Open-Meteo API (резервный, бесплатный)</summary>
    private async Task<CityInfo?> GetFromOpenMeteoAsync(string cityName)
    {
        try
        {
            string url = $"https://geocoding-api.open-meteo.com/v1/search?name={Uri.EscapeDataString(cityName)}&count=1&language=ru&format=json";
            var response = await _httpClient.GetAsync(url);
            
            if (!response.IsSuccessStatusCode) return null;
            
            var json = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize(json, WeatherJsonContext.Default.GeocodingResponse);
            
            if (data?.results == null || data.results.Count == 0) return null;
            
            var r = data.results[0];
            return new CityInfo
            {
                Name = r.name,
                Latitude = r.latitude,
                Longitude = r.longitude,
                Timezone = r.timezone,
                Country = r.country,
                AdminRegion = r.admin1,
                Elevation = r.elevation,
                Population = r.population
            };
        }
        catch
        {
            return null;
        }
    }
}

// ============================================================
// БЛОК 8: JOURNAL SERVICE - РАБОТА С ФАЙЛАМИ ДНЕВНИКА
// ============================================================

/// <summary>Сохранение и загрузка данных из weather_journal.json и config.cfg</summary>
public class JournalService
{
    private const string JOURNAL_FILE = "weather_journal.json";
    private const string CONFIG_FILE = "config.cfg";

    /// <summary>Загружает все записи из дневника</summary>
    public async Task<List<WeatherData>> LoadJournalAsync()
    {
        if (!File.Exists(JOURNAL_FILE)) return new List<WeatherData>();
        
        try
        {
            var json = await File.ReadAllTextAsync(JOURNAL_FILE);
            return string.IsNullOrWhiteSpace(json) 
                ? new List<WeatherData>()
                : JsonSerializer.Deserialize(json, WeatherJsonContext.Default.ListWeatherData) ?? new List<WeatherData>();
        }
        catch
        {
            // При повреждении файла создаем резервную копию
            string backupFile = JOURNAL_FILE + ".backup." + DateTime.Now.ToString("yyyyMMddHHmmss");
            File.Copy(JOURNAL_FILE, backupFile, true);
            Console.WriteLine($"   📦 Создана резервная копия: {backupFile}");
            return new List<WeatherData>();
        }
    }

    /// <summary>Сохраняет запись (обновляет, если уже есть за эту дату)</summary>
    public async Task<bool> SaveWeatherDataAsync(WeatherData data)
    {
        try
        {
            var journal = await LoadJournalAsync();
            journal.RemoveAll(w => w.Date.Date == data.Date.Date && w.City == data.City);
            journal.Add(data);
            journal = journal.OrderBy(w => w.Date).ToList();
            
            var json = JsonSerializer.Serialize(journal, WeatherJsonContext.Default.ListWeatherData);
            await File.WriteAllTextAsync(JOURNAL_FILE, json);
            Console.WriteLine($"   💾 Запись за {data.Date:dd.MM.yyyy} сохранена");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   ❌ Ошибка сохранения: {ex.Message}");
            return false;
        }
    }

    /// <summary>Ищет запись в дневнике по городу и дате</summary>
    public async Task<WeatherData?> GetWeatherFromJournalAsync(string city, DateTime date)
    {
        var journal = await LoadJournalAsync();
        return journal.FirstOrDefault(w => w.City.Equals(city, StringComparison.OrdinalIgnoreCase) && w.Date.Date == date.Date);
    }

    /// <summary>Получает все записи для указанного города (отсортированные по убыванию даты)</summary>
    public async Task<List<WeatherData>> GetCityJournalAsync(string city)
    {
        var journal = await LoadJournalAsync();
        return journal.Where(w => w.City.Equals(city, StringComparison.OrdinalIgnoreCase))
                      .OrderByDescending(w => w.Date)
                      .ToList();
    }

    /// <summary>Загружает конфигурацию из config.cfg</summary>
    public async Task<Config> LoadConfigAsync()
    {
        var config = new Config();
        if (!File.Exists(CONFIG_FILE)) return config;
        
        try
        {
            var content = await File.ReadAllTextAsync(CONFIG_FILE);
            var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length > 0) config.LastCity = lines[0].Trim();
            if (lines.Length > 1) config.LastDataSource = lines[1].Trim();
        }
        catch { }
        return config;
    }

    /// <summary>Сохраняет конфигурацию в config.cfg</summary>
    public async Task SaveConfigAsync(Config config)
    {
        try
        {
            await File.WriteAllTextAsync(CONFIG_FILE, $"{config.LastCity}\n{config.LastDataSource}");
        }
        catch { }
    }
}

// ============================================================
// БЛОК 9: ОСНОВНАЯ БИЗНЕС-ЛОГИКА - ДЛЯ ИСПОЛЬЗОВАНИЯ ИЗ GUI
// ============================================================

/// <summary>Главный класс приложения. Все методы можно вызывать из GUI</summary>
public class WeatherDiaryLogic
{
    private readonly WeatherService _weatherService;
    private readonly GeocodingService _geocodingService;
    private readonly JournalService _journalService;
    private CityInfo? _currentCity;
    private Config _currentConfig;

    public CityInfo? CurrentCity => _currentCity;
    public Config CurrentConfig => _currentConfig;
    public event Action<string>? OnLogMessage;

    public WeatherDiaryLogic()
    {
        var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _weatherService = new WeatherService(httpClient);
        _geocodingService = new GeocodingService(httpClient);
        _journalService = new JournalService();
        _currentConfig = new Config();
    }

    /// <summary>Инициализация: загрузка конфигурации и последнего города</summary>
    public async Task<bool> InitializeAsync()
    {
        Log("Инициализация...");
        _currentConfig = await _journalService.LoadConfigAsync();
        
        if (!string.IsNullOrEmpty(_currentConfig.LastCity))
        {
            Log($"Загружен город: {_currentConfig.LastCity}");
            await SetCityAsync(_currentConfig.LastCity);
        }
        return _currentCity != null;
    }

    /// <summary>Пункт меню 1: Погода на сегодня</summary>
    public async Task<WeatherResult> GetTodayWeatherAsync() =>
        _currentCity == null 
            ? new WeatherResult { Success = false, ErrorMessage = "Город не выбран" }
            : await GetWeatherForDateAsync(GetLocalDate());

    /// <summary>Пункт меню 2: Погода на указанную дату</summary>
    public async Task<WeatherResult> GetWeatherForDateAsync(DateTime date)
    {
        if (_currentCity == null)
            return new WeatherResult { Success = false, ErrorMessage = "Город не выбран" };

        // Сначала проверяем дневник
        var journalEntry = await _journalService.GetWeatherFromJournalAsync(_currentCity.Name, date);
        if (journalEntry != null)
            return new WeatherResult { Success = true, Data = journalEntry };

        // Запрашиваем из API
        var request = new WeatherRequest
        {
            CityName = _currentCity.Name,
            Date = date,
            Latitude = _currentCity.Latitude,
            Longitude = _currentCity.Longitude,
            Timezone = _currentCity.Timezone
        };

        var result = await _weatherService.GetWeatherDataAsync(request);
        
        if (result.Success && result.Data != null)
        {
            await _journalService.SaveWeatherDataAsync(result.Data);
            _currentConfig.LastDataSource = result.DataSource;
            await _journalService.SaveConfigAsync(_currentConfig);
        }
        
        return result;
    }

    /// <summary>Пункт меню 3: Сменить город</summary>
    public async Task<bool> SetCityAsync(string cityName)
    {
        Log($"Поиск города '{cityName}'...");
        var cityInfo = await _geocodingService.GetCityCoordinatesAsync(cityName);
        
        if (cityInfo == null)
        {
            Log($"Город '{cityName}' не найден");
            return false;
        }

        _currentCity = cityInfo;
        _currentConfig.LastCity = cityInfo.Name;
        await _journalService.SaveConfigAsync(_currentConfig);
        
        Log($"Город: {cityInfo.Name}, {cityInfo.Country}");
        Log($"Координаты: {cityInfo.Latitude:F5}, {cityInfo.Longitude:F5}");
        return true;
    }

    /// <summary>Пункт меню 4: Получить дневник для текущего города</summary>
    public async Task<List<WeatherData>> GetCityJournalAsync() =>
        _currentCity == null ? new List<WeatherData>() : await _journalService.GetCityJournalAsync(_currentCity.Name);

    /// <summary>Статистика по городу</summary>
    public async Task<CityStatistics> GetCityStatisticsAsync()
    {
        var journal = await GetCityJournalAsync();
        if (journal.Count == 0) return new CityStatistics();
        
        return new CityStatistics
        {
            TotalEntries = journal.Count,
            FirstDate = journal.Min(w => w.Date),
            LastDate = journal.Max(w => w.Date),
            MinTemperature = journal.Min(w => w.MinTemperature),
            MaxTemperature = journal.Max(w => w.MaxTemperature),
            MaxPrecipitation = journal.Max(w => w.Precipitation),
            MaxPressure = journal.Max(w => w.Pressure),
            MaxWindSpeed = journal.Max(w => w.WindSpeed)
        };
    }

    /// <summary>Текущая местная дата в городе</summary>
    public DateTime GetLocalDate()
    {
        if (string.IsNullOrEmpty(_currentCity?.Timezone)) return DateTime.Today;
        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(_currentCity.Timezone);
            return TimeZoneInfo.ConvertTime(DateTime.UtcNow, tz).Date;
        }
        catch { return DateTime.Today; }
    }

    /// <summary>Эмодзи для погоды</summary>
    public string GetWeatherEmoji(string condition) => condition switch
    {
        "Ясно" => "☀️", "Преимущественно ясно" => "🌤️", "Переменная облачность" => "⛅",
        "Пасмурно" => "☁️", "Туман" => "🌫️", var c when c.Contains("морось") => "🌧️",
        var c when c.Contains("дождь") => "☔", var c when c.Contains("ливень") => "💧",
        var c when c.Contains("снег") => "❄️", var c when c.Contains("снегопад") => "🌨️",
        var c when c.Contains("гроза") => "⛈️", _ => "🌡️"
    };

    private void Log(string message)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}");
        Console.ResetColor();
        OnLogMessage?.Invoke(message);
    }
}

// ============================================================
// БЛОК 10: КОНСОЛЬНЫЙ ИНТЕРФЕЙС
// ============================================================

public class WeatherDiaryApp
{
    private static readonly WeatherDiaryLogic _logic = new();

    public static async Task Run()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.Title = "Дневник погоды";
        
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.WriteLine("======================================");
        Console.WriteLine("       ДНЕВНИК ПОГОДЫ v2.0");
        Console.WriteLine("======================================");
        Console.ResetColor();
        
        await _logic.InitializeAsync();
        
        if (_logic.CurrentCity == null)
        {
            Console.WriteLine("\nДОБРО ПОЖАЛОВАТЬ!");
            await ChangeCityAsync();
        }
        
        while (true)
        {
            ShowMainMenu();
            var choice = Console.ReadLine()?.Trim();
            
            switch (choice)
            {
                case "1": await ShowTodayWeather(); break;
                case "2": await ShowCustomDateWeather(); break;
                case "3": await ChangeCityAsync(); break;
                case "4": await ShowJournal(); break;
                case "5":
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine("\nДо свидания!");
                    Console.ResetColor();
                    return;
                default:
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Неверный выбор. Нажмите 1-5");
                    Console.ResetColor();
                    break;
            }
        }
    }

    private static void ShowMainMenu()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("\n========== ГЛАВНОЕ МЕНЮ ==========");
        Console.ResetColor();
        
        if (_logic.CurrentCity != null)
        {
            Console.WriteLine($"\nТекущий город: {_logic.CurrentCity.Name}, {_logic.CurrentCity.Country}");
            Console.WriteLine($"Местная дата: {_logic.GetLocalDate():dd.MM.yyyy}");
        }
        else Console.WriteLine("\nГород не выбран");
        
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("\n1. Погода на сегодня");
        Console.WriteLine("2. Погода на дату");
        Console.WriteLine("3. Изменить город");
        Console.WriteLine("4. Просмотреть дневник");
        Console.WriteLine("5. Выход");
        Console.ResetColor();
        Console.Write("\nВаш выбор: ");
    }

    private static async Task ShowTodayWeather()
    {
        var result = await _logic.GetTodayWeatherAsync();
        DisplayWeatherResult(result);
    }

    private static async Task ShowCustomDateWeather()
    {
        Console.Write("\nВведите дату (ДД.ММ.ГГГГ): ");
        var input = Console.ReadLine()?.Trim();
        
        if (DateTime.TryParseExact(input, new[] { "dd.MM.yyyy", "d.M.yyyy" }, 
            CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date))
        {
            var result = await _logic.GetWeatherForDateAsync(date);
            DisplayWeatherResult(result);
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Неверный формат даты");
            Console.ResetColor();
        }
    }

    private static async Task ChangeCityAsync()
    {
        Console.Write("\nВведите название города: ");
        var cityName = Console.ReadLine()?.Trim();
        
        if (string.IsNullOrEmpty(cityName))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Название не может быть пустым");
            Console.ResetColor();
            return;
        }
        
        await _logic.SetCityAsync(cityName);
    }

    private static async Task ShowJournal()
    {
        var journal = await _logic.GetCityJournalAsync();
        var stats = await _logic.GetCityStatisticsAsync();
        
        if (journal.Count == 0)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\nДневник пуст");
            Console.ResetColor();
            return;
        }
        
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.WriteLine($"\n{new string('=', 100)}");
        Console.WriteLine($"ДНЕВНИК ПОГОДЫ - {_logic.CurrentCity?.Name?.ToUpper()}");
        Console.WriteLine($"(всего записей: {journal.Count})");
        Console.WriteLine($"{new string('=', 100)}");
        Console.ResetColor();
        
        Console.WriteLine($"{"Дата",-12} {"Мин T",-8} {"Макс T",-8} {"Осадки",-8} {"Влажн.",-8} {"Давл.",-8} {"Ветер",-8} {"Источник",-15} {"Явления"}");
        Console.WriteLine(new string('-', 100));
        
        foreach (var w in journal)
        {
            string emoji = _logic.GetWeatherEmoji(w.WeatherCondition);
            Console.WriteLine($"{w.Date:dd.MM.yyyy} {w.MinTemperature,6:F1}°C {w.MaxTemperature,6:F1}°C {w.Precipitation,6:F1}мм {w.Humidity,6:F0}% {w.Pressure,6:F0}мм {w.WindSpeed,6:F1}м/с {w.DataSource,-15} {w.WeatherCondition} {emoji}");
        }
        
        Console.WriteLine(new string('-', 100));
        
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"\nСТАТИСТИКА ПО ГОРОДУ {_logic.CurrentCity?.Name?.ToUpper()}:");
        Console.WriteLine($"Период: {stats.FirstDate:dd.MM.yyyy} - {stats.LastDate:dd.MM.yyyy}");
        Console.WriteLine($"Мин. температура: {stats.MinTemperature:F1}°C");
        Console.WriteLine($"Макс. температура: {stats.MaxTemperature:F1}°C");
        Console.WriteLine($"Макс. осадки: {stats.MaxPrecipitation:F1} мм");
        Console.WriteLine($"Макс. давление: {stats.MaxPressure:F0} мм рт.ст.");
        Console.WriteLine($"Макс. ветер: {stats.MaxWindSpeed:F1} м/с");
        Console.ResetColor();
        
        Console.WriteLine("\nНажмите любую клавишу...");
        Console.ReadKey();
    }

    private static void DisplayWeatherResult(WeatherResult result)
    {
        if (!result.Success || result.Data == null)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\nОшибка: {result.ErrorMessage}");
            Console.ResetColor();
            Console.WriteLine("\nНажмите любую клавишу...");
            Console.ReadKey();
            return;
        }
        
        string emoji = _logic.GetWeatherEmoji(result.Data.WeatherCondition);
        
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"\n{new string('=', 60)}");
        Console.WriteLine($"{result.Data.City.ToUpper()} - {result.Data.Date:dd.MM.yyyy} {emoji}");
        Console.WriteLine($"{new string('=', 60)}");
        Console.ResetColor();
        
        Console.WriteLine($"Температура: {result.Data.MinTemperature:F1}°C ... {result.Data.MaxTemperature:F1}°C");
        Console.WriteLine($"Осадки: {result.Data.Precipitation:F1} мм");
        Console.WriteLine($"Влажность: {result.Data.Humidity:F0}%");
        Console.WriteLine($"Давление: {result.Data.Pressure:F0} мм рт.ст.");
        Console.WriteLine($"Ветер: {result.Data.WindSpeed:F1} м/с");
        Console.WriteLine($"Явления: {result.Data.WeatherCondition} {emoji}");
        
        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.WriteLine($"Источник: {result.Data.DataSource}");
        Console.ResetColor();
        
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"{new string('=', 60)}");
        Console.ResetColor();
        
        Console.WriteLine("\nНажмите любую клавишу...");
        Console.ReadKey();
    }
}

// ============================================================
// БЛОК 11: ТОЧКА ВХОДА
// ============================================================

public static class Program
{
    public static async Task Main() => await WeatherDiaryApp.Run();
}