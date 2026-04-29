using System;

namespace SnmpServerPoller.Logging;

public class ConsoleLogger(string minLevel = "Information") : ILogger
{
    private readonly string _minLevel = minLevel;
    private readonly object _lockObj = new();

    private static int GetPriority(string level) => level switch
    {
        "Debug" => 0,
        "Information" => 1,
        "Warn" => 2,
        "Error" => 3,
        _ => 1
    };

    private void Write(string level, string message, Exception? ex, params object?[] args)
    {
        if (GetPriority(level) < GetPriority(_minLevel)) return;

        string formatted = args.Length > 0 ? string.Format(message, args) : message;
        string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        string prefix = $"[{timestamp}] [{level}] ";
        string fullMessage = prefix + formatted;

        if (ex != null)
        {
            fullMessage += $"\n{prefix}Exception: {ex.Message}";
            fullMessage += $"\n{prefix}Stack: {ex.StackTrace}";
        }

        lock (_lockObj)
        {
            ConsoleColor originalColor = Console.ForegroundColor;

            Console.ForegroundColor = level switch
            {
                "Error" => ConsoleColor.Red,
                "Warn" => ConsoleColor.Yellow,
                "Debug" => ConsoleColor.Gray,
                _ => originalColor
            };

            Console.WriteLine(fullMessage);
            Console.ForegroundColor = originalColor;
        }
    }

    public void Debug(string message, params object?[] args) => Write("Debug", message, null, args);
    public void Info(string message, params object?[] args) => Write("Information", message, null, args);
    public void Warn(string message, params object?[] args) => Write("Warn", message, null, args);
    public void Error(string message, params object?[] args) => Write("Error", message, null, args);
    public void Error(Exception ex, string message, params object?[] args) => Write("Error", message, ex, args);
}