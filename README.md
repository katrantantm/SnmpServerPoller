# C# проект мониторинг ресурсов по SNMP

## Универсальный SNMP Poller с конфигурацией из внешних файлов

Этот проект представляет собой универсальный SNMP poller, который собирает информацию о серверах и сетевых устройствах. Все OID, названия таблиц и их структура хранятся во внешних JSON файлах, что позволяет легко адаптировать код под новые устройства без изменения исходного кода.

## Структура проекта

```
/workspace
├── Config/
│   ├── SnmpConfig.cs          # Настройки SNMP (community, timeout, etc.)
│   ├── OidConfig.cs           # Загрузчик конфигурации OID из внешних файлов
│   └── snmp-tables.json       # JSON файл с описанием таблиц и OID
├── Snmp/
│   ├── SnmpManager.cs         # Оригинальный SNMP менеджер (hardcoded OID)
│   └── UniversalSnmpManager.cs # Универсальный SNMP менеджер (конфигурация из файла)
├── Models/
│   └── DataModels.cs          # Модели данных
├── Program.cs                 # Точка входа приложения
└── appsettings.json           # Основной файл конфигурации приложения
```

## Конфигурация OID

Файл `Config/snmp-tables.json` содержит:

### Таблицы (tables)
Каждая таблица имеет:
- `name` - отображаемое имя таблицы
- `description` - описание
- `baseOid` - базовый OID таблицы
- `fields` - список полей с их OID и типами данных

### Скалярные значения (scalars)
Сгруппированы по категориям:
- `system` - системная информация (SysName, SysDescr, etc.)
- `ipStats`, `tcpStats`, `udpStats`, `icmpStats`, `snmpStats` - статистика протоколов

## Использование UniversalSnmpManager

```csharp
using SnmpServerPoller.Snmp;
using SnmpServerPoller.Logging;

// Создание менеджера с загрузкой конфигурации из файла
ILogger logger = new ConsoleLogger();
var snmp = new UniversalSnmpManager(
    targetIp: "192.168.1.1",
    community: "public",
    logger: logger,
    oidConfigPath: "Config/snmp-tables.json"  // опционально
);

// Получение скалярных значений по категории и имени
string sysName = snmp.GetScalar("system", "SysName");
string sysDescr = snmp.GetScalar("system", "SysDescr");

// Получение данных таблиц
var interfaces = snmp.GetInterfaces();
var ipAddresses = snmp.GetIpAddresses();
var arpTable = snmp.GetArpTable();
var routes = snmp.GetRoutingTable();
var disks = snmp.GetStorageInfo();
var cpuLoad = snmp.GetCPULoad();
var processes = snmp.GetProcesses();
var devices = snmp.GetDevices();

// Получение статистики протоколов
var ipStats = snmp.GetProtocolStats("ipStats", "IpForwarding", "IpInReceives", "IpOutRequests");
var tcpStats = snmp.GetProtocolStats("tcpStats", "TcpMaxConn", "TcpInSegs", "TcpOutSegs");
```

## Добавление новых таблиц

Для добавления новой таблицы просто отредактируйте `Config/snmp-tables.json`:

```json
{
  "tables": {
    "newTable": {
      "name": "New Table",
      "description": "Описание таблицы",
      "baseOid": ".1.3.6.1.2.1.X.Y.Z",
      "fields": [
        { "name": "FieldName", "oid": ".1.3.6.1.2.1.X.Y.Z.1", "type": "string" },
        { "name": "AnotherField", "oid": ".1.3.6.1.2.1.X.Y.Z.2", "type": "int" }
      ]
    }
  }
}
```

Затем добавьте метод в `UniversalSnmpManager.cs`:

```csharp
public List<NewTableModel> GetNewTable()
{
    var list = new List<NewTableModel>();
    var tableData = WalkTable("newTable");
    foreach (var kvp in tableData)
    {
        var item = new NewTableModel();
        if (kvp.Value.ContainsKey("FieldName")) item.FieldName = kvp.Value["FieldName"];
        if (kvp.Value.ContainsKey("AnotherField")) item.AnotherField = ParseInt(kvp.Value["AnotherField"]);
        list.Add(item);
    }
    return list;
}
```

## Типы данных

Поддерживаемые типы полей:
- `index` - индекс записи
- `string` - строковое значение
- `int` - целое число
- `long` - длинное целое
- `ulong` - беззнаковое длинное целое
- `uint` - беззнаковое целое

## Преимущества универсального подхода

1. **Гибкость**: Изменение OID не требует перекомпиляции
2. **Расширяемость**: Легко добавить поддержку новых MIB
3. **Читаемость**: Вся структура данных видна в JSON файле
4. **Тестируемость**: Можно быстро менять конфигурацию для тестов
5. **Мульти-вендорность**: Разные конфигурации для разных производителей оборудования
