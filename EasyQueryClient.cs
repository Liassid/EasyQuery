using System;
using System.Threading;
using System.Threading.Tasks;
using SCP_SL_Query_Client;
using SCP_SL_Query_Client.NetworkObjects;

namespace EasyQuery;

/// <summary>
///     QueryClient wrapper for communicating with SCP:SL query server.
/// </summary>
public sealed class EasyQueryClient : IDisposable
{
    private readonly QueryHandshake.ClientFlags flags;
    private readonly string host;
    private readonly byte kickPower;
    private readonly string password;
    private readonly ulong permissions;
    private readonly int port;
    private readonly SemaphoreSlim sendLock = new(1, 1);
    private readonly string username;
    private QueryClient client;
    private TaskCompletionSource<CommandResponse> pendingCommand = new();
    private int reconnectAttempts;

    /// <summary>
    ///     Initializes a new instance of EasyQueryClient and connects to specified server.
    /// </summary>
    /// <param name="host">IP address of the query server.</param>
    /// <param name="port">Port of the query server.</param>
    /// <param name="password">Query administrator password.</param>
    /// <param name="permissions">Player permissions cast to ulong requested by the client. Defaults to all permissions.</param>
    /// <param name="kickPower">Kick power requested by the client. Defaults to maximum kick power.</param>
    /// <param name="username">Username for logging.</param>
    /// <param name="suppressCommandResponses">
    ///     If true, command responses will be suppressed and sending non-RA commands will
    ///     be allowed.
    /// </param>
    /// <param name="subscribeConsole">If true, client will subscribe to server console messages.</param>
    /// <param name="subscribeLogs">If true, client will subscribe to server log messages.</param>
    public EasyQueryClient(string host, int port, string password, ulong permissions = ulong.MaxValue,
        byte kickPower = byte.MaxValue, string username = null, bool suppressCommandResponses = false,
        bool subscribeConsole = false, bool subscribeLogs = false)
    {
        this.host = host;
        this.port = port;
        this.password = password;
        this.permissions = permissions;
        this.kickPower = kickPower;
        this.username = username;

        if (suppressCommandResponses)
            flags |= QueryHandshake.ClientFlags.SuppressCommandResponses;

        if (subscribeConsole)
            flags |= QueryHandshake.ClientFlags.SubscribeServerConsole;

        if (subscribeLogs)
            flags |= QueryHandshake.ClientFlags.SubscribeServerLogs;

        Connect();
    }

    /// <summary>
    ///     Releases all resources used by EasyQueryClient and disconnects from the server.
    /// </summary>
    public void Dispose()
    {
        pendingCommand.TrySetCanceled();
        client.Dispose();
    }

    /// <summary>
    ///     Occurs when console or server log message is received from the server.
    ///     This event is only triggered if client was configured to subscribe to such messages.
    /// </summary>
    public event ConsoleMessageReceived OnConsoleMessageReceived;

    /// <summary>
    ///     Sends a command to the server and waits for a response.
    /// </summary>
    /// <param name="command">Command (query) to send. Must be prefixed with '/' for remote admin commands.</param>
    /// <param name="timeout">Maximum time to wait for a response, in seconds. Defaults to 10 seconds.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains the command response,
    ///     or a default CommandResponse if command responses are suppressed.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when the command is null, empty, or whitespace.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the command doesn't start with '/' (for remote admin commands).</exception>
    /// <exception cref="TimeoutException">Thrown when the command times out waiting for a response.</exception>
    /// <exception cref="Exception">Thrown when the server returns a command exception.</exception>
    public async Task<CommandResponse> SendCommand(string command, int timeout = 10)
    {
        if (string.IsNullOrWhiteSpace(command))
            throw new ArgumentNullException(nameof(command));

        if (flags.HasFlagFast(QueryHandshake.ClientFlags.SuppressCommandResponses))
        {
            client.Send(command, QueryMessage.QueryContentTypeToServer.Command);
            return default;
        }

        if (!command.StartsWith("/"))
            throw new InvalidOperationException("Remote admin commands must be prefixed with '/'");

        await sendLock.WaitAsync();

        try
        {
            pendingCommand.TrySetCanceled();

            pendingCommand =
                new TaskCompletionSource<CommandResponse>(TaskCreationOptions.RunContinuationsAsynchronously);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeout));
            cts.Token.Register(() => pendingCommand.TrySetException(new TimeoutException()));

            client.Send(command, QueryMessage.QueryContentTypeToServer.Command);

            return await pendingCommand.Task;
        }
        finally
        {
            sendLock.Release();
        }
    }

    private void Connect()
    {
        client?.Dispose();

        client = new QueryClient(host, port, password);

        client.OnConnectedToServer += OnConnectedToServer;
        client.OnMessageReceived += OnMessageReceived;
        client.OnDisconnectedFromServer += OnDisconnectedFromServer;

        client.Connect(flags: flags, permissions: permissions, kickPower: kickPower, username: username);
    }

    private void OnConnectedToServer()
    {
        reconnectAttempts = 0;
    }

    private void OnMessageReceived(QueryMessage message)
    {
        switch ((QueryMessage.QueryContentTypeToClient)message.QueryContentType)
        {
            case QueryMessage.QueryContentTypeToClient.ConsoleString:
                OnConsoleMessageReceived?.Invoke(message.ToString());
                break;
            case QueryMessage.QueryContentTypeToClient.RemoteAdminPlaintextResponse:
                pendingCommand.TrySetResult(new CommandResponse(message.ToString(), true));
                break;
            case QueryMessage.QueryContentTypeToClient.RemoteAdminUnsuccessfulPlaintextResponse:
                pendingCommand.TrySetResult(new CommandResponse(message.ToString(), false));
                break;
            case QueryMessage.QueryContentTypeToClient.CommandException:
                pendingCommand.TrySetException(new Exception(message.ToString()));
                break;
        }
    }

    private void OnDisconnectedFromServer(DisconnectionReason reason)
    {
        client.OnConnectedToServer -= OnConnectedToServer;
        client.OnMessageReceived -= OnMessageReceived;
        client.OnDisconnectedFromServer -= OnDisconnectedFromServer;

        if (reason is DisconnectionReason.DisconnectedByClient || reconnectAttempts++ > 10)
        {
            Dispose();
            return;
        }

        Connect();
    }
}

/// <summary>
///     Represents a delegate for handling console and server log messages received from the server.
/// </summary>
/// <param name="message">Console or server log message content received from the server.</param>
public delegate void ConsoleMessageReceived(string message);

/// <summary>
///     Represents the response received from executing a command on the server.
/// </summary>
/// <param name="content">Content of the response message.</param>
/// <param name="isSuccess">A value indicating whether command was executed successfully.</param>
public readonly struct CommandResponse(string content, bool isSuccess)
{
    /// <summary>
    ///     Gets the content of the response message.
    /// </summary>
    /// <value>
    ///     The response content.
    /// </value>
    public string Content { get; } = content;

    /// <summary>
    ///     Gets a value indicating whether command was executed successfully.
    /// </summary>
    /// <value>
    ///     <c>true</c> if command was successful; otherwise, <c>false</c>.
    /// </value>
    public bool IsSuccess { get; } = isSuccess;

    /// <summary>
    ///     Returns a string representation of command response.
    /// </summary>
    /// <returns>
    ///     A string in the format "Success: {Content}" or "Failed: {Content}" depending on the success status.
    /// </returns>
    public override string ToString()
    {
        return $"{(IsSuccess ? "Success" : "Failed")}: {Content}";
    }
}