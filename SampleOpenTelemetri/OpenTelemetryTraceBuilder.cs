using Microsoft.Extensions.Options;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using SampleOpenTelemetri.Model;

public sealed class OpenTelemetryTraceBuilder : IDisposable
{
    private readonly OpenTelemetryConfiguration _openTelemetryConfiguration;
    private TracerProvider _tracerProvider;

    public OpenTelemetryTraceBuilder(OpenTelemetryConfiguration openTelemetryConfiguration)
    {
        _openTelemetryConfiguration = openTelemetryConfiguration;
    }
    public Tracer BuildTracer(string workderName)
    {

        var attributes = new List<KeyValuePair<string, object>>
        {
            new("deployment.environment", _openTelemetryConfiguration.EnvironmentName),
            new("host.name", Environment.MachineName)
        };

        Action<ResourceBuilder> configureResource = r => r.AddService(
            serviceName: _openTelemetryConfiguration.ServiceName,
            serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown",
            serviceInstanceId: Environment.MachineName).AddAttributes(attributes).AddEnvironmentVariableDetector();


        TracerProviderBuilder builder = Sdk.CreateTracerProviderBuilder()
                .ConfigureResource(configureResource)
                .AddSource(workderName)
                .AddAspNetCoreInstrumentation()
                .AddSqlClientInstrumentation()
                .AddHttpClientInstrumentation()
            ;

        _tracerProvider = AddExporter(builder).Build();

        return _tracerProvider.GetTracer(workderName);

    }
    public TracerProviderBuilder AddExporter(TracerProviderBuilder builder)
    {
        if (_openTelemetryConfiguration.Environment == "Production" || _openTelemetryConfiguration.Environment == "Stage")
        {
            return builder.AddOtlpExporter(otlpOptions =>
            {
                var serverUrl = _openTelemetryConfiguration.Otlp.ElasticApm.ServerUrl;
                var token = _openTelemetryConfiguration.Otlp.ElasticApm.SecretToken;

                otlpOptions.Endpoint = new Uri(serverUrl);
                otlpOptions.Headers = $"Authorization= ApiKey {token}";
            });
        }

        return builder.AddConsoleExporter();
    }

    public void Dispose()
    {
        _tracerProvider.Dispose();
    }
}