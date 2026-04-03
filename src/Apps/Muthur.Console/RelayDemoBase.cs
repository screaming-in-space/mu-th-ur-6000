using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Muthur.Clients;
using Muthur.Contracts;
using System.Threading.Channels;

namespace Muthur.Console;

/// <summary>
/// Base class for relay-driven demos. Connects to the SignalR relay hub,
/// manages an event channel, and provides a structured lifecycle:
/// ConnectAsync → RunAsync → DisposeAsync.
///
/// Subclasses override <see cref="OnEventAsync"/> to handle specific event types
/// and <see cref="SendWorkAsync"/> to fire the initial work.
/// </summary>
public abstract class RelayDemoBase : IAsyncDisposable
{
    private HubConnection? _connection;
    private readonly Channel<RelayEvent> _events = Channel.CreateUnbounded<RelayEvent>();

    protected ILogger Logger { get; }
    protected MuthurApiClient Client { get; }
    protected string AgentId { get; private set; } = "";
    protected ChannelWriter<RelayEvent> EventWriter => _events.Writer;

    protected RelayDemoBase(MuthurApiClient client, ILogger logger)
    {
        Client = client;
        Logger = logger;
    }

    /// <summary>Creates an agent session and connects to the relay hub. Call before RunAsync.</summary>
    public async Task ConnectAsync(
        string systemPrompt,
        IHttpMessageHandlerFactory handlerFactory,
        CancellationToken cancellationToken = default)
    {
        var session = await Client.CreateSessionAsync(systemPrompt, cancellationToken);
        AgentId = session.AgentId;
        Logger.LogInformation("Session created: AgentId={AgentId}", AgentId);

        _connection = new HubConnectionBuilder()
            .WithUrl($"http://muthur-api/v1/relay?agentId={AgentId}", options =>
            {
                options.HttpMessageHandlerFactory = _ =>
                    handlerFactory.CreateHandler(StartupExtensions.RelayHttpClientName);
            })
            .WithAutomaticReconnect()
            .Build();

        _connection.On<RelayEvent>("RelayEvent", evt => _events.Writer.TryWrite(evt));
        _connection.Closed += ex => { _events.Writer.TryComplete(ex); return Task.CompletedTask; };

        await _connection.StartAsync(cancellationToken);
        Logger.LogInformation("Connected to relay hub.");
    }

    /// <summary>
    /// Runs the demo: fires work via <see cref="SendWorkAsync"/>, then iterates
    /// the event channel calling <see cref="OnEventAsync"/> for each event.
    /// </summary>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromMinutes(5));

        // Fire work — don't await the result, the relay tells us what happened.
        await SendWorkAsync(timeoutCts.Token);
        Logger.LogInformation("Work dispatched — listening for events...");

        // Event loop — iterate until OnEventAsync signals completion or timeout.
        await foreach (var evt in _events.Reader.ReadAllAsync(timeoutCts.Token))
        {
            Logger.LogInformation("[{EventType}] {Message}", evt.EventType, evt.Message);

            var done = await OnEventAsync(evt, cancellationToken);
            if (done)
            {
                return;
            }
        }
    }

    /// <summary>Send the initial work (prompts, file processing requests, etc.).</summary>
    protected abstract Task SendWorkAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Handle a relay event. Return true to exit the event loop (demo complete).
    /// </summary>
    protected abstract Task<bool> OnEventAsync(RelayEvent evt, CancellationToken cancellationToken);

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }

        GC.SuppressFinalize(this);
    }
}
