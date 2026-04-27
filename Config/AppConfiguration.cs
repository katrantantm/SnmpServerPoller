using System;
using System.IO;

namespace SnmpServerPoller.Config
{
    /// <summary>
    /// Модель конфигурации SNMP-соединения
    /// </summary>
    public class SnmpSettings
    {
        public string TargetIp { get; set; } = "127.0.0.1";
        public string Community { get; set; } = "public";
        public int Timeout { get; set; } = 3000;
        public int Retries { get; set; } = 2;
        public string Version { get; set; } = "v2c";
        public int MaxProcesses { get; set; } = 200;
        public int MaxArpEntries { get; set; } = 1000;
        public int MaxInterfaces { get; set; } = 50;

        /// <summary>
        /// Валидация настроек SNMP
        /// </summary>
        public bool Validate(out string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(TargetIp))
            {
                errorMessage = "TargetIp не может быть пустым";
                return false;
            }

            if (!System.Net.IPAddress.TryParse(TargetIp, out _))
            {
                errorMessage = $"Неверный формат IP-адреса: {TargetIp}";
                return false;
            }

            if (string.IsNullOrWhiteSpace(Community))
            {
                errorMessage = "Community string не может быть пустым";
                return false;
            }

            if (Timeout <= 0 || Timeout > 30000)
            {
                errorMessage = $"Timeout должен быть в диапазоне 1-30000 мс (текущее: {Timeout})";
                return false;
            }

            if (Retries < 0 || Retries > 5)
            {
                errorMessage = $"Retries должен быть в диапазоне 0-5 (текущее: {Retries})";
                return false;
            }

            errorMessage = null;
            return true;
        }
    }

    /// <summary>
    /// Модель конфигурации Excel
    /// </summary>
    public class ExcelSettings
    {
        public string OutputPath { get; set; } = "ServerReport.xlsx";
        public string TemplatePath { get; set; }
        public bool AutoSave { get; set; } = true;

        /// <summary>
        /// Валидация путей к файлам Excel
        /// </summary>
        public bool Validate(out string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(OutputPath))
            {
                errorMessage = "OutputPath не может быть пустым";
                return false;
            }

            try
            {
                // Валидация пути без проверки существования файла
                var fullPath = Path.GetFullPath(OutputPath);
                
                // Проверка на потенциальный path traversal
                if (fullPath.Contains(".."))
                {
                    errorMessage = "OutputPath содержит недопустимые символы";
                    return false;
                }
            }
            catch (Exception ex)
            {
                errorMessage = $"Неверный формат OutputPath: {ex.Message}";
                return false;
            }

            // TemplatePath опционален
            if (!string.IsNullOrWhiteSpace(TemplatePath))
            {
                try
                {
                    var templateFullPath = Path.GetFullPath(TemplatePath);
                    if (templateFullPath.Contains(".."))
                    {
                        errorMessage = "TemplatePath содержит недопустимые символы";
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    errorMessage = $"Неверный формат TemplatePath: {ex.Message}";
                    return false;
                }
            }

            errorMessage = null;
            return true;
        }
    }

    /// <summary>
    /// Модель конфигурации логирования
    /// </summary>
    public class LoggingSettings
    {
        public string MinLevel { get; set; } = "Info";
        public bool EnableColors { get; set; } = true;
        public string FilePath { get; set; }

        public LogLevel GetMinLogLevel()
        {
            return MinLevel.ToLower() switch
            {
                "debug" => LogLevel.Debug,
                "info" => LogLevel.Info,
                "warn" => LogLevel.Warn,
                "error" => LogLevel.Error,
                _ => LogLevel.Info
            };
        }
    }

    /// <summary>
    /// Корневой класс конфигурации приложения
    /// </summary>
    public class AppConfiguration
    {
        public SnmpSettings Snmp { get; set; } = new SnmpSettings();
        public ExcelSettings Excel { get; set; } = new ExcelSettings();
        public LoggingSettings Logging { get; set; } = new LoggingSettings();

        /// <summary>
        /// Загрузка конфигурации из JSON файла
        /// </summary>
        public static AppConfiguration Load(string configPath = "appsettings.json")
        {
            if (!File.Exists(configPath))
            {
                throw new FileNotFoundException($"Файл конфигурации не найден: {configPath}");
            }

            try
            {
                var json = File.ReadAllText(configPath);
                var config = Newtonsoft.Json.JsonConvert.DeserializeObject<AppConfiguration>(json);
                
                // Валидация всех секций
                if (!config.Snmp.Validate(out var snmpError))
                {
                    throw new InvalidOperationException($"Ошибка валидации SNMP настроек: {snmpError}");
                }

                if (!config.Excel.Validate(out var excelError))
                {
                    throw new InvalidOperationException($"Ошибка валидации Excel настроек: {excelError}");
                }

                return config;
            }
            catch (Newtonsoft.Json.JsonException ex)
            {
                throw new InvalidOperationException($"Ошибка парсинга JSON конфигурации: {ex.Message}", ex);
            }
        }
    }
}
