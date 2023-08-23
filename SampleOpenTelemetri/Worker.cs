using System.Diagnostics;
using System.Text;
using OpenTelemetry.Trace;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace SampleOpenTelemetri
{
    public class ConsumeRabbitMQHostedService : BackgroundService
    {
        private readonly ILogger _logger;
        private IConnection _connection;
        private IModel _channel;

        public ConsumeRabbitMQHostedService(ILoggerFactory loggerFactory)
        {
            this._logger = loggerFactory.CreateLogger<ConsumeRabbitMQHostedService>();
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
                using var source = new ActivitySource("Begin Rabbit Consuming");

                var tracer = TracerProvider.Default.GetTracer("Omid.Test");
                using (var parentSpan = tracer.StartActiveSpan("parent span"))
                {
                    parentSpan.SetAttribute("mystring", "value");
                    parentSpan.SetAttribute("myint", 100);
                    parentSpan.SetAttribute("mydouble", 101.089);
                    parentSpan.SetAttribute("mybool", true);
                    parentSpan.UpdateName("parent span new name");

                    var childSpan = tracer.StartSpan("child span");
                    childSpan.AddEvent("sample event").SetAttribute("ch", "value").SetAttribute("more", "attributes");
                    childSpan.SetStatus(Status.Ok);
                    childSpan.End();
                }
                using (var activity = source.StartActivity("Converting data from byte to string", ActivityKind.Client))
                {
                    // received message  
                    var content = System.Text.Encoding.UTF8.GetString(ea.Body.ToArray());
                    activity?.SetTag("content", content);
                    // handle the received message  
                    HandleMessage(content);
                }

                _channel.BasicAck(ea.DeliveryTag, false);
            };

            consumer.Shutdown += OnConsumerShutdown;
            consumer.Registered += OnConsumerRegistered;
            consumer.Unregistered += OnConsumerUnregistered;
            consumer.ConsumerCancelled += OnConsumerConsumerCancelled;

            _channel.BasicConsume("orders", false, consumer);
            return Task.CompletedTask;
        }

        private void HandleMessage(string content)
        {
            // we just print this message   
            _logger.LogInformation($"consumer received {content}");
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