using SnmpSharpNet;
using SnmpServerPoller.Config;
using SnmpServerPoller.Logging;
using SnmpServerPoller.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;

namespace SnmpServerPoller.Snmp
{
    /// <summary>
    /// Интерфейс для SNMP-клиента, позволяющий мокировать зависимости в тестах
    /// </summary>
    public interface ISnmpClient
    {
        string GetScalar(string oid);
        ulong GetScalarAsLong(string oid);
        Dictionary<string, string> WalkTable(string rootOid);
    }

    /// <summary>
    /// Менеджер для работы с SNMP-запросами
    /// Реализует IDisposable для освобождения ресурсов
    /// </summary>
    public class SnmpManager : ISnmpClient, IDisposable
    {
        private readonly string _targetIp;
        private readonly string _community;
        private readonly ILogger _logger;
        private readonly int _timeout;
        private readonly int _retries;
        private bool _disposed = false;

        /// <summary>
        /// Конструктор с параметрами конфигурации
        /// </summary>
        public SnmpManager(SnmpSettings settings, ILogger logger = null)
            : this(settings.TargetIp, settings.Community, logger, settings.Timeout, settings.Retries)
        {
        }

        /// <summary>
        /// Конструктор с явными параметрами (для обратной совместимости)
        /// </summary>
        public SnmpManager(string targetIp, string community, ILogger logger = null, int timeout = 3000, int retries = 2)
        {
            if (string.IsNullOrWhiteSpace(targetIp))
                throw new ArgumentException("Target IP не может быть пустым", nameof(targetIp));
            
            if (!IPAddress.TryParse(targetIp, out _))
                throw new ArgumentException($"Неверный формат IP-адреса: {targetIp}", nameof(targetIp));
            
            if (string.IsNullOrWhiteSpace(community))
                throw new ArgumentException("Community string не может быть пустым", nameof(community));

            _targetIp = targetIp;
            _community = community;
            _logger = logger ?? new ConsoleLogger();
            _timeout = Math.Max(1000, Math.Min(timeout, 30000)); // Ограничение 1-30 сек
            _retries = Math.Max(0, Math.Min(retries, 5)); // Ограничение 0-5 попыток
            
            _logger.Debug("SnmpManager инициализирован для {0} (timeout={1}ms, retries={2})", 
                _targetIp, _timeout, _retries);
        }

        public string GetScalar(string oid)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(SnmpManager));

            try
            {
                _logger.Debug("Запрос OID: {0}", oid);
                
                using (SimpleSnmp snmp = new(_targetIp, _community))
                {
                    // Применение настроек таймаута и повторных попыток
                    snmp.Timeout = _timeout;
                    snmp.MaxRetry = _retries; // Исправлено: было snmp.Retries
                    
                    Dictionary<Oid, AsnType> result = snmp.Get(SnmpVersion.Ver2, new[] { oid });
                    if (result != null && result.Count > 0)
                    {
                        string value = DecodeRawData(result.First().Value.ToString());
                        _logger.Debug("Получено: {0} = {1}", oid, value);
                        return value;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Warn("Ошибка при запросе {0}: {1}", oid, ex.Message);
            }
            return null;
        }

        public ulong GetScalarAsLong(string oid)
        {
            string rawStr = GetScalar(oid);
            ulong result;
            if (ulong.TryParse(rawStr, out result)) return result;

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
                _logger.Debug("Не удалось преобразовать {0} в число: {1}", oid, ex.Message);
            }
            return 0;
        }

        private string DecodeRawData(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            if (input.Contains(".") || input.Contains(":")) return input;
            string clean = input.Replace(" ", "");
            if (IsHex(clean))
            {
                try
                {
                    byte[] data = new byte[clean.Length / 2];
                    for (int i = 0; i < clean.Length; i += 2)
                        data[i / 2] = Convert.ToByte(clean.Substring(i, 2), 16);
                    if (data.Length == 6) return string.Join(":", data.Select(b => b.ToString("X2")));
                    if (data.Length == 4) return string.Join(".", data);
                    string decoded = Encoding.UTF8.GetString(data);
                    if (decoded.All(c => char.IsControl(c) || c >= 32)) return decoded;
                }
                catch (Exception ex)
                {
                    _logger?.Debug($"Ошибка декодирования HEX '{input}': {ex.Message}");
                }
            }
            return input;
        }

        private bool IsHex(string input)
        {
            if (input.Length % 2 != 0) return false;
            foreach (char c in input)
                if (!"0123456789ABCDEFabcdef".Contains(c)) return false;
            return true;
        }

        public Dictionary<string, string> WalkTable(string rootOid)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(SnmpManager));

            var result = new Dictionary<string, string>();
            try
            {
                _logger.Debug("Walk таблицы: {0}", rootOid);
                
                using (SimpleSnmp snmp = new(_targetIp, _community))
                {
                    // Применение настроек таймаута и повторных попыток
                    snmp.Timeout = _timeout;
                    snmp.MaxRetry = _retries; // Исправлено: было snmp.Retries
                    
                    Dictionary<Oid, AsnType> snmpResult = snmp.Walk(SnmpVersion.Ver2, rootOid);
                    if (snmpResult == null) return result;

                    foreach (var kvp in snmpResult)
                    {
                        string fullOid = kvp.Key.ToString();
                        string index = fullOid.Substring(rootOid.Length);
                        if (index.StartsWith(".")) index = index.Substring(1);
                        result[index] = DecodeRawData(kvp.Value.ToString());
                    }

                    _logger.Debug("Walk {0}: получено {1} записей", rootOid, result.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.Error("Ошибка Walk {0}: {1}", rootOid, ex);
            }
            return result;
        }

        private int ParseInt(string val) { return int.TryParse(val, out int i) ? i : 0; }
        private long ParseLong(string val) { return long.TryParse(val, out long l) ? l : 0; }
        private ulong ParseULong(string val) { return ulong.TryParse(val, out ulong ul) ? ul : 0; }
        private uint ParseUInt(string val) { return uint.TryParse(val, out uint ui) ? ui : 0; }

        public List<InterfaceInfo> GetInterfaces()
        {
            _logger.Info("Сбор данных интерфейсов...");
            var interfaces = new Dictionary<int, InterfaceInfo>();
            foreach (var kvp in WalkTable(SnmpConfig.IfDescr))
            {
                int idx = ParseInt(kvp.Key);
                interfaces[idx] = new InterfaceInfo { Index = idx, Description = kvp.Value };
            }
            foreach (var kvp in WalkTable(SnmpConfig.IfType))
                if (interfaces.ContainsKey(ParseInt(kvp.Key))) interfaces[ParseInt(kvp.Key)].Type = ParseInt(kvp.Value);
            foreach (var kvp in WalkTable(SnmpConfig.IfMtu))
                if (interfaces.ContainsKey(ParseInt(kvp.Key))) interfaces[ParseInt(kvp.Key)].Mtu = ParseLong(kvp.Value);
            foreach (var kvp in WalkTable(SnmpConfig.IfSpeed))
                if (interfaces.ContainsKey(ParseInt(kvp.Key))) interfaces[ParseInt(kvp.Key)].Speed = ParseULong(kvp.Value);
            foreach (var kvp in WalkTable(SnmpConfig.IfAdminStatus))
                if (interfaces.ContainsKey(ParseInt(kvp.Key))) interfaces[ParseInt(kvp.Key)].AdminStatus = ParseInt(kvp.Value);
            foreach (var kvp in WalkTable(SnmpConfig.IfOperStatus))
                if (interfaces.ContainsKey(ParseInt(kvp.Key))) interfaces[ParseInt(kvp.Key)].OperStatus = ParseInt(kvp.Value);
            foreach (var kvp in WalkTable(SnmpConfig.IfInOctets))
                if (interfaces.ContainsKey(ParseInt(kvp.Key))) interfaces[ParseInt(kvp.Key)].InOctets = ParseULong(kvp.Value);
            foreach (var kvp in WalkTable(SnmpConfig.IfOutOctets))
                if (interfaces.ContainsKey(ParseInt(kvp.Key))) interfaces[ParseInt(kvp.Key)].OutOctets = ParseULong(kvp.Value);

            var list = interfaces.Values.OrderBy(i => i.Index).ToList();
            _logger.Info("Найдено интерфейсов: {0}", list.Count);
            return list;
        }

        public List<IpAddressInfo> GetIpAddresses()
        {
            _logger.Info("Сбор IP-адресов...");
            var list = new List<IpAddressInfo>();
            var addresses = WalkTable(SnmpConfig.IpAdEntAddr);
            var masks = WalkTable(SnmpConfig.IpAdEntNetMask);
            var ifIndexes = WalkTable(SnmpConfig.IpAdEntIfIndex);
            foreach (var kvp in addresses)
            {
                var entry = new IpAddressInfo
                {
                    Address = kvp.Key,
                    Mask = masks.ContainsKey(kvp.Key) ? masks[kvp.Key] : "0.0.0.0"
                };
                if (ifIndexes.ContainsKey(kvp.Key)) entry.IfIndex = ParseInt(ifIndexes[kvp.Key]);
                list.Add(entry);
            }
            _logger.Info("Найдено IP-адресов: {0}", list.Count);
            return list;
        }

        public List<ArpEntry> GetArpTable()
        {
            _logger.Info("Сбор ARP таблицы...");
            var list = new List<ArpEntry>();
            var physAddrs = WalkTable(SnmpConfig.IpNetToMediaPhysAddress);
            var ifIndexes = WalkTable(SnmpConfig.IpNetToMediaIfIndex);
            var types = WalkTable(SnmpConfig.IpNetToMediaType);
            foreach (var kvp in physAddrs)
            {
                string[] parts = kvp.Key.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 5)
                {
                    var entry = new ArpEntry
                    {
                        IfIndex = int.Parse(parts[0]),
                        Ip = string.Join(".", parts[1], parts[2], parts[3], parts[4]),
                        Mac = kvp.Value
                    };
                    if (types.ContainsKey(kvp.Key)) entry.Type = ParseInt(types[kvp.Key]);
                    if (entry.Type != 2) list.Add(entry);
                }
            }
            _logger.Info("Найдено ARP записей: {0}", list.Count);
            return list;
        }

        public List<RouteEntry> GetRoutingTable()
        {
            _logger.Info("Сбор таблицы маршрутизации...");
            var list = new List<RouteEntry>();
            var dests = WalkTable(SnmpConfig.IpRouteDest);
            var masks = WalkTable(SnmpConfig.IpRouteMask);
            var nextHops = WalkTable(SnmpConfig.IpRouteNextHop);
            var ifIndexes = WalkTable(SnmpConfig.IpRouteIfIndex);
            var types = WalkTable(SnmpConfig.IpRouteType);
            var protos = WalkTable(SnmpConfig.IpRouteProto);
            var metrics = WalkTable(SnmpConfig.IpRouteMetric1);
            var ages = WalkTable(SnmpConfig.IpRouteAge);
            foreach (var kvp in dests)
            {
                var entry = new RouteEntry { Dest = kvp.Key };
                if (masks.ContainsKey(kvp.Key)) entry.Mask = masks[kvp.Key];
                if (nextHops.ContainsKey(kvp.Key)) entry.NextHop = nextHops[kvp.Key];
                if (ifIndexes.ContainsKey(kvp.Key)) entry.IfIndex = ParseInt(ifIndexes[kvp.Key]);
                if (types.ContainsKey(kvp.Key)) entry.Type = ParseInt(types[kvp.Key]) == 3 ? "direct" : "indirect";
                if (protos.ContainsKey(kvp.Key)) entry.Proto = ParseInt(protos[kvp.Key]) == 2 ? "local" : ParseInt(protos[kvp.Key]).ToString();
                if (metrics.ContainsKey(kvp.Key)) entry.Metric = ParseInt(metrics[kvp.Key]);
                if (ages.ContainsKey(kvp.Key)) entry.Age = ParseInt(ages[kvp.Key]);
                list.Add(entry);
            }
            _logger.Info("Найдено маршрутов: {0}", list.Count);
            return list;
        }

        public List<DiskInfo> GetStorageInfo()
        {
            _logger.Info("Сбор информации о дисках...");
            var list = new List<DiskInfo>();
            var descrs = WalkTable(SnmpConfig.HrStorageDescr);
            var units = WalkTable(SnmpConfig.HrStorageUnits);
            var sizes = WalkTable(SnmpConfig.HrStorageSize);
            var used = WalkTable(SnmpConfig.HrStorageUsed);
            foreach (var kvp in descrs)
            {
                double unitFactor = units.ContainsKey(kvp.Key) ? ParseLong(units[kvp.Key]) : 0;
                if (unitFactor == 0) unitFactor = 1024;
                double sizeVal = sizes.ContainsKey(kvp.Key) ? ParseLong(sizes[kvp.Key]) : 0;
                double usedVal = used.ContainsKey(kvp.Key) ? ParseLong(used[kvp.Key]) : 0;
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
            _logger.Info("Найдено дисков: {0}", list.Count);
            return list;
        }

        public List<CpuCore> GetCPULoad()
        {
            _logger.Info("Сбор данных CPU...");
            var list = new List<CpuCore>();
            var loads = WalkTable(SnmpConfig.HrProcessorLoad);
            int count = 0;
            foreach (var kvp in loads) list.Add(new CpuCore { Index = count++, Load = ParseInt(kvp.Value) });
            _logger.Info("Найдено ядер CPU: {0}", list.Count);
            return list;
        }

        public List<ProcessInfo> GetProcesses()
        {
            _logger.Info("Сбор списка процессов...");
            var list = new List<ProcessInfo>();
            var names = WalkTable(SnmpConfig.HrSWRunName);
            var paths = WalkTable(SnmpConfig.HrSWRunPath);
            var params_st = WalkTable(SnmpConfig.HrSWRunParams);
            var types = WalkTable(SnmpConfig.HrSWRunType);
            var statuses = WalkTable(SnmpConfig.HrSWRunStatus);
            int count = 0;
            foreach (var kvp in names)
            {
                if (count >= SnmpConfig.MaxProcesses) break;
                var p = new ProcessInfo { Name = kvp.Value };
                if (paths.ContainsKey(kvp.Key)) p.Path = paths[kvp.Key];
                if (params_st.ContainsKey(kvp.Key)) p.Params = params_st[kvp.Key];
                int typeVal = types.ContainsKey(kvp.Key) ? ParseInt(types[kvp.Key]) : 1;
                p.Type = typeVal == 4 ? "App" : "System";
                int statusVal = statuses.ContainsKey(kvp.Key) ? ParseInt(statuses[kvp.Key]) : 0;
                p.Status = (statusVal == 1 || statusVal == 2) ? "Running" : "Other";
                list.Add(p); count++;
            }
            _logger.Info("Найдено процессов: {0}", list.Count);
            return list;
        }

        public List<DeviceInfo> GetDevices()
        {
            _logger.Info("Сбор информации об устройствах...");
            var list = new List<DeviceInfo>();
            var types = WalkTable(SnmpConfig.HrDeviceType);
            var descrs = WalkTable(SnmpConfig.HrDeviceDescr);
            var statuses = WalkTable(SnmpConfig.HrDeviceStatus);
            var errors = WalkTable(SnmpConfig.HrDeviceErrors);
            foreach (var kvp in types)
            {
                int t = ParseInt(kvp.Value);
                if (t > 0)
                {
                    var d = new DeviceInfo { Type = t };
                    if (descrs.ContainsKey(kvp.Key)) d.Description = descrs[kvp.Key];
                    int s = statuses.ContainsKey(kvp.Key) ? ParseInt(statuses[kvp.Key]) : 0;
                    d.Status = s == 2 ? "Running" : "Unknown";
                    if (errors.ContainsKey(kvp.Key)) d.Errors = ParseUInt(errors[kvp.Key]);
                    list.Add(d);
                }
            }
            _logger.Info("Найдено устройств: {0}", list.Count);
            return list;
        }

        #region IDisposable Implementation

        /// <summary>
        /// Освобождение неуправляемых ресурсов
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Шаблонный метод освобождения ресурсов
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Освобождение управляемых ресурсов (если есть)
                    _logger.Debug("SnmpManager освобождает управляемые ресурсы");
                }

                // Освобождение неуправляемых ресурсов (если есть)
                _disposed = true;
                _logger.Debug("SnmpManager освобождён");
            }
        }

        /// <summary>
        /// Финализатор для гарантированного освобождения ресурсов
        /// </summary>
        ~SnmpManager()
        {
            Dispose(false);
        }

        #endregion
    }
}