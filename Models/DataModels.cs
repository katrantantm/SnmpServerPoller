namespace SnmpServerPoller.Models
{
    public class InterfaceInfo
    {
        public int Index { get; set; }
        public string Description { get; set; }
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

    public class IpAddressInfo
    {
        public string Address { get; set; }
        public string Mask { get; set; }
        public int IfIndex { get; set; }
    }

    public class ArpEntry
    {
        public string Ip { get; set; }
        public string Mac { get; set; }
        public int IfIndex { get; set; }
        public int Type { get; set; }
    }

    public class RouteEntry
    {
        public string Dest { get; set; }
        public string Mask { get; set; }
        public string NextHop { get; set; }
        public int IfIndex { get; set; }
        public string Type { get; set; }
        public string Proto { get; set; }
        public int Metric { get; set; }
        public int Age { get; set; }
    }

    public class DiskInfo
    {
        public string Description { get; set; }
        public double TotalMB { get; set; }
        public double UsedMB { get; set; }
        public double Percent { get; set; }
    }

    public class CpuCore
    {
        public int Index { get; set; }
        public int Load { get; set; }
    }

    public class ProcessInfo
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public string Params { get; set; }
        public string Type { get; set; }
        public string Status { get; set; }
    }

    public class DeviceInfo
    {
        public int Type { get; set; }
        public string Description { get; set; }
        public string Status { get; set; }
        public uint Errors { get; set; }
    }

    public class StatEntry
    {
        public string Name { get; set; }
        public string Value { get; set; }
    }
}