using System;
using System.Collections.Generic;
using SnmpServerPoller.Config;
using SnmpServerPoller.Logging;
using SnmpServerPoller.Models;
using SnmpServerPoller.Reporting;
using SnmpServerPoller.Snmp;

namespace SnmpServerPoller
{
    /// <summary>
    /// Главная точка входа приложения SNMP Poller
    /// </summary>
    class Program
    {
        static void Main(string[] args)
        {
            AppConfiguration config = null;
            
            try
            {
                // Загрузка конфигурации из appsettings.json
                config = AppConfiguration.Load("appsettings.json");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка загрузки конфигурации: {ex.Message}");
                Console.WriteLine("Используются настройки по умолчанию...");
                config = new AppConfiguration();
            }

            ILogger logger = new ConsoleLogger(config.Logging.GetMinLogLevel());
            string serverIp = config.Snmp.TargetIp;

            try
            {
                logger.Info("🚀 Запуск SNMP Poller");
                logger.Info("Целевой сервер: {0}", serverIp);
                logger.Info("Community: {0}", config.Snmp.Community);
                logger.Info("Timeout: {0}ms, Retries: {1}", config.Snmp.Timeout, config.Snmp.Retries);

                // Создание SnmpManager с использованием конфигурации и автоматическим освобождением ресурсов
                using (SnmpManager snmp = new(config.Snmp, logger))
                {
                    string sysName = snmp.GetScalar(SnmpConfig.SysName);
                    if (string.IsNullOrEmpty(sysName))
                    { 
                        logger.Error("❌ SNMP недоступен на {0}", serverIp); 
                        Console.ReadKey(); 
                        return; 
                    }

                    logger.Info("✅ Подключено к {0}. Чтение данных...", sysName);

                    using ExcelReporter excel = new(config.Excel.OutputPath, logger);
                    excel.AddTitle("Период опроса: " + DateTime.Now);
                    excel.AddSpacing();

                    logger.Info("📋 Сбор системной информации...");
                    excel.AddTitle("Системная информация");
                    excel.WriteScalar("Описание", snmp.GetScalar(SnmpConfig.SysDescr));
                    excel.WriteScalar("Имя хоста", sysName);
                    excel.WriteScalar("Время работы", snmp.GetScalar(SnmpConfig.SysUpTime));
                    excel.WriteScalar("Контакт", snmp.GetScalar(SnmpConfig.SysContact));
                    excel.WriteScalar("Расположение", snmp.GetScalar(SnmpConfig.SysLocation));
                    excel.AddSpacing();

                    logger.Info("🔄 Сбор данных интерфейсов...");
                    var interfaces = snmp.GetInterfaces();
                    logger.Info("   Найдено интерфейсов: {0}", interfaces.Count);
                    excel.WriteInterfaces(interfaces);

                    logger.Info("📡 Сбор IP-адресов...");
                    var ips = snmp.GetIpAddresses();
                    logger.Info("   Найдено IP: {0}", ips.Count);
                    excel.WriteIpAddresses(ips);

                    logger.Info("🌐 Сбор ARP таблицы...");
                    var arps = snmp.GetArpTable();
                    logger.Info("   Найдено ARP записей: {0}", arps.Count);
                    excel.WriteArpTable(arps);

                    logger.Info("🛣️ Сбор таблицы маршрутизации...");
                    var routes = snmp.GetRoutingTable();
                    logger.Info("   Найдено маршрутов: {0}", routes.Count);
                    excel.WriteRoutingTable(routes);

                    logger.Info("💾 Сбор информации о дисках...");
                    var disks = snmp.GetStorageInfo();
                    logger.Info("   Найдено дисков: {0}", disks.Count);
                    excel.WriteDisks(disks);

                    logger.Info("⚡ Сбор данных CPU...");
                    var cpus = snmp.GetCPULoad();
                    logger.Info("   Найдено ядер CPU: {0}", cpus.Count);
                    excel.WriteCPU(cpus);

                    logger.Info("📋 Сбор списка процессов...");
                    var procs = snmp.GetProcesses();
                    logger.Info("   Найдено процессов: {0}", procs.Count);
                    excel.WriteProcesses(procs);

                    logger.Info("🔧 Сбор информации об устройствах...");
                    var devs = snmp.GetDevices();
                    logger.Info("   Найдено устройств: {0}", devs.Count);
                    excel.WriteDevices(devs);

                    logger.Info("📊 Сбор статистики протоколов...");

                    var ipStats = new List<StatEntry>
                        {
                            new() { Name = "Forwarding", Value = snmp.GetScalar(SnmpConfig.IpForwarding) == "1" ? "Yes" : "No" },
                            new() { Name = "In Receives", Value = snmp.GetScalarAsLong(SnmpConfig.IpInReceives).ToString("N0") },
                            new() { Name = "Out Requests", Value = snmp.GetScalarAsLong(SnmpConfig.IpOutRequests).ToString("N0") }
                        };
                    excel.WriteStats("Статистика IP", ipStats);

                    var tcpStats = new List<StatEntry>
                        {
                            new() { Name = "Max Connections", Value = snmp.GetScalarAsLong(SnmpConfig.TcpMaxConn).ToString("N0") },
                            new() { Name = "In Segments", Value = snmp.GetScalarAsLong(SnmpConfig.TcpInSegs).ToString("N0") },
                            new() { Name = "Out Segments", Value = snmp.GetScalarAsLong(SnmpConfig.TcpOutSegs).ToString("N0") }
                        };
                    excel.WriteStats("Статистика TCP", tcpStats);

                    var udpStats = new List<StatEntry>
                        {
                            new() { Name = "In Datagrams", Value = snmp.GetScalarAsLong(SnmpConfig.UdpInDatagrams).ToString("N0") },
                            new() { Name = "Out Datagrams", Value = snmp.GetScalarAsLong(SnmpConfig.UdpOutDatagrams).ToString("N0") }
                        };
                    excel.WriteStats("Статистика UDP", udpStats);

                    var icmpStats = new List<StatEntry>
                        {
                            new() { Name = "In Msgs", Value = snmp.GetScalarAsLong(SnmpConfig.IcmpInMsgs).ToString("N0") },
                            new() { Name = "Out Msgs", Value = snmp.GetScalarAsLong(SnmpConfig.IcmpOutMsgs).ToString("N0") },
                            new() { Name = "In Echos", Value = snmp.GetScalarAsLong(SnmpConfig.IcmpInEchos).ToString("N0") },
                            new() { Name = "Out Echos", Value = snmp.GetScalarAsLong(SnmpConfig.IcmpOutEchos).ToString("N0") }
                        };
                    excel.WriteStats("Статистика ICMP", icmpStats);

                    var snmpStats = new List<StatEntry>
                        {
                            new() { Name = "In Pkts", Value = snmp.GetScalarAsLong(SnmpConfig.SnmpInPkts).ToString("N0") },
                            new() { Name = "Out Pkts", Value = snmp.GetScalarAsLong(SnmpConfig.SnmpOutPkts).ToString("N0") }
                        };
                    excel.WriteStats("Статистика SNMP Агента", snmpStats);

                    logger.Info("✅ Обработка завершена успешно");
                    Console.WriteLine("Готово. Нажмите Enter для выхода...");
                    Console.ReadKey();
                }
            }
            catch (Exception ex)
            {
                logger?.Error("❌ Критическая ошибка: {0}", ex);
                Console.WriteLine($"Критическая ошибка: {ex.Message}");
                Console.ReadKey();
            }
        }
    }
}