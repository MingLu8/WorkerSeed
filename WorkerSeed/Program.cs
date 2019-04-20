using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

namespace WorkerSeed
{
    class Program
    {
        private const string _prefix = "my_";
        private const string _appsettings = "appsettings.json";
        private const string _hostsettings = "hostsettings.json";
        private static TelemetryClient _telemetryClient;
        public static async Task Main(string[] args)
        {
            _telemetryClient = new TelemetryClient
            {
                InstrumentationKey = "f2b1c173-c5ae-463e-83ad-1cfccc1557f7"
            };
            //var log = new LoggerConfiguration()
            //    .WriteTo
            //    .ApplicationInsights(_telemetryClient, TelemetryConverter.Events)
            //    .CreateLogger();
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .WriteTo
                .ApplicationInsights(_telemetryClient, TelemetryConverter.Traces)
                .CreateLogger();

            try
            {
                var host = BuildHost(args);
                await host.RunAsync();
                //_telemetryClient.Flush();

                // The AI Documentation mentions that calling .Flush() *can* be asynchronous and non-blocking so
                // depending on the underlying Channel to AI you might want to wait some time
                // specific to your application and its connectivity constraints for the flush to finish.
                
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Host terminated unexpectedly");
            }
            finally
            {
                Log.CloseAndFlush();
                _telemetryClient.Flush();
                await Task.Delay(1000);
            }
            //IServiceProvider serviceProvider = null;


            //ILogger<Program> logger = serviceProvider.GetRequiredService<ILogger<Program>>();

            //// Begin a new scope. This is optional.
            //using (logger.BeginScope(new Dictionary<string, object> { { "Method", nameof(Main) } }))
            //{
            //    logger.LogInformation("Logger is working"); // this will be captured by Application Insights.
            //}


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
                    configApp.AddJsonFile(
                        $"appsettings.{hostContext.HostingEnvironment.EnvironmentName}.json",
                        optional: false);
                    configApp.AddCommandLine(args, _switchMappings);
                })
                .ConfigureServices((hostContext, services) =>
                {
                    services.Configure<HostOptions>(option =>
                    {
                        option.ShutdownTimeout = System.TimeSpan.FromSeconds(20);
                    });
                    services.AddOptions();
                    services.Configure<AppConfig>(hostContext.Configuration.GetSection("AppConfig"));
                    services.AddLogging(builder =>
                    {
                        // Optional: Apply filters to configure LogLevel Trace or above is sent to
                        // Application Insights for all categories.
                        //builder.AddFilter<Microsoft.Extensions.Logging.ApplicationInsights.ApplicationInsightsLoggerProvider>("", LogLevel.Trace);
                        //var appInsightsKey = hostContext.Configuration["Logging:ApplicationInsights:Instrumentationkey"];
                        //builder.AddApplicationInsights(appInsightsKey);
                    });

                    //var channel = new InMemoryChannel();
                    //services.Configure<TelemetryConfiguration>((config) => { config.TelemetryChannel = channel; });
                    // services.Configure<Application>(hostContext.Configuration.GetSection("application"));
                    services.AddHostedService<MyService>();
                    //serviceProvider = services.BuildServiceProvider();


                    // Explicitly call Flush() followed by sleep is required in Console Apps.
                    // This is to ensure that even if application terminates, telemetry is sent to the back-end.
                    //channel.Flush();
                    //Thread.Sleep(1000);

                })
                //.ConfigureLogging((hostContext, configLogging) =>
                //{
                //    configLogging.AddConfiguration(hostContext.Configuration.GetSection("Logging"));
                //    configLogging.AddConsole();

                //})
                .UseConsoleLifetime()
                .UseSerilog(
                    //(hostingContext, loggerConfiguration) => loggerConfiguration
                    ////.ReadFrom.Configuration(hostingContext.Configuration)
                    //.Enrich.FromLogContext()
                    //.WriteTo.Console()
                    //.WriteTo
                    //.ApplicationInsights(_telemetryClient, TelemetryConverter.Events)
                )
                .Build();
        }

        private static readonly Dictionary<string, string> _switchMappings = new Dictionary<string, string>
        {
            { "-debug",     "AppConfig:IsDebug" }
        };
        //static void Main(string[] args)
        //{
        //    // Create DI container.
        //    IServiceCollection services = new ServiceCollection();

        //    // Channel is explicitly configured to do flush on it later.
        //    var channel = new InMemoryChannel();
        //    services.Configure<TelemetryConfiguration>(
        //        (config) =>
        //        {
        //            config.TelemetryChannel = channel;
        //        }
        //    );

        //    // Add the logging pipelines to use. We are using Application Insights only here.
        //    services.AddLogging(builder =>
        //    {
        //        // Optional: Apply filters to configure LogLevel Trace or above is sent to
        //        // Application Insights for all categories.
        //        builder.AddFilter<Microsoft.Extensions.Logging.ApplicationInsights.ApplicationInsightsLoggerProvider>
        //            ("", LogLevel.Trace);
        //        builder.AddApplicationInsights("--YourAIKeyHere--");
        //    });

        //    // Build ServiceProvider.
        //    IServiceProvider serviceProvider = services.BuildServiceProvider();

        //    ILogger<Program> logger = serviceProvider.GetRequiredService<ILogger<Program>>();

        //    // Begin a new scope. This is optional.
        //    using (logger.BeginScope(new Dictionary<string, object> { { "Method", nameof(Main) } }))
        //    {
        //        logger.LogInformation("Logger is working"); // this will be captured by Application Insights.
        //    }

        //    // Explicitly call Flush() followed by sleep is required in Console Apps.
        //    // This is to ensure that even if application terminates, telemetry is sent to the back-end.
        //    channel.Flush();
        //    Thread.Sleep(1000);
        //}
    }
}
