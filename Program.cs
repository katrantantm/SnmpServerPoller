using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using SnmpServerPoller.Config;
using SnmpServerPoller.Logging;
using SnmpServerPoller.Models;
using SnmpServerPoller.Reporting;
using SnmpServerPoller.Snmp;

namespace SnmpServerPoller
{
    /// <summary>
    /// Конфигурация приложения
    /// </summary>
    public class AppConfig
    {
        public SnmpSettings Snmp { get; set; } = new();
        public ExcelSettings Excel { get; set; } = new();
        public LoggingSettings Logging { get; set; } = new();
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
        public string TemplatePath { get; set; } = "ServerReport.xlsx";
        public bool AutoSave { get; set; } = true;
    }

    public class LoggingSettings
    {
        public string LogLevel { get; set; } = "Information";
        public string FilePath { get; set; } = "poller.log";
    }

    /// <summary>
    /// Результат опроса сервера
    /// </summary>
    public class ServerSurveyResult
    {
        public string ServerIp { get; init; }
        public string SysName { get; init; }
        public SystemInfo SystemInfo { get; init; }
        public List<InterfaceInfo> Interfaces { get; init; } = new();
        public List<IpAddressInfo> IpAddresses { get; init; } = new();
        public List<ArpEntry> ArpTable { get; init; } = new();
        public List<RouteEntry> Routes { get; init; } = new();
        public List<DiskInfo> Disks { get; init; } = new();
        public List<CpuCore> CpuCores { get; init; } = new();
        public List<ProcessInfo> Processes { get; init; } = new();
        public List<DeviceInfo> Devices { get; init; } = new();
        public ProtocolStats ProtocolStats { get; init; } = new();
    }

    public class SystemInfo
    {
        public string Description { get; init; }
        public string HostName { get; init; }
        public string UpTime { get; init; }
        public string Contact { get; init; }
        public string Location { get; init; }
    }

    public class ProtocolStats
    {
        public List<StatEntry> Ip { get; init; } = new();
        public List<StatEntry> Tcp { get; init; } = new();
        public List<StatEntry> Udp { get; init; } = new();
        public List<StatEntry> Icmp { get; init; } = new();
        public List<StatEntry> Snmp { get; init; } = new();
    }

    class Program
    {
        static void Main(string[] args)
        {
            var config = LoadConfiguration("appsettings.json");
            ILogger logger = CreateLogger(config.Logging);

            try
            {
                logger.Info("🚀 Запуск SNMP Poller v2.0");
                logger.Info("Конфигурация загружена из appsettings.json");

                string serverIp = args.Length > 0 ? args[0] : "87.242.86.112";
                logger.Info("Целевой сервер: {0}", serverIp);

                var surveyResult = PerformSurvey(serverIp, config, logger);
                
                if (surveyResult != null)
                {
                    ExportToExcel(surveyResult, config.Excel.TemplatePath, logger);
                }

                logger.Info("✅ Обработка завершена успешно");
                Console.WriteLine("\nГотово. Нажмите Enter для выхода...");
                Console.ReadKey();
            }
            catch (Exception ex)
            {
                logger.Error("❌ Критическая ошибка", ex);
                Console.ReadKey();
            }
        }

        private static AppConfig LoadConfiguration(string configPath)
        {
            if (File.Exists(configPath))
            {
                string json = File.ReadAllText(configPath);
                return JsonConvert.DeserializeObject<AppConfig>(json) ?? new AppConfig();
            }
            return new AppConfig();
        }

        private static ILogger CreateLogger(LoggingSettings settings)
        {
            var loggers = new List<ILogger>();
            
            // Консольный логгер
            loggers.Add(new ConsoleLogger(settings.LogLevel));
            
            // Файловый логгер (если указан путь)
            if (!string.IsNullOrEmpty(settings.FilePath))
            {
                loggers.Add(new FileLogger(settings.FilePath, settings.LogLevel));
            }
            
            return loggers.Count == 1 ? loggers[0] : new CompositeLogger(loggers);
        }

        private static ServerSurveyResult PerformSurvey(string serverIp, AppConfig config, ILogger logger)
        {
            var snmp = new SnmpManager(serverIp, config.Snmp.Community, logger);
            
            // Проверка доступности SNMP
            string sysName = snmp.GetScalar(SnmpConfig.SysName);
            if (string.IsNullOrEmpty(sysName))
            { 
                logger.Error("❌ SNMP недоступен на {0}", serverIp); 
                return null;
            }

            logger.Info("✅ Подключено к {0}. Чтение данных...", sysName);

            return new ServerSurveyResult
            {
                ServerIp = serverIp,
                SysName = sysName,
                SystemInfo = CollectSystemInfo(snmp),
                Interfaces = snmp.GetInterfaces(),
                IpAddresses = snmp.GetIpAddresses(),
                ArpTable = snmp.GetArpTable(),
                Routes = snmp.GetRoutingTable(),
                Disks = snmp.GetStorageInfo(),
                CpuCores = snmp.GetCPULoad(),
                Processes = snmp.GetProcesses(),
                Devices = snmp.GetDevices(),
                ProtocolStats = CollectProtocolStats(snmp)
            };
        }

        private static SystemInfo CollectSystemInfo(SnmpManager snmp)
        {
            return new SystemInfo
            {
                Description = snmp.GetScalar(SnmpConfig.SysDescr),
                HostName = snmp.GetScalar(SnmpConfig.SysName),
                UpTime = snmp.GetScalar(SnmpConfig.SysUpTime),
                Contact = snmp.GetScalar(SnmpConfig.SysContact),
                Location = snmp.GetScalar(SnmpConfig.SysLocation)
            };
        }

        private static ProtocolStats CollectProtocolStats(SnmpManager snmp)
        {
            return new ProtocolStats
            {
                Ip = new List<StatEntry>
                {
                    new() { Name = "Forwarding", Value = snmp.GetScalar(SnmpConfig.IpForwarding) == "1" ? "Yes" : "No" },
                    new() { Name = "In Receives", Value = snmp.GetScalarAsLong(SnmpConfig.IpInReceives).ToString("N0") },
                    new() { Name = "Out Requests", Value = snmp.GetScalarAsLong(SnmpConfig.IpOutRequests).ToString("N0") }
                },
                Tcp = new List<StatEntry>
                {
                    new() { Name = "Max Connections", Value = snmp.GetScalarAsLong(SnmpConfig.TcpMaxConn).ToString("N0") },
                    new() { Name = "In Segments", Value = snmp.GetScalarAsLong(SnmpConfig.TcpInSegs).ToString("N0") },
                    new() { Name = "Out Segments", Value = snmp.GetScalarAsLong(SnmpConfig.TcpOutSegs).ToString("N0") }
                },
                Udp = new List<StatEntry>
                {
                    new() { Name = "In Datagrams", Value = snmp.GetScalarAsLong(SnmpConfig.UdpInDatagrams).ToString("N0") },
                    new() { Name = "Out Datagrams", Value = snmp.GetScalarAsLong(SnmpConfig.UdpOutDatagrams).ToString("N0") }
                },
                Icmp = new List<StatEntry>
                {
                    new() { Name = "In Msgs", Value = snmp.GetScalarAsLong(SnmpConfig.IcmpInMsgs).ToString("N0") },
                    new() { Name = "Out Msgs", Value = snmp.GetScalarAsLong(SnmpConfig.IcmpOutMsgs).ToString("N0") },
                    new() { Name = "In Echos", Value = snmp.GetScalarAsLong(SnmpConfig.IcmpInEchos).ToString("N0") },
                    new() { Name = "Out Echos", Value = snmp.GetScalarAsLong(SnmpConfig.IcmpOutEchos).ToString("N0") }
                },
                Snmp = new List<StatEntry>
                {
                    new() { Name = "In Pkts", Value = snmp.GetScalarAsLong(SnmpConfig.SnmpInPkts).ToString("N0") },
                    new() { Name = "Out Pkts", Value = snmp.GetScalarAsLong(SnmpConfig.SnmpOutPkts).ToString("N0") }
                }
            };
        }

        private static void ExportToExcel(ServerSurveyResult result, string excelFilePath, ILogger logger)
        {
            using var excel = new ExcelReporter(excelFilePath, logger);
            
            excel.AddTitle($"Период опроса: {DateTime.Now} | Сервер: {result.SysName} ({result.ServerIp})");
            excel.AddSpacing();

            logger.Info("📋 Запись системной информации...");
            excel.WriteSystemInfo(result.SystemInfo);
            excel.AddSpacing();

            logger.Info("🔄 Запись данных интерфейсов ({0} шт.)...", result.Interfaces.Count);
            excel.WriteInterfaces(result.Interfaces);

            logger.Info("📡 Запись IP-адресов ({0} шт.)...", result.IpAddresses.Count);
            excel.WriteIpAddresses(result.IpAddresses);

            logger.Info("🌐 Запись ARP таблицы ({0} записей)...", result.ArpTable.Count);
            excel.WriteArpTable(result.ArpTable);

            logger.Info("🛣️ Запись таблицы маршрутизации ({0} маршрутов)...", result.Routes.Count);
            excel.WriteRoutingTable(result.Routes);

            logger.Info("💾 Запись информации о дисках ({0} шт.)...", result.Disks.Count);
            excel.WriteDisks(result.Disks);

            logger.Info("⚡ Запись данных CPU ({0} ядер)...", result.CpuCores.Count);
            excel.WriteCPU(result.CpuCores);

            logger.Info("📋 Запись списка процессов ({0} шт.)...", result.Processes.Count);
            excel.WriteProcesses(result.Processes);

            logger.Info("🔧 Запись информации об устройствах ({0} шт.)...", result.Devices.Count);
            excel.WriteDevices(result.Devices);

            logger.Info("📊 Запись статистики протоколов...");
            excel.WriteStats("Статистика IP", result.ProtocolStats.Ip);
            excel.WriteStats("Статистика TCP", result.ProtocolStats.Tcp);
            excel.WriteStats("Статистика UDP", result.ProtocolStats.Udp);
            excel.WriteStats("Статистика ICMP", result.ProtocolStats.Icmp);
            excel.WriteStats("Статистика SNMP Агента", result.ProtocolStats.Snmp);
        }
    }
}
