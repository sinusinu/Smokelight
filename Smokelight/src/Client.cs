using System.Net;
using System.Net.Sockets;

namespace Smokelight;

public class Client : IDisposable {
    // events
    public delegate void ConnectedEventHandler(object sender);
    public event ConnectedEventHandler? Connected;

    public delegate void DisconnectedEventHandler(object sender);
    public event DisconnectedEventHandler? Disconnected;
    
    public class PayloadReceivedEventArgs {
        public Payload[] Payloads { get; private set; }
        public PayloadReceivedEventArgs(Payload[] payloads) { Payloads = payloads; }
    }
    public delegate void PayloadReceivedEventHandler(object sender, PayloadReceivedEventArgs e);
    public event PayloadReceivedEventHandler? PayloadReceived;

    // variables
    private TcpClient client;
    private NetworkStream? stream;
    internal bool run;

    // ctor
    public Client() {
        client = new TcpClient();
    }

    // functions
    public async Task ConnectAsync(IPAddress address, int port) {
        await client.ConnectAsync(address, port);
        stream = client.GetStream();
        Connected?.Invoke(this);
        _ = ReceiveLoopAsync();
    }

    private async Task ReceiveLoopAsync() {
        run = true;
        
        using (var stream = client.GetStream()) {
            Payload[]? payloads = null;
            while (run) {
                payloads = null;

                if (!client.Connected) {
                    run = false;
                    break;
                }

                if (stream.DataAvailable) {
                    // return value could be used to do proper a error handling later
                    payloads = await Payload.TryUnpackFromStream(stream);
                    
                    if (payloads is not null) PayloadReceived?.Invoke(this, new(payloads));
                }

                await Task.Yield();
            }
        }

        client.Close();
        Disconnected?.Invoke(this);
    }

    public async Task SendPayloadsAsync(Payload[] payloads) {
        if (!client.Connected || stream is null || !stream.CanWrite) throw new InvalidOperationException("Client is not connected");
        
        byte[] packedPayloads = Payload.Pack(payloads);
        await stream.WriteAsync(packedPayloads);
    }

    public async Task DisconnectAsync() {
        if (client.Connected) client.Close();
    }

    public void Dispose() {
        client.Dispose();
    }
}
