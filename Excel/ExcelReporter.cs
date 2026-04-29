using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using SnmpServerPoller.Logging;
using SnmpServerPoller.Models;

namespace SnmpServerPoller.Reporting
{
    public class ExcelReporter : IDisposable
    {
        private Microsoft.Office.Interop.Excel.Application _xlApp;
        private Microsoft.Office.Interop.Excel.Workbook _xlWorkbook;
        private Microsoft.Office.Interop.Excel.Worksheet _xlSheet;
        private int _currentRow;
        private bool _disposed;
        private readonly ILogger _logger;

        private const int COLOR_HEADER_BG = 0x4472C4;
        private const int COLOR_HEADER_TEXT = 0xFFFFFF;
        private const int COLOR_BORDER = 0x000000;

        public ExcelReporter(string filePath, ILogger logger = null)
        {
            _logger = logger ?? new ConsoleLogger("Information");
            try
            {
                _logger.Info("Инициализация Excel: {0}", filePath);
                _xlApp = new Microsoft.Office.Interop.Excel.Application();
                _xlWorkbook = _xlApp.Workbooks.Open(filePath);

                try { _xlSheet = (Microsoft.Office.Interop.Excel.Worksheet)_xlWorkbook.Sheets["Сервер"]; }
                catch { _xlSheet = (Microsoft.Office.Interop.Excel.Worksheet)_xlWorkbook.Sheets[1]; }

                var rangeToClear = _xlSheet.Range[
                    _xlSheet.Cells[5, 1],
                    _xlSheet.Cells[_xlSheet.Rows.Count, _xlSheet.Columns.Count]];
                rangeToClear.Clear();

                _currentRow = 1;
                _xlApp.Visible = true;
                _xlApp.WindowState = Microsoft.Office.Interop.Excel.XlWindowState.xlMaximized;

                _logger.Info("Excel успешно инициализирован");
            }
            catch (Exception ex)
            {
                _logger.Error("Ошибка инициализации Excel: {0}", ex);
                _xlApp?.ScreenUpdating = true;
                throw new Exception("Ошибка Excel: " + ex.Message);
            }
        }

        public void Close() { Dispose(); }

        public void AddTitle(string title)
        {
            var range = (Microsoft.Office.Interop.Excel.Range)_xlSheet.Cells[_currentRow, 1];
            range.Value2 = title;
            ((Microsoft.Office.Interop.Excel.Range)range.Font).Bold = true;
            ((Microsoft.Office.Interop.Excel.Range)range.Font).Size = 12;
            range.Interior.Color = 0xE7E6E6;
            _currentRow++;
        }

        public void WriteScalar(string label, string value)
        {
            ((Microsoft.Office.Interop.Excel.Range)_xlSheet.Cells[_currentRow, 1]).Value2 = label;
            ((Microsoft.Office.Interop.Excel.Range)_xlSheet.Cells[_currentRow, 2]).Value2 = value;
            _currentRow++;
        }

        public void AddSpacing() => _currentRow += 2;

        private void StyleTable(Microsoft.Office.Interop.Excel.Range fullTableRange, int colsCount, bool hasNumbers = false)
        {
            if (fullTableRange == null) return;
            fullTableRange.Font.Size = 10;
            fullTableRange.Font.Name = "Calibri";
            fullTableRange.Interior.Color = 0xFFFFFF;
            fullTableRange.VerticalAlignment = Microsoft.Office.Interop.Excel.XlVAlign.xlVAlignCenter;

            var borders = fullTableRange.Borders;
            borders[Microsoft.Office.Interop.Excel.XlBordersIndex.xlEdgeLeft].LineStyle = Microsoft.Office.Interop.Excel.XlLineStyle.xlContinuous;
            borders[Microsoft.Office.Interop.Excel.XlBordersIndex.xlEdgeLeft].Weight = Microsoft.Office.Interop.Excel.XlBorderWeight.xlMedium;
            borders[Microsoft.Office.Interop.Excel.XlBordersIndex.xlEdgeLeft].Color = COLOR_BORDER;
            borders[Microsoft.Office.Interop.Excel.XlBordersIndex.xlEdgeTop].LineStyle = Microsoft.Office.Interop.Excel.XlLineStyle.xlContinuous;
            borders[Microsoft.Office.Interop.Excel.XlBordersIndex.xlEdgeTop].Weight = Microsoft.Office.Interop.Excel.XlBorderWeight.xlMedium;
            borders[Microsoft.Office.Interop.Excel.XlBordersIndex.xlEdgeTop].Color = COLOR_BORDER;
            borders[Microsoft.Office.Interop.Excel.XlBordersIndex.xlEdgeBottom].LineStyle = Microsoft.Office.Interop.Excel.XlLineStyle.xlContinuous;
            borders[Microsoft.Office.Interop.Excel.XlBordersIndex.xlEdgeBottom].Weight = Microsoft.Office.Interop.Excel.XlBorderWeight.xlMedium;
            borders[Microsoft.Office.Interop.Excel.XlBordersIndex.xlEdgeBottom].Color = COLOR_BORDER;
            borders[Microsoft.Office.Interop.Excel.XlBordersIndex.xlEdgeRight].LineStyle = Microsoft.Office.Interop.Excel.XlLineStyle.xlContinuous;
            borders[Microsoft.Office.Interop.Excel.XlBordersIndex.xlEdgeRight].Weight = Microsoft.Office.Interop.Excel.XlBorderWeight.xlMedium;
            borders[Microsoft.Office.Interop.Excel.XlBordersIndex.xlEdgeRight].Color = COLOR_BORDER;

            if (fullTableRange.Rows.Count > 1)
            {
                borders[Microsoft.Office.Interop.Excel.XlBordersIndex.xlInsideHorizontal].LineStyle = Microsoft.Office.Interop.Excel.XlLineStyle.xlContinuous;
                borders[Microsoft.Office.Interop.Excel.XlBordersIndex.xlInsideHorizontal].Weight = Microsoft.Office.Interop.Excel.XlBorderWeight.xlThin;
                borders[Microsoft.Office.Interop.Excel.XlBordersIndex.xlInsideHorizontal].Color = COLOR_BORDER;
            }
            if (fullTableRange.Columns.Count > 1)
            {
                borders[Microsoft.Office.Interop.Excel.XlBordersIndex.xlInsideVertical].LineStyle = Microsoft.Office.Interop.Excel.XlLineStyle.xlContinuous;
                borders[Microsoft.Office.Interop.Excel.XlBordersIndex.xlInsideVertical].Weight = Microsoft.Office.Interop.Excel.XlBorderWeight.xlThin;
                borders[Microsoft.Office.Interop.Excel.XlBordersIndex.xlInsideVertical].Color = COLOR_BORDER;
            }
            if (fullTableRange.Rows.Count >= 1)
            {
                var headerRange = (Microsoft.Office.Interop.Excel.Range)fullTableRange.Rows[1];
                headerRange.Interior.Color = COLOR_HEADER_BG;
                ((Microsoft.Office.Interop.Excel.Range)headerRange.Font).Color = COLOR_HEADER_TEXT;
                ((Microsoft.Office.Interop.Excel.Range)headerRange.Font).Bold = true;
                headerRange.HorizontalAlignment = Microsoft.Office.Interop.Excel.XlHAlign.xlHAlignCenter;
                headerRange.WrapText = true;
            }
            if (hasNumbers)
                ((Microsoft.Office.Interop.Excel.Range)fullTableRange.Columns[colsCount]).HorizontalAlignment = Microsoft.Office.Interop.Excel.XlHAlign.xlHAlignRight;
        }

        private void WriteToRange(object[,] data, int colsCount, bool hasNumbers)
        {
            int rows = data.GetUpperBound(0) + 1;
            var startCell = (Microsoft.Office.Interop.Excel.Range)_xlSheet.Cells[_currentRow, 1];
            var endCell = (Microsoft.Office.Interop.Excel.Range)_xlSheet.Cells[_currentRow + rows - 1, colsCount];
            var range = _xlSheet.Range[startCell, endCell];
            range.Value2 = data;
            StyleTable(range, colsCount, hasNumbers);
            _xlSheet.Columns.AutoFit();
            _currentRow += rows + 2;
        }

        private string FormatSpeed(ulong speed)
        {
            if (speed >= 1000000000) return (speed / 1000000000.0).ToString("0.0") + " Gbps";
            if (speed >= 1000000) return (speed / 1000000.0).ToString("0.0") + " Mbps";
            return speed + " bps";
        }

        public void WriteInterfaces(List<InterfaceInfo> list)
        {
            if (list == null) return;
            AddTitle("Сетевые интерфейсы");
            object[,] data = new object[list.Count + 1, 11];
            data[0, 0] = "Idx"; data[0, 1] = "Descr"; data[0, 2] = "Type"; data[0, 3] = "MTU"; data[0, 4] = "Speed";
            data[0, 5] = "In"; data[0, 6] = "Out"; data[0, 7] = "InErr"; data[0, 8] = "OutErr"; data[0, 9] = "Admin"; data[0, 10] = "Oper";
            for (int i = 0; i < list.Count; i++)
            {
                var item = list[i];
                data[i + 1, 0] = item.Index; data[i + 1, 1] = item.Description; data[i + 1, 2] = item.Type;
                data[i + 1, 3] = item.Mtu; data[i + 1, 4] = FormatSpeed(item.Speed); data[i + 1, 5] = item.InOctets;
                data[i + 1, 6] = item.OutOctets; data[i + 1, 7] = item.InErrors; data[i + 1, 8] = item.OutErrors;
                data[i + 1, 9] = item.AdminStatus; data[i + 1, 10] = item.OperStatus;
            }
            WriteToRange(data, 11, true);
        }

        public void WriteIpAddresses(List<IpAddressInfo> list)
        {
            if (list == null) return;
            AddTitle("IP-адреса интерфейсов");
            object[,] data = new object[list.Count + 1, 3];
            data[0, 0] = "IP Address"; data[0, 1] = "NetMask"; data[0, 2] = "IfIndex";
            for (int i = 0; i < list.Count; i++) { data[i + 1, 0] = list[i].Address; data[i + 1, 1] = list[i].Mask; data[i + 1, 2] = list[i].IfIndex; }
            WriteToRange(data, 3, false);
        }

        public void WriteArpTable(List<ArpEntry> list)
        {
            if (list == null) return;
            AddTitle("ARP-таблица");
            object[,] data = new object[list.Count + 1, 4];
            data[0, 0] = "IP Address"; data[0, 1] = "MAC Address"; data[0, 2] = "IfIndex"; data[0, 3] = "Type";
            for (int i = 0; i < list.Count; i++) { data[i + 1, 0] = list[i].Ip; data[i + 1, 1] = list[i].Mac; data[i + 1, 2] = list[i].IfIndex; data[i + 1, 3] = list[i].Type; }
            WriteToRange(data, 4, false);
        }

        public void WriteRoutingTable(List<RouteEntry> list)
        {
            if (list == null) return;
            AddTitle("Таблица маршрутизации");
            object[,] data = new object[list.Count + 1, 8];
            data[0, 0] = "Dest"; data[0, 1] = "Mask"; data[0, 2] = "NextHop"; data[0, 3] = "IfIdx";
            data[0, 4] = "Type"; data[0, 5] = "Proto"; data[0, 6] = "Metric"; data[0, 7] = "Age";
            for (int i = 0; i < list.Count; i++)
            {
                var r = list[i];
                data[i + 1, 0] = r.Dest; data[i + 1, 1] = r.Mask; data[i + 1, 2] = r.NextHop;
                data[i + 1, 3] = r.IfIndex; data[i + 1, 4] = r.Type; data[i + 1, 5] = r.Proto;
                data[i + 1, 6] = r.Metric; data[i + 1, 7] = r.Age;
            }
            WriteToRange(data, 8, false);
        }

        public void WriteDisks(List<DiskInfo> list)
        {
            if (list == null) return;
            AddTitle("Дисковое пространство");
            object[,] data = new object[list.Count + 1, 4];
            data[0, 0] = "Disk"; data[0, 1] = "Total (MB)"; data[0, 2] = "Used (MB)"; data[0, 3] = "Used (%)";
            for (int i = 0; i < list.Count; i++) { data[i + 1, 0] = list[i].Description; data[i + 1, 1] = list[i].TotalMB; data[i + 1, 2] = list[i].UsedMB; data[i + 1, 3] = list[i].Percent; }
            WriteToRange(data, 4, true);
        }

        public void WriteCPU(List<CpuCore> list)
        {
            if (list == null) return;
            AddTitle("Загрузка процессора");
            object[,] data = new object[list.Count + 1, 2];
            data[0, 0] = "Core"; data[0, 1] = "Load (%)";
            for (int i = 0; i < list.Count; i++) { data[i + 1, 0] = list[i].Index; data[i + 1, 1] = list[i].Load; }
            WriteToRange(data, 2, true);
        }

        public void WriteProcesses(List<ProcessInfo> list)
        {
            if (list == null) return;
            AddTitle("Запущенные процессы");
            object[,] data = new object[list.Count + 1, 5];
            data[0, 0] = "Name"; data[0, 1] = "Path"; data[0, 2] = "Params"; data[0, 3] = "Type"; data[0, 4] = "Status";
            for (int i = 0; i < list.Count; i++) { data[i + 1, 0] = list[i].Name; data[i + 1, 1] = list[i].Path; data[i + 1, 2] = list[i].Params; data[i + 1, 3] = list[i].Type; data[i + 1, 4] = list[i].Status; }
            WriteToRange(data, 5, false);
        }

        public void WriteDevices(List<DeviceInfo> list)
        {
            if (list == null) return;
            AddTitle("Оборудование");
            object[,] data = new object[list.Count + 1, 4];
            data[0, 0] = "Type"; data[0, 1] = "Description"; data[0, 2] = "Status"; data[0, 3] = "Errors";
            for (int i = 0; i < list.Count; i++) { data[i + 1, 0] = list[i].Type; data[i + 1, 1] = list[i].Description; data[i + 1, 2] = list[i].Status; data[i + 1, 3] = list[i].Errors; }
            WriteToRange(data, 4, true);
        }

        public void WriteStats(string title, List<StatEntry> list)
        {
            if (list == null) return;
            AddTitle(title);
            object[,] data = new object[list.Count + 1, 2];
            data[0, 0] = "Parameter"; data[0, 1] = "Value";
            for (int i = 0; i < list.Count; i++) { data[i + 1, 0] = list[i].Name; data[i + 1, 1] = list[i].Value; }
            WriteToRange(data, 2, true);
        }

        public void Dispose()
        {
            if (_disposed) return;
            try
            {
                _logger.Debug("Освобождение ресурсов Excel...");
                if (_xlWorkbook != null) { _xlWorkbook.Close(true); Marshal.ReleaseComObject(_xlWorkbook); _xlWorkbook = null; }
                if (_xlApp != null) { _xlApp.Quit(); Marshal.ReleaseComObject(_xlApp); _xlApp = null; }
                if (_xlSheet != null) { Marshal.ReleaseComObject(_xlSheet); _xlSheet = null; }
                GC.Collect(); GC.WaitForPendingFinalizers();
                _logger.Debug("Excel ресурсы освобождены");
            }
            catch (Exception ex)
            {
                _logger.Error("Ошибка при освобождении Excel: {0}", ex);
            }
            _disposed = true;
        }

        ~ExcelReporter() { Dispose(); }
    }
}