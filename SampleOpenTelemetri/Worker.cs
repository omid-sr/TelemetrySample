using Microsoft.Extensions.Options;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using SampleOpenTelemetri.Model;
using System.Text;

namespace SampleOpenTelemetri
{
    public class ConsumeRabbitMQHostedService : BackgroundService
    {
        private readonly OpenTelemetryConfiguration _openTelemetryConfiguration;
        private readonly ILogger _logger;
        private IConnection _connection;
        private IModel _channel;

        public ConsumeRabbitMQHostedService(ILoggerFactory loggerFactory, IOptionsMonitor<OpenTelemetryConfiguration> openTelemetryConfiguration)
        {
            _openTelemetryConfiguration = openTelemetryConfiguration.CurrentValue;
            _logger = loggerFactory.CreateLogger<ConsumeRabbitMQHostedService>();
            InitRabbitMQ();
        }

        private void InitRabbitMQ()
        {
            var factory = new ConnectionFactory
            {
                HostName = "localhost",
                UserName = "test",
                Password = "test",
                Port = 5672
            };

            // create connection  
            _connection = factory.CreateConnection();

            // create channel  
            _channel = _connection.CreateModel();

            _channel.QueueDeclare("orders",
                durable: true,
                exclusive: false,
                autoDelete: false);
            _channel.BasicQos(0, 1, false);

            _connection.ConnectionShutdown += RabbitMQ_ConnectionShutdown;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {

            stoppingToken.ThrowIfCancellationRequested();

            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += (ch, ea) =>
            {
                var attributes = new List<KeyValuePair<string, object>>
                {
                    new("deployment.environment", "Sima"),
                    new("host.name", Environment.MachineName)
                };

                Action<ResourceBuilder> configureResource = r => r.AddService(
                        serviceName: ServiceName,
                        serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown",
                        serviceInstanceId: Environment.MachineName)
                    .AddAttributes(attributes)
                    .AddEnvironmentVariableDetector();

                //using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                //    .ConfigureResource(configureResource)
                //    .AddSource("ConsumeRabbitMQHostedService")
                //    .AddAspNetCoreInstrumentation()
                //    .AddSqlClientInstrumentation()
                //    .AddHttpClientInstrumentation()
                //    .AddOtlpExporter(otlpOptions =>
                //    {
                //        var serverUrl = "http://st-elk-stapp:8200";
                //        var token = "aVdDcjA0a0J1YUJXenBrdjg3ejU6bDJ5Y2E4ZmJSY0NOUXRVVC1HNExzQQ==";

                //        otlpOptions.Endpoint = new Uri(serverUrl);
                //        otlpOptions.Headers = $"Authorization= ApiKey {token}";
                //    })
                //    .Build();

                //var tracer = tracerProvider.GetTracer("ConsumeRabbitMQHostedService");

                using var traceBuilder = new OpenTelemetryTraceBuilder(_openTelemetryConfiguration);

                var tracer = traceBuilder.BuildTracer("worker 1");

                using (var span = tracer.StartActiveSpan("ExecuteAsync-rabbit-Consume"))
                {

                    var content = Encoding.UTF8.GetString(ea.Body.ToArray());
                    HandleMessage(content);
                }


                // received message


                _channel.BasicAck(ea.DeliveryTag, false);
            };

            consumer.Shutdown += OnConsumerShutdown;
            consumer.Registered += OnConsumerRegistered;
            consumer.Unregistered += OnConsumerUnregistered;
            consumer.ConsumerCancelled += OnConsumerConsumerCancelled;

            _channel.BasicConsume("orders", false, consumer);
            return Task.CompletedTask;
        }

        public string ServiceName = "Omid_Test";

        private void HandleMessage(string content)
        {

            using var traceBuilder = new OpenTelemetryTraceBuilder(_openTelemetryConfiguration);

            var tracer = traceBuilder.BuildTracer("worker 2");
            using (var span = tracer.StartActiveSpan("HandleMessage"))
            {
                Thread.Sleep(TimeSpan.FromSeconds(1));
                _logger.LogInformation($"consumer received {content}");
            }
        }

        private void OnConsumerConsumerCancelled(object sender, ConsumerEventArgs e) { }
        private void OnConsumerUnregistered(object sender, ConsumerEventArgs e) { }
        private void OnConsumerRegistered(object sender, ConsumerEventArgs e) { }
        private void OnConsumerShutdown(object sender, ShutdownEventArgs e) { }
        private void RabbitMQ_ConnectionShutdown(object sender, ShutdownEventArgs e) { }

        public override void Dispose()
        {
            _channel.Close();
            _connection.Close();
            base.Dispose();
        }
    }
}