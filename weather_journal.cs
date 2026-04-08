// Weather-journal application
using System; // Базовые типы и функции (консоль, исключения, даты)
using System.IO; // Работа с файлами (чтение/запись)
using System.Net.Http; // Отправка HTTP-запросов к API погоды и геокодинга
using System.Text.Json; // Сериализация/десериализация JSON (современный аналог Newtonsoft.Json)
using System.Text.Json.Serialization; // Атрибуты для настройки JSON сериализации
using System.Collections.Generic; // Коллекции (List, Dictionary)
using System.Linq; // LINQ-запросы для работы с коллекциями (Where, Select, Average)
using System.Globalization; // Форматирование чисел с учетом культур (InvariantCulture для точки)

// ========== 1. КЛАССЫ ДАННЫХ ==========
// Класс для хранения конфигурации (теперь используется только для совместимости с JSON контекстом)
public class Config
{
    // Auto-property с инициализацией пустой строкой для предотвращения null
    public string LastCity { get; set; } = string.Empty;
}

// Класс для хранения данных о погоде
// Содержит все параметры, которые сохраняются в дневнике
public class WeatherData
{
    public DateTime Date { get; set; }           // Дата наблюдения (без времени)
    public string City { get; set; } = string.Empty; // Название города (для фильтрации записей)
    public double Latitude { get; set; }         // Широта (для повторных API запросов)
    public double Longitude { get; set; }        // Долгота (для повторных API запросов)
    public string Timezone { get; set; } = string.Empty; // Часовой пояс (IANA, например "Europe/Moscow")
    public double MinTemperature { get; set; }   // Минимальная температура за день (°C)
    public double MaxTemperature { get; set; }   // Максимальная температура за день (°C)
    public double Precipitation { get; set; }    // Сумма осадков за день (мм)
    public double Humidity { get; set; }         // Средняя влажность (%)
    public double Pressure { get; set; }         // Среднее давление (мм рт.ст.)
    public double WindSpeed { get; set; }        // Средняя скорость ветра (м/с)
    public string WeatherCondition { get; set; } = string.Empty; // Текстовое описание погоды
    public DateTime LocalDateTime { get; set; }  // Момент получения данных (местное время города)
}

// Класс для ответа от Geocoding API (поиск координат по названию города)
public class GeocodingResponse
{
    // Список найденных городов (инициализируется пустым списком)
    public List<GeocodingResult> results { get; set; } = new();
}

// Класс с детальной информацией о найденном городе от Geocoding API
public class GeocodingResult
{
    // Имена свойств соответствуют JSON-ответу от API (в camelCase)
    public int id { get; set; }                  // Уникальный ID города в базе Open-Meteo
    public string name { get; set; } = string.Empty; // Официальное название города
    public double latitude { get; set; }         // Широта (десятичные градусы)
    public double longitude { get; set; }        // Долгота (десятичные градусы)
    public double elevation { get; set; }        // Высота над уровнем моря (метры)
    public string feature_code { get; set; } = string.Empty; // Тип (PPLC - столица, PPL - город и т.д.)
    public string country_code { get; set; } = string.Empty; // Двухбуквенный код страны (RU, US)
    public string timezone { get; set; } = string.Empty; // Часовой пояс (IANA)
    public int population { get; set; }          // Население (для статистики)
    public string country { get; set; } = string.Empty; // Название страны
    public string admin1 { get; set; } = string.Empty; // Первый уровень административного деления (область/штат)
}

// Модель для Open-Meteo API (прогноз и архив погоды)
public class OpenMeteoResponse
{
    // Координаты, которые реально использовал API (могут немного отличаться от запрошенных)
    public double latitude { get; set; }
    public double longitude { get; set; }
    public double elevation { get; set; }        // Высота точки
    public string timezone { get; set; } = string.Empty; // Использованный часовой пояс
    public string timezone_abbreviation { get; set; } = string.Empty; // Сокращение (MSK, GMT)
    public int utc_offset_seconds { get; set; }  // Смещение от UTC в секундах
    public HourlyUnits hourly_units { get; set; } = new(); // Единицы измерения (для отображения)
    public HourlyData hourly { get; set; } = new(); // Почасовые данные
}

// Класс с единицами измерения для почасовых данных
public class HourlyUnits
{
    // Все свойства - строки, содержащие единицы измерения (°C, mm, %)
    public string time { get; set; } = string.Empty;
    public string temperature_2m { get; set; } = string.Empty;
    public string relativehumidity_2m { get; set; } = string.Empty;
    public string precipitation { get; set; } = string.Empty;
    public string pressure_msl { get; set; } = string.Empty; // pressure at mean sea level
    public string windspeed_10m { get; set; } = string.Empty; // скорость ветра на высоте 10м
    public string weathercode { get; set; } = string.Empty; // WMO код погоды
}

// Класс с почасовыми данными погоды
public class HourlyData
{
    // Все списки синхронизированы по индексу (time[0] соответствует temperature_2m[0])
    public List<string> time { get; set; } = new();           // ISO строки времени
    public List<double> temperature_2m { get; set; } = new(); // Температура на высоте 2м
    public List<int> relativehumidity_2m { get; set; } = new(); // Относительная влажность
    public List<double> precipitation { get; set; } = new();  // Осадки (дождь/снег)
    public List<double> pressure_msl { get; set; } = new();   // Давление на уровне моря (гПа)
    public List<double> windspeed_10m { get; set; } = new();  // Скорость ветра (км/ч)
    public List<int> weathercode { get; set; } = new();       // Код погоды WMO
}

// ========== 2. КОНТЕКСТ ГЕНЕРАЦИИ JSON ==========
// Этот класс необходим для source generation в .NET 6+
// Он заменяет рефлексию при сериализации/десериализации JSON (повышает производительность)
[JsonSourceGenerationOptions(
    WriteIndented = true,           // Форматировать JSON с отступами (читаемо для человека)
    PropertyNameCaseInsensitive = true, // Игнорировать регистр при десериализации (name = Name)
    GenerationMode = JsonSourceGenerationMode.Default // Генерировать как метаданные, так и сериализаторы
)]
// Указываем все типы, которые будут сериализоваться в JSON (для генерации кода)
[JsonSerializable(typeof(Config))]
[JsonSerializable(typeof(WeatherData))]
[JsonSerializable(typeof(List<WeatherData>))]
[JsonSerializable(typeof(OpenMeteoResponse))]
[JsonSerializable(typeof(HourlyData))]
[JsonSerializable(typeof(HourlyUnits))]
[JsonSerializable(typeof(GeocodingResponse))]
[JsonSerializable(typeof(GeocodingResult))]
public partial class WeatherJsonContext : JsonSerializerContext
{
    // partial class - компилятор сгенерирует реализацию автоматически
}

// ========== 3. ОСНОВНОЙ КЛАСС ПРИЛОЖЕНИЯ ==========
public class WeatherDiaryApp
{
    // ========== КОНСТАНТЫ (ВСЕ В НАЧАЛЕ КЛАССА) ==========
    
    // HTTP клиент для отправки запросов к API
    // static - один экземпляр на все приложение (переиспользование соединений)
    private static readonly HttpClient httpClient = new HttpClient();
    
    // Имена файлов для хранения данных
    private const string CONFIG_FILE = "config.cfg";        // Текстовый файл с последним городом
    private const string JOURNAL_FILE = "weather_journal.json"; // JSON файл с дневником погоды
    
    // URL для API запросов
    private const string GEOCODING_API_URL = "https://geocoding-api.open-meteo.com/v1/search"; // Поиск координат
    private const string WEATHER_API_FORECAST_URL = "https://api.open-meteo.com/v1/forecast";  // Прогноз
    private const string WEATHER_API_ARCHIVE_URL = "https://archive-api.open-meteo.com/v1/archive"; // Архив
    
    // Параметры API запросов
    private const string HOURLY_PARAMETERS = "temperature_2m,relativehumidity_2m,precipitation,pressure_msl,windspeed_10m,weathercode";
    private const int FORECAST_DAYS = 1;                    // Запрашиваем только 1 день прогноза
    private const int COORDINATES_PRECISION = 5;            // Точность координат (знаков после запятой ~1 метр)
    private const double HPA_TO_MMHG = 0.75006375541921;    // 1 гПа = 0.75006375541921 мм рт.ст.
    private const double KMH_TO_MS = 3.6;                    // 1 м/с = 3.6 км/ч
    
    // Форматы дат
    private static readonly string[] DATE_FORMATS = { "dd.MM.yyyy", "d.M.yyyy", "dd.MM.yy", "d.M.yy" }; // Поддерживаемые форматы ввода
    private const string DATE_DISPLAY_FORMAT = "dd.MM.yyyy"; // Формат отображения даты
    private const string DATE_API_FORMAT = "yyyy-MM-dd";    // Формат даты для API (ISO)
    
    // Ширина столбцов таблицы (для красивого вывода)
    private const int COLUMN_DATE_WIDTH = 12;        // Дата (в формате ДД.ММ.ГГГГ)
    private const int COLUMN_TEMP_MIN_WIDTH = 6;     // Минимальная температура
    private const int COLUMN_TEMP_MAX_WIDTH = 7;     // Максимальная температура (увеличена на 1 символ для минуса)
    private const int COLUMN_PRECIP_WIDTH = 6;       // Осадки
    private const int COLUMN_HUMIDITY_WIDTH = 6;     // Влажность
    private const int COLUMN_PRESSURE_WIDTH = 6;     // Давление
    private const int COLUMN_WIND_WIDTH = 6;         // Ветер
    private const int COLUMN_CONDITION_WIDTH = 30;   // Явления (с запасом для длинных описаний)
    
    // Цвета для логирования
    private const ConsoleColor INFO_COLOR = ConsoleColor.Green;   // Информационные сообщения
    private const ConsoleColor ERROR_COLOR = ConsoleColor.Red;    // Ошибки
    private const ConsoleColor SUCCESS_COLOR = ConsoleColor.Yellow; // Успешные операции
    
    // ========== ПОЛЯ КЛАССА ==========
    
    // Текущее состояние приложения (хранятся в памяти)
    private static string currentCity = string.Empty;       // Текущий выбранный город
    private static double currentLatitude;                  // Широта текущего города
    private static double currentLongitude;                 // Долгота текущего города
    private static string currentTimezone = string.Empty;   // Часовой пояс текущего города
    private static DateTime currentLocalTime;                // Текущее местное время в городе
    
    // CultureInfo для форматирования чисел с точкой (требуется для API)
    // InvariantCulture гарантирует использование точки как разделителя (не запятой)
    private static readonly CultureInfo invariantCulture = CultureInfo.InvariantCulture;
    
    // Настройки JSON сериализации с source generation
    private static readonly JsonSerializerOptions jsonOptions = new JsonSerializerOptions
    {
        WriteIndented = true, // Красивый вывод
        TypeInfoResolver = WeatherJsonContext.Default, // Используем source generator
        PropertyNameCaseInsensitive = true // Игнорируем регистр
    };

    // ========== МЕТОДЫ ПРИЛОЖЕНИЯ ==========
    
    /// <summary>
    /// Главная точка входа в приложение
    /// </summary>
    public static void Run()
    {
        // Устанавливаем UTF-8 кодировку для поддержки эмодзи и русского языка
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        
        try // Обработка критических ошибок на верхнем уровне
        {
            LogInfo("Запуск приложения Дневник погоды");
            LoadConfiguration(); // Загружаем сохраненный город из config.cfg
            
            // Если город не загружен из конфига (первый запуск), запрашиваем сразу
            if (string.IsNullOrEmpty(currentCity))
            {
                Console.WriteLine("\n=== ДОБРО ПОЖАЛОВАТЬ! ===");
                Console.WriteLine("Для начала работы укажите ваш город.");
                ChangeCity(); // Запрашиваем город у пользователя
            }
            
            // Главный цикл приложения - показываем меню бесконечно
            while (true)
            {
                ShowMainMenu(); // Показ меню и обработка выбора
            }
        }
        catch (Exception ex) // Ловим любые исключения, которые не были обработаны
        {
            LogError($"Критическая ошибка: {ex.Message}");
        }
    }

    /// <summary>
    /// Загружает конфигурацию из текстового файла config.cfg
    /// Формат файла: просто первая строка содержит название города
    /// </summary>
    private static void LoadConfiguration()
    {
        try
        {
            // Проверяем существование файла конфигурации
            if (File.Exists(CONFIG_FILE))
            {
                // Читаем весь файл как строку и удаляем пробелы по краям
                string fileContent = File.ReadAllText(CONFIG_FILE).Trim();
                LogInfo($"Загружен файл конфигурации ({fileContent.Length} байт)");
                
                // Если файл пустой, выходим (оставляем currentCity пустым)
                if (string.IsNullOrWhiteSpace(fileContent))
                {
                    LogInfo("Файл конфигурации пуст.");
                    currentCity = string.Empty;
                    return;
                }
                
                // Разбиваем содержимое на строки и берем первую непустую строку
                // Split по \r и \n, удаляем пустые строки, берем первую, обрезаем пробелы
                currentCity = fileContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                                         .FirstOrDefault()?.Trim() ?? string.Empty;
                
                // Если город успешно прочитан, определяем его координаты
                if (!string.IsNullOrEmpty(currentCity))
                {
                    LogInfo($"✅ Конфигурация загружена. Город: {currentCity}");
                    
                    // Получаем координаты для сохраненного города (и обновляем время)
                    Console.WriteLine("🔍 Определение координат города...");
                    if (GetCityCoordinates(currentCity)) // Запрос к API геокодинга
                    {
                        LogInfo($"✅ Координаты определены: {currentLatitude.ToString("F5", invariantCulture)}, {currentLongitude.ToString("F5", invariantCulture)}, часовой пояс: {currentTimezone}");
                        // UpdateLocalTime() уже вызывается внутри GetCityCoordinates
                    }
                }
                else
                {
                    LogInfo("В конфигурации не указан город.");
                }
            }
            else
            {
                LogInfo("Файл конфигурации не найден.");
                currentCity = string.Empty; // Явно указываем, что город не выбран
            }
        }
        catch (Exception ex) // Ловим ошибки файловых операций
        {
            LogError($"Ошибка загрузки конфигурации: {ex.Message}");
            currentCity = string.Empty; // Сбрасываем город при ошибке
        }
    }

    /// <summary>
    /// Сохраняет текущий город в текстовый файл config.cfg
    /// Использует атомарную операцию: сначала запись во временный файл, затем замена
    /// </summary>
    private static void SaveConfiguration()
    {
        try
        {
            // Просто записываем название города в текстовый файл
            string tempFile = CONFIG_FILE + ".tmp"; // Временный файл в той же директории
            File.WriteAllText(tempFile, currentCity); // Запись
            LogInfo($"Конфигурация записана во временный файл: {tempFile}");
            
            // Атомарно заменяем основной файл временным (чтобы не потерять данные при сбое)
            if (File.Exists(CONFIG_FILE))
            {
                // Если основной файл существует, заменяем его (создается backup)
                File.Replace(tempFile, CONFIG_FILE, null); // null = не создавать backup
                LogInfo($"✅ Конфигурация обновлена: {CONFIG_FILE}");
            }
            else
            {
                // Если основного файла нет, просто переименовываем временный
                File.Move(tempFile, CONFIG_FILE);
                LogInfo($"✅ Конфигурация создана: {CONFIG_FILE}");
            }
            
            LogInfo($"Город сохранен: {currentCity}");
        }
        catch (Exception ex) // Ошибки записи/перемещения файлов
        {
            LogError($"❌ Ошибка сохранения конфигурации: {ex.Message}");
        }
    }

    /// <summary>
    /// Обновляет местное время для текущего города
    /// </summary>
    private static void UpdateLocalTime()
    {
        try
        {
            if (!string.IsNullOrEmpty(currentTimezone))
            {
                // Получаем объект часового пояса по IANA имени (например, "Europe/Moscow")
                TimeZoneInfo tz = TimeZoneInfo.FindSystemTimeZoneById(currentTimezone);
                // Конвертируем UTC время в местное время города
                currentLocalTime = TimeZoneInfo.ConvertTime(DateTime.UtcNow, tz);
                // Логирование убрано, чтобы не засорять консоль
            }
        }
        catch (Exception ex) // Если часовой пояс не найден в системе
        {
            LogError($"Ошибка обновления местного времени: {ex.Message}");
            currentLocalTime = DateTime.Now; // Fallback к системному времени (может отличаться)
        }
    }

    /// <summary>
    /// Получает текущую местную дату для города (без времени)
    /// </summary>
    private static DateTime GetCurrentLocalDate()
    {
        try
        {
            if (!string.IsNullOrEmpty(currentTimezone))
            {
                TimeZoneInfo tz = TimeZoneInfo.FindSystemTimeZoneById(currentTimezone);
                return TimeZoneInfo.ConvertTime(DateTime.UtcNow, tz).Date; // .Date отбрасывает время
            }
        }
        catch
        {
            // Игнорируем ошибку и возвращаем системную дату
        }
        return DateTime.Today; // Системная дата как fallback
    }

    /// <summary>
    /// Получает координаты и часовой пояс города через Geocoding API
    /// Вызывается при смене города или загрузке конфигурации
    /// </summary>
    /// <param name="cityName">Название города для поиска</param>
    /// <returns>true если город найден, false в противном случае</returns>
    private static bool GetCityCoordinates(string cityName)
    {
        try
        {
            // Формируем URL для запроса к Geocoding API
            // Uri.EscapeDataString экранирует специальные символы (пробелы, кириллицу)
            string url = $"{GEOCODING_API_URL}?name={Uri.EscapeDataString(cityName)}&count=1&language=ru&format=json";
            
            LogInfo($"🌐 Геокодинг запрос: {url}");
            Console.WriteLine("   Ожидание ответа от сервера...");
            
            // Отправляем синхронный GET запрос (GetAwaiter().GetResult() для синхронного вызова)
            var response = httpClient.GetAsync(url).GetAwaiter().GetResult();
            LogInfo($"   Статус ответа: {(int)response.StatusCode} {response.ReasonPhrase}");
            
            // Проверяем успешность запроса (выбросит исключение при 4xx/5xx)
            response.EnsureSuccessStatusCode();
            
            // Читаем тело ответа как строку
            var json = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            LogInfo($"   Получено {json.Length} байт данных");
            
            // Десериализуем JSON в объект GeocodingResponse с использованием source generator
            var geocodingData = JsonSerializer.Deserialize(json, WeatherJsonContext.Default.GeocodingResponse);
            
            // Проверяем, есть ли результаты
            if (geocodingData?.results != null && geocodingData.results.Count > 0)
            {
                // Берем первый (самый релевантный) результат
                var result = geocodingData.results[0];
                
                // Сохраняем полученные данные в поля класса
                currentLatitude = result.latitude;
                currentLongitude = result.longitude;
                currentTimezone = result.timezone;
                currentCity = result.name; // Используем официальное название из API (может отличаться от введенного)
                
                // Обновляем местное время (без логирования внутри метода)
                UpdateLocalTime();
                
                // Выводим подробную информацию о найденном городе
                LogInfo($"✅ Найден город: {result.name}, {result.country}");
                LogInfo($"   Координаты: {currentLatitude.ToString("F5", invariantCulture)}, {currentLongitude.ToString("F5", invariantCulture)}");
                LogInfo($"   Часовой пояс: {currentTimezone}");
                LogInfo($"   Местное время: {currentLocalTime:dd.MM.yyyy HH:mm:ss}");
                LogInfo($"   Высота над уровнем моря: {result.elevation} м");
                LogInfo($"   Население: {result.population:N0} чел."); // N0 - формат с разделителями тысяч
                LogInfo($"   Регион: {result.admin1}");
                
                return true; // Город успешно найден
            }
            else
            {
                LogError($"❌ Город '{cityName}' не найден");
                return false;
            }
        }
        catch (HttpRequestException ex) // Ошибки сети/HTTP (сервер недоступен, таймаут)
        {
            LogError($"❌ Ошибка HTTP при геокодинге: {ex.Message}");
            return false;
        }
        catch (JsonException ex) // Ошибки парсинга JSON (изменился формат ответа)
        {
            LogError($"❌ Ошибка парсинга ответа геокодинга: {ex.Message}");
            return false;
        }
        catch (Exception ex) // Любые другие непредвиденные ошибки
        {
            LogError($"❌ Неожиданная ошибка при геокодинге: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Отображает главное меню и обрабатывает выбор пользователя
    /// </summary>
    private static void ShowMainMenu()
    {
        // Обновляем местное время перед показом меню (чтобы отображать актуальное)
        UpdateLocalTime();

        // Визуальное оформление меню
        Console.WriteLine("\n==============================");
        Console.WriteLine("     ДНЕВНИК ПОГОДЫ 📔");
        Console.WriteLine("==============================");
        
        // Показываем информацию о текущем городе, если он выбран
        if (!string.IsNullOrEmpty(currentCity))
        {
            Console.WriteLine($"🏙️ Город: {currentCity}");
            if (currentLatitude != 0 && currentLongitude != 0) // Координаты определены
            {
                Console.WriteLine($"📍 Координаты: {currentLatitude.ToString("F5", invariantCulture)}, {currentLongitude.ToString("F5", invariantCulture)}");
                Console.WriteLine($"🕐 Часовой пояс: {currentTimezone}");
                
                // Показываем местное время в городе
                Console.WriteLine($"⏰ Местное время: {currentLocalTime:dd.MM.yyyy HH:mm:ss}");
            }
        }
        else
        {
            Console.WriteLine("⚠️ Город не указан");
        }
        
        // Пункты меню
        Console.WriteLine("\n1. 🌤️ Погода на сегодня (по местному времени)");
        Console.WriteLine("2. 📅 Погода на дату");
        Console.WriteLine("3. 🏙️ Изменить город");
        
        // Динамический пункт меню с текущим городом (если он есть)
        if (!string.IsNullOrEmpty(currentCity))
        {
            Console.WriteLine($"4. 📖 Просмотреть дневник погоды для {currentCity}");
        }
        else
        {
            Console.WriteLine("4. 📖 Просмотреть дневник погоды");
        }
        
        Console.WriteLine("5. 🚪 Выйти");
        Console.Write("\n👉 Выберите действие (1-5): ");

        // Получаем выбор пользователя (убираем пробелы)
        var choice = Console.ReadLine()?.Trim();

        // Обрабатываем выбор
        switch (choice)
        {
            case "1":
                // Используем местную дату города (сегодня по местному времени)
                GetWeatherForDate(GetCurrentLocalDate());
                break;
            case "2":
                GetWeatherForCustomDate(); // Запрашиваем дату у пользователя
                break;
            case "3":
                ChangeCity(); // Смена города
                break;
            case "4":
                ViewWeatherDiaryTable(); // Просмотр дневника
                break;
            case "5":
                LogInfo("Завершение работы");
                Environment.Exit(0); // Немедленный выход
                break;
            default:
                Console.WriteLine("❌ Неверный выбор. Нажмите 1-5");
                break;
        }
    }

    /// <summary>
    /// Отображает дневник погоды в виде таблицы для текущего города
    /// Сортировка: самые новые даты вверху, самые старые внизу
    /// </summary>
    private static void ViewWeatherDiaryTable()
    {
        try
        {
            // Проверяем, выбран ли город
            if (string.IsNullOrEmpty(currentCity))
            {
                Console.WriteLine("\n⚠️ Сначала необходимо указать город.");
                ChangeCity(); // Предлагаем ввести город
                if (string.IsNullOrEmpty(currentCity)) return; // Пользователь отменил или ошибка
            }

            if (!File.Exists(JOURNAL_FILE)) // Файл дневника еще не создан
            {
                Console.WriteLine("\n📭 Дневник погоды пуст. Добавьте первую запись!");
                return;
            }

            string fileContent = File.ReadAllText(JOURNAL_FILE);
            LogInfo($"Загружен файл дневника ({fileContent.Length} байт)");
            
            if (string.IsNullOrWhiteSpace(fileContent)) // Файл пустой
            {
                Console.WriteLine("\n📭 Дневник погоды пуст.");
                return;
            }
            
            List<WeatherData> journal; // Объявляем переменную
            try
            {
                // Десериализуем JSON в список записей
                journal = JsonSerializer.Deserialize(fileContent, WeatherJsonContext.Default.ListWeatherData) ?? new List<WeatherData>();
                LogInfo($"✅ Загружено {journal.Count} записей");
            }
            catch (JsonException ex) // Файл поврежден (невалидный JSON)
            {
                LogError($"❌ Ошибка парсинга дневника: {ex.Message}");
                // Создаем резервную копию поврежденного файла
                string backupFile = JOURNAL_FILE + ".backup." + DateTime.Now.ToString("yyyyMMddHHmmss");
                File.Copy(JOURNAL_FILE, backupFile, true); // true = разрешить перезапись
                LogInfo($"📦 Создана резервная копия: {backupFile}");
                File.Delete(JOURNAL_FILE); // Удаляем поврежденный файл
                Console.WriteLine("\n📭 Файл дневника поврежден. Создан новый.");
                return;
            }

            // Фильтруем только записи для текущего города (без учета регистра)
            var cityEntries = journal.Where(w => w.City.Equals(currentCity, StringComparison.OrdinalIgnoreCase)).ToList();

            if (cityEntries.Count == 0)
            {
                Console.WriteLine($"\n📭 В дневнике нет записей для города {currentCity}.");
                return;
            }

            // Рассчитываем общую ширину таблицы (для красивого вывода)
            int totalWidth = 3 + // Начальная черта и пробелы
                COLUMN_DATE_WIDTH + 3 + // Дата + разделители
                COLUMN_TEMP_MIN_WIDTH + 3 +
                COLUMN_TEMP_MAX_WIDTH + 3 +
                COLUMN_PRECIP_WIDTH + 3 +
                COLUMN_HUMIDITY_WIDTH + 3 +
                COLUMN_PRESSURE_WIDTH + 3 +
                COLUMN_WIND_WIDTH + 3 +
                COLUMN_CONDITION_WIDTH + 2; // Последний столбец без черты в конце

            Console.WriteLine("\n" + new string('=', totalWidth)); // Верхняя граница
            Console.WriteLine($"📊 ДНЕВНИК ПОГОДЫ - {currentCity.ToUpper()}");
            Console.WriteLine("   (самые новые даты вверху, самые старые внизу)");
            Console.WriteLine(new string('=', totalWidth));

            // Заголовки с правильным выравниванием: отрицательное число = влево, положительное = вправо
            string header = $"| {"Дата",-COLUMN_DATE_WIDTH} | {"Мин T",COLUMN_TEMP_MIN_WIDTH} | {"Макс T",COLUMN_TEMP_MAX_WIDTH} | {"Осадки",COLUMN_PRECIP_WIDTH} | {"Влажн.",COLUMN_HUMIDITY_WIDTH} | {"Давл.",COLUMN_PRESSURE_WIDTH} | {"Ветер",COLUMN_WIND_WIDTH} | {"Явления",-COLUMN_CONDITION_WIDTH}";
            string separator = new string('-', header.Length); // Разделительная линия
            
            Console.WriteLine(separator);
            Console.WriteLine(header);
            Console.WriteLine(separator);
            
            // Сортировка по убыванию даты (самые новые вверху) и вывод
            foreach (var weather in cityEntries.OrderByDescending(w => w.Date))
            {
                string emoji = GetWeatherEmoji(weather.WeatherCondition); // Эмодзи для погоды
                string conditionWithEmoji = $"{weather.WeatherCondition} {emoji}";
                
                // Используем константы ширины столбцов для выравнивания
                Console.WriteLine(
                    $"| {weather.Date,-COLUMN_DATE_WIDTH:dd.MM.yyyy} | " + // Дата влево
                    $"{weather.MinTemperature,COLUMN_TEMP_MIN_WIDTH:F1} | " + // Числа вправо
                    $"{weather.MaxTemperature,COLUMN_TEMP_MAX_WIDTH:F1} | " +
                    $"{weather.Precipitation,COLUMN_PRECIP_WIDTH:F1} | " +
                    $"{weather.Humidity,COLUMN_HUMIDITY_WIDTH:F0} | " + // Без десятых
                    $"{weather.Pressure,COLUMN_PRESSURE_WIDTH:F0} | " +
                    $"{weather.WindSpeed,COLUMN_WIND_WIDTH:F1} | " +
                    $"{conditionWithEmoji,-COLUMN_CONDITION_WIDTH}" // Текст влево
                );
            }
            
            Console.WriteLine(separator);
            
            // Статистика только для текущего города
            Console.WriteLine("\n📊 СТАТИСТИКА ДЛЯ ГОРОДА " + currentCity.ToUpper() + ":");
            Console.WriteLine($"   Всего записей: {cityEntries.Count}");
            Console.WriteLine($"   Период: {cityEntries.Min(w => w.Date):dd.MM.yyyy} - {cityEntries.Max(w => w.Date):dd.MM.yyyy}");
            Console.WriteLine($"   🌡️ Мин. температура: {cityEntries.Min(w => w.MinTemperature):F1}°C");
            Console.WriteLine($"   🌡️ Макс. температура: {cityEntries.Max(w => w.MaxTemperature):F1}°C");
            Console.WriteLine($"   💧 Макс. осадки: {cityEntries.Max(w => w.Precipitation):F1} мм");
            Console.WriteLine($"   🌀 Макс. давление: {cityEntries.Max(w => w.Pressure):F0} мм рт.ст.");
            Console.WriteLine($"   🌬️ Макс. ветер: {cityEntries.Max(w => w.WindSpeed):F1} м/с");
            
            Console.WriteLine("\n🔍 Нажмите любую клавишу для продолжения...");
            Console.ReadKey(); // Ждем нажатия клавиши, чтобы пользователь успел прочитать
        }
        catch (Exception ex) // Любые ошибки при чтении/обработке дневника
        {
            LogError($"Ошибка при чтении дневника: {ex.Message}");
        }
    }

    /// <summary>
    /// Изменяет текущий город
    /// Запрашивает у пользователя новое название, ищет координаты и сохраняет в конфиг
    /// </summary>
    private static void ChangeCity()
    {
        Console.Write("\n🏙️ Введите название города: ");
        var newCity = Console.ReadLine()?.Trim(); // Читаем и убираем пробелы
        
        // Проверяем, что введено непустое значение
        if (string.IsNullOrEmpty(newCity))
        {
            Console.WriteLine("❌ Название города не может быть пустым.");
            return;
        }
        
        Console.WriteLine($"\n🔍 Поиск города '{newCity}'...");
        
        // Пытаемся найти координаты города
        if (GetCityCoordinates(newCity)) // Запрос к API геокодинга
        {
            // Если город найден, сохраняем в конфигурацию
            SaveConfiguration();
            
            // Показываем успешный результат с учетом местного времени
            Console.ForegroundColor = SUCCESS_COLOR;
            Console.WriteLine($"\n✅ Город изменен на: {currentCity}");
            Console.WriteLine($"   Координаты: {currentLatitude.ToString("F5", invariantCulture)}, {currentLongitude.ToString("F5", invariantCulture)}");
            Console.WriteLine($"   Часовой пояс: {currentTimezone}");
            Console.WriteLine($"   ⏰ Местное время: {currentLocalTime:dd.MM.yyyy HH:mm:ss}");
            Console.ResetColor();
            
            // Спрашиваем, хочет ли пользователь сразу получить погоду на сегодня по местному времени
            Console.Write($"\n📅 Получить погоду на сегодня ({currentLocalTime:dd.MM.yyyy})? (д/н): ");
            var answer = Console.ReadLine()?.Trim().ToLower();
            
            // Проверяем положительный ответ (поддерживаем русский и английский)
            if (answer == "д" || answer == "да" || answer == "y" || answer == "yes")
            {
                GetWeatherForDate(currentLocalTime.Date); // Запрос погоды
            }
        }
        else
        {
            // Если город не найден, сообщаем об ошибке
            Console.WriteLine($"\n❌ Город '{newCity}' не найден. Проверьте название и попробуйте снова.");
        }
    }

    /// <summary>
    /// Запрашивает у пользователя дату в формате ДД.ММ.ГГГГ и проверяет её
    /// </summary>
    private static void GetWeatherForCustomDate()
    {
        Console.Write("\n📅 Введите дату (ДД.ММ.ГГГГ): ");
        string input = Console.ReadLine()?.Trim() ?? "";
        
        // Проверяем, что дата введена
        if (string.IsNullOrEmpty(input))
        {
            Console.WriteLine("❌ Дата не введена.");
            return;
        }
        
        // Пытаемся распарсить дату с использованием нескольких форматов
        if (DateTime.TryParseExact(input, DATE_FORMATS, 
            CultureInfo.InvariantCulture, // Не зависит от региональных настроек системы
            DateTimeStyles.None, 
            out DateTime date))
        {
            // Получаем текущую местную дату города
            DateTime localToday = GetCurrentLocalDate();
            
            // Проверяем, что дата не больше сегодняшнего дня (по местному времени города)
            if (date > localToday)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"\n⚠️ Дневник погоды содержит данные только о прошедших днях и сегодняшнем дне.");
                Console.WriteLine($"   Сегодня по местному времени: {localToday:dd.MM.yyyy}");
                Console.WriteLine($"   Для получения данных на {date:dd.MM.yyyy} используйте прогноз погоды (пункт 1 меню).");
                Console.ResetColor();
                return; // Не запрашиваем API для будущих дат
            }
            
            // Если дата корректна (прошлая или сегодня), запрашиваем погоду
            GetWeatherForDate(date);
        }
        else
        {
            Console.WriteLine($"❌ Неверный формат. Используйте {DATE_DISPLAY_FORMAT} (например, 25.12.2024)");
        }
    }

    /// <summary>
    /// Получает погоду для указанной даты
    /// Сначала проверяет наличие записи в дневнике, если нет - запрашивает из API
    /// </summary>
    /// <param name="date">Дата, для которой нужна погода</param>
    private static void GetWeatherForDate(DateTime date)
    {
        try
        {
            // Проверяем, выбран ли город
            if (string.IsNullOrEmpty(currentCity))
            {
                Console.WriteLine("\n⚠️ Сначала необходимо указать город.");
                ChangeCity(); // Предлагаем ввести город
                if (string.IsNullOrEmpty(currentCity)) return; // Пользователь отменил
            }

            // Проверяем, есть ли координаты (если нет - получаем их через геокодинг)
            if (currentLatitude == 0 || currentLongitude == 0)
            {
                LogInfo("Координаты не определены, выполняем геокодинг...");
                if (!GetCityCoordinates(currentCity)) // Запрос координат
                {
                    LogError("❌ Не удалось определить координаты города");
                    return;
                }
            }

            // Проверяем, есть ли уже запись в дневнике на эту дату
            if (File.Exists(JOURNAL_FILE))
            {
                try
                {
                    string fileContent = File.ReadAllText(JOURNAL_FILE);
                    if (!string.IsNullOrWhiteSpace(fileContent))
                    {
                        var journal = JsonSerializer.Deserialize(fileContent, WeatherJsonContext.Default.ListWeatherData) ?? new List<WeatherData>();
                        // Ищем запись с такой же датой и городом (без учета времени)
                        var existingEntry = journal.FirstOrDefault(w => w.Date.Date == date.Date && w.City == currentCity);
                        
                        // Если запись найдена, показываем её и выходим
                        if (existingEntry != null)
                        {
                            Console.WriteLine($"\n📖 Найдена запись в дневнике за {date:dd.MM.yyyy}:");
                            DisplayWeatherData(existingEntry);
                            return; // Не запрашиваем API
                        }
                    }
                }
                catch (JsonException ex)
                {
                    // Если файл поврежден, логируем ошибку, но продолжаем запрос к API
                    LogInfo($"Файл дневника поврежден: {ex.Message}, продолжаем запрос к API");
                }
            }

            // Если записи нет, запрашиваем из API
            LogInfo($"Запрос погоды для {currentCity} на {date:dd.MM.yyyy}");
            
            var weatherData = FetchWeatherData(date); // Запрос к API
            
            // Если данные получены, показываем и сохраняем
            if (weatherData != null)
            {
                DisplayWeatherData(weatherData); // Вывод в консоль
                SaveWeatherData(weatherData);    // Сохранение в JSON
            }
            else
            {
                Console.WriteLine("\n❌ Не удалось получить данные о погоде. Попробуйте позже.");
            }
        }
        catch (Exception ex) // Обработка непредвиденных ошибок
        {
            LogError($"Ошибка получения данных: {ex.Message}");
        }
    }

    /// <summary>
    /// Запрашивает данные о погоде из Open-Meteo API
    /// </summary>
    /// <param name="date">Дата для запроса</param>
    /// <returns>Объект с данными о погоде или null в случае ошибки</returns>
    private static WeatherData? FetchWeatherData(DateTime date)
    {
        try
        {
            // ВАЖНО: Используем InvariantCulture для форматирования чисел с точкой
            // Это гарантирует, что в URL будет точка, а не запятая (иначе API вернет ошибку)
            string latStr = currentLatitude.ToString($"F{COORDINATES_PRECISION}", invariantCulture);
            string lonStr = currentLongitude.ToString($"F{COORDINATES_PRECISION}", invariantCulture);
            
            // Формируем URL в зависимости от даты
            string url;
            if (date < GetCurrentLocalDate())
            {
                // Для прошедших дат используем архивное API
                url = $"{WEATHER_API_ARCHIVE_URL}?" +
                    $"latitude={latStr}&longitude={lonStr}&" +
                    $"start_date={date:yyyy-MM-dd}&end_date={date:yyyy-MM-dd}&" + // Диапазон из одного дня
                    $"hourly={HOURLY_PARAMETERS}&" +
                    $"timezone={currentTimezone}";
            }
            else
            {
                // Для сегодня и будущего используем прогнозное API
                url = $"{WEATHER_API_FORECAST_URL}?" +
                    $"latitude={latStr}&longitude={lonStr}&" +
                    $"hourly={HOURLY_PARAMETERS}&" +
                    $"timezone={currentTimezone}&forecast_days={FORECAST_DAYS}";
            }

            LogInfo($"🌐 API запрос погоды:");
            LogInfo($"   URL: {url}");
            Console.WriteLine("   Ожидание ответа...");
            
            // Отправляем запрос
            var response = httpClient.GetAsync(url).GetAwaiter().GetResult();
            LogInfo($"   Статус: {(int)response.StatusCode} {response.ReasonPhrase}");
            
            // Проверяем успешность запроса
            if (!response.IsSuccessStatusCode)
            {
                // Если ошибка, читаем тело ответа для диагностики (там часто описание проблемы)
                string errorContent = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                LogError($"   Ошибка от API: {errorContent}");
                return null;
            }
            
            // Читаем тело ответа
            var json = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            LogInfo($"   Получено {json.Length} байт данных");
            
            // Десериализуем JSON
            var apiData = JsonSerializer.Deserialize(json, WeatherJsonContext.Default.OpenMeteoResponse);
            
            // Проверяем, что данные получены (есть почасовые температуры)
            if (apiData?.hourly != null && apiData.hourly.temperature_2m.Count > 0)
            {
                LogInfo($"✅ Данные получены успешно");
                LogInfo($"   Часовой пояс: {apiData.timezone}");
                
                // Конвертируем API ответ в нашу модель данных
                return ConvertToWeatherData(apiData, date);
            }
            
            LogError("❌ Нет данных от API");
            return null;
        }
        catch (HttpRequestException ex) // Ошибки сети/HTTP
        {
            LogError($"❌ Ошибка HTTP: {ex.Message}");
            return null;
        }
        catch (JsonException ex) // Ошибки парсинга JSON
        {
            LogError($"❌ Ошибка JSON: {ex.Message}");
            return null;
        }
        catch (Exception ex) // Любые другие ошибки
        {
            LogError($"❌ Ошибка: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Конвертирует ответ от Open-Meteo API в нашу модель WeatherData
    /// </summary>
    /// <param name="apiData">Данные от API</param>
    /// <param name="date">Запрашиваемая дата</param>
    /// <returns>Объект WeatherData для сохранения</returns>
    private static WeatherData ConvertToWeatherData(OpenMeteoResponse apiData, DateTime date)
    {
        try
        {
            // Конвертируем скорость ветра из км/ч в м/с
            // В API скорость ветра в км/ч, нам нужны м/с для удобства
            double windSpeedMs = apiData.hourly.windspeed_10m.Average() / KMH_TO_MS;
            
            // Обновляем местное время (без логирования)
            UpdateLocalTime();
            
            // Создаем объект с данными
            var weatherData = new WeatherData
            {
                Date = date, // Запрашиваемая дата
                City = currentCity,
                Latitude = Math.Round(apiData.latitude, COORDINATES_PRECISION), // Округляем до 5 знаков
                Longitude = Math.Round(apiData.longitude, COORDINATES_PRECISION),
                Timezone = apiData.timezone,
                LocalDateTime = currentLocalTime, // Время получения данных
                // Минимальная температура за день
                MinTemperature = Math.Round(apiData.hourly.temperature_2m.Min(), 1),
                // Максимальная температура за день
                MaxTemperature = Math.Round(apiData.hourly.temperature_2m.Max(), 1),
                // Сумма осадков за день
                Precipitation = Math.Round(apiData.hourly.precipitation.Sum(), 1),
                // Средняя влажность
                Humidity = Math.Round(apiData.hourly.relativehumidity_2m.Average(), 1),
                // Среднее давление (конвертируем гПа в мм рт.ст.)
                Pressure = Math.Round(apiData.hourly.pressure_msl.Average() * HPA_TO_MMHG, 1),
                // Средняя скорость ветра (уже в м/с)
                WindSpeed = Math.Round(windSpeedMs, 1),
                // Описание погоды по самому частому коду
                WeatherCondition = GetWeatherDescription(GetMostFrequentWeatherCode(apiData.hourly.weathercode))
            };

            return weatherData;
        }
        catch (Exception ex) // Ошибки при вычислениях (деление на ноль, пустые списки)
        {
            LogError($"Ошибка конвертации: {ex.Message}");
            throw; // Пробрасываем исключение выше для обработки
        }
    }

    /// <summary>
    /// Находит самый частый код погоды за день
    /// </summary>
    /// <param name="weatherCodes">Список кодов погоды по часам</param>
    /// <returns>Наиболее часто встречающийся код</returns>
    private static int GetMostFrequentWeatherCode(List<int> weatherCodes)
    {
        if (weatherCodes == null || weatherCodes.Count == 0)
            return 0; // Возвращаем 0 (ясно) по умолчанию
            
        // Группируем коды, сортируем по убыванию количества и берем первый
        return weatherCodes
            .GroupBy(x => x) // Группируем одинаковые коды
            .OrderByDescending(g => g.Count()) // Сортируем по размеру группы (убывание)
            .First() // Берем первую (самую большую) группу
            .Key; // Возвращаем ключ группы (код погоды)
    }

    /// <summary>
    /// Преобразует код погоды WMO в текстовое описание на русском
    /// </summary>
    /// <param name="weatherCode">Код погоды из API</param>
    /// <returns>Описание погоды</returns>
    private static string GetWeatherDescription(int weatherCode)
    {
        return weatherCode switch // switch expression (C# 8.0+)
        {
            // Коды WMO (Всемирная метеорологическая организация)
            0 => "Ясно",
            1 => "Преимущественно ясно",
            2 => "Переменная облачность",
            3 => "Пасмурно",
            45 or 48 => "Туман", // 45 и 48 - разные типы тумана
            51 => "Легкая морось",
            53 => "Морось",
            55 => "Сильная морось",
            56 => "Легкая ледяная морось",
            57 => "Ледяная морось",
            61 => "Небольшой дождь",
            63 => "Умеренный дождь",
            65 => "Сильный дождь",
            66 => "Легкий ледяной дождь",
            67 => "Ледяной дождь",
            71 => "Небольшой снег",
            73 => "Умеренный снег",
            75 => "Сильный снег",
            77 => "Снежная крупа",
            80 => "Небольшой ливень",
            81 => "Умеренный ливень",
            82 => "Сильный ливень",
            85 => "Небольшой снегопад",
            86 => "Сильный снегопад",
            95 => "Гроза",
            96 => "Гроза с градом",
            99 => "Сильная гроза с градом",
            _ => "Неизвестно" // Для всех остальных кодов
        };
    }

    /// <summary>
    /// Возвращает эмодзи для отображения погоды
    /// </summary>
    /// <param name="weatherCondition">Текстовое описание погоды</param>
    /// <returns>Строка с эмодзи</returns>
    private static string GetWeatherEmoji(string weatherCondition)
    {
        return weatherCondition switch // pattern matching по строкам
        {
            "Ясно" => "☀️",
            "Преимущественно ясно" => "🌤️",
            "Переменная облачность" => "⛅",
            "Пасмурно" => "☁️",
            "Туман" => "🌫️",
            // Используем Contains для групп похожих условий
            var condition when condition.Contains("морось") => "🌧️",
            var condition when condition.Contains("дождь") => "☔",
            var condition when condition.Contains("ливень") => "💧",
            var condition when condition.Contains("снег") => "❄️",
            var condition when condition.Contains("снегопад") => "🌨️",
            var condition when condition.Contains("гроза") => "⛈️",
            var condition when condition.Contains("град") => "🧊",
            _ => "🌡️" // Термометр по умолчанию
        };
    }

    /// <summary>
    /// Отображает данные о погоде в консоли
    /// </summary>
    /// <param name="data">Данные о погоде</param>
    private static void DisplayWeatherData(WeatherData data)
    {
        string emoji = GetWeatherEmoji(data.WeatherCondition); // Получаем эмодзи
        
        Console.WriteLine($"\n{new string('=', 60)}"); // Верхняя граница
        Console.WriteLine($"📍 {data.City.ToUpper()} - {data.Date:dd.MM.yyyy} {emoji}");
        Console.WriteLine(new string('=', 60));
        Console.WriteLine($"🌡️ Температура: {data.MinTemperature:F1}°C ... {data.MaxTemperature:F1}°C");
        Console.WriteLine($"💧 Осадки: {data.Precipitation:F1} мм");
        Console.WriteLine($"💨 Влажность: {data.Humidity:F0}%"); // Без десятых
        Console.WriteLine($"🌀 Давление: {data.Pressure:F0} мм рт.ст."); // Без десятых
        Console.WriteLine($"🌬️ Ветер: {data.WindSpeed:F1} м/с");
        Console.WriteLine($"🌤️ Явления: {data.WeatherCondition} {emoji}");
        Console.WriteLine($"🕐 Местное время: {currentLocalTime:dd.MM.yyyy HH:mm:ss}");
        Console.WriteLine(new string('=', 60));
        Console.WriteLine(); // Пустая строка для разделения
    }

    /// <summary>
    /// Сохраняет данные о погоде в дневник (weather_journal.json)
    /// Если запись на эту дату уже существует, обновляет её
    /// </summary>
    /// <param name="newData">Новые данные для сохранения</param>
    private static void SaveWeatherData(WeatherData newData)
    {
        try
        {
            List<WeatherData> journal = new List<WeatherData>();
            
            // Если файл дневника существует, загружаем существующие записи
            if (File.Exists(JOURNAL_FILE))
            {
                string fileContent = File.ReadAllText(JOURNAL_FILE);
                if (!string.IsNullOrWhiteSpace(fileContent))
                {
                    try
                    {
                        journal = JsonSerializer.Deserialize(fileContent, WeatherJsonContext.Default.ListWeatherData) ?? new List<WeatherData>();
                        LogInfo($"Загружено {journal.Count} существующих записей");
                    }
                    catch (JsonException ex)
                    {
                        // Если файл поврежден, создаем резервную копию и начинаем новый дневник
                        LogInfo($"Файл дневника поврежден: {ex.Message}. Создаем новый.");
                        string backupFile = JOURNAL_FILE + ".backup." + DateTime.Now.ToString("yyyyMMddHHmmss");
                        File.Copy(JOURNAL_FILE, backupFile, true);
                        LogInfo($"📦 Резервная копия: {backupFile}");
                        journal = new List<WeatherData>(); // Начинаем с чистого листа
                    }
                }
            }

            // Проверяем, есть ли уже запись на эту дату для этого города
            var existingEntry = journal.FirstOrDefault(w => w.Date.Date == newData.Date.Date && w.City == newData.City);
            if (existingEntry != null)
            {
                // Если есть, удаляем старую (будет добавлена обновленная)
                journal.Remove(existingEntry);
                LogInfo($"🔄 Обновление существующей записи за {newData.Date:dd.MM.yyyy}");
            }
            
            // Добавляем новую запись
            journal.Add(newData);
            journal = journal.OrderBy(w => w.Date).ToList(); // Сортируем по дате
            LogInfo($"Добавлена новая запись. Всего в дневнике: {journal.Count}");
            
            // Сохраняем через временный файл для атомарности
            string tempFile = JOURNAL_FILE + ".tmp";
            var json = JsonSerializer.Serialize(journal, WeatherJsonContext.Default.ListWeatherData);
            File.WriteAllText(tempFile, json);
            LogInfo($"Данные записаны во временный файл ({json.Length} байт)");
            
            // Атомарно заменяем основной файл
            if (File.Exists(JOURNAL_FILE))
            {
                File.Replace(tempFile, JOURNAL_FILE, null); // null = без backup
                LogInfo($"✅ Дневник обновлен: {JOURNAL_FILE}");
            }
            else
            {
                File.Move(tempFile, JOURNAL_FILE);
                LogInfo($"✅ Дневник создан: {JOURNAL_FILE}");
            }
        }
        catch (Exception ex) // Ошибки записи файла
        {
            LogError($"❌ Ошибка сохранения: {ex.Message}");
        }
    }

    /// <summary>
    /// Логирует информационное сообщение зеленым цветом
    /// </summary>
    /// <param name="message">Сообщение для логирования</param>
    private static void LogInfo(string message)
    {
        Console.ForegroundColor = INFO_COLOR;
        Console.WriteLine($"[INFO] {DateTime.Now:HH:mm:ss} - {message}");
        Console.ResetColor();
    }

    /// <summary>
    /// Логирует сообщение об ошибке красным цветом
    /// </summary>
    /// <param name="message">Сообщение об ошибке</param>
    private static void LogError(string message)
    {
        Console.ForegroundColor = ERROR_COLOR;
        Console.WriteLine($"[ERROR] {DateTime.Now:HH:mm:ss} - {message}");
        Console.ResetColor();
    }
}

// Точка входа в приложение (отдельный статический класс)
public static class Program
{
    public static void Main()
    {
        WeatherDiaryApp.Run(); // Запускаем основное приложение
    }
}
