using System;
using System.Collections.Generic;
using System.Linq;
using SnmpServerPoller.Config;
using SnmpServerPoller.Logging;
using SnmpServerPoller.Models;
using SnmpServerPoller.Reporting;
using SnmpServerPoller.Snmp;

namespace SnmpServerPoller
{
    class Program
    {
        static void Main(string[] args)
        {
            // Загрузка конфигурации из файла
            var config = ConfigurationLoader.Load();
            
            // Создание логгера с записью в файл и консоль
            ILogger logger = new CompositeLogger(
                new ConsoleLogger(config.Logging?.LogLevel ?? "Information"),
                new FileLogger(config.Logging?.FilePath ?? "logs/poller.log", config.Logging?.LogLevel ?? "Information")
            );

            string serverIp = args.Length > 0 ? args[0] : config.Snmp?.TargetIp ?? "87.242.86.112";
            string outputPath = config.Export?.OutputPath ?? "output";
            string[] exportFormats = config.Export?.Formats ?? new[] { "csv" };

            try
            {
                logger.Info("🚀 Запуск SNMP Poller");
                logger.Info("Целевой сервер: {0}", serverIp);
                logger.Info("Форматы экспорта: {0}", string.Join(", ", exportFormats));

                SnmpManager snmp = new(serverIp, SnmpConfig.Community, logger);
                
                string sysName = snmp.GetScalar(SnmpConfig.SysName);
                if (string.IsNullOrEmpty(sysName))
                { 
                    logger.Error("❌ SNMP недоступен на {0}", serverIp); 
                    Console.ReadKey(); 
                    return; 
                }

                logger.Info("✅ Подключено к {0}. Чтение данных...", sysName);

                // Сбор данных
                logger.Info("📋 Сбор системной информации...");
                var sysDescr = snmp.GetScalar(SnmpConfig.SysDescr);
                var sysUpTime = snmp.GetScalar(SnmpConfig.SysUpTime);
                var sysContact = snmp.GetScalar(SnmpConfig.SysContact);
                var sysLocation = snmp.GetScalar(SnmpConfig.SysLocation);

                logger.Info("🔄 Сбор данных интерфейсов...");
                var interfaces = snmp.GetInterfaces();
                logger.Info("   Найдено интерфейсов: {0}", interfaces.Count);

                logger.Info("📡 Сбор IP-адресов...");
                var ips = snmp.GetIpAddresses();
                logger.Info("   Найдено IP: {0}", ips.Count);

                logger.Info("🌐 Сбор ARP таблицы...");
                var arps = snmp.GetArpTable();
                logger.Info("   Найдено ARP записей: {0}", arps.Count);

                logger.Info("🛣️ Сбор таблицы маршрутизации...");
                var routes = snmp.GetRoutingTable();
                logger.Info("   Найдено маршрутов: {0}", routes.Count);

                logger.Info("💾 Сбор информации о дисках...");
                var disks = snmp.GetStorageInfo();
                logger.Info("   Найдено дисков: {0}", disks.Count);

                logger.Info("⚡ Сбор данных CPU...");
                var cpus = snmp.GetCPULoad();
                logger.Info("   Найдено ядер CPU: {0}", cpus.Count);

                logger.Info("📋 Сбор списка процессов...");
                var procs = snmp.GetProcesses();
                logger.Info("   Найдено процессов: {0}", procs.Count);

                logger.Info("🔧 Сбор информации об устройствах...");
                var devs = snmp.GetDevices();
                logger.Info("   Найдено устройств: {0}", devs.Count);

                logger.Info("📊 Сбор статистики протоколов...");
                var ipStats = new List<StatEntry>
                    {
                        new() { Name = "Forwarding", Value = snmp.GetScalar(SnmpConfig.IpForwarding) == "1" ? "Yes" : "No" },
                        new() { Name = "In Receives", Value = snmp.GetScalarAsLong(SnmpConfig.IpInReceives).ToString("N0") },
                        new() { Name = "Out Requests", Value = snmp.GetScalarAsLong(SnmpConfig.IpOutRequests).ToString("N0") }
                    };

                var tcpStats = new List<StatEntry>
                    {
                        new() { Name = "Max Connections", Value = snmp.GetScalarAsLong(SnmpConfig.TcpMaxConn).ToString("N0") },
                        new() { Name = "In Segments", Value = snmp.GetScalarAsLong(SnmpConfig.TcpInSegs).ToString("N0") },
                        new() { Name = "Out Segments", Value = snmp.GetScalarAsLong(SnmpConfig.TcpOutSegs).ToString("N0") }
                    };

                var udpStats = new List<StatEntry>
                    {
                        new() { Name = "In Datagrams", Value = snmp.GetScalarAsLong(SnmpConfig.UdpInDatagrams).ToString("N0") },
                        new() { Name = "Out Datagrams", Value = snmp.GetScalarAsLong(SnmpConfig.UdpOutDatagrams).ToString("N0") }
                    };

                var icmpStats = new List<StatEntry>
                    {
                        new() { Name = "In Msgs", Value = snmp.GetScalarAsLong(SnmpConfig.IcmpInMsgs).ToString("N0") },
                        new() { Name = "Out Msgs", Value = snmp.GetScalarAsLong(SnmpConfig.IcmpOutMsgs).ToString("N0") },
                        new() { Name = "In Echos", Value = snmp.GetScalarAsLong(SnmpConfig.IcmpInEchos).ToString("N0") },
                        new() { Name = "Out Echos", Value = snmp.GetScalarAsLong(SnmpConfig.IcmpOutEchos).ToString("N0") }
                    };

                var snmpStats = new List<StatEntry>
                    {
                        new() { Name = "In Pkts", Value = snmp.GetScalarAsLong(SnmpConfig.SnmpInPkts).ToString("N0") },
                        new() { Name = "Out Pkts", Value = snmp.GetScalarAsLong(SnmpConfig.SnmpOutPkts).ToString("N0") }
                    };

                // Экспорт в Excel (если требуется и доступен)
                if (exportFormats.Contains("excel") && !string.IsNullOrEmpty(config.Excel?.TemplatePath))
                {
                    try
                    {
                        using ExcelReporter excel = new(config.Excel.TemplatePath, logger);
                        excel.AddTitle("Период опроса: " + DateTime.Now);
                        excel.AddSpacing();

                        excel.AddTitle("Системная информация");
                        excel.WriteScalar("Описание", sysDescr);
                        excel.WriteScalar("Имя хоста", sysName);
                        excel.WriteScalar("Время работы", sysUpTime);
                        excel.WriteScalar("Контакт", sysContact);
                        excel.WriteScalar("Расположение", sysLocation);
                        excel.AddSpacing();

                        excel.WriteInterfaces(interfaces);
                        excel.WriteIpAddresses(ips);
                        excel.WriteArpTable(arps);
                        excel.WriteRoutingTable(routes);
                        excel.WriteDisks(disks);
                        excel.WriteCPU(cpus);
                        excel.WriteProcesses(procs);
                        excel.WriteDevices(devs);
                        excel.WriteStats("Статистика IP", ipStats);
                        excel.WriteStats("Статистика TCP", tcpStats);
                        excel.WriteStats("Статистика UDP", udpStats);
                        excel.WriteStats("Статистика ICMP", icmpStats);
                        excel.WriteStats("Статистика SNMP Агента", snmpStats);

                        logger.Info("✅ Excel экспорт завершен");
                    }
                    catch (Exception ex)
                    {
                        logger.Warn("⚠️ Ошибка экспорта в Excel: {0}. Продолжаем с другими форматами.", ex.Message);
                    }
                }

                // Экспорт в CSV
                if (exportFormats.Contains("csv"))
                {
                    using CsvReporter csv = new(outputPath, logger);
                    csv.WriteInterfaces(interfaces);
                    csv.WriteIpAddresses(ips);
                    csv.WriteArpTable(arps);
                    csv.WriteRoutingTable(routes);
                    csv.WriteDisks(disks);
                    csv.WriteCPU(cpus);
                    csv.WriteProcesses(procs);
                    csv.WriteDevices(devs);
                    csv.WriteStats("Статистика IP", ipStats);
                    csv.WriteStats("Статистика TCP", tcpStats);
                    csv.WriteStats("Статистика UDP", udpStats);
                    csv.WriteStats("Статистика ICMP", icmpStats);
                    csv.WriteStats("Статистика SNMP Агента", snmpStats);
                    logger.Info("✅ CSV экспорт завершен");
                }

                // Экспорт в PDF (текстовый формат)
                if (exportFormats.Contains("pdf"))
                {
                    using PdfReporter pdf = new(outputPath, logger);
                    pdf.WriteInterfaces(interfaces);
                    pdf.WriteIpAddresses(ips);
                    pdf.WriteArpTable(arps);
                    pdf.WriteRoutingTable(routes);
                    pdf.WriteDisks(disks);
                    pdf.WriteCPU(cpus);
                    pdf.WriteProcesses(procs);
                    pdf.WriteDevices(devs);
                    pdf.WriteStats("Статистика IP", ipStats);
                    pdf.WriteStats("Статистика TCP", tcpStats);
                    pdf.WriteStats("Статистика UDP", udpStats);
                    pdf.WriteStats("Статистика ICMP", icmpStats);
                    pdf.WriteStats("Статистика SNMP Агента", snmpStats);
                    logger.Info("✅ PDF экспорт завершен");
                }

                logger.Info("✅ Обработка завершена успешно");
                Console.WriteLine("Готово. Нажмите Enter для выхода...");
                Console.ReadKey();
            }
            catch (Exception ex)
            {
                logger.Error("❌ Критическая ошибка: {0}", ex);
                Console.ReadKey();
            }
        }
    }
}