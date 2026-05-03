using System;
using System.IO;

namespace SnmpServerPoller.Logging
{
    public class FileLogger : ILogger
    {
        private readonly string _minLevel;
        private readonly string _filePath;
        private readonly object _lockObj = new();

        public FileLogger(string filePath, string minLevel = "Information")
        {
            _filePath = filePath;
            _minLevel = minLevel;

            // Создаем директорию если не существует
            string? dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
        }

        private int GetPriority(string level)
        {
            if (level == "Debug") return 0;
            if (level == "Information") return 1;
            if (level == "Warn") return 2;
            if (level == "Error") return 3;
            return 1;
        }

        private void Write(string level, string message, Exception? ex, params object[] args)
        {
            if (GetPriority(level) < GetPriority(_minLevel)) return;

            string formatted = args.Length > 0 ? string.Format(message, args) : message;
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            string prefix = "[" + timestamp + "] [" + level + "] ";
            string fullMessage = prefix + formatted;

            if (ex != null)
            {
                fullMessage += "\n" + prefix + "Exception: " + ex.Message;
                fullMessage += "\n" + prefix + "Stack: " + ex.StackTrace;
            }

            lock (_lockObj)
            {
                try
                {
                    using (var writer = new StreamWriter(_filePath, true))
                    {
                        writer.WriteLine(fullMessage);
                    }
                }
                catch (Exception writeEx)
                {
                    Console.WriteLine($"[FileLogger] Ошибка записи в файл: {writeEx.Message}");
                }
            }
        }

        public void Debug(string message, params object[] args)
        {
            Write("Debug", message, null, args);
        }

        public void Info(string message, params object[] args)
        {
            Write("Information", message, null, args);
        }

        public void Warn(string message, params object[] args)
        {
            Write("Warn", message, null, args);
        }

        public void Error(string message, params object[] args)
        {
            Write("Error", message, null, args);
        }

        public void Error(string message, Exception ex, params object[] args)
        {
            Write("Error", message, ex, args);
        }
    }
}
