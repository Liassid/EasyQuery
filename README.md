# EasyQuery

A wrapper for zabszk's [SCP-SL-Remote-Query-Admin](https://github.com/zabszk/SCP-SL-Remote-Query-Admin) client

## Quick Start

```csharp
using EasyQuery;

// Connect to server
var client = new EasyQueryClient("127.0.0.1", 7777, "query_admin_password");

// Send command and get response
var response = await client.SendCommand("/gban-kick 9");
Console.WriteLine($"Success: {response.IsSuccess}");
Console.WriteLine($"Response: {response.Content}");

// Clean up and disconnect
client.Dispose();
```

## Initialization

Call `EasyQuery.Prepare()` early in your application:

```csharp
static void Main(string[] args)
{
    EasyQuery.Prepare(); // Must be called first!

    RunMyApplication();
}

static void RunMyApplication()
{
    using var client = new EasyQueryClient("127.0.0.1", 7777, "password");
    // ...
}
```

## Constructor Options

```csharp
new EasyQueryClient(
    host: "127.0.0.1",                    // Server IP/hostname
    port: 7777,                           // Query port
    password: "query_admin_password",     // Query administrator password
    permissions: ulong.MaxValue,          // Permissions to request (default: all)
    kickPower: byte.MaxValue,             // Kick power to request (default: max)
    username: "Andimal",                  // Username for logs (optional)
    suppressCommandResponses: false,      // Don't wait and return empty command responses
    subscribeConsole: false,              // Get realtime console output 
    subscribeLogs: false                  // Get realtime server logs
);
```

## Realtime Monitoring

```csharp
var client = new EasyQueryClient("127.0.0.1", 7777, "query_admin_password", 
    subscribeConsole: true, subscribeLogs: true);

client.OnConsoleMessageReceived += message => {
    Console.WriteLine($"Server console/log output: {message}");
};
```

## Important Notes

- Commands must start with `/` for remote admin commands (e.g. `/players`, not `players`)
- When `suppressCommandResponses` is true, you can send non-RA commands without the `/` prefix
- Reconnection happens automatically but there's a 10-attempt limit