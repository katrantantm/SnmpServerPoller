namespace SnmpServerPoller.Snmp;

public partial class SnmpManager : IDisposable
{
    private readonly string _targetIp;
    private readonly string _community;
    private readonly ILogger _logger;
    private bool _disposed;

    public SnmpManager(string targetIp, string community, ILogger? logger = null)
    {
        _targetIp = targetIp;
        _community = community;
        _logger = logger ?? new ConsoleLogger("Information");
    }

    public string? GetScalar(string oid)
    {
        try
        {
            _logger.Debug("Запрос OID: {Oid}", oid);
            using var snmp = new SimpleSnmp(_targetIp, _community);
            var result = snmp.Get(SnmpVersion.Ver2, [new Oid(oid)]);
            
            if (result != null && result.Count > 0)
            {
                string value = DecodeRawData(result.First().Value.ToString());
                _logger.Debug("Получено: {Oid} = {Value}", oid, value);
                return value;
            }
        }
        catch (Exception ex)
        {
            _logger.Warn("Ошибка при запросе {Oid}: {Message}", oid, ex.Message);
        }
        return null;
    }

    public async Task<string?> GetScalarAsync(string oid, CancellationToken cancellationToken = default)
    {
        return await Task.Run(() => GetScalar(oid), cancellationToken);
    }

    public ulong GetScalarAsLong(string oid)
    {
        string? rawStr = GetScalar(oid);
        if (string.IsNullOrEmpty(rawStr)) return 0;
        
        if (ulong.TryParse(rawStr, out ulong result)) return result;

        string clean = rawStr.Replace(" ", "").Replace("(", "").Replace(")", "");
        if (clean.Length % 2 != 0 || !IsHex(clean)) return 0;

        try
        {
            byte[] data = new byte[clean.Length / 2];
            for (int i = 0; i < clean.Length; i += 2)
                data[i / 2] = Convert.ToByte(clean.Substring(i, 2), 16);
            if (BitConverter.IsLittleEndian) Array.Reverse(data);
            if (data.Length == 4) return BitConverter.ToUInt32(data, 0);
            if (data.Length == 8) return BitConverter.ToUInt64(data, 0);
        }
        catch (Exception ex)
        {
            _logger.Debug("Не удалось преобразовать {Oid} в число: {Count}", oid, ex.Message);
        }
        return 0;
    }

    private string DecodeRawData(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        if (input.Contains(".") || input.Contains(":")) return input;
        
        string clean = input.Replace(" ", "");
        if (!IsHex(clean)) return input;
        
        try
        {
            byte[] data = new byte[clean.Length / 2];
            for (int i = 0; i < clean.Length; i += 2)
                data[i / 2] = Convert.ToByte(clean.Substring(i, 2), 16);
            
            if (data.Length == 6) 
                return string.Join(":", data.Select(b => b.ToString("X2")));
            if (data.Length == 4) 
                return string.Join(".", data);
            
            string decoded = System.Text.Encoding.UTF8.GetString(data);
            if (decoded.All(c => char.IsControl(c) || c >= 32)) return decoded;
        }
        catch { }
        
        return input;
    }

    private static bool IsHex(string input)
    {
        if (input.Length % 2 != 0) return false;
        foreach (char c in input)
            if (!"0123456789ABCDEFabcdef".Contains(c)) return false;
        return true;
    }

    public Dictionary<string, string> WalkTable(string rootOid)
    {
        var result = new Dictionary<string, string>();
        try
        {
            _logger.Debug("Walk таблицы: {Oid}", rootOid);
            using var snmp = new SimpleSnmp(_targetIp, _community);
            var snmpResult = snmp.Walk(SnmpVersion.Ver2, rootOid);
            if (snmpResult == null) return result;

            foreach (var kvp in snmpResult)
            {
                string fullOid = kvp.Key.ToString();
                string index = fullOid.StartsWith(rootOid + ".") 
                    ? fullOid.Substring(rootOid.Length + 1) 
                    : fullOid.Substring(rootOid.Length);
                result[index] = DecodeRawData(kvp.Value.ToString());
            }

            _logger.Debug("Walk {Oid}: получено {Count} записей", rootOid, result.Count);
        }
        catch (Exception ex)
        {
            _logger.Error("Ошибка Walk {Oid}: {Count}", rootOid, ex);
        }
        return result;
    }

    private static int ParseInt(string? val) => int.TryParse(val, out int i) ? i : 0;
    private static long ParseLong(string? val) => long.TryParse(val, out long l) ? l : 0;
    private static ulong ParseULong(string? val) => ulong.TryParse(val, out ulong ul) ? ul : 0;
    private static uint ParseUInt(string? val) => uint.TryParse(val, out uint ui) ? ui : 0;

    public List<InterfaceInfo> GetInterfaces()
    {
        _logger.Info("Сбор данных интерфейсов...");
        var interfaces = new Dictionary<int, InterfaceInfo>();
        
        foreach (var kvp in WalkTable(SnmpOids.IfDescr))
        {
            int idx = ParseInt(kvp.Key);
            interfaces[idx] = new InterfaceInfo { Index = idx, Description = kvp.Value };
        }
        
        MergeTableData(interfaces, WalkTable(SnmpOids.IfType), (i, v) => i.Type = ParseInt(v));
        MergeTableData(interfaces, WalkTable(SnmpOids.IfMtu), (i, v) => i.Mtu = ParseLong(v));
        MergeTableData(interfaces, WalkTable(SnmpOids.IfSpeed), (i, v) => i.Speed = ParseULong(v));
        MergeTableData(interfaces, WalkTable(SnmpOids.IfAdminStatus), (i, v) => i.AdminStatus = ParseInt(v));
        MergeTableData(interfaces, WalkTable(SnmpOids.IfOperStatus), (i, v) => i.OperStatus = ParseInt(v));
        MergeTableData(interfaces, WalkTable(SnmpOids.IfInOctets), (i, v) => i.InOctets = ParseULong(v));
        MergeTableData(interfaces, WalkTable(SnmpOids.IfOutOctets), (i, v) => i.OutOctets = ParseULong(v));

        var list = interfaces.Values.OrderBy(i => i.Index).ToList();
        _logger.Info("Найдено интерфейсов: {Count}", list.Count);
        return list;
    }

    private static void MergeTableData<T>(Dictionary<int, T> interfaces, Dictionary<string, string> table, Action<T, string> setter) where T : class
    {
        foreach (var kvp in table)
        {
            int idx = ParseInt(kvp.Key);
            if (interfaces.TryGetValue(idx, out var iface))
                setter(iface, kvp.Value);
        }
    }

    public List<IpAddressInfo> GetIpAddresses()
    {
        _logger.Info("Сбор IP-адресов...");
        var list = new List<IpAddressInfo>();
        var addresses = WalkTable(SnmpOids.IpAdEntAddr);
        var masks = WalkTable(SnmpOids.IpAdEntNetMask);
        var ifIndexes = WalkTable(SnmpOids.IpAdEntIfIndex);
        
        foreach (var kvp in addresses)
        {
            var entry = new IpAddressInfo
            {
                Address = kvp.Key,
                Mask = masks.GetValueOrDefault(kvp.Key, "0.0.0.0")
            };
            if (ifIndexes.TryGetValue(kvp.Key, out var ifIndex))
                entry.IfIndex = ParseInt(ifIndex);
            list.Add(entry);
        }
        
        _logger.Info("Найдено IP-адресов: {Count}", list.Count);
        return list;
    }

    public List<ArpEntry> GetArpTable()
    {
        _logger.Info("Сбор ARP таблицы...");
        var list = new List<ArpEntry>();
        var physAddrs = WalkTable(SnmpOids.IpNetToMediaPhysAddress);
        var ifIndexes = WalkTable(SnmpOids.IpNetToMediaIfIndex);
        var types = WalkTable(SnmpOids.IpNetToMediaType);
        
        foreach (var kvp in physAddrs)
        {
            string[] parts = kvp.Key.Split(['.'], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 5)
            {
                var entry = new ArpEntry
                {
                    IfIndex = int.Parse(parts[0]),
                    Ip = string.Join(".", parts[1], parts[2], parts[3], parts[4]),
                    Mac = kvp.Value
                };
                if (types.TryGetValue(kvp.Key, out var type))
                    entry.Type = ParseInt(type);
                if (entry.Type != 2) list.Add(entry);
            }
        }
        
        _logger.Info("Найдено ARP записей: {Count}", list.Count);
        return list;
    }

    public List<RouteEntry> GetRoutingTable()
    {
        _logger.Info("Сбор таблицы маршрутизации...");
        var list = new List<RouteEntry>();
        var dests = WalkTable(SnmpOids.IpRouteDest);
        var masks = WalkTable(SnmpOids.IpRouteMask);
        var nextHops = WalkTable(SnmpOids.IpRouteNextHop);
        var ifIndexes = WalkTable(SnmpOids.IpRouteIfIndex);
        var types = WalkTable(SnmpOids.IpRouteType);
        var protos = WalkTable(SnmpOids.IpRouteProto);
        var metrics = WalkTable(SnmpOids.IpRouteMetric1);
        var ages = WalkTable(SnmpOids.IpRouteAge);
        
        foreach (var kvp in dests)
        {
            var entry = new RouteEntry { Dest = kvp.Key };
            if (masks.TryGetValue(kvp.Key, out var mask)) entry.Mask = mask;
            if (nextHops.TryGetValue(kvp.Key, out var nextHop)) entry.NextHop = nextHop;
            if (ifIndexes.TryGetValue(kvp.Key, out var ifIndex)) entry.IfIndex = ParseInt(ifIndex);
            if (types.TryGetValue(kvp.Key, out var type)) entry.Type = ParseInt(type) == 3 ? "direct" : "indirect";
            if (protos.TryGetValue(kvp.Key, out var proto)) entry.Proto = ParseInt(proto) == 2 ? "local" : ParseInt(proto).ToString();
            if (metrics.TryGetValue(kvp.Key, out var metric)) entry.Metric = ParseInt(metric);
            if (ages.TryGetValue(kvp.Key, out var age)) entry.Age = ParseInt(age);
            list.Add(entry);
        }
        
        _logger.Info("Найдено маршрутов: {Count}", list.Count);
        return list;
    }

    public List<DiskInfo> GetStorageInfo()
    {
        _logger.Info("Сбор информации о дисках...");
        var list = new List<DiskInfo>();
        var descrs = WalkTable(SnmpOids.HrStorageDescr);
        var units = WalkTable(SnmpOids.HrStorageUnits);
        var sizes = WalkTable(SnmpOids.HrStorageSize);
        var used = WalkTable(SnmpOids.HrStorageUsed);
        
        foreach (var kvp in descrs)
        {
            double unitFactor = units.TryGetValue(kvp.Key, out var unit) ? ParseLong(unit) : 0;
            if (unitFactor == 0) unitFactor = 1024;
            
            double sizeVal = sizes.TryGetValue(kvp.Key, out var size) ? ParseLong(size) : 0;
            double usedVal = used.TryGetValue(kvp.Key, out var usedValStr) ? ParseLong(usedValStr) : 0;
            
            if (sizeVal > 1000)
            {
                list.Add(new DiskInfo
                {
                    Description = kvp.Value,
                    TotalMB = Math.Round((sizeVal * unitFactor) / 1048576, 0),
                    UsedMB = Math.Round((usedVal * unitFactor) / 1048576, 0),
                    Percent = sizeVal > 0 ? Math.Round((usedVal * 100.0 / sizeVal), 1) : 0
                });
            }
        }
        
        _logger.Info("Найдено дисков: {Count}", list.Count);
        return list;
    }

    public List<CpuCore> GetCPULoad()
    {
        _logger.Info("Сбор данных CPU...");
        var list = new List<CpuCore>();
        var loads = WalkTable(SnmpOids.HrProcessorLoad);
        int count = 0;
        foreach (var kvp in loads)
            list.Add(new CpuCore { Index = count++, Load = ParseInt(kvp.Value) });
        
        _logger.Info("Найдено ядер CPU: {Count}", list.Count);
        return list;
    }

    public List<ProcessInfo> GetProcesses()
    {
        _logger.Info("Сбор списка процессов...");
        var list = new List<ProcessInfo>();
        var names = WalkTable(SnmpOids.HrSWRunName);
        var paths = WalkTable(SnmpOids.HrSWRunPath);
        var params_st = WalkTable(SnmpOids.HrSWRunParams);
        var types = WalkTable(SnmpOids.HrSWRunType);
        var statuses = WalkTable(SnmpOids.HrSWRunStatus);
        int count = 0;
        
        foreach (var kvp in names)
        {
            if (count >= SnmpOids.MaxProcesses) break;
            var p = new ProcessInfo { Name = kvp.Value };
            if (paths.TryGetValue(kvp.Key, out var path)) p.Path = path;
            if (params_st.TryGetValue(kvp.Key, out var param)) p.Params = param;
            int typeVal = types.TryGetValue(kvp.Key, out var type) ? ParseInt(type) : 1;
            p.Type = typeVal == 4 ? "App" : "System";
            int statusVal = statuses.TryGetValue(kvp.Key, out var status) ? ParseInt(status) : 0;
            p.Status = (statusVal == 1 || statusVal == 2) ? "Running" : "Other";
            list.Add(p); 
            count++;
        }
        
        _logger.Info("Найдено процессов: {Oid}", list.Count);
        return list;
    }

    public List<DeviceInfo> GetDevices()
    {
        _logger.Info("Сбор информации об устройствах...");
        var list = new List<DeviceInfo>();
        var types = WalkTable(SnmpOids.HrDeviceType);
        var descrs = WalkTable(SnmpOids.HrDeviceDescr);
        var statuses = WalkTable(SnmpOids.HrDeviceStatus);
        var errors = WalkTable(SnmpOids.HrDeviceErrors);
        
        foreach (var kvp in types)
        {
            int t = ParseInt(kvp.Value);
            if (t > 0)
            {
                var d = new DeviceInfo { Type = t };
                if (descrs.TryGetValue(kvp.Key, out var descr)) d.Description = descr;
                int s = statuses.TryGetValue(kvp.Key, out var status) ? ParseInt(status) : 0;
                d.Status = s == 2 ? "Running" : "Unknown";
                if (errors.TryGetValue(kvp.Key, out var error)) d.Errors = ParseUInt(error);
                list.Add(d);
            }
        }
        
        _logger.Info("Найдено устройств: {Oid}", list.Count);
        return list;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                // Cleanup managed resources if needed
            }
            _disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    ~SnmpManager() => Dispose(disposing: false);
}