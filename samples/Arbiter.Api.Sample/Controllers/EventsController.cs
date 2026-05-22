using System.Text.Json;
using Arbiter.Api.Attributes;
using Arbiter.Api.Controllers;
using Arbiter.Api.Http;
using Arbiter.Api.Results;

namespace Arbiter.Api.Sample.Controllers;

[Route("api/events")]
public class EventsController : ControllerBase
{
    [HttpGet("stream")]
    public IActionResult StreamEvents()
    {
        return Sse(async writer => {
            for (var i = 0; i < 10; i++)
            {
                var eventData = new {
                    Id = i,
                    Message = $"Event {i}",
                    Timestamp = DateTime.UtcNow,
                };

                await writer.WriteAsync(new SseEvent {
                    Id = i.ToString(),
                    Event = "message",
                    Data = JsonSerializer.Serialize(eventData),
                });

                await Task.Delay(1000, HttpContext.CancellationToken);
            }
        });
    }

    [HttpGet("counter")]
    public IActionResult CounterStream()
    {
        return Sse(async writer => {
            var counter = 0;

            while (!HttpContext.CancellationToken.IsCancellationRequested)
            {
                await writer.WriteAsync(new SseEvent {
                    Data = JsonSerializer.Serialize(new {
                        Count = counter++,
                        Timestamp = DateTime.UtcNow,
                    }),
                });

                await Task.Delay(2000, HttpContext.CancellationToken);
            }
        });
    }
}
