using System;
using System.Collections.Generic;
using System.IO;
using SnmpServerPoller.Logging;
using SnmpServerPoller.Models;

namespace SnmpServerPoller.Reporting
{
    public class CsvReporter : IDisposable
    {
        private readonly string _outputPath;
        private readonly ILogger _logger;
        private bool _disposed;

        public CsvReporter(string outputPath, ILogger? logger = null)
        {
            _outputPath = outputPath;
            _logger = logger ?? new ConsoleLogger();
            
            if (!Directory.Exists(_outputPath))
            {
                Directory.CreateDirectory(_outputPath);
            }
        }

        private string EscapeCsv(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        private void WriteCsvFile(string fileName, string[] headers, List<string[]> rows)
        {
            string filePath = Path.Combine(_outputPath, fileName);
            _logger.Info("Запись CSV: {0}", filePath);

            using (var writer = new StreamWriter(filePath, false))
            {
                // Заголовки
                writer.WriteLine(string.Join(",", headers));
                
                // Данные
                foreach (var row in rows)
                {
                    writer.WriteLine(string.Join(",", row));
                }
            }
        }

        public void WriteInterfaces(List<InterfaceInfo> list)
        {
            if (list == null || list.Count == 0) return;
            
            var headers = new[] { "Idx", "Descr", "Type", "MTU", "Speed", "In", "Out", "InErr", "OutErr", "Admin", "Oper" };
            var rows = new List<string[]>();
            
            foreach (var item in list)
            {
                rows.Add(new[]
                {
                    item.Index.ToString(),
                    EscapeCsv(item.Description),
                    item.Type.ToString(),
                    item.Mtu.ToString(),
                    FormatSpeed(item.Speed),
                    item.InOctets.ToString(),
                    item.OutOctets.ToString(),
                    item.InErrors.ToString(),
                    item.OutErrors.ToString(),
                    item.AdminStatus.ToString(),
                    item.OperStatus.ToString()
                });
            }
            
            WriteCsvFile("interfaces.csv", headers, rows);
        }

        public void WriteIpAddresses(List<IpAddressInfo> list)
        {
            if (list == null || list.Count == 0) return;
            
            var headers = new[] { "IP Address", "NetMask", "IfIndex" };
            var rows = new List<string[]>();
            
            foreach (var item in list)
            {
                rows.Add(new[]
                {
                    EscapeCsv(item.Address),
                    EscapeCsv(item.Mask),
                    item.IfIndex.ToString()
                });
            }
            
            WriteCsvFile("ip_addresses.csv", headers, rows);
        }

        public void WriteArpTable(List<ArpEntry> list)
        {
            if (list == null || list.Count == 0) return;
            
            var headers = new[] { "IP Address", "MAC Address", "IfIndex", "Type" };
            var rows = new List<string[]>();
            
            foreach (var item in list)
            {
                rows.Add(new[]
                {
                    EscapeCsv(item.Ip),
                    EscapeCsv(item.Mac),
                    item.IfIndex.ToString(),
                    item.Type.ToString()
                });
            }
            
            WriteCsvFile("arp_table.csv", headers, rows);
        }

        public void WriteRoutingTable(List<RouteEntry> list)
        {
            if (list == null || list.Count == 0) return;
            
            var headers = new[] { "Dest", "Mask", "NextHop", "IfIdx", "Type", "Proto", "Metric", "Age" };
            var rows = new List<string[]>();
            
            foreach (var r in list)
            {
                rows.Add(new[]
                {
                    EscapeCsv(r.Dest),
                    EscapeCsv(r.Mask),
                    EscapeCsv(r.NextHop),
                    r.IfIndex.ToString(),
                    EscapeCsv(r.Type),
                    EscapeCsv(r.Proto),
                    r.Metric.ToString(),
                    r.Age.ToString()
                });
            }
            
            WriteCsvFile("routing_table.csv", headers, rows);
        }

        public void WriteDisks(List<DiskInfo> list)
        {
            if (list == null || list.Count == 0) return;
            
            var headers = new[] { "Disk", "Total (MB)", "Used (MB)", "Used (%)" };
            var rows = new List<string[]>();
            
            foreach (var item in list)
            {
                rows.Add(new[]
                {
                    EscapeCsv(item.Description),
                    item.TotalMB.ToString("F0"),
                    item.UsedMB.ToString("F0"),
                    item.Percent.ToString("F1")
                });
            }
            
            WriteCsvFile("disks.csv", headers, rows);
        }

        public void WriteCPU(List<CpuCore> list)
        {
            if (list == null || list.Count == 0) return;
            
            var headers = new[] { "Core", "Load (%)" };
            var rows = new List<string[]>();
            
            foreach (var item in list)
            {
                rows.Add(new[]
                {
                    item.Index.ToString(),
                    item.Load.ToString()
                });
            }
            
            WriteCsvFile("cpu.csv", headers, rows);
        }

        public void WriteProcesses(List<ProcessInfo> list)
        {
            if (list == null || list.Count == 0) return;
            
            var headers = new[] { "Name", "Path", "Params", "Type", "Status" };
            var rows = new List<string[]>();
            
            foreach (var item in list)
            {
                rows.Add(new[]
                {
                    EscapeCsv(item.Name),
                    EscapeCsv(item.Path),
                    EscapeCsv(item.Params),
                    EscapeCsv(item.Type),
                    EscapeCsv(item.Status)
                });
            }
            
            WriteCsvFile("processes.csv", headers, rows);
        }

        public void WriteDevices(List<DeviceInfo> list)
        {
            if (list == null || list.Count == 0) return;
            
            var headers = new[] { "Type", "Description", "Status", "Errors" };
            var rows = new List<string[]>();
            
            foreach (var item in list)
            {
                rows.Add(new[]
                {
                    item.Type.ToString(),
                    EscapeCsv(item.Description),
                    EscapeCsv(item.Status),
                    item.Errors.ToString()
                });
            }
            
            WriteCsvFile("devices.csv", headers, rows);
        }

        public void WriteStats(string title, List<StatEntry> list)
        {
            if (list == null || list.Count == 0) return;
            
            string fileName = title.ToLower().Replace(" ", "_").Replace("(", "").Replace(")", "") + ".csv";
            var headers = new[] { "Parameter", "Value" };
            var rows = new List<string[]>();
            
            foreach (var item in list)
            {
                rows.Add(new[]
                {
                    EscapeCsv(item.Name),
                    EscapeCsv(item.Value)
                });
            }
            
            WriteCsvFile(fileName, headers, rows);
        }

        private string FormatSpeed(ulong speed)
        {
            if (speed >= 1000000000) return (speed / 1000000000.0).ToString("0.0") + " Gbps";
            if (speed >= 1000000) return (speed / 1000000.0).ToString("0.0") + " Mbps";
            return speed + " bps";
        }

        public void Dispose()
        {
            if (_disposed) return;
            _logger.Debug("CSV Reporter освобожден");
            _disposed = true;
        }

        ~CsvReporter() { Dispose(); }
    }
}
