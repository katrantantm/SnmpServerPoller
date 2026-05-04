using System;
using SnmpServerPoller.Config;
using SnmpServerPoller.Logging;
using SnmpServerPoller.Reporting;

namespace SnmpServerPoller
{
    class Program
    {
        static void Main(string[] args)
        {
            // Загрузка конфигурации из файла
            var config = ConfigurationLoader.Load();
            
            // Создание логгера с записью в файл и консоль
            ILogger logger = new CompositeLogger(
                new ConsoleLogger(config.Logging?.LogLevel ?? "Information"),
                new FileLogger(config.Logging?.FilePath ?? "logs/poller.log", config.Logging?.LogLevel ?? "Information")
            );

            string serverIp = args.Length > 0 ? args[0] : config.Snmp?.TargetIp ?? "87.242.86.112";
            string community = config.Snmp?.Community ?? "public";
            string outputPath = config.Export?.OutputPath ?? "output";

            try
            {
                logger.Info("🚀 Запуск универсального SNMP Poller");
                logger.Info("Целевой сервер: {0}", serverIp);
                logger.Info("Сообщество: {0}", community);
                logger.Info("Путь вывода: {0}", outputPath);

                // Использование универсального генератора отчетов
                using var generator = new UniversalReportGenerator(serverIp, community, outputPath, logger);
                
                // Генерация всех отчетов (CSV и PDF) на основе конфигурации из snmp-tables.json
                generator.GenerateAllReports();

                logger.Info("✅ Обработка завершена успешно");
                Console.WriteLine("Готово. Нажмите Enter для выхода...");
                Console.ReadKey();
            }
            catch (Exception ex)
            {
                logger.Error("❌ Критическая ошибка: {0}", ex);
                Console.ReadKey();
            }
        }
    }
}