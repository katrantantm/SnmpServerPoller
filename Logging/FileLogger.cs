using System;
using System.IO;

namespace SnmpServerPoller.Logging
{
    /// <summary>
    /// Логгер для записи в файл
    /// </summary>
    public class FileLogger : ILogger
    {
        private readonly string _filePath;
        private readonly string _minLevel;
        private readonly object _lockObj = new();

        public FileLogger(string filePath, string minLevel = "Information")
        {
            _filePath = filePath;
            _minLevel = minLevel;
            
            // Создаем директорию если не существует
            string? directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        private int GetPriority(string level) => level switch
        {
            "Debug" => 0,
            "Information" => 1,
            "Warn" => 2,
            "Error" => 3,
            _ => 1
        };

        private void Write(string level, string message, Exception? ex, params object[] args)
        {
            if (GetPriority(level) < GetPriority(_minLevel)) return;

            string formatted = args.Length > 0 ? string.Format(message, args) : message;
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            string logLine = $"[{timestamp}] [{level}] {formatted}";

            if (ex != null)
            {
                logLine += $"\n[{timestamp}] [{level}] Exception: {ex.Message}";
                logLine += $"\n[{timestamp}] [{level}] Stack: {ex.StackTrace}";
            }

            lock (_lockObj)
            {
                try
                {
                    File.AppendAllText(_filePath, logLine + Environment.NewLine);
                }
                catch (IOException ioEx)
                {
                    Console.WriteLine($"Ошибка записи в лог-файл: {ioEx.Message}");
                }
            }
        }

        public void Debug(string message, params object[] args) => Write("Debug", message, null, args);
        public void Info(string message, params object[] args) => Write("Information", message, null, args);
        public void Warn(string message, params object[] args) => Write("Warn", message, null, args);
        public void Error(string message, params object[] args) => Write("Error", message, null, args);
        public void Error(string message, Exception ex, params object[] args) => Write("Error", message, ex, args);
    }
}
