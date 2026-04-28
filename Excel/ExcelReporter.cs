using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using SnmpServerPoller.Logging;
using SnmpServerPoller.Models;
using Excel = Microsoft.Office.Interop.Excel;

namespace SnmpServerPoller.Reporting
{
    /// <summary>
    /// Класс для экспорта данных SNMP в Excel с форматированием
    /// </summary>
    public class ExcelReporter : IDisposable
    {
        private Excel.Application? _xlApp;
        private Excel.Workbook? _xlWorkbook;
        private Excel.Worksheet? _xlSheet;
        private int _currentRow;
        private bool _disposed;
        private readonly ILogger _logger;

        // Цвета для форматирования
        private const int COLOR_HEADER_BG = 0x4472C4;
        private const int COLOR_HEADER_TEXT = 0xFFFFFF;
        private const int COLOR_BORDER = 0x000000;
        private const int COLOR_TITLE_BG = 0xE7E6E6;

        public ExcelReporter(string filePath, ILogger logger = null)
        {
            _logger = logger ?? new ConsoleLogger();
            try
            {
                _logger.Info("Инициализация Excel: {0}", filePath);
                _xlApp = new Excel.Application();
                _xlWorkbook = _xlApp.Workbooks.Open(filePath);

                try 
                { 
                    _xlSheet = (Excel.Worksheet)_xlWorkbook.Sheets["Сервер"]; 
                }
                catch 
                { 
                    _xlSheet = (Excel.Worksheet)_xlWorkbook.Sheets[1]; 
                }

                // Очистка старых данных
                var rangeToClear = _xlSheet.Range[
                    _xlSheet.Cells[5, 1],
                    _xlSheet.Cells[_xlSheet.Rows.Count, _xlSheet.Columns.Count]];
                rangeToClear.Clear();

                _currentRow = 1;
                _xlApp.Visible = true;
                _xlApp.WindowState = Excel.XlWindowState.xlMaximized;

                _logger.Info("Excel успешно инициализирован");
            }
            catch (Exception ex)
            {
                _logger.Error("Ошибка инициализации Excel", ex);
                _xlApp?.ScreenUpdating = true;
                throw new Exception("Ошибка Excel: " + ex.Message, ex);
            }
        }

        public void Close() => Dispose();

        public void AddTitle(string title)
        {
            if (_xlSheet == null) return;
            
            var range = _xlSheet.Cells[_currentRow, 1];
            range.Value = title;
            range.Font.Bold = true;
            range.Font.Size = 12;
            range.Interior.Color = COLOR_TITLE_BG;
            _currentRow++;
        }

        public void WriteSystemInfo(SystemInfo info)
        {
            if (_xlSheet == null || info == null) return;
            
            AddTitle("Системная информация");
            WriteScalar("Описание", info.Description);
            WriteScalar("Имя хоста", info.HostName);
            WriteScalar("Время работы", info.UpTime);
            WriteScalar("Контакт", info.Contact);
            WriteScalar("Расположение", info.Location);
        }

        public void WriteScalar(string label, string? value)
        {
            if (_xlSheet == null) return;
            
            _xlSheet.Cells[_currentRow, 1].Value = label;
            _xlSheet.Cells[_currentRow, 2].Value = value ?? "N/A";
            _currentRow++;
        }

        public void AddSpacing() => _currentRow += 2;

        private void StyleTable(Excel.Range fullTableRange, int colsCount, bool hasNumbers = false)
        {
            if (fullTableRange == null || _xlSheet == null) return;
            
            fullTableRange.Font.Size = 10;
            fullTableRange.Font.Name = "Calibri";
            fullTableRange.Interior.Color = 0xFFFFFF;
            fullTableRange.VerticalAlignment = Excel.XlVAlign.xlVAlignCenter;

            var borders = fullTableRange.Borders;
            ApplyBorder(borders, Excel.XlBordersIndex.xlEdgeLeft, Excel.XlBorderWeight.xlMedium);
            ApplyBorder(borders, Excel.XlBordersIndex.xlEdgeTop, Excel.XlBorderWeight.xlMedium);
            ApplyBorder(borders, Excel.XlBordersIndex.xlEdgeBottom, Excel.XlBorderWeight.xlMedium);
            ApplyBorder(borders, Excel.XlBordersIndex.xlEdgeRight, Excel.XlBorderWeight.xlMedium);

            if (fullTableRange.Rows.Count > 1)
                ApplyBorder(borders, Excel.XlBordersIndex.xlInsideHorizontal, Excel.XlBorderWeight.xlThin);
            
            if (fullTableRange.Columns.Count > 1)
                ApplyBorder(borders, Excel.XlBordersIndex.xlInsideVertical, Excel.XlBorderWeight.xlThin);

            // Стиль заголовка
            if (fullTableRange.Rows.Count >= 1)
            {
                var headerRange = fullTableRange.Rows[1] as Excel.Range;
                if (headerRange != null)
                {
                    headerRange.Interior.Color = COLOR_HEADER_BG;
                    headerRange.Font.Color = COLOR_HEADER_TEXT;
                    headerRange.Font.Bold = true;
                    headerRange.HorizontalAlignment = Excel.XlHAlign.xlHAlignCenter;
                    headerRange.WrapText = true;
                }
            }
            
            if (hasNumbers)
                fullTableRange.Columns[colsCount].HorizontalAlignment = Excel.XlHAlign.xlHAlignRight;
        }

        private void ApplyBorder(Excel.Borders borders, Excel.XlBordersIndex index, Excel.XlBorderWeight weight)
        {
            var border = borders[index];
            border.LineStyle = Excel.XlLineStyle.xlContinuous;
            border.Weight = weight;
            border.Color = COLOR_BORDER;
        }

        private void WriteToRange(object[,] data, int colsCount, bool hasNumbers)
        {
            if (_xlSheet == null) return;
            
            int rows = data.GetUpperBound(0) + 1;
            var startCell = _xlSheet.Cells[_currentRow, 1];
            var endCell = _xlSheet.Cells[_currentRow + rows - 1, colsCount];
            var range = _xlSheet.Range[startCell, endCell];
            range.Value = data;
            StyleTable(range, colsCount, hasNumbers);
            _xlSheet.Columns.AutoFit();
            _currentRow += rows + 2;
        }

        private static string FormatSpeed(ulong speed)
        {
            if (speed >= 1_000_000_000) return (speed / 1_000_000_000.0).ToString("0.0") + " Gbps";
            if (speed >= 1_000_000) return (speed / 1_000_000.0).ToString("0.0") + " Mbps";
            return speed + " bps";
        }

        public void WriteInterfaces(List<InterfaceInfo> list)
        {
            if (_xlSheet == null || list == null || list.Count == 0) return;
            
            AddTitle("Сетевые интерфейсы");
            object[,] data = new object[list.Count + 1, 11];
            string[] headers = { "Idx", "Descr", "Type", "MTU", "Speed", "In", "Out", "InErr", "OutErr", "Admin", "Oper" };
            
            for (int j = 0; j < headers.Length; j++)
                data[0, j] = headers[j];
            
            for (int i = 0; i < list.Count; i++)
            {
                var item = list[i];
                data[i + 1, 0] = item.Index;
                data[i + 1, 1] = item.Description;
                data[i + 1, 2] = item.Type;
                data[i + 1, 3] = item.Mtu;
                data[i + 1, 4] = FormatSpeed(item.Speed);
                data[i + 1, 5] = item.InOctets;
                data[i + 1, 6] = item.OutOctets;
                data[i + 1, 7] = item.InErrors;
                data[i + 1, 8] = item.OutErrors;
                data[i + 1, 9] = item.AdminStatus;
                data[i + 1, 10] = item.OperStatus;
            }
            WriteToRange(data, 11, true);
        }

        public void WriteIpAddresses(List<IpAddressInfo> list)
        {
            if (_xlSheet == null || list == null || list.Count == 0) return;
            
            AddTitle("IP-адреса интерфейсов");
            object[,] data = new object[list.Count + 1, 3];
            string[] headers = { "IP Address", "NetMask", "IfIndex" };
            
            for (int j = 0; j < headers.Length; j++)
                data[0, j] = headers[j];
            
            for (int i = 0; i < list.Count; i++)
            {
                data[i + 1, 0] = list[i].Address;
                data[i + 1, 1] = list[i].Mask;
                data[i + 1, 2] = list[i].IfIndex;
            }
            WriteToRange(data, 3, false);
        }

        public void WriteArpTable(List<ArpEntry> list)
        {
            if (_xlSheet == null || list == null || list.Count == 0) return;
            
            AddTitle("ARP-таблица");
            object[,] data = new object[list.Count + 1, 4];
            string[] headers = { "IP Address", "MAC Address", "IfIndex", "Type" };
            
            for (int j = 0; j < headers.Length; j++)
                data[0, j] = headers[j];
            
            for (int i = 0; i < list.Count; i++)
            {
                data[i + 1, 0] = list[i].Ip;
                data[i + 1, 1] = list[i].Mac;
                data[i + 1, 2] = list[i].IfIndex;
                data[i + 1, 3] = list[i].Type;
            }
            WriteToRange(data, 4, false);
        }

        public void WriteRoutingTable(List<RouteEntry> list)
        {
            if (_xlSheet == null || list == null || list.Count == 0) return;
            
            AddTitle("Таблица маршрутизации");
            object[,] data = new object[list.Count + 1, 8];
            string[] headers = { "Dest", "Mask", "NextHop", "IfIdx", "Type", "Proto", "Metric", "Age" };
            
            for (int j = 0; j < headers.Length; j++)
                data[0, j] = headers[j];
            
            for (int i = 0; i < list.Count; i++)
            {
                var r = list[i];
                data[i + 1, 0] = r.Dest;
                data[i + 1, 1] = r.Mask;
                data[i + 1, 2] = r.NextHop;
                data[i + 1, 3] = r.IfIndex;
                data[i + 1, 4] = r.Type;
                data[i + 1, 5] = r.Proto;
                data[i + 1, 6] = r.Metric;
                data[i + 1, 7] = r.Age;
            }
            WriteToRange(data, 8, false);
        }

        public void WriteDisks(List<DiskInfo> list)
        {
            if (_xlSheet == null || list == null || list.Count == 0) return;
            
            AddTitle("Дисковое пространство");
            object[,] data = new object[list.Count + 1, 4];
            string[] headers = { "Disk", "Total (MB)", "Used (MB)", "Used (%)" };
            
            for (int j = 0; j < headers.Length; j++)
                data[0, j] = headers[j];
            
            for (int i = 0; i < list.Count; i++)
            {
                data[i + 1, 0] = list[i].Description;
                data[i + 1, 1] = list[i].TotalMB;
                data[i + 1, 2] = list[i].UsedMB;
                data[i + 1, 3] = list[i].Percent;
            }
            WriteToRange(data, 4, true);
        }

        public void WriteCPU(List<CpuCore> list)
        {
            if (_xlSheet == null || list == null || list.Count == 0) return;
            
            AddTitle("Загрузка процессора");
            object[,] data = new object[list.Count + 1, 2];
            string[] headers = { "Core", "Load (%)" };
            
            for (int j = 0; j < headers.Length; j++)
                data[0, j] = headers[j];
            
            for (int i = 0; i < list.Count; i++)
            {
                data[i + 1, 0] = list[i].Index;
                data[i + 1, 1] = list[i].Load;
            }
            WriteToRange(data, 2, true);
        }

        public void WriteProcesses(List<ProcessInfo> list)
        {
            if (_xlSheet == null || list == null || list.Count == 0) return;
            
            AddTitle("Запущенные процессы");
            object[,] data = new object[list.Count + 1, 5];
            string[] headers = { "Name", "Path", "Params", "Type", "Status" };
            
            for (int j = 0; j < headers.Length; j++)
                data[0, j] = headers[j];
            
            for (int i = 0; i < list.Count; i++)
            {
                data[i + 1, 0] = list[i].Name;
                data[i + 1, 1] = list[i].Path;
                data[i + 1, 2] = list[i].Params;
                data[i + 1, 3] = list[i].Type;
                data[i + 1, 4] = list[i].Status;
            }
            WriteToRange(data, 5, false);
        }

        public void WriteDevices(List<DeviceInfo> list)
        {
            if (_xlSheet == null || list == null || list.Count == 0) return;
            
            AddTitle("Оборудование");
            object[,] data = new object[list.Count + 1, 4];
            string[] headers = { "Type", "Description", "Status", "Errors" };
            
            for (int j = 0; j < headers.Length; j++)
                data[0, j] = headers[j];
            
            for (int i = 0; i < list.Count; i++)
            {
                data[i + 1, 0] = list[i].Type;
                data[i + 1, 1] = list[i].Description;
                data[i + 1, 2] = list[i].Status;
                data[i + 1, 3] = list[i].Errors;
            }
            WriteToRange(data, 4, true);
        }

        public void WriteStats(string title, List<StatEntry> list)
        {
            if (_xlSheet == null || list == null || list.Count == 0) return;
            
            AddTitle(title);
            object[,] data = new object[list.Count + 1, 2];
            string[] headers = { "Parameter", "Value" };
            
            for (int j = 0; j < headers.Length; j++)
                data[0, j] = headers[j];
            
            for (int i = 0; i < list.Count; i++)
            {
                data[i + 1, 0] = list[i].Name;
                data[i + 1, 1] = list[i].Value;
            }
            WriteToRange(data, 2, true);
        }

        public void Dispose()
        {
            if (_disposed) return;
            
            try
            {
                _logger.Debug("Освобождение ресурсов Excel...");
                
                if (_xlWorkbook != null) 
                { 
                    _xlWorkbook.Close(true); 
                    Marshal.ReleaseComObject(_xlWorkbook); 
                    _xlWorkbook = null; 
                }
                
                if (_xlApp != null) 
                { 
                    _xlApp.Quit(); 
                    Marshal.ReleaseComObject(_xlApp); 
                    _xlApp = null; 
                }
                
                if (_xlSheet != null) 
                { 
                    Marshal.ReleaseComObject(_xlSheet); 
                    _xlSheet = null; 
                }
                
                GC.Collect(); 
                GC.WaitForPendingFinalizers();
                _logger.Debug("Excel ресурсы освобождены");
            }
            catch (Exception ex)
            {
                _logger.Error("Ошибка при освобождении Excel", ex);
            }
            
            _disposed = true;
        }

        ~ExcelReporter() => Dispose();
    }
}
