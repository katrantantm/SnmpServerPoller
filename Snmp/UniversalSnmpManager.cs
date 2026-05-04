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
                    string value = DecodeRawData(result.First().Value.ToString(), result.First().Value);
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
                    var walkResult = WalkSingleField(field.Oid, field.Type, field.Format, field.Map);
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
        private Dictionary<string, string> WalkSingleField(string rootOid, string? fieldType = null, string? format = null, Dictionary<string, string>? map = null)
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
                    
                    // Для полей типа "index" значение берётся из индекса OID
                    if (fieldType == "index")
                    {
                        // Проверяем, это OID таблицы ARP или маршрутизации (IP адрес) или простой индекс
                        // Для ARP (.1.3.6.1.2.1.4.22.1.3) и Routing (.1.3.6.1.2.1.4.21.1.x) нужен IP
                        bool isIpTable = rootOid.Contains(".1.3.6.1.2.1.4.22.1.3") ||  // arpTable NetAddress
                                         rootOid.Contains(".1.3.6.1.2.1.4.20.1.") ||   // ipAddresses
                                         rootOid.Contains(".1.3.6.1.2.1.4.21.1.");      // routingTable
                        
                        if (isIpTable)
                        {
                            result[index] = DecodeIndexToIpAddress(index);
                        }
                        else
                        {
                            // Для обычных индексов (процессы, диски, интерфейсы) возвращаем число
                            result[index] = index.Split('.')[0];
                        }
                    }
                    // Для полей типа "octetstring" (например MAC адрес) декодируем как шестнадцатеричную строку
                    else if (fieldType == "octetstring")
                    {
                        string decodedValue = DecodeOctetStringFromAsnType(kvp.Value);
                        result[index] = decodedValue;
                    }
                    else
                    {
                        // Декодирование с учетом кодировки и формата
                        string rawValue = kvp.Value.ToString();
                        string decodedValue = DecodeRawData(rawValue, kvp.Value);
                        
                        // Применяем маппинг если указан (для int типов)
                        if (map != null && map.TryGetValue(decodedValue, out var mappedValue))
                        {
                            decodedValue = mappedValue;
                        }
                        // Если маппинг не найден, но тип числовой - пробуем применить как есть
                        else if (map != null && long.TryParse(decodedValue, out _))
                        {
                            // Числовое значение без маппинга оставляем как есть
                        }
                        
                        // Применяем форматирование если указано
                        if (!string.IsNullOrEmpty(format))
                        {
                            decodedValue = ApplyFormat(decodedValue, format);
                        }
                        
                        result[index] = decodedValue;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Debug("Ошибка Walk {0}: {1}", rootOid, ex.Message);
            }
            return result;
        }

        /// <summary>
        /// Декодирование индекса OID в IP адрес
        /// Поддерживает несколько форматов:
        /// 1. Числовой: "192.168.1.1" или ".192.168.1.1"
        /// 2. Бинарный: строка где каждый символ = один байт IP
        /// 3. UTF-8 encoded bytes: когда байты IP были интерпретированы как UTF-8 текст
        /// </summary>
        private string DecodeIndexToIpAddress(string index)
        {
            if (string.IsNullOrEmpty(index)) return index;
            
            // Формат 1: Числовой формат с точками (например, "192.168.1.1")
            if (index.Contains("."))
            {
                string[] parts = index.Split('.');
                
                // Для IP адреса ожидаем 4 октета
                if (parts.Length >= 4)
                {
                    try
                    {
                        byte[] octets = new byte[4];
                        bool allParsed = true;
                        for (int i = 0; i < 4; i++)
                        {
                            if (!byte.TryParse(parts[i], out octets[i]))
                            {
                                allParsed = false;
                                break;
                            }
                        }
                        if (allParsed)
                        {
                            return $"{octets[0]}.{octets[1]}.{octets[2]}.{octets[3]}";
                        }
                    }
                    catch
                    {
                        // Ошибка парсинга, пробуем другие форматы
                    }
                }
            }
            
            // Формат 2: Бинарный формат - каждый символ строки представляет байт IP адреса
            if (index.Length >= 4)
            {
                try
                {
                    byte[] octets = new byte[4];
                    bool allValid = true;
                    for (int i = 0; i < 4; i++)
                    {
                        int codePoint = index[i];
                        if (codePoint > 255)
                        {
                            allValid = false;
                            break;
                        }
                        octets[i] = (byte)codePoint;
                    }
                    if (allValid)
                    {
                        return $"{octets[0]}.{octets[1]}.{octets[2]}.{octets[3]}";
                    }
                }
                catch
                {
                    // Ошибка, пробуем следующий формат
                }
            }
            
            // Формат 3: UTF-8 encoded bytes
            // Когда байты IP адреса были неправильно интерпретированы как UTF-8 текст
            // и теперь нужно получить оригинальные байты из UTF-8 представления
            try
            {
                byte[] utf8Bytes = System.Text.Encoding.UTF8.GetBytes(index);
                if (utf8Bytes.Length >= 4)
                {
                    // Проверяем, могут ли первые 4 байта быть IP адресом
                    // (все байты <= 255, что всегда true для byte[])
                    // Дополнительная проверка: первый байт должен быть в диапазоне IP (1-223 для unicast)
                    if (utf8Bytes[0] >= 1 && utf8Bytes[0] <= 223)
                    {
                        return $"{utf8Bytes[0]}.{utf8Bytes[1]}.{utf8Bytes[2]}.{utf8Bytes[3]}";
                    }
                }
            }
            catch
            {
                // Ошибка, возвращаем как есть
            }
            
            return index;
        }

        /// <summary>
        /// Применение форматирования к значению
        /// </summary>
        private string ApplyFormat(string value, string format)
        {
            if (string.IsNullOrEmpty(value)) return value;
            
            switch (format.ToLower())
            {
                case "speed":
                    // Форматирование скорости: значение в битах/сек -> человекочитаемый формат
                    if (ulong.TryParse(value, out ulong speed))
                    {
                        if (speed == 0)
                            return "0";
                        if (speed >= 1_000_000_000_000)
                            return $"{speed / 1_000_000_000_000.0:F1} Tb/s";
                        if (speed >= 1_000_000_000)
                            return $"{speed / 1_000_000_000.0:F1} Gb/s";
                        if (speed >= 1_000_000)
                            return $"{speed / 1_000_000.0:F1} Mb/s";
                        if (speed >= 1_000)
                            return $"{speed / 1_000.0:F1} Kb/s";
                        return $"{speed} b/s";
                    }
                    break;
            }
            
            return value;
        }

        /// <summary>
        /// Декодирование OctetString в MAC адрес (формат XX:XX:XX:XX:XX:XX)
        /// </summary>
        private string DecodeOctetString(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            
            // Удаляем пробелы и точки
            string clean = input.Replace(" ", "").Replace(".", "");
            
            // Проверяем, является ли строка шестнадцатеричной
            if (IsHex(clean) && clean.Length % 2 == 0)
            {
                try
                {
                    byte[] data = new byte[clean.Length / 2];
                    for (int i = 0; i < clean.Length; i += 2)
                        data[i / 2] = Convert.ToByte(clean.Substring(i, 2), 16);
                    
                    // Форматируем как MAC адрес (XX:XX:XX:XX:XX:XX)
                    return string.Join(":", data.Select(b => b.ToString("X2")));
                }
                catch { }
            }
            
            return input;
        }

        /// <summary>
        /// Декодирование OctetString из ASN.1 типа в MAC адрес (формат XX:XX:XX:XX:XX:XX)
        /// </summary>
        private string DecodeOctetStringFromAsnType(AsnType asnValue)
        {
            if (asnValue == null) return string.Empty;
            
            try
            {
                // Получаем байты из OctetString
                byte[] bytes = new byte[asnValue.Length];
                for (int i = 0; i < asnValue.Length; i++)
                {
                    bytes[i] = asnValue[i];
                }
                
                // Если это MAC адрес (6 байтов), форматируем как XX:XX:XX:XX:XX:XX
                if (bytes.Length == 6)
                {
                    return string.Join(":", bytes.Select(b => b.ToString("X2")));
                }
                // Для других длин тоже показываем в hex формате
                else if (bytes.Length > 0)
                {
                    return string.Join(":", bytes.Select(b => b.ToString("X2")));
                }
                
                return string.Empty;
            }
            catch
            {
                // Если ошибка, пробуем декодировать через ToString()
                return DecodeOctetString(asnValue.ToString());
            }
        }

        /// <summary>
        /// Декодирование значения с поддержкой различных типов данных
        /// </summary>
        private string DecodeRawData(string input, AsnType asnValue = null)
        {
            if (string.IsNullOrEmpty(input)) return input;
            
            // Проверяем тип ASN.1 для правильного декодирования
            if (asnValue != null)
            {
                // Обработка Integer/Integer32 - возвращаем числовое значение
                if (asnValue is Integer32 asnInt)
                {
                    return asnInt.Value.ToString();
                }
                
                // Обработка Counter32
                if (asnValue is Counter32 counter32)
                {
                    return counter32.Value.ToString();
                }
                
                // Обработка Counter64 для больших чисел
                if (asnValue is Counter64 counter64)
                {
                    return counter64.Value.ToString();
                }
                
                // Обработка Gauge32
                if (asnValue is Gauge32 gauge32)
                {
                    return gauge32.Value.ToString();
                }
                
                // Обработка OctetString - может содержать IP адрес в бинарном формате или текст
                if (asnValue is OctetString octetStr)
                {
                    try
                    {
                        // Получаем байты через индексатор или метод ToByteArray
                        byte[] bytes = new byte[octetStr.Length];
                        for (int i = 0; i < octetStr.Length; i++)
                        {
                            bytes[i] = octetStr[i];
                        }
                        
                        // Проверяем, является ли это IP адресом (4 байта)
                        if (bytes.Length == 4)
                        {
                            return $"{bytes[0]}.{bytes[1]}.{bytes[2]}.{bytes[3]}";
                        }
                        
                        if (bytes != null && bytes.Length > 0)
                        {
                            string utf8Str = Encoding.UTF8.GetString(bytes);
                            // Проверяем, является ли строка читаемой
                            if (utf8Str.Any(c => c >= 32 && c < 127) || utf8Str.All(c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c) || char.IsPunctuation(c)))
                                return utf8Str.Trim();
                        }
                    }
                    catch { }
                }
            }
            
            // Стандартная обработка шестнадцатеричных данных
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
                    if (decoded.All(c => c >= 32 || char.IsWhiteSpace(c)))
                        return decoded.Trim();
                }
                catch { }
            }
            return input;
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
                        Index = kvp.Value.ContainsKey("Index") ? ParseInt(kvp.Value["Index"]) : 0,
                        Type = kvp.Value.ContainsKey("Type") ? kvp.Value["Type"] : string.Empty,
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
                if (kvp.Value.ContainsKey("Index")) proc.Index = ParseInt(kvp.Value["Index"]);
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

        /// <summary>
        /// Получить скалярные значения категории как словарь
        /// </summary>
        public Dictionary<string, string> GetScalars(string category)
        {
            var result = new Dictionary<string, string>();
            try
            {
                var config = OidConfigLoader.Load();
                if (config.Scalars.ContainsKey(category))
                {
                    foreach (var kvp in config.Scalars[category])
                    {
                        result[kvp.Key] = GetScalar(category, kvp.Key);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Warn("Ошибка при запросе скаляров {0}: {1}", category, ex.Message);
            }
            return result;
        }

        /// <summary>
        /// Универсальный метод для получения любой таблицы по ключу конфигурации
        /// </summary>
        public Dictionary<string, Dictionary<string, string>> GetUniversalTable(string tableKey)
        {
            return WalkTable(tableKey);
        }

        #region Helper Methods

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
