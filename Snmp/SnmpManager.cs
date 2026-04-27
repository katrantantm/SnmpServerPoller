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
            _logger = logger ?? new ConsoleLogger(LogLevel.Info);
            _timeout = Math.Max(1000, Math.Min(timeout, 30000));
            _retries = Math.Max(0, Math.Min(retries, 5));

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

                SimpleSnmp snmp = new SimpleSnmp(_targetIp, _community);
                snmp.Timeout = _timeout;

                Dictionary<Oid, AsnType> result = snmp.Get(SnmpVersion.Ver2, new[] { oid });
                if (result != null && result.Count > 0)
                {
                    string value = DecodeRawData(result.First().Value.ToString());
                    _logger.Debug("Получено: {0} = {1}", oid, value);
                    return value;
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
            string value = GetScalar(oid);
            if (ulong.TryParse(value, out ulong result))
            {
                return result;
            }
            _logger.Warn("Не удалось преобразовать значение OID {0} в ulong: {1}", oid, value);
            return 0;
        }

        public Dictionary<string, string> WalkTable(string rootOid)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(SnmpManager));

            var result = new Dictionary<string, string>();
            try
            {
                _logger.Debug("Walk таблицы: {0}", rootOid);

                SimpleSnmp snmp = new SimpleSnmp(_targetIp, _community);
                snmp.Timeout = _timeout;

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
            catch (Exception ex)
            {
                _logger.Error("Ошибка Walk {0}: {1}", rootOid, ex);
            }
            return result;
        }

        private string DecodeRawData(string rawValue)
        {
            if (string.IsNullOrEmpty(rawValue)) return string.Empty;

            try
            {
                byte[] bytes = Encoding.ASCII.GetBytes(rawValue);
                string text = Encoding.ASCII.GetString(bytes);
                
                if (text.Any(c => c < 32 && c != '\n' && c != '\r' && c != '\t'))
                {
                    return BitConverter.ToString(bytes).Replace("-", ":");
                }
                return text;
            }
            catch (Exception ex)
            {
                _logger.Debug("Ошибка декодирования: {0}", ex.Message);
                return "[DecodeError]";
            }
        }

        #region Методы получения данных

        public List<InterfaceInfo> GetInterfaces()
        {
            var interfaces = new Dictionary<int, InterfaceInfo>();

            void MergeData(string oid, Action<int, string> setter)
            {
                foreach (var kvp in WalkTable(oid))
                {
                    if (int.TryParse(kvp.Key, out int idx))
                        setter(idx, kvp.Value);
                }
            }

            foreach (var kvp in WalkTable(SnmpConfig.IfDescr))
            {
                if (int.TryParse(kvp.Key, out int idx))
                {
                    interfaces[idx] = new InterfaceInfo { Index = idx };
                }
            }

            if (interfaces.Count == 0) return new List<InterfaceInfo>();

            MergeData(SnmpConfig.IfDescr, (idx, val) => { if (interfaces.ContainsKey(idx)) interfaces[idx].Description = val; });
            MergeData(SnmpConfig.IfType, (idx, val) => { if (interfaces.ContainsKey(idx)) interfaces[idx].Type = val; });
            MergeData(SnmpConfig.IfMtu, (idx, val) => { if (interfaces.ContainsKey(idx)) interfaces[idx].Mtu = val; });
            MergeData(SnmpConfig.IfSpeed, (idx, val) => { if (interfaces.ContainsKey(idx)) interfaces[idx].Speed = val; });
            MergeData(SnmpConfig.IfPhysAddress, (idx, val) => { if (interfaces.ContainsKey(idx)) interfaces[idx].PhysAddress = val; });
            MergeData(SnmpConfig.IfAdminStatus, (idx, val) => { if (interfaces.ContainsKey(idx)) interfaces[idx].AdminStatus = val; });
            MergeData(SnmpConfig.IfOperStatus, (idx, val) => { if (interfaces.ContainsKey(idx)) interfaces[idx].OperStatus = val; });
            MergeData(SnmpConfig.IfInOctets, (idx, val) => { if (interfaces.ContainsKey(idx)) interfaces[idx].InOctets = val; });
            MergeData(SnmpConfig.IfOutOctets, (idx, val) => { if (interfaces.ContainsKey(idx)) interfaces[idx].OutOctets = val; });
            MergeData(SnmpConfig.IfInErrors, (idx, val) => { if (interfaces.ContainsKey(idx)) interfaces[idx].InErrors = val; });
            MergeData(SnmpConfig.IfOutErrors, (idx, val) => { if (interfaces.ContainsKey(idx)) interfaces[idx].OutErrors = val; });

            return interfaces.Values.ToList();
        }

        public List<IpAddrEntry> GetIpAddresses()
        {
            var ipList = new Dictionary<string, IpAddrEntry>();

            foreach (var kvp in WalkTable(SnmpConfig.IpAdEntAddr))
            {
                ipList[kvp.Value] = new IpAddrEntry { IpAddress = kvp.Value };
            }

            return ipList.Values.ToList();
        }

        public List<ArpEntry> GetArpTable()
        {
            return new List<ArpEntry>();
        }

        public List<DiskInfo> GetDiskSpace()
        {
            var disks = new Dictionary<int, DiskInfo>();

            void MergeDiskData(string oid, Action<int, string> setter)
            {
                foreach (var kvp in WalkTable(oid))
                {
                    if (int.TryParse(kvp.Key, out int idx))
                    {
                        if (!disks.ContainsKey(idx)) disks[idx] = new DiskInfo { Index = idx };
                        setter(idx, kvp.Value);
                    }
                }
            }

            MergeDiskData(SnmpConfig.HrStorageDescr, (i, v) => disks[i].Description = v);
            MergeDiskData(SnmpConfig.HrStorageSize, (i, v) => disks[i].Size = v);
            MergeDiskData(SnmpConfig.HrStorageUsed, (i, v) => disks[i].Used = v);
            MergeDiskData(SnmpConfig.HrStorageAllocationUnits, (i, v) => disks[i].AllocationUnits = v);

            return disks.Values.ToList();
        }

        public List<CpuInfo> GetCpuLoad()
        {
            var cpuList = new List<CpuInfo>();
            int coreIndex = 1;

            foreach (var kvp in WalkTable(SnmpConfig.HrProcessorLoad))
            {
                cpuList.Add(new CpuInfo
                {
                    CoreIndex = coreIndex++,
                    LoadPercent = kvp.Value
                });
            }

            return cpuList;
        }

        public List<ProcessInfo> GetProcesses(int maxCount = 200)
        {
            var processes = new Dictionary<int, ProcessInfo>();

            void MergeProcData(string oid, Action<int, string> setter)
            {
                foreach (var kvp in WalkTable(oid))
                {
                    if (int.TryParse(kvp.Key, out int idx) && processes.Count < maxCount)
                    {
                        if (!processes.ContainsKey(idx)) processes[idx] = new ProcessInfo { Pid = idx };
                        setter(idx, kvp.Value);
                    }
                }
            }

            MergeProcData(SnmpConfig.HrSWRunName, (i, v) => processes[i].Name = v);
            MergeProcData(SnmpConfig.HrSWRunPath, (i, v) => processes[i].Path = v);
            MergeProcData(SnmpConfig.HrSWRunParameters, (i, v) => processes[i].Parameters = v);
            MergeProcData(SnmpConfig.HrSWRunType, (i, v) => processes[i].Type = v);
            MergeProcData(SnmpConfig.HrSWRunStatus, (i, v) => processes[i].Status = v);

            return processes.Values.Take(maxCount).ToList();
        }

        #endregion

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
            }
        }
    }
}
