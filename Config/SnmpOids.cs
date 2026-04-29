namespace SnmpServerPoller.Config;

public static class SnmpOids
{
    // System OIDs
    public const string SysDescr = ".1.3.6.1.2.1.1.1.0";
    public const string SysUpTime = ".1.3.6.1.2.1.1.3.0";
    public const string SysName = ".1.3.6.1.2.1.1.5.0";
    public const string SysContact = ".1.3.6.1.2.1.1.4.0";
    public const string SysLocation = ".1.3.6.1.2.1.1.6.0";

    // Interfaces OIDs
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

    // IP OIDs
    public const string IpAdEntAddr = ".1.3.6.1.2.1.4.20.1.1";
    public const string IpAdEntNetMask = ".1.3.6.1.2.1.4.20.1.3";
    public const string IpAdEntIfIndex = ".1.3.6.1.2.1.4.20.1.2";

    // Routing OIDs
    public const string IpRouteDest = ".1.3.6.1.2.1.4.21.1.1";
    public const string IpRouteMask = ".1.3.6.1.2.1.4.21.1.11";
    public const string IpRouteNextHop = ".1.3.6.1.2.1.4.21.1.7";
    public const string IpRouteIfIndex = ".1.3.6.1.2.1.4.21.1.2";
    public const string IpRouteType = ".1.3.6.1.2.1.4.21.1.8";
    public const string IpRouteProto = ".1.3.6.1.2.1.4.21.1.9";
    public const string IpRouteMetric1 = ".1.3.6.1.2.1.4.21.1.3";
    public const string IpRouteAge = ".1.3.6.1.2.1.4.21.1.10";

    // ARP OIDs
    public const string IpNetToMediaPhysAddress = ".1.3.6.1.2.1.4.22.1.2";
    public const string IpNetToMediaNetAddress = ".1.3.6.1.2.1.4.22.1.3";
    public const string IpNetToMediaIfIndex = ".1.3.6.1.2.1.4.22.1.1";
    public const string IpNetToMediaType = ".1.3.6.1.2.1.4.22.1.4";

    // Storage OIDs
    public const string HrStorageDescr = ".1.3.6.1.2.1.25.2.3.1.3";
    public const string HrStorageUnits = ".1.3.6.1.2.1.25.2.3.1.4";
    public const string HrStorageSize = ".1.3.6.1.2.1.25.2.3.1.5";
    public const string HrStorageUsed = ".1.3.6.1.2.1.25.2.3.1.6";

    // CPU OIDs
    public const string HrProcessorLoad = ".1.3.6.1.2.1.25.3.3.1.2";

    // Processes OIDs
    public const string HrSWRunName = ".1.3.6.1.2.1.25.4.2.1.2";
    public const string HrSWRunPath = ".1.3.6.1.2.1.25.4.2.1.4";
    public const string HrSWRunParams = ".1.3.6.1.2.1.25.4.2.1.5";
    public const string HrSWRunType = ".1.3.6.1.2.1.25.4.2.1.6";
    public const string HrSWRunStatus = ".1.3.6.1.2.1.25.4.2.1.7";

    // Devices OIDs
    public const string HrDeviceType = ".1.3.6.1.2.1.25.3.2.1.2";
    public const string HrDeviceDescr = ".1.3.6.1.2.1.25.3.2.1.3";
    public const string HrDeviceStatus = ".1.3.6.1.2.1.25.3.2.1.5";
    public const string HrDeviceErrors = ".1.3.6.1.2.1.25.3.2.1.6";

    // Stats OIDs
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
