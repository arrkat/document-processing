using JobService.Events;

namespace JobService.Endpoints;

public static class ProcessorEventsEndpoint
{
    public const string Path = "/ws/processor-events";

    public static IEndpointRouteBuilder MapProcessorEventsEndpoint(this IEndpointRouteBuilder app)
    {
        app.Map(Path, async (HttpContext context, ProcessorEventsReceiver receiver) =>
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            using var socket = await context.WebSockets.AcceptWebSocketAsync();
            await receiver.HandleAsync(socket, context.RequestAborted);
        }).ExcludeFromDescription();

        return app;
    }
}
