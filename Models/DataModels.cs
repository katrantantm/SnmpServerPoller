namespace SnmpServerPoller.Models
{
    /// <summary>
    /// Информация о сетевом интерфейсе
    /// </summary>
    public class InterfaceInfo
    {
        public int Index { get; set; }
        public string? Description { get; set; }
        public int Type { get; set; }
        public long Mtu { get; set; }
        public ulong Speed { get; set; }
        public int AdminStatus { get; set; }
        public int OperStatus { get; set; }
        public ulong InOctets { get; set; }
        public ulong OutOctets { get; set; }
        public uint InErrors { get; set; }
        public uint OutErrors { get; set; }
    }

    /// <summary>
    /// Информация об IP адресе
    /// </summary>
    public class IpAddressInfo
    {
        public string? Address { get; set; }
        public string? Mask { get; set; }
        public int IfIndex { get; set; }
    }

    /// <summary>
    /// Запись ARP таблицы
    /// </summary>
    public class ArpEntry
    {
        public string? Ip { get; set; }
        public string? Mac { get; set; }
        public int IfIndex { get; set; }
        public int Type { get; set; }
    }

    /// <summary>
    /// Запись таблицы маршрутизации
    /// </summary>
    public class RouteEntry
    {
        public string? Dest { get; set; }
        public string? Mask { get; set; }
        public string? NextHop { get; set; }
        public int IfIndex { get; set; }
        public string? Type { get; set; }
        public string? Proto { get; set; }
        public int Metric { get; set; }
        public int Age { get; set; }
    }

    /// <summary>
    /// Информация о диске
    /// </summary>
    public class DiskInfo
    {
        public string? Description { get; set; }
        public double TotalMB { get; set; }
        public double UsedMB { get; set; }
        public double Percent { get; set; }
    }

    /// <summary>
    /// Информация о ядре CPU
    /// </summary>
    public class CpuCore
    {
        public int Index { get; set; }
        public int Load { get; set; }
    }

    /// <summary>
    /// Информация о процессе
    /// </summary>
    public class ProcessInfo
    {
        public string? Name { get; set; }
        public string? Path { get; set; }
        public string? Params { get; set; }
        public string? Type { get; set; }
        public string? Status { get; set; }
    }

    /// <summary>
    /// Информация об устройстве
    /// </summary>
    public class DeviceInfo
    {
        public int Type { get; set; }
        public string? Description { get; set; }
        public string? Status { get; set; }
        public uint Errors { get; set; }
    }

    /// <summary>
    /// Статистическая запись
    /// </summary>
    public class StatEntry
    {
        public string? Name { get; set; }
        public string? Value { get; set; }
    }
}
