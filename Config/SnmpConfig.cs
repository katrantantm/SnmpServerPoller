using System;
using System.IO;
using Newtonsoft.Json;

namespace SnmpServerPoller.Config
{
    public class AppConfig
    {
        public SnmpSettings? Snmp { get; set; }
        public ExcelSettings? Excel { get; set; }
        public LoggingSettings? Logging { get; set; }
        public ExportSettings? Export { get; set; }
    }

    public class SnmpSettings
    {
        public string Community { get; set; } = "public";
        public int Timeout { get; set; } = 5000;
        public int Retries { get; set; } = 3;
        public string Version { get; set; } = "v2c";
        public int MaxProcesses { get; set; } = 200;
        public int MaxArpEntries { get; set; } = 1000;
    }

    public class ExcelSettings
    {
        public string? TemplatePath { get; set; }
        public bool AutoSave { get; set; } = true;
    }

    public class LoggingSettings
    {
        public string LogLevel { get; set; } = "Information";
        public string FilePath { get; set; } = "logs/poller.log";
    }

    public class ExportSettings
    {
        public string OutputPath { get; set; } = "output";
        public string[] Formats { get; set; } = new[] { "csv" };
    }

    public static class ConfigurationLoader
    {
        private static AppConfig? _config;
        private static readonly object _lockObj = new();

        public static AppConfig Load(string configPath = "appsettings.json")
        {
            lock (_lockObj)
            {
                if (_config != null) return _config;

                try
                {
                    if (!File.Exists(configPath))
                    {
                        Console.WriteLine($"⚠️ Файл конфигурации '{configPath}' не найден. Используются значения по умолчанию.");
                        _config = CreateDefaultConfig();
                    }
                    else
                    {
                        string json = File.ReadAllText(configPath);
                        _config = JsonConvert.DeserializeObject<AppConfig>(json) ?? CreateDefaultConfig();
                        
                        // Создаем директорию для логов если не существует
                        if (!string.IsNullOrEmpty(_config.Logging?.FilePath))
                        {
                            string logDir = Path.GetDirectoryName(_config.Logging.FilePath);
                            if (!string.IsNullOrEmpty(logDir) && !Directory.Exists(logDir))
                            {
                                Directory.CreateDirectory(logDir);
                            }
                        }
                        
                        // Создаем директорию для экспорта если не существует
                        if (!string.IsNullOrEmpty(_config.Export?.OutputPath) && !Directory.Exists(_config.Export.OutputPath))
                        {
                            Directory.CreateDirectory(_config.Export.OutputPath);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Ошибка загрузки конфигурации: {ex.Message}");
                    _config = CreateDefaultConfig();
                }

                return _config;
            }
        }

        private static AppConfig CreateDefaultConfig()
        {
            return new AppConfig
            {
                Snmp = new SnmpSettings { Community = "public", Timeout = 5000, Retries = 3 },
                Excel = new ExcelSettings { AutoSave = true },
                Logging = new LoggingSettings { LogLevel = "Information", FilePath = "logs/poller.log" },
                Export = new ExportSettings { OutputPath = "output", Formats = new[] { "csv" } }
            };
        }

        public static void Reset()
        {
            lock (_lockObj)
            {
                _config = null!;
            }
        }
    }

    public static class SnmpConfig
    {
        static SnmpConfig()
        {
            Reload();
        }

        public static void Reload()
        {
            var config = ConfigurationLoader.Load();
            Community = config.Snmp?.Community ?? "public";
            MaxProcesses = config.Snmp?.MaxProcesses ?? 200;
        }

        public static string Community { get; private set; } = "public";
        public static int MaxProcesses { get; private set; } = 200;

        // System
        public const string SysDescr = ".1.3.6.1.2.1.1.1.0";
        public const string SysUpTime = ".1.3.6.1.2.1.1.3.0";
        public const string SysName = ".1.3.6.1.2.1.1.5.0";
        public const string SysContact = ".1.3.6.1.2.1.1.4.0";
        public const string SysLocation = ".1.3.6.1.2.1.1.6.0";

        // Interfaces
        public const string IfDescr = ".1.3.6.1.2.1.2.2.1.2";
        public const string IfType = ".1.3.6.1.2.1.2.2.1.3";
        public const string IfMtu = ".1.3.6.1.2.1.2.2.1.4";
        public const string IfSpeed = ".1.3.6.1.2.1.2.2.1.5";
        public const string IfAdminStatus = ".1.3.6.1.2.1.2.2.1.7";
        public const string IfOperStatus = ".1.3.6.1.2.1.2.2.1.8";
        public const string IfInOctets = ".1.3.6.1.2.1.2.2.1.10";
        public const string IfOutOctets = ".1.3.6.1.2.1.2.2.1.16";
        public const string IfInErrors = ".1.3.6.1.2.1.2.2.1.14";
        public const string IfOutErrors = ".1.3.6.1.2.1.2.2.1.20";

        // IP
        public const string IpAdEntAddr = ".1.3.6.1.2.1.4.20.1.1";
        public const string IpAdEntNetMask = ".1.3.6.1.2.1.4.20.1.3";
        public const string IpAdEntIfIndex = ".1.3.6.1.2.1.4.20.1.2";

        // Routing
        public const string IpRouteDest = ".1.3.6.1.2.1.4.21.1.1";
        public const string IpRouteMask = ".1.3.6.1.2.1.4.21.1.11";
        public const string IpRouteNextHop = ".1.3.6.1.2.1.4.21.1.7";
        public const string IpRouteIfIndex = ".1.3.6.1.2.1.4.21.1.2";
        public const string IpRouteType = ".1.3.6.1.2.1.4.21.1.8";
        public const string IpRouteProto = ".1.3.6.1.2.1.4.21.1.9";
        public const string IpRouteMetric1 = ".1.3.6.1.2.1.4.21.1.3";
        public const string IpRouteAge = ".1.3.6.1.2.1.4.21.1.10";

        // ARP
        public const string IpNetToMediaPhysAddress = ".1.3.6.1.2.1.4.22.1.2";
        public const string IpNetToMediaNetAddress = ".1.3.6.1.2.1.4.22.1.3";
        public const string IpNetToMediaIfIndex = ".1.3.6.1.2.1.4.22.1.1";
        public const string IpNetToMediaType = ".1.3.6.1.2.1.4.22.1.4";

        // Storage
        public const string HrStorageDescr = ".1.3.6.1.2.1.25.2.3.1.3";
        public const string HrStorageUnits = ".1.3.6.1.2.1.25.2.3.1.4";
        public const string HrStorageSize = ".1.3.6.1.2.1.25.2.3.1.5";
        public const string HrStorageUsed = ".1.3.6.1.2.1.25.2.3.1.6";

        // CPU
        public const string HrProcessorLoad = ".1.3.6.1.2.1.25.3.3.1.2";

        // Processes
        public const string HrSWRunName = ".1.3.6.1.2.1.25.4.2.1.2";
        public const string HrSWRunPath = ".1.3.6.1.2.1.25.4.2.1.4";
        public const string HrSWRunParams = ".1.3.6.1.2.1.25.4.2.1.5";
        public const string HrSWRunType = ".1.3.6.1.2.1.25.4.2.1.6";
        public const string HrSWRunStatus = ".1.3.6.1.2.1.25.4.2.1.7";

        // Devices
        public const string HrDeviceType = ".1.3.6.1.2.1.25.3.2.1.2";
        public const string HrDeviceDescr = ".1.3.6.1.2.1.25.3.2.1.3";
        public const string HrDeviceStatus = ".1.3.6.1.2.1.25.3.2.1.5";
        public const string HrDeviceErrors = ".1.3.6.1.2.1.25.3.2.1.6";

        // Stats
        public const string IpForwarding = ".1.3.6.1.2.1.4.1.0";
        public const string IpInReceives = ".1.3.6.1.2.1.4.3.0";
        public const string IpOutRequests = ".1.3.6.1.2.1.4.10.0";
        public const string TcpMaxConn = ".1.3.6.1.2.1.6.4.0";
        public const string TcpInSegs = ".1.3.6.1.2.1.6.10.0";
        public const string TcpOutSegs = ".1.3.6.1.2.1.6.11.0";
        public const string UdpInDatagrams = ".1.3.6.1.2.1.7.1.0";
        public const string UdpOutDatagrams = ".1.3.6.1.2.1.7.4.0";
        public const string SnmpInPkts = ".1.3.6.1.2.1.11.1.0";
        public const string SnmpOutPkts = ".1.3.6.1.2.1.11.2.0";
        public const string IcmpInMsgs = ".1.3.6.1.2.1.5.1.0";
        public const string IcmpOutMsgs = ".1.3.6.1.2.1.5.14.0";
        public const string IcmpInEchos = ".1.3.6.1.2.1.5.8.0";
        public const string IcmpOutEchos = ".1.3.6.1.2.1.5.21.0";
    }
}