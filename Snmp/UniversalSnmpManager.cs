using SnmpSharpNet;
using SnmpServerPoller.Config;
using SnmpServerPoller.Logging;
using SnmpServerPoller.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;

namespace SnmpServerPoller.Snmp
{
    /// <summary>
    /// Универсальный SNMP менеджер с конфигурацией из внешних файлов
    /// </summary>
    public class UniversalSnmpManager
    {
        private readonly string _targetIp;
        private readonly string _community;
        private readonly ILogger _logger;
        private readonly OidConfiguration _oidConfig;

        public UniversalSnmpManager(string targetIp, string community, ILogger? logger = null, string? oidConfigPath = null)
        {
            _targetIp = targetIp;
            _community = community;
            _logger = logger ?? new ConsoleLogger();
            _oidConfig = OidConfigLoader.Load(oidConfigPath ?? "Config/snmp-tables.json");
        }

        /// <summary>
        /// Получить скалярное значение по категории и имени
        /// </summary>
        public string GetScalar(string category, string name)
        {
            try
            {
                string oid = OidConfigLoader.GetScalarOid(category, name);
                _logger.Debug("Запрос OID: {0} ({1}.{2})", oid, category, name);
                
                SimpleSnmp snmp = new(_targetIp, _community);
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
                _logger.Warn("Ошибка при запросе {0}.{1}: {2}", category, name, ex.Message);
            }
            return null!;
        }

        /// <summary>
        /// Получить скалярное значение как число
        /// </summary>
        public ulong GetScalarAsLong(string category, string name)
        {
            string rawStr = GetScalar(category, name);
            return ParseToULong(rawStr);
        }

        /// <summary>
        /// Универсальный метод для получения данных таблицы
        /// </summary>
        public Dictionary<string, Dictionary<string, string>> WalkTable(string tableKey)
        {
            var result = new Dictionary<string, Dictionary<string, string>>();
            
            try
            {
                TableConfig tableConfig = OidConfigLoader.GetTableConfig(tableKey);
                _logger.Debug("Walk таблицы: {0} (OID: {1})", tableConfig.Name, tableConfig.BaseOid);
                
                // Собираем данные для каждого поля
                var fieldData = new Dictionary<string, Dictionary<string, string>>();
                
                foreach (var field in tableConfig.Fields)
                {
                    var walkResult = WalkSingleField(field.Oid);
                    fieldData[field.Name] = walkResult;
                }
                
                // Определяем индексы (объединяем все ключи)
                var allIndexes = new HashSet<string>();
                foreach (var field in fieldData.Values)
                {
                    foreach (var key in field.Keys)
                    {
                        allIndexes.Add(key);
                    }
                }
                
                // Формируем результат по индексам
                foreach (var index in allIndexes)
                {
                    var entry = new Dictionary<string, string>();
                    foreach (var fieldName in fieldData.Keys)
                    {
                        if (fieldData[fieldName].TryGetValue(index, out var value))
                        {
                            entry[fieldName] = value;
                        }
                    }
                    result[index] = entry;
                }
                
                _logger.Debug("Walk {0}: получено {1} записей", tableConfig.Name, result.Count);
            }
            catch (Exception ex)
            {
                _logger.Error("Ошибка Walk таблицы {0}: {1}", tableKey, ex);
            }
            
            return result;
        }

        /// <summary>
        /// Walk одного поля таблицы
        /// </summary>
        private Dictionary<string, string> WalkSingleField(string rootOid)
        {
            var result = new Dictionary<string, string>();
            try
            {
                SimpleSnmp snmp = new(_targetIp, _community);
                Dictionary<Oid, AsnType> snmpResult = snmp.Walk(SnmpVersion.Ver2, rootOid);
                
                if (snmpResult == null) return result;

                foreach (var kvp in snmpResult)
                {
                    string fullOid = kvp.Key.ToString();
                    string index = fullOid.Substring(rootOid.Length);
                    if (index.StartsWith(".")) index = index.Substring(1);
                    result[index] = DecodeRawData(kvp.Value.ToString());
                }
            }
            catch (Exception ex)
            {
                _logger.Debug("Ошибка Walk {0}: {1}", rootOid, ex.Message);
            }
            return result;
        }

        /// <summary>
        /// Получить системную информацию
        /// </summary>
        public SystemInfo GetSystemInfo()
        {
            _logger.Info("📋 Сбор системной информации...");
            return new SystemInfo
            {
                SysDescr = GetScalar("system", "SysDescr"),
                SysName = GetScalar("system", "SysName"),
                SysUpTime = GetScalar("system", "SysUpTime"),
                SysContact = GetScalar("system", "SysContact"),
                SysLocation = GetScalar("system", "SysLocation")
            };
        }

        /// <summary>
        /// Получить данные интерфейсов
        /// </summary>
        public List<InterfaceInfo> GetInterfaces()
        {
            _logger.Info("🔄 Сбор данных интерфейсов...");
            var list = new List<InterfaceInfo>();
            
            var tableData = WalkTable("interfaces");
            foreach (var kvp in tableData)
            {
                var info = new InterfaceInfo();
                if (kvp.Value.ContainsKey("Index")) info.Index = ParseInt(kvp.Value["Index"]);
                if (kvp.Value.ContainsKey("Description")) info.Description = kvp.Value["Description"];
                if (kvp.Value.ContainsKey("Type")) info.Type = ParseInt(kvp.Value["Type"]);
                if (kvp.Value.ContainsKey("Mtu")) info.Mtu = ParseLong(kvp.Value["Mtu"]);
                if (kvp.Value.ContainsKey("Speed")) info.Speed = ParseULong(kvp.Value["Speed"]);
                if (kvp.Value.ContainsKey("AdminStatus")) info.AdminStatus = ParseInt(kvp.Value["AdminStatus"]);
                if (kvp.Value.ContainsKey("OperStatus")) info.OperStatus = ParseInt(kvp.Value["OperStatus"]);
                if (kvp.Value.ContainsKey("InOctets")) info.InOctets = ParseULong(kvp.Value["InOctets"]);
                if (kvp.Value.ContainsKey("OutOctets")) info.OutOctets = ParseULong(kvp.Value["OutOctets"]);
                if (kvp.Value.ContainsKey("InErrors")) info.InErrors = ParseUInt(kvp.Value["InErrors"]);
                if (kvp.Value.ContainsKey("OutErrors")) info.OutErrors = ParseUInt(kvp.Value["OutErrors"]);
                list.Add(info);
            }
            
            list = list.OrderBy(i => i.Index).ToList();
            _logger.Info("   Найдено интерфейсов: {0}", list.Count);
            return list;
        }

        /// <summary>
        /// Получить IP адреса
        /// </summary>
        public List<IpAddressInfo> GetIpAddresses()
        {
            _logger.Info("📡 Сбор IP-адресов...");
            var list = new List<IpAddressInfo>();
            
            var tableData = WalkTable("ipAddresses");
            foreach (var kvp in tableData)
            {
                var info = new IpAddressInfo();
                if (kvp.Value.ContainsKey("Address")) info.Address = kvp.Value["Address"];
                if (kvp.Value.ContainsKey("Mask")) info.Mask = kvp.Value["Mask"];
                if (kvp.Value.ContainsKey("IfIndex")) info.IfIndex = ParseInt(kvp.Value["IfIndex"]);
                list.Add(info);
            }
            
            _logger.Info("   Найдено IP: {0}", list.Count);
            return list;
        }

        /// <summary>
        /// Получить ARP таблицу
        /// </summary>
        public List<ArpEntry> GetArpTable()
        {
            _logger.Info("🌐 Сбор ARP таблицы...");
            var list = new List<ArpEntry>();
            
            var tableData = WalkTable("arpTable");
            foreach (var kvp in tableData)
            {
                var entry = new ArpEntry();
                if (kvp.Value.ContainsKey("IfIndex")) entry.IfIndex = ParseInt(kvp.Value["IfIndex"]);
                if (kvp.Value.ContainsKey("PhysAddress")) entry.Mac = kvp.Value["PhysAddress"];
                if (kvp.Value.ContainsKey("NetAddress")) entry.Ip = kvp.Value["NetAddress"];
                if (kvp.Value.ContainsKey("Type")) entry.Type = ParseInt(kvp.Value["Type"]);
                
                // Фильтруем только динамические записи (type != 2)
                if (entry.Type != 2) list.Add(entry);
            }
            
            _logger.Info("   Найдено ARP записей: {0}", list.Count);
            return list;
        }

        /// <summary>
        /// Получить таблицу маршрутизации
        /// </summary>
        public List<RouteEntry> GetRoutingTable()
        {
            _logger.Info("🛣️ Сбор таблицы маршрутизации...");
            var list = new List<RouteEntry>();
            
            var tableData = WalkTable("routingTable");
            foreach (var kvp in tableData)
            {
                var entry = new RouteEntry();
                if (kvp.Value.ContainsKey("Dest")) entry.Dest = kvp.Value["Dest"];
                if (kvp.Value.ContainsKey("Mask")) entry.Mask = kvp.Value["Mask"];
                if (kvp.Value.ContainsKey("NextHop")) entry.NextHop = kvp.Value["NextHop"];
                if (kvp.Value.ContainsKey("IfIndex")) entry.IfIndex = ParseInt(kvp.Value["IfIndex"]);
                if (kvp.Value.ContainsKey("Type")) entry.Type = ParseInt(kvp.Value["Type"]) == 3 ? "direct" : "indirect";
                if (kvp.Value.ContainsKey("Proto")) entry.Proto = ParseInt(kvp.Value["Proto"]) == 2 ? "local" : ParseInt(kvp.Value["Proto"]).ToString();
                if (kvp.Value.ContainsKey("Metric")) entry.Metric = ParseInt(kvp.Value["Metric"]);
                if (kvp.Value.ContainsKey("Age")) entry.Age = ParseInt(kvp.Value["Age"]);
                list.Add(entry);
            }
            
            _logger.Info("   Найдено маршрутов: {0}", list.Count);
            return list;
        }

        /// <summary>
        /// Получить информацию о дисках
        /// </summary>
        public List<DiskInfo> GetStorageInfo()
        {
            _logger.Info("💾 Сбор информации о дисках...");
            var list = new List<DiskInfo>();
            
            var tableData = WalkTable("storage");
            foreach (var kvp in tableData)
            {
                double unitFactor = kvp.Value.ContainsKey("Units") ? ParseLong(kvp.Value["Units"]) : 1024;
                if (unitFactor == 0) unitFactor = 1024;
                
                double sizeVal = kvp.Value.ContainsKey("Size") ? ParseLong(kvp.Value["Size"]) : 0;
                double usedVal = kvp.Value.ContainsKey("Used") ? ParseLong(kvp.Value["Used"]) : 0;
                
                if (sizeVal > 1000 && kvp.Value.ContainsKey("Descr"))
                {
                    list.Add(new DiskInfo
                    {
                        Description = kvp.Value["Descr"],
                        TotalMB = Math.Round((sizeVal * unitFactor) / 1048576, 0),
                        UsedMB = Math.Round((usedVal * unitFactor) / 1048576, 0),
                        Percent = sizeVal > 0 ? Math.Round((usedVal * 100.0 / sizeVal), 1) : 0
                    });
                }
            }
            
            _logger.Info("   Найдено дисков: {0}", list.Count);
            return list;
        }

        /// <summary>
        /// Получить загрузку CPU
        /// </summary>
        public List<CpuCore> GetCPULoad()
        {
            _logger.Info("⚡ Сбор данных CPU...");
            var list = new List<CpuCore>();
            
            var tableData = WalkTable("cpu");
            int count = 0;
            foreach (var kvp in tableData)
            {
                int load = kvp.Value.ContainsKey("Load") ? ParseInt(kvp.Value["Load"]) : 0;
                list.Add(new CpuCore { Index = count++, Load = load });
            }
            
            _logger.Info("   Найдено ядер CPU: {0}", list.Count);
            return list;
        }

        /// <summary>
        /// Получить список процессов
        /// </summary>
        public List<ProcessInfo> GetProcesses()
        {
            _logger.Info("📋 Сбор списка процессов...");
            var list = new List<ProcessInfo>();
            
            var tableData = WalkTable("processes");
            int count = 0;
            foreach (var kvp in tableData)
            {
                if (count >= SnmpConfig.MaxProcesses) break;
                
                var proc = new ProcessInfo();
                if (kvp.Value.ContainsKey("Name")) proc.Name = kvp.Value["Name"];
                if (kvp.Value.ContainsKey("Path")) proc.Path = kvp.Value["Path"];
                if (kvp.Value.ContainsKey("Params")) proc.Params = kvp.Value["Params"];
                if (kvp.Value.ContainsKey("Type"))
                {
                    int typeVal = ParseInt(kvp.Value["Type"]);
                    proc.Type = typeVal == 4 ? "App" : "System";
                }
                if (kvp.Value.ContainsKey("Status"))
                {
                    int statusVal = ParseInt(kvp.Value["Status"]);
                    proc.Status = (statusVal == 1 || statusVal == 2) ? "Running" : "Other";
                }
                list.Add(proc);
                count++;
            }
            
            _logger.Info("   Найдено процессов: {0}", list.Count);
            return list;
        }

        /// <summary>
        /// Получить информацию об устройствах
        /// </summary>
        public List<DeviceInfo> GetDevices()
        {
            _logger.Info("🔧 Сбор информации об устройствах...");
            var list = new List<DeviceInfo>();
            
            var tableData = WalkTable("devices");
            foreach (var kvp in tableData)
            {
                var device = new DeviceInfo();
                if (kvp.Value.ContainsKey("Type")) device.Type = ParseInt(kvp.Value["Type"]);
                if (device.Type > 0)
                {
                    if (kvp.Value.ContainsKey("Descr")) device.Description = kvp.Value["Descr"];
                    if (kvp.Value.ContainsKey("Status"))
                    {
                        int s = ParseInt(kvp.Value["Status"]);
                        device.Status = s == 2 ? "Running" : "Unknown";
                    }
                    if (kvp.Value.ContainsKey("Errors")) device.Errors = ParseUInt(kvp.Value["Errors"]);
                    list.Add(device);
                }
            }
            
            _logger.Info("   Найдено устройств: {0}", list.Count);
            return list;
        }

        /// <summary>
        /// Получить статистику протоколов
        /// </summary>
        public List<StatEntry> GetProtocolStats(string category, params string[] names)
        {
            var stats = new List<StatEntry>();
            foreach (var name in names)
            {
                stats.Add(new StatEntry
                {
                    Name = name,
                    Value = GetScalarAsLong(category, name).ToString("N0")
                });
            }
            return stats;
        }

        #region Helper Methods

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
                    
                    if (data.Length == 6) 
                        return string.Join(":", data.Select(b => b.ToString("X2")));
                    if (data.Length == 4) 
                        return string.Join(".", data);
                    
                    string decoded = Encoding.UTF8.GetString(data);
                    if (decoded.All(c => char.IsControl(c) || c >= 32)) 
                        return decoded;
                }
                catch { }
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

        private int ParseInt(string val) => int.TryParse(val, out int i) ? i : 0;
        private long ParseLong(string val) => long.TryParse(val, out long l) ? l : 0;
        private ulong ParseULong(string val) => ulong.TryParse(val, out ulong ul) ? ul : 0;
        private uint ParseUInt(string val) => uint.TryParse(val, out uint ui) ? ui : 0;
        
        private ulong ParseToULong(string rawStr)
        {
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
            catch { }
            return 0;
        }

        #endregion
    }

    /// <summary>
    /// Модель системной информации
    /// </summary>
    public class SystemInfo
    {
        public string SysDescr { get; set; } = string.Empty;
        public string SysName { get; set; } = string.Empty;
        public string SysUpTime { get; set; } = string.Empty;
        public string SysContact { get; set; } = string.Empty;
        public string SysLocation { get; set; } = string.Empty;
    }
}
