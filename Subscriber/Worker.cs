using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;

namespace Subscriber;

public class Worker : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(1000, stoppingToken);
            try
            {
                var factory = new ConnectionFactory
                {
                    HostName = "localhost",
                    UserName = "test",
                    Password = "test",
                    Port = 5672
                };
                var connection = factory.CreateConnection();
                using var channel = connection.CreateModel();
                channel.QueueDeclare("orders",
                    durable: true,
                    exclusive: false,
                    autoDelete: false);

                var consumer = new EventingBasicConsumer(channel);
                consumer.Received += (model, eventArgs) =>
                {
                    CustomOpenTelemetry.ActivitySource.StartActivity("going to");
                    var body = eventArgs.Body.ToArray();
                    var message = Encoding.UTF8.GetString(body);

                    Console.WriteLine($"Message received: {message}");
                };

                channel.BasicConsume(queue: "orders", autoAck: true, consumer: consumer);
            }
            catch (Exception exception)
            {

            }
        }
    }
}