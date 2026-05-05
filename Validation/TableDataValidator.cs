using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using SnmpServerPoller.Models;

namespace SnmpServerPoller.Validation
{
    /// <summary>
    /// Валидатор сетевых данных (IP, MAC адреса)
    /// </summary>
    public static class NetworkValidator
    {
        private static readonly Regex IpAddressPattern = new(@"^(\d{1,3})\.(\d{1,3})\.(\d{1,3})\.(\d{1,3})$", RegexOptions.Compiled);
        private static readonly Regex MacDashPattern = new(@"^([0-9A-Fa-f]{2}-){5}[0-9A-Fa-f]{2}$", RegexOptions.Compiled);
        private static readonly Regex MacColonPattern = new(@"^([0-9A-Fa-f]{2}:){5}[0-9A-Fa-f]{2}$", RegexOptions.Compiled);
        private static readonly Regex MacPlainPattern = new(@"^[0-9A-Fa-f]{12}$", RegexOptions.Compiled);

        /// <summary>
        /// Проверка корректности IPv4 адреса
        /// </summary>
        public static bool IsValidIpAddress(string? ip)
        {
            if (string.IsNullOrEmpty(ip)) return false;

            var match = IpAddressPattern.Match(ip);
            if (!match.Success) return false;

            for (int i = 1; i <= 4; i++)
            {
                if (!byte.TryParse(match.Groups[i].Value, out _))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Проверка корректности MAC адреса
        /// Поддерживаемые форматы: XX-XX-XX-XX-XX-XX, XX:XX:XX:XX:XX:XX, XXXXXXXXXXXX
        /// </summary>
        public static bool IsValidMacAddress(string? mac)
        {
            if (string.IsNullOrEmpty(mac)) return false;

            return MacDashPattern.IsMatch(mac) 
                || MacColonPattern.IsMatch(mac) 
                || MacPlainPattern.IsMatch(mac);
        }
    }

    /// <summary>
    /// Валидатор данных SNMP таблиц
    /// </summary>
    public class TableDataValidator
    {
        private readonly List<ValidationResult> _errors = new();
        private readonly List<ValidationResult> _warnings = new();

        /// <summary>
        /// Результаты валидации
        /// </summary>
        public IReadOnlyList<ValidationResult> Errors => _errors.AsReadOnly();
        public IReadOnlyList<ValidationResult> Warnings => _warnings.AsReadOnly();
        public bool HasErrors => _errors.Count > 0;

        /// <summary>
        /// Валидировать ARP таблицу
        /// </summary>
        public void ValidateArpTable(IReadOnlyList<ArpEntry> entries, string tableName = "ARP Table")
        {
            foreach (var entry in entries)
            {
                if (!NetworkValidator.IsValidIpAddress(entry.Ip))
                {
                    _errors.Add(new ValidationResult(tableName, $"Invalid IP address: {entry.Ip}", ValidationSeverity.Error));
                }

                if (!NetworkValidator.IsValidMacAddress(entry.Mac))
                {
                    _errors.Add(new ValidationResult(tableName, $"Invalid MAC address: {entry.Mac}", ValidationSeverity.Error));
                }

                if (entry.IfIndex <= 0)
                {
                    _warnings.Add(new ValidationResult(tableName, $"Invalid IfIndex: {entry.IfIndex}", ValidationSeverity.Warning));
                }

                if (entry.Type is < 1 or > 4)
                {
                    _warnings.Add(new ValidationResult(tableName, $"Unknown ARP type: {entry.Type}", ValidationSeverity.Warning));
                }
            }
        }

        /// <summary>
        /// Валидировать таблицу маршрутизации
        /// </summary>
        public void ValidateRoutingTable(IReadOnlyList<RouteEntry> entries, string tableName = "Routing Table")
        {
            foreach (var entry in entries)
            {
                if (!NetworkValidator.IsValidIpAddress(entry.Dest))
                {
                    _errors.Add(new ValidationResult(tableName, $"Invalid destination IP address: {entry.Dest}", ValidationSeverity.Error));
                }

                if (!NetworkValidator.IsValidIpAddress(entry.Mask))
                {
                    _errors.Add(new ValidationResult(tableName, $"Invalid subnet mask: {entry.Mask}", ValidationSeverity.Error));
                }

                if (!string.IsNullOrEmpty(entry.NextHop) && !NetworkValidator.IsValidIpAddress(entry.NextHop))
                {
                    _errors.Add(new ValidationResult(tableName, $"Invalid next hop IP address: {entry.NextHop}", ValidationSeverity.Error));
                }

                if (entry.IfIndex <= 0)
                {
                    _warnings.Add(new ValidationResult(tableName, $"Invalid IfIndex: {entry.IfIndex}", ValidationSeverity.Warning));
                }

                if (entry.Metric < 0)
                {
                    _warnings.Add(new ValidationResult(tableName, $"Negative metric: {entry.Metric}", ValidationSeverity.Warning));
                }
            }
        }

        /// <summary>
        /// Валидировать таблицу IP адресов
        /// </summary>
        public void ValidateIpAddresses(IReadOnlyList<IpAddressInfo> entries, string tableName = "IP Addresses")
        {
            foreach (var entry in entries)
            {
                if (!NetworkValidator.IsValidIpAddress(entry.Address))
                {
                    _errors.Add(new ValidationResult(tableName, $"Invalid IP address: {entry.Address}", ValidationSeverity.Error));
                }

                if (!NetworkValidator.IsValidIpAddress(entry.Mask))
                {
                    _errors.Add(new ValidationResult(tableName, $"Invalid subnet mask: {entry.Mask}", ValidationSeverity.Error));
                }

                if (entry.IfIndex <= 0)
                {
                    _warnings.Add(new ValidationResult(tableName, $"Invalid IfIndex: {entry.IfIndex}", ValidationSeverity.Warning));
                }
            }
        }

        /// <summary>
        /// Очистить результаты валидации
        /// </summary>
        public void Clear()
        {
            _errors.Clear();
            _warnings.Clear();
        }

        /// <summary>
        /// Получить сводку результатов валидации
        /// </summary>
        public string GetSummary() => $"Validation complete: {_errors.Count} errors, {_warnings.Count} warnings";
    }

    /// <summary>
    /// Результат валидации
    /// </summary>
    public class ValidationResult
    {
        public string TableName { get; }
        public string Message { get; }
        public ValidationSeverity Severity { get; }

        public ValidationResult(string tableName, string message, ValidationSeverity severity)
        {
            TableName = tableName;
            Message = message;
            Severity = severity;
        }

        public override string ToString() => $"[{Severity}] {TableName}: {Message}";
    }

    /// <summary>
    /// Уровень серьезности результата валидации
    /// </summary>
    public enum ValidationSeverity
    {
        Error,
        Warning,
        Info
    }
}
