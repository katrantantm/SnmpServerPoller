using System;
using System.Collections.Generic;
using SnmpServerPoller.Config;
using SnmpServerPoller.Logging;
using SnmpServerPoller.Models;
using SnmpServerPoller.Reporting;
using SnmpServerPoller.Snmp;

namespace SnmpServerPoller;

public class Program
{
    private static readonly ILogger Logger = new ConsoleLogger("Information");
    
    public static void Main(string[] args)
    {
        var config = ConfigurationLoader.Load();
        string excelFilePath = config.Excel.TemplatePath;
        string serverIp = args.Length > 0 ? args[0] : config.DefaultServerIp;

        try
        {
            Logger.Info("🚀 Запуск SNMP Poller");
            Logger.Info("Целевой сервер: {0}", serverIp);

            using var snmp = new SnmpManager(serverIp, config.Snmp.Community, Logger);
            
            string sysName = snmp.GetScalar(SnmpConfig.SysName);
            if (string.IsNullOrEmpty(sysName))
            { 
                Logger.Error("❌ SNMP недоступен на {0}", serverIp); 
                return; 
            }

            Logger.Info("✅ Подключено к {0}. Чтение данных...", sysName);

            using var excel = new ExcelReporter(excelFilePath, Logger);
            CollectAndWriteData(snmp, excel);

            Logger.Info("✅ Обработка завершена успешно");
            Console.WriteLine("Готово. Нажмите Enter для выхода...");
            Console.ReadKey();
        }
        catch (Exception ex)
        {
            Logger.Error("❌ Критическая ошибка", ex);
            Console.ReadKey();
        }
    }

    private static void CollectAndWriteData(SnmpManager snmp, ExcelReporter excel)
    {
        excel.AddTitle("Период опроса: " + DateTime.Now);
        excel.AddSpacing();

        Logger.Info("📋 Сбор системной информации...");
        WriteSystemInfo(snmp, excel);

        Logger.Info("🔄 Сбор данных интерфейсов...");
        var interfaces = snmp.GetInterfaces();
        Logger.Info("   Найдено интерфейсов: {0}", interfaces.Count);
        excel.WriteInterfaces(interfaces);

        Logger.Info("📡 Сбор IP-адресов...");
        var ips = snmp.GetIpAddresses();
        Logger.Info("   Найдено IP: {0}", ips.Count);
        excel.WriteIpAddresses(ips);

        Logger.Info("🌐 Сбор ARP таблицы...");
        var arps = snmp.GetArpTable();
        Logger.Info("   Найдено ARP записей: {0}", arps.Count);
        excel.WriteArpTable(arps);

        Logger.Info("🛣️ Сбор таблицы маршрутизации...");
        var routes = snmp.GetRoutingTable();
        Logger.Info("   Найдено маршрутов: {0}", routes.Count);
        excel.WriteRoutingTable(routes);

        Logger.Info("💾 Сбор информации о дисках...");
        var disks = snmp.GetStorageInfo();
        Logger.Info("   Найдено дисков: {0}", disks.Count);
        excel.WriteDisks(disks);

        Logger.Info("⚡ Сбор данных CPU...");
        var cpus = snmp.GetCPULoad();
        Logger.Info("   Найдено ядер CPU: {0}", cpus.Count);
        excel.WriteCPU(cpus);

        Logger.Info("📋 Сбор списка процессов...");
        var procs = snmp.GetProcesses();
        Logger.Info("   Найдено процессов: {0}", procs.Count);
        excel.WriteProcesses(procs);

        Logger.Info("🔧 Сбор информации об устройствах...");
        var devs = snmp.GetDevices();
        Logger.Info("   Найдено устройств: {0}", devs.Count);
        excel.WriteDevices(devs);

        Logger.Info("📊 Сбор статистики протоколов...");
        WriteProtocolStats(snmp, excel);
    }

    private static void WriteSystemInfo(SnmpManager snmp, ExcelReporter excel)
    {
        excel.AddTitle("Системная информация");
        excel.WriteScalar("Описание", snmp.GetScalar(SnmpConfig.SysDescr));
        excel.WriteScalar("Имя хоста", snmp.GetScalar(SnmpConfig.SysName));
        excel.WriteScalar("Время работы", snmp.GetScalar(SnmpConfig.SysUpTime));
        excel.WriteScalar("Контакт", snmp.GetScalar(SnmpConfig.SysContact));
        excel.WriteScalar("Расположение", snmp.GetScalar(SnmpConfig.SysLocation));
        excel.AddSpacing();
    }

    private static void WriteProtocolStats(SnmpManager snmp, ExcelReporter excel)
    {
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
    }
}