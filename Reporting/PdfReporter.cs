using System;
using System.Collections.Generic;
using System.IO;
using SnmpServerPoller.Logging;
using SnmpServerPoller.Models;

namespace SnmpServerPoller.Reporting
{
    public class PdfReporter : IDisposable
    {
        private readonly string _outputPath;
        private readonly ILogger _logger;
        private bool _disposed;

        public PdfReporter(string outputPath, ILogger logger = null)
        {
            _outputPath = outputPath;
            _logger = logger ?? new ConsoleLogger();
            
            if (!Directory.Exists(_outputPath))
            {
                Directory.CreateDirectory(_outputPath);
            }
        }

        public void WriteInterfaces(List<InterfaceInfo> list)
        {
            if (list == null || list.Count == 0) return;
            WriteSimpleTable("interfaces.pdf", "Сетевые интерфейсы", 
                new[] { "Idx", "Descr", "Type", "MTU", "Speed", "In", "Out", "InErr", "OutErr", "Admin", "Oper" },
                ConvertInterfaces(list));
        }

        public void WriteIpAddresses(List<IpAddressInfo> list)
        {
            if (list == null || list.Count == 0) return;
            WriteSimpleTable("ip_addresses.pdf", "IP-адреса",
                new[] { "IP Address", "NetMask", "IfIndex" },
                ConvertIpAddresses(list));
        }

        public void WriteArpTable(List<ArpEntry> list)
        {
            if (list == null || list.Count == 0) return;
            WriteSimpleTable("arp_table.pdf", "ARP-таблица",
                new[] { "IP Address", "MAC Address", "IfIndex", "Type" },
                ConvertArpTable(list));
        }

        public void WriteRoutingTable(List<RouteEntry> list)
        {
            if (list == null || list.Count == 0) return;
            WriteSimpleTable("routing_table.pdf", "Таблица маршрутизации",
                new[] { "Dest", "Mask", "NextHop", "IfIdx", "Type", "Proto", "Metric", "Age" },
                ConvertRoutingTable(list));
        }

        public void WriteDisks(List<DiskInfo> list)
        {
            if (list == null || list.Count == 0) return;
            WriteSimpleTable("disks.pdf", "Дисковое пространство",
                new[] { "Disk", "Total (MB)", "Used (MB)", "Used (%)" },
                ConvertDisks(list));
        }

        public void WriteCPU(List<CpuCore> list)
        {
            if (list == null || list.Count == 0) return;
            WriteSimpleTable("cpu.pdf", "Загрузка процессора",
                new[] { "Core", "Load (%)" },
                ConvertCPU(list));
        }

        public void WriteProcesses(List<ProcessInfo> list)
        {
            if (list == null || list.Count == 0) return;
            WriteSimpleTable("processes.pdf", "Процессы",
                new[] { "Name", "Path", "Params", "Type", "Status" },
                ConvertProcesses(list));
        }

        public void WriteDevices(List<DeviceInfo> list)
        {
            if (list == null || list.Count == 0) return;
            WriteSimpleTable("devices.pdf", "Оборудование",
                new[] { "Type", "Description", "Status", "Errors" },
                ConvertDevices(list));
        }

        public void WriteStats(string title, List<StatEntry> list)
        {
            if (list == null || list.Count == 0) return;
            string fileName = title.ToLower().Replace(" ", "_").Replace("(", "").Replace(")", "") + ".pdf";
            WriteSimpleTable(fileName, title,
                new[] { "Parameter", "Value" },
                ConvertStats(list));
        }

        private void WriteSimpleTable(string fileName, string title, string[] headers, List<string[]> rows)
        {
            string filePath = Path.Combine(_outputPath, fileName);
            _logger.Info("Запись PDF: {0}", filePath);

            using (var writer = new StreamWriter(filePath, false))
            {
                // Простой текстовый формат с расширением .pdf (для просмотра в текстовом редакторе)
                // В реальном проекте здесь应该 использовать библиотеку вроде iTextSharp или QuestPDF
                writer.WriteLine("%PDF-1.4");
                writer.WriteLine("SNMP Server Report - " + title);
                writer.WriteLine("Generated: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                writer.WriteLine(new string('=', 80));
                writer.WriteLine();
                
                // Заголовки
                writer.WriteLine(string.Join(" | ", headers));
                writer.WriteLine(new string('-', 80));
                
                // Данные
                foreach (var row in rows)
                {
                    writer.WriteLine(string.Join(" | ", row));
                }
                
                writer.WriteLine();
                writer.WriteLine(new string('=', 80));
                writer.WriteLine("End of report");
            }
        }

        private List<string[]> ConvertInterfaces(List<InterfaceInfo> list)
        {
            var rows = new List<string[]>();
            foreach (var item in list)
            {
                rows.Add(new[]
                {
                    item.Index.ToString(),
                    item.Description,
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
            return rows;
        }

        private List<string[]> ConvertIpAddresses(List<IpAddressInfo> list)
        {
            var rows = new List<string[]>();
            foreach (var item in list)
            {
                rows.Add(new[] { item.Address, item.Mask, item.IfIndex.ToString() });
            }
            return rows;
        }

        private List<string[]> ConvertArpTable(List<ArpEntry> list)
        {
            var rows = new List<string[]>();
            foreach (var item in list)
            {
                rows.Add(new[] { item.Ip, item.Mac, item.IfIndex.ToString(), item.Type.ToString() });
            }
            return rows;
        }

        private List<string[]> ConvertRoutingTable(List<RouteEntry> list)
        {
            var rows = new List<string[]>();
            foreach (var r in list)
            {
                rows.Add(new[] { r.Dest, r.Mask, r.NextHop, r.IfIndex.ToString(), r.Type, r.Proto, r.Metric.ToString(), r.Age.ToString() });
            }
            return rows;
        }

        private List<string[]> ConvertDisks(List<DiskInfo> list)
        {
            var rows = new List<string[]>();
            foreach (var item in list)
            {
                rows.Add(new[] { item.Description, item.TotalMB.ToString("F0"), item.UsedMB.ToString("F0"), item.Percent.ToString("F1") });
            }
            return rows;
        }

        private List<string[]> ConvertCPU(List<CpuCore> list)
        {
            var rows = new List<string[]>();
            foreach (var item in list)
            {
                rows.Add(new[] { item.Index.ToString(), item.Load.ToString() });
            }
            return rows;
        }

        private List<string[]> ConvertProcesses(List<ProcessInfo> list)
        {
            var rows = new List<string[]>();
            foreach (var item in list)
            {
                rows.Add(new[] { item.Name, item.Path, item.Params, item.Type, item.Status });
            }
            return rows;
        }

        private List<string[]> ConvertDevices(List<DeviceInfo> list)
        {
            var rows = new List<string[]>();
            foreach (var item in list)
            {
                rows.Add(new[] { item.Type.ToString(), item.Description, item.Status, item.Errors.ToString() });
            }
            return rows;
        }

        private List<string[]> ConvertStats(List<StatEntry> list)
        {
            var rows = new List<string[]>();
            foreach (var item in list)
            {
                rows.Add(new[] { item.Name, item.Value });
            }
            return rows;
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
            _logger.Debug("PDF Reporter освобожден");
            _disposed = true;
        }

        ~PdfReporter() { Dispose(); }
    }
}
