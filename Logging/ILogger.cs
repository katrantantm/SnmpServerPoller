using System;

namespace SnmpServerPoller.Logging
{
    public interface ILogger
    {
        void Debug(string message, params object[] args);
        void Info(string message, params object[] args);
        void Warn(string message, params object[] args);
        void Error(string message, params object[] args);
        void Error(string message, Exception ex, params object[] args);
    }
}