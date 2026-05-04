using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SnmpServerPoller.Logging;
using SnmpServerPoller.Models;

namespace SnmpServerPoller.Reporting
{
    public class PdfReporter : IDisposable
    {
        private readonly string _outputPath;
        private readonly ILogger _logger;
        private bool _disposed;

        public PdfReporter(string outputPath, ILogger? logger = null)
        {
            _outputPath = outputPath;
            _logger = logger ?? new ConsoleLogger();
            
            if (!Directory.Exists(_outputPath))
            {
                Directory.CreateDirectory(_outputPath);
            }
            
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public void WriteInterfaces(List<InterfaceInfo> list)
        {
            if (list == null || list.Count == 0) return;
            
            var fileName = "interfaces.pdf";
            var filePath = Path.Combine(_outputPath, fileName);
            _logger.Info("Запись PDF: {0}", filePath);

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(20);
                    page.DefaultTextStyle(x => x.FontSize(9).FontFamily(FontFamily.Arial));
                    
                    page.Header()
                        .Text($"Сетевые интерфейсы\nGenerated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}")
                        .SemiBold().FontSize(14).AlignCenter();
                    
                    page.Content()
                        .PaddingVertical(10)
                        .Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(0.5); // Idx
                                columns.RelativeColumn(2);   // Descr
                                columns.RelativeColumn(1);   // Type
                                columns.RelativeColumn(0.8); // MTU
                                columns.RelativeColumn(1);   // Speed
                                columns.RelativeColumn(1.2); // In
                                columns.RelativeColumn(1.2); // Out
                                columns.RelativeColumn(0.8); // InErr
                                columns.RelativeColumn(0.8); // OutErr
                                columns.RelativeColumn(0.7); // Admin
                                columns.RelativeColumn(0.7); // Oper
                            });

                            table.Header(header =>
                            {
                                header.Cell().Element(CellStyle).Text("Idx");
                                header.Cell().Element(CellStyle).Text("Descr");
                                header.Cell().Element(CellStyle).Text("Type");
                                header.Cell().Element(CellStyle).Text("MTU");
                                header.Cell().Element(CellStyle).Text("Speed");
                                header.Cell().Element(CellStyle).Text("In");
                                header.Cell().Element(CellStyle).Text("Out");
                                header.Cell().Element(CellStyle).Text("InErr");
                                header.Cell().Element(CellStyle).Text("OutErr");
                                header.Cell().Element(CellStyle).Text("Admin");
                                header.Cell().Element(CellStyle).Text("Oper");
                                
                                static IContainer CellStyle(IContainer container) 
                                    => container.DefaultTextStyle(x => x.SemiBold()).Padding(3).BorderBottom(1).BorderColor(Colors.Black);
                            });

                            foreach (var item in list)
                            {
                                table.Cell().Element(CellStyleData).Text(item.Index.ToString());
                                table.Cell().Element(CellStyleData).Text(item.Description);
                                table.Cell().Element(CellStyleData).Text(item.Type.ToString());
                                table.Cell().Element(CellStyleData).Text(item.Mtu.ToString());
                                table.Cell().Element(CellStyleData).Text(FormatSpeed(item.Speed));
                                table.Cell().Element(CellStyleData).Text(FormatBytes(item.InOctets));
                                table.Cell().Element(CellStyleData).Text(FormatBytes(item.OutOctets));
                                table.Cell().Element(CellStyleData).Text(item.InErrors.ToString());
                                table.Cell().Element(CellStyleData).Text(item.OutErrors.ToString());
                                table.Cell().Element(CellStyleData).Text(item.AdminStatus.ToString());
                                table.Cell().Element(CellStyleData).Text(item.OperStatus.ToString());
                                
                                static IContainer CellStyleData(IContainer container) 
                                    => container.Padding(3).BorderBottom(1).BorderColor(Colors.Grey.Lighten2);
                            }
                        });
                    
                    page.Footer()
                        .AlignCenter()
                        .Text(x =>
                        {
                            x.Span("Страница ");
                            x.CurrentPageNumber();
                            x.Span(" из ");
                            x.TotalPages();
                        });
                });
            }).GeneratePdf(filePath);
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

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(20);
                    page.DefaultTextStyle(x => x.FontSize(9).FontFamily(FontFamily.Arial));
                    
                    page.Header()
                        .Text($"{title}\nGenerated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}")
                        .SemiBold().FontSize(14).AlignCenter();
                    
                    page.Content()
                        .PaddingVertical(10)
                        .Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                foreach (var header in headers)
                                {
                                    columns.RelativeColumn();
                                }
                            });

                            table.Header(header =>
                            {
                                foreach (var headerText in headers)
                                {
                                    header.Cell().Element(CellStyle).Text(headerText);
                                }
                                
                                static IContainer CellStyle(IContainer container) 
                                    => container.DefaultTextStyle(x => x.SemiBold()).Padding(3).BorderBottom(1).BorderColor(Colors.Black);
                            });

                            foreach (var row in rows)
                            {
                                foreach (var cellText in row)
                                {
                                    table.Cell().Element(CellStyleData).Text(cellText);
                                }
                                
                                static IContainer CellStyleData(IContainer container) 
                                    => container.Padding(3).BorderBottom(1).BorderColor(Colors.Grey.Lighten2);
                            }
                        });
                    
                    page.Footer()
                        .AlignCenter()
                        .Text(x =>
                        {
                            x.Span("Страница ");
                            x.CurrentPageNumber();
                            x.Span(" из ");
                            x.TotalPages();
                        });
                });
            }).GeneratePdf(filePath);
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
                    FormatBytes(item.InOctets),
                    FormatBytes(item.OutOctets),
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
        
        private string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
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
