using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace Smokelight;

public class Server : IDisposable {
    // events
    public class ClientConnectedEventArgs {
        public Guid Id { get; private set; }
        public ClientConnectedEventArgs(Guid id) { Id = id; }
    }
    public delegate void ClientConnectedEventHandler(object sender, ClientConnectedEventArgs e);
    public event ClientConnectedEventHandler? ClientConnected;

    public class ClientDisconnectedEventArgs {
        public Guid Id { get; private set; }
        public ClientDisconnectedEventArgs(Guid id) { Id = id; }
    }
    public delegate void ClientDisconnectedEventHandler(object sender, ClientDisconnectedEventArgs e);
    public event ClientDisconnectedEventHandler? ClientDisconnected;

    public class PayloadReceivedEventArgs {
        public Guid Id { get; private set; }
        public Payload[] Payloads { get; private set; }
        
        public PayloadReceivedEventArgs(Guid id, Payload[] payloads) {
            Id = id;
            Payloads = payloads;
        }
    }
    public delegate void PayloadReceivedEventHandler(object sender, PayloadReceivedEventArgs e);
    public event PayloadReceivedEventHandler? PayloadReceived;

    // variables
    private TcpListener listener;
    private bool listening = false;

    private CancellationTokenSource ctsSockRead = new();
    
    private ConcurrentDictionary<Guid, ServerClient> connectedClients = new();

    // ctor
    public Server(int port) : this(IPAddress.Any, port) {}
    public Server(IPAddress bindAddress, int port) {
        listener = new TcpListener(bindAddress, port);
    }

    // functions
    public void StartAsync() {
        listening = true;
        listener.Start();
        _ = InternalStartAsync();
    }

    private async Task InternalStartAsync() {
        while (listening) {
            TcpClient? rclient;
            try {
                rclient = await listener.AcceptTcpClientAsync();
            } catch {
                continue;
            }
            var client = new ServerClient(rclient);
            connectedClients.TryAdd(client.Id, client); // TODO: do error check here
            ClientConnected?.Invoke(this, new(client.Id));
            _ = ClientReceiveLoopAsync(client);
        }
    }

    private async Task ClientReceiveLoopAsync(ServerClient client) {
        client.run = true;
        var clientId = client.Id;

        Payload[]? payloads = null;
        while (client.run) {
            payloads = null;

            payloads = await Payload.TryUnpackFromStream(client.Stream, ctsSockRead.Token);
            if (payloads is not null) {
                PayloadReceived?.Invoke(this, new(clientId, payloads));
            } else {
                // TODO: handle socket error and data error separately - only close socket on socket error
                break;
            }

            await Task.Yield();
        }

        // TODO: wrt StopAsync - there should be a better approach...
        connectedClients.TryRemove(client.Id, out _);
        ClientDisconnected?.Invoke(this, new(clientId));
        client.Dispose();
    }

    public async Task SendPayloadAsync(Guid clientId, Payload payload) {
        await SendPayloadsAsync(clientId, [ payload ]);
    }

    public async Task SendPayloadsAsync(Guid clientId, Payload[] payloads) {
        if (!connectedClients.TryGetValue(clientId, out var targetClient)) {
            throw new InvalidOperationException($"Client with GUID {clientId} does not exist");
        }

        byte[] packedPayloads = Payload.Pack(payloads);
        await InternalSendAsync(targetClient, packedPayloads);
    }

    public async Task BroadcastPayloadAsync(Payload payload) {
        await BroadcastPayloadsAsync([ payload ]);
    }

    public async Task BroadcastPayloadsAsync(Payload[] payloads) {
        byte[] packedPayloads = Payload.Pack(payloads);
        List<Task> tasks = new();
        foreach (var client in connectedClients.Values) {
            tasks.Add(InternalSendAsync(client, packedPayloads));
        }
        await Task.WhenAll(tasks);
    }

    private async Task InternalSendAsync(ServerClient client, byte[] data) {
        await client.Stream.WriteAsync(data);
    }

    public async Task StopAsync() {
        listening = false;
        listener.Stop();

        ctsSockRead.Cancel();

        if (connectedClients.Count > 0) {
            List<Task> disconnectTasks = new();
            foreach (var client in connectedClients.Values) {
                client.run = false; 
                // TODO: wrt ClientReceiveLoopAsync - while this will dispose the clients sometimes later, there should be a better approach...
                await client.DisconnectAsync();
            }
        }
    }

    public void Dispose() {
        listener.Dispose();
    }

    // wow what a name
    internal class ServerClient : IDisposable {
        public Guid Id { get; private set; }
        internal bool run = false;
        
        // WOW what a name
        public TcpClient ClientTcpClient { get; private set; }
        internal NetworkStream Stream { get; private set; }

        internal ServerClient(TcpClient tcpClient) {
            Id = Guid.NewGuid();

            ClientTcpClient = tcpClient;
            Stream = ClientTcpClient.GetStream();
        }

        internal async Task DisconnectAsync() {
            // TcpClient.Dispose should call GetStream().Close and TcpClient.Close
            if (ClientTcpClient.Connected) ClientTcpClient.Close();
        }

        public void Dispose() {
            ClientTcpClient.Dispose();
        }
    }
}