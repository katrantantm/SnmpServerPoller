using SnmpServerPoller.Config;
using SnmpServerPoller.Logging;
using SnmpServerPoller.Models;
using SnmpServerPoller.Reporting;
using SnmpServerPoller.Snmp;

var config = ConfigurationLoader.Load();
var logger = new ConsoleLogger(config.Logging.LogLevel);
var excelFilePath = config.Excel.TemplatePath;
var serverIp = args.Length > 0 ? args[0] : config.DefaultServerIp;

logger.Info("🚀 Запуск SNMP Poller");
logger.Info("Целевой сервер: {ServerIp}", serverIp);

try
{
    await using var snmp = new SnmpManager(serverIp, config.Snmp.Community, logger);
    
    string? sysName = await snmp.GetScalarAsync(SnmpOids.SysName);
    if (string.IsNullOrEmpty(sysName))
    { 
        logger.Error("❌ SNMP недоступен на {ServerIp}", serverIp); 
        return; 
    }

    logger.Info("✅ Подключено к {SysName}. Чтение данных...", sysName);

    await using var excel = new ExcelReporter(excelFilePath, logger);
    await CollectAndWriteDataAsync(snmp, excel, logger);

    logger.Info("✅ Обработка завершена успешно");
    
    if (!args.Contains("--no-wait"))
    {
        Console.WriteLine("Готово. Нажмите Enter для выхода...");
        Console.ReadKey();
    }
}
catch (Exception ex)
{
    logger.Error(ex, "❌ Критическая ошибка");
    if (!args.Contains("--no-wait"))
        Console.ReadKey();
}

static async Task CollectAndWriteDataAsync(SnmpManager snmp, ExcelReporter excel, ILogger logger)
{
    excel.AddTitle("Период опроса: " + DateTime.Now);
    excel.AddSpacing();

    logger.Info("📋 Сбор системной информации...");
    WriteSystemInfo(snmp, excel);

    logger.Info("🔄 Сбор данных интерфейсов...");
    var interfaces = snmp.GetInterfaces();
    logger.Info("   Найдено интерфейсов: {Count}", interfaces.Count);
    excel.WriteInterfaces(interfaces);

    logger.Info("📡 Сбор IP-адресов...");
    var ips = snmp.GetIpAddresses();
    logger.Info("   Найдено IP: {Count}", ips.Count);
    excel.WriteIpAddresses(ips);

    logger.Info("🌐 Сбор ARP таблицы...");
    var arps = snmp.GetArpTable();
    logger.Info("   Найдено ARP записей: {Count}", arps.Count);
    excel.WriteArpTable(arps);

    logger.Info("🛣️ Сбор таблицы маршрутизации...");
    var routes = snmp.GetRoutingTable();
    logger.Info("   Найдено маршрутов: {Count}", routes.Count);
    excel.WriteRoutingTable(routes);

    logger.Info("💾 Сбор информации о дисках...");
    var disks = snmp.GetStorageInfo();
    logger.Info("   Найдено дисков: {Count}", disks.Count);
    excel.WriteDisks(disks);

    logger.Info("⚡ Сбор данных CPU...");
    var cpus = snmp.GetCPULoad();
    logger.Info("   Найдено ядер CPU: {Count}", cpus.Count);
    excel.WriteCPU(cpus);

    logger.Info("📋 Сбор списка процессов...");
    var procs = snmp.GetProcesses();
    logger.Info("   Найдено процессов: {Count}", procs.Count);
    excel.WriteProcesses(procs);

    logger.Info("🔧 Сбор информации об устройствах...");
    var devs = snmp.GetDevices();
    logger.Info("   Найдено устройств: {Count}", devs.Count);
    excel.WriteDevices(devs);

    logger.Info("📊 Сбор статистики протоколов...");
    WriteProtocolStats(snmp, excel);
    
    await Task.CompletedTask;
}

static void WriteSystemInfo(SnmpManager snmp, ExcelReporter excel)
{
    excel.AddTitle("Системная информация");
    excel.WriteScalar("Описание", snmp.GetScalar(SnmpOids.SysDescr) ?? "N/A");
    excel.WriteScalar("Имя хоста", snmp.GetScalar(SnmpOids.SysName) ?? "N/A");
    excel.WriteScalar("Время работы", snmp.GetScalar(SnmpOids.SysUpTime) ?? "N/A");
    excel.WriteScalar("Контакт", snmp.GetScalar(SnmpOids.SysContact) ?? "N/A");
    excel.WriteScalar("Расположение", snmp.GetScalar(SnmpOids.SysLocation) ?? "N/A");
    excel.AddSpacing();
}

static void WriteProtocolStats(SnmpManager snmp, ExcelReporter excel)
{
    var ipStats = new List<StatEntry>
    {
        new() { Name = "Forwarding", Value = snmp.GetScalar(SnmpOids.IpForwarding) == "1" ? "Yes" : "No" },
        new() { Name = "In Receives", Value = snmp.GetScalarAsLong(SnmpOids.IpInReceives).ToString("N0") },
        new() { Name = "Out Requests", Value = snmp.GetScalarAsLong(SnmpOids.IpOutRequests).ToString("N0") }
    };
    excel.WriteStats("Статистика IP", ipStats);

    var tcpStats = new List<StatEntry>
    {
        new() { Name = "Max Connections", Value = snmp.GetScalarAsLong(SnmpOids.TcpMaxConn).ToString("N0") },
        new() { Name = "In Segments", Value = snmp.GetScalarAsLong(SnmpOids.TcpInSegs).ToString("N0") },
        new() { Name = "Out Segments", Value = snmp.GetScalarAsLong(SnmpOids.TcpOutSegs).ToString("N0") }
    };
    excel.WriteStats("Статистика TCP", tcpStats);

    var udpStats = new List<StatEntry>
    {
        new() { Name = "In Datagrams", Value = snmp.GetScalarAsLong(SnmpOids.UdpInDatagrams).ToString("N0") },
        new() { Name = "Out Datagrams", Value = snmp.GetScalarAsLong(SnmpOids.UdpOutDatagrams).ToString("N0") }
    };
    excel.WriteStats("Статистика UDP", udpStats);

    var icmpStats = new List<StatEntry>
    {
        new() { Name = "In Msgs", Value = snmp.GetScalarAsLong(SnmpOids.IcmpInMsgs).ToString("N0") },
        new() { Name = "Out Msgs", Value = snmp.GetScalarAsLong(SnmpOids.IcmpOutMsgs).ToString("N0") },
        new() { Name = "In Echos", Value = snmp.GetScalarAsLong(SnmpOids.IcmpInEchos).ToString("N0") },
        new() { Name = "Out Echos", Value = snmp.GetScalarAsLong(SnmpOids.IcmpOutEchos).ToString("N0") }
    };
    excel.WriteStats("Статистика ICMP", icmpStats);

    var snmpStats = new List<StatEntry>
    {
        new() { Name = "In Pkts", Value = snmp.GetScalarAsLong(SnmpOids.SnmpInPkts).ToString("N0") },
        new() { Name = "Out Pkts", Value = snmp.GetScalarAsLong(SnmpOids.SnmpOutPkts).ToString("N0") }
    };
    excel.WriteStats("Статистика SNMP Агента", snmpStats);
}