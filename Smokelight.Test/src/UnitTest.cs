using System.Net;

namespace Smokelight.Test;

public class UnitTest {
    [Fact]
    public async Task BasicEchoTest() {
        bool echoed = false;
        Payload echoPayload = new("echo", "hello world");

        Server server = new Server(12345);
        server.PayloadReceived += async (o, e) => {
            if (e.Payloads.Length == 1) {
                if (e.Payloads[0].Type == Payload.PayloadType.Text) {
                    await server.SendPayloadsAsync(e.Id, [ new(e.Payloads[0].Name, e.Payloads[0].TextData!) ]);
                }
            }
        };
        server.StartAsync();
        await Task.Delay(500);

        Client client = new Client();
        client.Connected += async (o) => {
            await client.SendPayloadsAsync([ echoPayload ]);
        };
        client.PayloadReceived += (o, e) => {
            if (e.Payloads.Length == 1 && echoPayload == e.Payloads[0]) echoed = true;
        };
        await client.ConnectAsync(IPAddress.Loopback, 12345);

        await Task.Delay(1000);

        Assert.True(echoed);
    }
}
