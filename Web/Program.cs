using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using System.Runtime.InteropServices;
using System;

namespace Web
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            var logFilePath = configuration["Serilog:LogFilePath"] ?? "Logs/mylog-.txt";
            var retainedFileCountLimit = int.TryParse(configuration["Serilog:RetainedFileCountLimit"], out var retained) ? retained : 30;
            var fileSizeLimitBytes = long.TryParse(configuration["Serilog:FileSizeLimitBytes"], out var fileSizeLimit) ? fileSizeLimit : 10_485_760;
            var rollOnFileSizeLimit = bool.TryParse(configuration["Serilog:RollOnFileSizeLimit"], out var roll) ? roll : true;
            var outputTemplate = configuration["Serilog:OutputTemplate"] ?? "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}";
            var adminSecret = configuration["AdminErrorDetailsSecret"];
            Log.Information("Admin secret configured: {HasSecret}", string.IsNullOrEmpty(adminSecret) ? "NO" : "YES");

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Is(ParseLogLevel(configuration["Serilog:MinimumLevel:Default"], LogEventLevel.Debug))
                .MinimumLevel.Override("Microsoft", ParseLogLevel(configuration["Serilog:MinimumLevel:Override:Microsoft"], LogEventLevel.Information))
                .MinimumLevel.Override("Microsoft.Hosting.Lifetime", ParseLogLevel(configuration["Serilog:MinimumLevel:Override:Microsoft.Hosting.Lifetime"], LogEventLevel.Information))
                .Enrich.FromLogContext()
                .WriteTo.File(
                    logFilePath,
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: retainedFileCountLimit,
                    fileSizeLimitBytes: fileSizeLimitBytes,
                    rollOnFileSizeLimit: rollOnFileSizeLimit,
                    shared: true,
                    outputTemplate: outputTemplate)
                .CreateLogger();

            try
            {
                Log.Information("Starting web host");
                Log.Information("Runtime diagnostics: Framework={Framework}, EnvironmentVersion={EnvironmentVersion}, OS={OS}, Architecture={Architecture}",
                    RuntimeInformation.FrameworkDescription,
                    Environment.Version,
                    RuntimeInformation.OSDescription,
                    RuntimeInformation.OSArchitecture);
                CreateHostBuilder(args).Build().Run();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Host terminated unexpectedly");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseSerilog()
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });

        private static LogEventLevel ParseLogLevel(string value, LogEventLevel defaultLevel)
        {
            return Enum.TryParse<LogEventLevel>(value, true, out var level)
                ? level
                : defaultLevel;
        }
    }
}
