using OpenTelemetry.Exporter;
using OpenTelemetry.Instrumentation.AspNetCore;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();




var serviceName = builder.Configuration.GetValue<string>("OpenTelemetry:ServiceName");
var attributes = new List<KeyValuePair<string, object>>
{
    new KeyValuePair<string, object>("deployment.environment", serviceName),
    new KeyValuePair<string, object>("host.name", Environment.MachineName)
};

Action<ResourceBuilder> configureResource = r => r.AddService(
    serviceName: serviceName,
    serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown",
    serviceInstanceId: Environment.MachineName).AddAttributes(attributes).AddEnvironmentVariableDetector();

builder.Services.AddOpenTelemetry()
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
        builder.Services.Configure<AspNetCoreInstrumentationOptions>(
            builder.Configuration.GetSection("OpenTelemetry:AspNetCoreInstrumentation"));

        var tracingExporter = builder.Configuration.GetValue<string>("OpenTelemetry:UseTracingExporter").ToLowerInvariant();
        switch (tracingExporter)
        {
            case "jaeger":
                traceBuilder.AddJaegerExporter();

                traceBuilder.ConfigureServices(services =>
                {
                    // Use IConfiguration binding for Jaeger exporter options.
                    services.Configure<JaegerExporterOptions>(builder.Configuration.GetSection("OpenTelemetry:Jaeger"));

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
                    services.Configure<ZipkinExporterOptions>(builder.Configuration.GetSection("OpenTelemetry:Zipkin"));
                });
                break;

            case "otlp":

                //here we are using elastic apm with otlp exporter
                traceBuilder.AddOtlpExporter(otlpOptions =>
                {
                    var serverUrl = builder.Configuration.GetValue<string>("OpenTelemetry:Otlp:ElasticApm:ServerUrl");
                    var token = builder.Configuration.GetValue<string>("OpenTelemetry:Otlp:ElasticApm:SecretToken");

                    otlpOptions.Endpoint = new Uri(serverUrl);
                    otlpOptions.Headers = $"Authorization= ApiKey {token}";
                });
                break;

            default:
                traceBuilder.AddConsoleExporter();
                break;
        }
    });

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

