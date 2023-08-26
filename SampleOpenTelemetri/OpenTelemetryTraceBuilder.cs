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


        _tracerProvider = Sdk.CreateTracerProviderBuilder()
            .ConfigureResource(configureResource)
            .AddSource(workderName)
            .AddAspNetCoreInstrumentation()
            .AddSqlClientInstrumentation()
            .AddHttpClientInstrumentation()
            .AddOtlpExporter(otlpOptions =>
            {
                var serverUrl = _openTelemetryConfiguration.Otlp.ElasticApm.ServerUrl;
                var token = _openTelemetryConfiguration.Otlp.ElasticApm.SecretToken;

                otlpOptions.Endpoint = new Uri(serverUrl);
                otlpOptions.Headers = $"Authorization= ApiKey {token}";
            })
            .Build();

        return _tracerProvider.GetTracer(workderName);

    }


    public void Dispose()
    {

        _tracerProvider.Dispose();
    }
}