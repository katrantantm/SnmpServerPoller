using System;

namespace SnmpServerPoller.Logging
{
    /// <summary>
    /// Консольный логгер с поддержкой уровней и цветного вывода
    /// </summary>
    public class ConsoleLogger : ILogger
    {
        private readonly LogLevel _minLevel;
        private readonly object _lockObj = new();
        private readonly bool _enableColors;

        /// <summary>
        /// Конструктор по умолчанию
        /// </summary>
        public ConsoleLogger() : this(LogLevel.Info, true)
        {
        }

        /// <summary>
        /// Конструктор с типизированным уровнем логирования
        /// </summary>
        public ConsoleLogger(LogLevel minLevel, bool enableColors = true)
        {
            _minLevel = minLevel;
            _enableColors = enableColors;
        }

        /// <summary>
        /// Конструктор с уровнем логирования в виде строки (для загрузки из конфига)
        /// </summary>
        public ConsoleLogger(string minLevelString, bool enableColors = true)
            : this(ParseLogLevel(minLevelString), enableColors)
        {
        }

        private static LogLevel ParseLogLevel(string level)
        {
            string lower = level.ToLower();
            switch (lower)
            {
                case "debug":
                    return LogLevel.Debug;
                case "info":
                case "information":
                    return LogLevel.Info;
                case "warn":
                case "warning":
                    return LogLevel.Warn;
                case "error":
                    return LogLevel.Error;
                default:
                    return LogLevel.Info;
            }
        }

        private int GetPriority(LogLevel level)
        {
            return (int)level;
        }

        private void Write(LogLevel level, string message, Exception ex, params object[] args)
        {
            if (GetPriority(level) < GetPriority(_minLevel)) return;

            string formatted = args.Length > 0 ? string.Format(message, args) : message;
            string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            string levelStr = level.ToString().Substring(0, 4).ToUpper();
            string prefix = "[" + timestamp + "] [" + levelStr + "] ";
            string fullMessage = prefix + formatted;

            if (ex != null)
            {
                fullMessage += "\n" + prefix + "Exception: " + ex.Message;
                fullMessage += "\n" + prefix + "Stack: " + ex.StackTrace;
            }

            lock (_lockObj)
            {
                if (_enableColors)
                {
                    ConsoleColor originalColor = Console.ForegroundColor;

                    if (level == LogLevel.Error)
                        Console.ForegroundColor = ConsoleColor.Red;
                    else if (level == LogLevel.Warn)
                        Console.ForegroundColor = ConsoleColor.Yellow;
                    else if (level == LogLevel.Debug)
                        Console.ForegroundColor = ConsoleColor.Gray;

                    Console.WriteLine(fullMessage);
                    Console.ForegroundColor = originalColor;
                }
                else
                {
                    Console.WriteLine(fullMessage);
                }
            }
        }

        public void Debug(string message, params object[] args)
        {
            Write(LogLevel.Debug, message, null, args);
        }

        public void Info(string message, params object[] args)
        {
            Write(LogLevel.Info, message, null, args);
        }

        public void Warn(string message, params object[] args)
        {
            Write(LogLevel.Warn, message, null, args);
        }

        public void Error(string message, params object[] args)
        {
            Write(LogLevel.Error, message, null, args);
        }

        public void Error(string message, Exception ex, params object[] args)
        {
            Write(LogLevel.Error, message, ex, args);
        }
    }
}