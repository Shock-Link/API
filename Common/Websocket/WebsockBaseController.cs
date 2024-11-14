using System.Net.Mime;
using System.Net.WebSockets;
using System.Threading.Channels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using OneOf;
using OneOf.Types;
using OpenShock.Common.Errors;
using OpenShock.Common.Problems;
using OpenShock.Common.Utils;
using JsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;

namespace OpenShock.Common.Websocket;

/// <summary>
/// Base for json serialized websocket controller, you can override the SendMessageMethod to implement a different serializer
/// </summary>
/// <typeparam name="T"></typeparam>
public abstract class WebsocketBaseController<T> : OpenShockControllerBase, IAsyncDisposable, IDisposable, IWebsocketController<T> where T : class
{
    /// <inheritdoc />
    public abstract Guid Id { get; }

    public virtual int MaxChunkSize => 256;

    /// <summary>
    /// Logger
    /// </summary>
    protected readonly ILogger<WebsocketBaseController<T>> Logger;

    /// <summary>
    /// Close cancellation token to be called manually when termination of the current websocket is requested. Called on Dispose as well.
    /// </summary>
    protected readonly CancellationTokenSource Close = new();

    /// <summary>
    /// When passing a cancellation token, pass this Linked token, it is a Link from ApplicationStopping and Close.
    /// </summary>
    protected readonly CancellationTokenSource Linked;

    /// <summary>
    /// Channel for multithreading thread safety of the websocket, MessageLoop is the only reader for this channel
    /// </summary>
    private readonly Channel<T> _channel = Channel.CreateUnbounded<T>();

    protected WebSocket? WebSocket;

    /// <summary>
    /// DI
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="lifetime"></param>
    public WebsocketBaseController(ILogger<WebsocketBaseController<T>> logger, IHostApplicationLifetime lifetime)
    {
        Logger = logger;
        Linked = CancellationTokenSource.CreateLinkedTokenSource(Close.Token, lifetime.ApplicationStopping);
    }


    /// <inheritdoc />
    [NonAction]
    public ValueTask QueueMessage(T data) => _channel.Writer.WriteAsync(data);

    private bool _disposed;

    /// <inheritdoc />
    [NonAction]
    public void Dispose()
    {
        DisposeAsync().AsTask().Wait();
    }
    
    /// <inheritdoc />
    [NonAction]
    public virtual async ValueTask DisposeAsync()
    {
        if(_disposed) return;
        Logger.LogTrace("Disposing websocket controller..");
        _disposed = true;
        await DisposeControllerAsync();
        await UnregisterConnection();
        
        _channel.Writer.Complete();
        await Close.CancelAsync();
        WebSocket?.Dispose();
        Logger.LogTrace("Disposed websocket controller");
    }
    
    /// <summary>
    /// Dispose function for any inheriting controller
    /// </summary>
    /// <returns></returns>
    [NonAction]
    public virtual ValueTask DisposeControllerAsync() => ValueTask.CompletedTask;

    /// <summary>
    /// Initial get request to the websocket route - rewrite to websocket connection
    /// </summary>
    [ApiExplorerSettings(IgnoreApi = true)]
    [HttpGet]
    public async Task Get()
    {
        if (!HttpContext.WebSockets.IsWebSocketRequest)
        {
            var jsonOptions = HttpContext.RequestServices.GetRequiredService<IOptions<JsonOptions>>();
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            var response = WebsocketError.NonWebsocketRequest;
            response.AddContext(HttpContext);
            await HttpContext.Response.WriteAsJsonAsync(response, jsonOptions.Value.SerializerOptions, contentType: MediaTypeNames.Application.ProblemJson);
            await Close.CancelAsync();
            return;
        }

        var connectionPrecondition = await ConnectionPrecondition();
        if (connectionPrecondition.IsT1)
        {
            var jsonOptions = HttpContext.RequestServices.GetRequiredService<IOptions<JsonOptions>>();
            var response = connectionPrecondition.AsT1.Value;
            HttpContext.Response.StatusCode = response.Status ?? StatusCodes.Status400BadRequest;
            response.AddContext(HttpContext);
            await HttpContext.Response.WriteAsJsonAsync(response, jsonOptions.Value.SerializerOptions, contentType: MediaTypeNames.Application.ProblemJson);
            
            await Close.CancelAsync();
            return;
        }

        Logger.LogInformation("Opening websocket connection");
        WebSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();

        await RegisterConnection();

#pragma warning disable CS4014
        LucTask.Run(MessageLoop);
#pragma warning restore CS4014

        await SendInitialData();
        
        await Logic();
        
        await UnregisterConnection();

        await Close.CancelAsync();
    }

    #region Send Loop

    /// <summary>
    /// Message loop to send out messages in the channel
    /// </summary>
    [NonAction]
    private async Task MessageLoop()
    {
        await foreach (var msg in _channel.Reader.ReadAllAsync(Linked.Token))
        {
            try
            {
                await SendWebSocketMessage(msg, WebSocket!, Linked.Token);
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Error while sending message to client");
                throw;
            }
        }
    }

    /// <summary>
    /// Implementation method for sending the message out to the websocket, you might also wanna apply serialization here
    /// </summary>
    /// <param name="message"></param>
    /// <param name="websocket"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [NonAction]
    protected virtual Task SendWebSocketMessage(T message, WebSocket websocket, CancellationToken cancellationToken) =>
        JsonWebSocketUtils.SendFullMessage(message, websocket, cancellationToken, MaxChunkSize);


    #endregion

    /// <summary>
    /// Main receiver logic for the websocket
    /// </summary>
    /// <returns></returns>
    [NonAction]
    protected abstract Task Logic();
    
    /// <summary>
    /// Send initial data to the client
    /// </summary>
    /// <returns></returns>
    [NonAction]
    protected virtual Task SendInitialData() => Task.CompletedTask;

    /// <summary>
    /// Action when the websocket connection is created to register the connection to a websocket manager
    /// </summary>
    [NonAction]
    protected virtual Task RegisterConnection() => Task.CompletedTask;

    /// <summary>
    /// Action when the websocket connection is destroyed to unregister the connection to a websocket manager
    /// </summary>
    [NonAction]
    protected virtual Task UnregisterConnection() => Task.CompletedTask;

    /// <summary>
    /// Action when the websocket connection is destroyed to unregister the connection to a websocket manager
    /// </summary>
    [NonAction]
    protected virtual Task<OneOf<Success, Error<OpenShockProblem>>> ConnectionPrecondition() =>
        Task.FromResult(OneOf<Success, Error<OpenShockProblem>>.FromT0(new Success()));
    
    ~WebsocketBaseController()
    {
        DisposeAsync().AsTask().Wait();
    }
}