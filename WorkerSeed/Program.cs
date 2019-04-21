using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.ApplicationInsights;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace WorkerSeed
{
    class Program
    {
        private const string _prefix = "my_";
        private const string _appsettings = "appsettings.json";
        private const string _hostsettings = "hostsettings.json";
        public static async Task Main(string[] args)
        {
            ILogger logger = null;
            try
            {
                var host = BuildHost(args);
                logger = host.Services.GetRequiredService<ILogger<Program>>();
                logger?.LogInformation("Worker starting...");
                await host.RunAsync();
                logger?.LogInformation("Worker ended.");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                logger?.LogCritical(ex, "Worker terminated unexpectedly.");
            }
            finally
            {
                await Task.Delay(10000);
            }
        }

        public static IHost BuildHost(string[] args)
        {
            return new HostBuilder()
                .ConfigureHostConfiguration(configHost =>
                {
                    configHost.SetBasePath(Directory.GetCurrentDirectory());
                    configHost.AddJsonFile(_hostsettings, optional: true);
                    configHost.AddEnvironmentVariables(prefix: _prefix);
                })
                .ConfigureAppConfiguration((hostContext, configApp) =>
                {
                    configApp.AddJsonFile(_appsettings, optional: false);
                    configApp.AddJsonFile($"appsettings.{hostContext.HostingEnvironment.EnvironmentName}.json", optional: false);
                    configApp.AddCommandLine(args, _switchMappings);
                })
                .ConfigureServices((hostContext, services) =>
                {
                    services.Configure<HostOptions>(option =>
                    {
                        option.ShutdownTimeout = TimeSpan.FromSeconds(20);
                    });
                    services.AddOptions();
                    services.AddLogging();
                    services.Configure<AppConfig>(hostContext.Configuration.GetSection("AppConfig"));
                    services.AddHostedService<MyService>();
                })
                .ConfigureLogging((hostContext, configLogging) =>
                {
                    configLogging.AddConfiguration(hostContext.Configuration.GetSection("Logging"));
                    configLogging.AddConsole((options) =>
                    {
                        options.IncludeScopes = Convert.ToBoolean(hostContext.Configuration["Logging:IncludeScopes"]);
                    });
                    configLogging.AddApplicationInsights(hostContext.Configuration["Logging:ApplicationInsights:Instrumentationkey"]);

                    // Optional: Apply filters to configure LogLevel Trace or above is sent to
                    // ApplicationInsights for all categories.
                    configLogging.AddFilter<ApplicationInsightsLoggerProvider>("", LogLevel.Trace);

                    // Additional filtering For category starting in "Microsoft",
                    // only Warning or above will be sent to Application Insights.
                    configLogging.AddFilter<ApplicationInsightsLoggerProvider>("Microsoft", LogLevel.Warning);

                })
                .UseConsoleLifetime()
             
                .Build();
        }

        private static readonly Dictionary<string, string> _switchMappings = new Dictionary<string, string>
        {
            { "-debug",     "AppConfig:IsDebug" }
        };
    }
}
