using System;
using System.Collections.Generic;

namespace SnmpServerPoller.Logging
{
    /// <summary>
    /// Композитный логгер для записи в несколько источников одновременно
    /// </summary>
    public class CompositeLogger : ILogger
    {
        private readonly IEnumerable<ILogger> _loggers;

        public CompositeLogger(IEnumerable<ILogger> loggers)
        {
            _loggers = loggers ?? throw new ArgumentNullException(nameof(loggers));
        }

        public void Debug(string message, params object[] args)
        {
            foreach (var logger in _loggers)
                logger.Debug(message, args);
        }

        public void Info(string message, params object[] args)
        {
            foreach (var logger in _loggers)
                logger.Info(message, args);
        }

        public void Warn(string message, params object[] args)
        {
            foreach (var logger in _loggers)
                logger.Warn(message, args);
        }

        public void Error(string message, params object[] args)
        {
            foreach (var logger in _loggers)
                logger.Error(message, args);
        }

        public void Error(string message, Exception ex, params object[] args)
        {
            foreach (var logger in _loggers)
                logger.Error(message, ex, args);
        }
    }
}
