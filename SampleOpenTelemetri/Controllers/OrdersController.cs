#nullable disable
using Microsoft.AspNetCore.Mvc;
using Producer.RabbitMQ;
using SampleOpenTelemetri.Model;

namespace Producer.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class OrdersController : ControllerBase
    {
        private readonly IMessageProducer _messagePublisher;

        public OrdersController(IMessageProducer messagePublisher)
        {
            _messagePublisher = messagePublisher;
        }

        [HttpPost]
        public async Task<IActionResult> CreateOrder(OrderDto orderDto)
        {
            _messagePublisher.SendMessage(orderDto);

            return Ok();
        }
    }
}
