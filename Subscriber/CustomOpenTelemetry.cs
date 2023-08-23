using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Exporter;
using OpenTelemetry.Instrumentation.AspNetCore;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Subscriber
{
    public static class CustomOpenTelemetry
    {
        private const string ServiceName = "Sima.Allocation.Agent";
        public static readonly ActivitySource ActivitySource = new ActivitySource(ServiceName);
        public static void AddCustomTracing(this IServiceCollection serviceCollection, IConfiguration configuration)
        {
            try
            {
                // In .NET CORE 3.1 This switch must be set before creating the GrpcChannel/HttpClient.
                AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

                Action<ResourceBuilder> configureResource = r =>
                    r.AddService(
                            serviceName: ServiceName,
                            serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown",
                            serviceInstanceId: Environment.MachineName);

                serviceCollection.AddOpenTelemetry()
                    .ConfigureResource(configureResource)
                    .WithTracing(traceBuilder =>
                    {
                        // Ensure the TracerProvider subscribes to any custom ActivitySources.
                        traceBuilder
                            .AddSource(configuration.GetValue<string>("OpenTelemetry:ServiceName"))
                            .AddHttpClientInstrumentation()
                            .AddSqlClientInstrumentation()
                            .AddAspNetCoreInstrumentation();

                        // Use IConfiguration binding for AspNetCore instrumentation options.
                        serviceCollection.Configure<AspNetCoreInstrumentationOptions>(
                            configuration.GetSection("OpenTelemetry:AspNetCoreInstrumentation"));


                        var tracingExporter = configuration.GetValue<string>("OpenTelemetry:UseTracingExporter").ToLowerInvariant();
                        switch (tracingExporter)
                        {
                            case "jaeger":
                                traceBuilder.AddJaegerExporter();

                                traceBuilder.ConfigureServices(services =>
                                {
                                    // Use IConfiguration binding for Jaeger exporter options.
                                    services.Configure<JaegerExporterOptions>(configuration.GetSection("OpenTelemetry:Jaeger"));

                                    // Customize the HttpClient that will be used when JaegerExporter is configured for HTTP transport.
                                    services.AddHttpClient("JaegerExporter",
                                        configureClient: (client) => client.DefaultRequestHeaders.Add("X-MyCustomHeader", "value"));
                                });
                                break;
                            default:
                                traceBuilder.AddConsoleExporter();
                                break;
                        }
                    });
            }
            catch (Exception e)
            {
                throw;
            }

        }
    }
}
