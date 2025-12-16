using System.Net;

namespace Smokelight.Test;

public class UnitTest {
    [Fact]
    public async Task BasicEchoTest() {
        bool echoed = false;
        Payload echoPayload = new("echo", "hello world");
        CancellationTokenSource cts = new();

        Server server = new Server(12345);
        server.PayloadReceived += async (o, e) => {
            if (e.Payloads.Length == 1) {
                if (e.Payloads[0].Type == Payload.PayloadType.Text) {
                    await server.SendPayloadsAsync(e.Id, [ new(e.Payloads[0].Name, e.Payloads[0].TextData!) ]);
                }
            }
        };
        server.StartAsync();

        Client client = new Client();
        client.Connected += async (o) => {
            await client.SendPayloadsAsync([ echoPayload ]);
        };
        client.PayloadReceived += (o, e) => {
            if (e.Payloads.Length == 1 && echoPayload == e.Payloads[0]) {
                echoed = true;
                cts.Cancel();
            }
        };
        await client.ConnectAsync(IPAddress.Loopback, 12345);

        try { await Task.Delay(1000, cts.Token); } catch (TaskCanceledException) when (cts.IsCancellationRequested) {}

        Assert.True(echoed);
    }
}
