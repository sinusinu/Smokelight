# Smokelight

A super simple, barebones C# networking library that can send and receive mixed text and binary payloads over TCP.

Was originally part of a personal project, now separated as a library for reusability.

> [!WARNING]
> Smokelight is still under the development. There are no proper error handling, testing, validation, etc.

> [!WARNING]
> Smokelight is made for personal use only. No package is provided. You don't wanna use it anyway.

# Sample Usage

## Server

```csharp
// note: Server is IDisposable, remember to dispose it
using var server = new Server(12345);

server.ClientConnected += (o, e) => {
    // connected client gets assigned of random GUID
    Guid clientId = e.Id;
};
server.ClientDisconnected += (o, e) => {
    Guid clientId = e.Id;
};
server.PayloadReceived += (o, e) => {
    Console.WriteLine($"{e.Payloads.Length} payload(s) received from {e.Id}");
    for (int i = 0; i < e.Payloads.Length; i++) {
        Console.WriteLine($"Payload {i}: {e.Payloads[i].Name} / {e.Payloads[i].Type}");
        if (e.Payloads[i].Type == Payload.PayloadType.Text) Console.WriteLine($"Payload {i} Content: {e.Payloads[i].TextData}");
        else Console.WriteLine($"Payload {i} Content: {e.Payloads[i].BinaryData.Length} byte(s) binary blob");
    }
};

// start listening (beware: not an async function but starts fire-and-forget async loop)
server.StartAsync();

// send a text payload to a client
await server.SendPayloadsAsync(someClientId, [ new("test", "hello") ]);

// send a binary payload to a client
await server.SendPayloadsAsync(someClientId, [ new("sig", [ 0x68, 0x65, 0x6C, 0x6C, 0x6F, ]) ]);

// send multiple payloads to all clients connected
await server.BroadcastPayloadsAsync([ new("alert", "something"), new("attachment", [ 0x68, 0x65, 0x6C, 0x6C, 0x6F, ]) ]);

// stop listening and close all active connections
await server.StopAsync();
```

## Client

```csharp
// note: Client is IDisposable too, remember to dispose it
using var client = new Client();

client.Connected += (o) => {};
client.Disconnected += (o) => {};
client.PayloadReceived += (o, e) => {
    Console.WriteLine($"{e.Payloads.Length} payload(s) received");
    for (int i = 0; i < e.Payloads.Length; i++) {
        Console.WriteLine($"Payload {i}: {e.Payloads[i].Name} / {e.Payloads[i].Type}");
        if (e.Payloads[i].Type == Payload.PayloadType.Text) Console.WriteLine($"Payload {i} Content: {e.Payloads[i].TextData}");
        else Console.WriteLine($"Payload {i} Content: {e.Payloads[i].BinaryData.Length} byte(s) binary blob");
    }
};

// connect to server
await client.ConnectAsync(IPAddress.Loopback, 12345);

// send a payload to server
await client.SendPayloadsAsync([ new("test", "hello") ]);

// disconnect from server
await client.DisconnectAsync();
```

# License

WTFPL