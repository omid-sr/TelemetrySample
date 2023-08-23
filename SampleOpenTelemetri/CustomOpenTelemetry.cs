using OpenTelemetry.Resources;
using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Instrumentation.AspNetCore;
using OpenTelemetry.Trace;
using Microsoft.Extensions.Configuration;

public static class CustomOpenTelemetry
{
    private const string ServiceName = "Omid.Test";

    public static void AddCustomTracing2(this IServiceCollection services, IConfiguration configuration)
    {

        var attributes = new List<KeyValuePair<string, object>>
        {
            new("deployment.environment", ServiceName),
            new("host.name", Environment.MachineName)
        };

        Action<ResourceBuilder> configureResource = r => r.AddService(
            serviceName: ServiceName,
            serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown",
            serviceInstanceId: Environment.MachineName).AddAttributes(attributes).AddEnvironmentVariableDetector();
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource("Samples.SampleClient", "Samples.SampleServer")
            .ConfigureResource(r => r.AddService("zipkin-test"))
            .AddOtlpExporter(otlpOptions =>
            {
                var serverUrl = "http://st-elk-stapp:8200";
                var token = "aVdDcjA0a0J1YUJXenBrdjg3ejU6bDJ5Y2E4ZmJSY0NOUXRVVC1HNExzQQ==";

                otlpOptions.Endpoint = new Uri(serverUrl);
                otlpOptions.Headers = $"Authorization= ApiKey {token}";


                // otlpOptions.Endpoint = new Uri(configuration.GetValue<string>("OpenTelemetry:Otlp:Endpoint"));

            })
            .Build();
    }
    public static void AddCustomTracing(this IServiceCollection services, IConfiguration configuration)
    {
        var attributes = new List<KeyValuePair<string, object>>
{
    new("deployment.environment", ServiceName),
    new("host.name", Environment.MachineName)
};

        Action<ResourceBuilder> configureResource = r => r.AddService(
            serviceName: ServiceName,
            serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown",
            serviceInstanceId: Environment.MachineName).AddAttributes(attributes).AddEnvironmentVariableDetector();

        services.AddOpenTelemetry()
            .ConfigureResource(configureResource)
            .WithTracing(traceBuilder =>
            {
                // Tracing
                //traceBuilder.AddAspNetCoreInstrumentation(o =>
                //{
                //    o.Filter = ctx => ctx.Request.Path.Value.StartsWith("api");
                //    o.RecordException = true;
                //});

                // Ensure the TracerProvider subscribes to any custom ActivitySources.
                traceBuilder
                    .AddHttpClientInstrumentation()
                    .AddAspNetCoreInstrumentation()
                    .AddSqlClientInstrumentation();

                // Use IConfiguration binding for AspNetCore instrumentation options.
                services.Configure<AspNetCoreInstrumentationOptions>(
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

                    case "zipkin":
                        traceBuilder.AddZipkinExporter();

                        traceBuilder.ConfigureServices(services =>
                        {
                            // Use IConfiguration binding for Zipkin exporter options.
                            services.Configure<ZipkinExporterOptions>(configuration.GetSection("OpenTelemetry:Zipkin"));
                        });
                        break;

                    case "otlp":

                        //here we are using elastic apm with otlp exporter
                        traceBuilder.AddOtlpExporter(otlpOptions =>
                        {
                            var serverUrl = "http://st-elk-stapp:8200";
                            var token = "aVdDcjA0a0J1YUJXenBrdjg3ejU6bDJ5Y2E4ZmJSY0NOUXRVVC1HNExzQQ==";

                            otlpOptions.Endpoint = new Uri(serverUrl);
                            otlpOptions.Headers = $"Authorization= ApiKey {token}";


                            // otlpOptions.Endpoint = new Uri(configuration.GetValue<string>("OpenTelemetry:Otlp:Endpoint"));

                        });
                        break;

                    default:
                        traceBuilder.AddConsoleExporter();
                        break;
                }
            });

    }
}