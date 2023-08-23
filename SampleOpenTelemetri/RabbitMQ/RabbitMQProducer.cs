#nullable disable
using System.Text;
using Newtonsoft.Json;
using Producer.RabbitMQ;
using RabbitMQ.Client;

namespace SampleOpenTelemetri.RabbitMQ
{
    public class RabbitMQProducer : IMessageProducer
    {
        public void SendMessage<T>(T message)
        {
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

                var json = JsonConvert.SerializeObject(message);
                var body = Encoding.UTF8.GetBytes(json);

                channel.BasicPublish(exchange: "", routingKey: "orders", body: body);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }
    }
}
