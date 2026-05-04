using System;

namespace SnmpServerPoller.Logging
{
    public class ConsoleLogger : ILogger
    {
        private readonly string _minLevel;
        private readonly object _lockObj = new object();

        public ConsoleLogger(string minLevel = "Information")
        {
            _minLevel = minLevel;
        }

        private int GetPriority(string level)
        {
            if (level == "Debug") return 0;
            if (level == "Information") return 1;
            if (level == "Warn") return 2;
            if (level == "Error") return 3;
            return 1;
        }

        private void Write(string level, string message, Exception ex, params object[] args)
        {
            if (GetPriority(level) < GetPriority(_minLevel)) return;

            string formatted = args.Length > 0 ? string.Format(message, args) : message;
            string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            string prefix = "[" + timestamp + "] [" + level + "] ";
            string fullMessage = prefix + formatted;

            if (ex != null)
            {
                fullMessage += "\n" + prefix + "Exception: " + ex.Message;
                fullMessage += "\n" + prefix + "Stack: " + ex.StackTrace;
            }

            lock (_lockObj)
            {
                ConsoleColor originalColor = Console.ForegroundColor;

                if (level == "Error")
                    Console.ForegroundColor = ConsoleColor.Red;
                else if (level == "Warn")
                    Console.ForegroundColor = ConsoleColor.Yellow;
                else if (level == "Debug")
                    Console.ForegroundColor = ConsoleColor.Gray;

                Console.WriteLine(fullMessage);
                Console.ForegroundColor = originalColor;
            }
        }

        public void Debug(string message, params object?[] args)
        {
            Write("Debug", message, null, args);
        }

        public void Info(string message, params object?[] args)
        {
            Write("Information", message, null, args);
        }

        public void Warn(string message, params object?[] args)
        {
            Write("Warn", message, null, args);
        }

        public void Error(string message, params object?[] args)
        {
            Write("Error", message, null, args);
        }

        public void Error(string message, Exception ex, params object?[] args)
        {
            Write("Error", message, ex, args);
        }
    }
}