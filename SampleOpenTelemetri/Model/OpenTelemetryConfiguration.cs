namespace SampleOpenTelemetri.Model;

public sealed class OpenTelemetryConfiguration
{
    public const string ConfigName = "OpenTelemetry";

    public string UseTracingExporter { get; set; }
    public string EnvironmentName { get; set; }
    public string ServiceName { get; set; }
    public string Environment { get; set; }
    public Aspnetcoreinstrumentation AspNetCoreInstrumentation { get; set; }
    public OTLPConfigs Otlp { get; set; }

    public class Aspnetcoreinstrumentation
    {
        public string RecordException { get; set; }
    }

    public class OTLPConfigs
    {
        public ElasticAPMConfigs ElasticApm { get; set; }
    }

    public class ElasticAPMConfigs
    {
        public string ServerUrl { get; set; }
        public string SecretToken { get; set; }
        public bool EnableOpenTelemetryBridge { get; set; }
    }
}