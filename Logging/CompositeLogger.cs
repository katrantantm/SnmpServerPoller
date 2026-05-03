using System;

namespace SnmpServerPoller.Logging
{
    public class CompositeLogger : ILogger
    {
        private readonly ILogger[] _loggers;

        public CompositeLogger(params ILogger[] loggers)
        {
            _loggers = loggers ?? Array.Empty<ILogger>();
        }

        public void Debug(string message, params object[] args)
        {
            foreach (var logger in _loggers)
            {
                try { logger.Debug(message, args); }
                catch { /* Игнорируем ошибки отдельных логгеров */ }
            }
        }

        public void Info(string message, params object[] args)
        {
            foreach (var logger in _loggers)
            {
                try { logger.Info(message, args); }
                catch { /* Игнорируем ошибки отдельных логгеров */ }
            }
        }

        public void Warn(string message, params object[] args)
        {
            foreach (var logger in _loggers)
            {
                try { logger.Warn(message, args); }
                catch { /* Игнорируем ошибки отдельных логгеров */ }
            }
        }

        public void Error(string message, params object[] args)
        {
            foreach (var logger in _loggers)
            {
                try { logger.Error(message, args); }
                catch { /* Игнорируем ошибки отдельных логгеров */ }
            }
        }

        public void Error(string message, Exception ex, params object[] args)
        {
            foreach (var logger in _loggers)
            {
                try { logger.Error(message, ex, args); }
                catch { /* Игнорируем ошибки отдельных логгеров */ }
            }
        }
    }
}
