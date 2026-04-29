using Microsoft.Extensions.Configuration;

namespace SnmpServerPoller.Config;

public class SnmpSettings
{
    public string Community { get; set; } = "public";
    public int Timeout { get; set; } = 5000;
    public int Retries { get; set; } = 3;
    public string Version { get; set; } = "v2c";
    public int MaxProcesses { get; set; } = 200;
    public int MaxArpEntries { get; set; } = 1000;
}

public class ExcelSettings
{
    public string TemplatePath { get; set; } = "ServerReport.xlsx";
    public bool AutoSave { get; set; } = true;
}

public class LoggingSettings
{
    public string LogLevel { get; set; } = "Information";
    public string FilePath { get; set; } = "logs/poller.log";
}

public class AppConfig
{
    public SnmpSettings Snmp { get; set; } = new();
    public ExcelSettings Excel { get; set; } = new();
    public LoggingSettings Logging { get; set; } = new();
    public string DefaultServerIp { get; set; } = "127.0.0.1";
}

public static class ConfigurationLoader
{
    public static AppConfig Load()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .Build();

        var config = new AppConfig();
        configuration.Bind(config);
        
        return config;
    }
}
