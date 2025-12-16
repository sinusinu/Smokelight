using System.Net;

namespace Smokelight.Test;

public class UnitTest {
    const int port = 12345;

    [Fact]
    public async Task BasicEchoTest() {
        bool echoed = false;
        Payload echoPayload = new("echo", "hello world");
        CancellationTokenSource cts = new();

        using Server server = new Server(port);
        server.PayloadReceived += async (o, e) => await server.SendPayloadsAsync(e.Id, e.Payloads);
        server.StartAsync();

        using Client client = new Client();
        client.PayloadReceived += (o, e) => {
            if (e.Payloads.Length == 1 && echoPayload == e.Payloads[0]) {
                echoed = true;
                cts.Cancel();
            }
        };
        client.Connected += async (o) => await client.SendPayloadAsync(echoPayload);
        await client.ConnectAsync(IPAddress.Loopback, port);

        try { await Task.Delay(1000, cts.Token); } catch (TaskCanceledException) when (cts.IsCancellationRequested) {}

        await server.StopAsync();

        Assert.True(echoed);
    }

    [Fact]
    public async Task ConcurrencyTest() {
        int sum = 0;
        int recvCount = 0;
        Lock sumLock = new();
        CancellationTokenSource cts = new();

        Random random = new();
        int[] randomNumbers = { random.Next() % 100, random.Next() % 100, random.Next() % 100 };

        Server server = new Server(port);
        server.PayloadReceived += (o, e) => {
            int receivedNumber = BitConverter.ToInt32(e.Payloads[0].BinaryData);
            lock (sumLock) {
                sum += receivedNumber;
                recvCount++;
                if (recvCount == 3) cts.Cancel();
            }
        };
        server.StartAsync();

        Client? clientOne = new Client();
        clientOne.Connected += async (o) => await clientOne.SendPayloadAsync(new("number", BitConverter.GetBytes(randomNumbers[0])));
        Client? clientTwo = new Client();
        clientTwo.Connected += async (o) => await clientTwo.SendPayloadAsync(new("number", BitConverter.GetBytes(randomNumbers[1])));
        Client? clientThree = new Client();
        clientThree.Connected += async (o) => await clientThree.SendPayloadAsync(new("number", BitConverter.GetBytes(randomNumbers[2])));
        
        Task[] connectTasks = {
            clientOne.ConnectAsync(IPAddress.Loopback, port),
            clientTwo.ConnectAsync(IPAddress.Loopback, port),
            clientThree.ConnectAsync(IPAddress.Loopback, port)
        };
        await Task.WhenAll(connectTasks);

        try { await Task.Delay(1000, cts.Token); } catch (TaskCanceledException) when (cts.IsCancellationRequested) {}
        
        await clientOne.DisconnectAsync();
        clientOne.Dispose();
        clientOne = null;
        await clientTwo.DisconnectAsync();
        clientTwo.Dispose();
        clientTwo = null;
        await clientThree.DisconnectAsync();
        clientThree.Dispose();
        clientThree = null;

        await server.StopAsync();

        Assert.True(recvCount == 3);
        Assert.True(sum == (randomNumbers[0] + randomNumbers[1] + randomNumbers[2]));
    }

    [Fact]
    public async Task ReentryTest() {
        bool[] echoed = { false, false, false };
        Payload echoPayload = new("echo", "hello world");
        CancellationTokenSource[] cts = { new(), new(), new() };

        Server server = new Server(port);
        server.PayloadReceived += async (o, e) => await server.SendPayloadsAsync(e.Id, e.Payloads);
        server.StartAsync();

        Client? clientOne = new Client();
        clientOne.PayloadReceived += (o, e) => {
            if (e.Payloads.Length == 1 && echoPayload == e.Payloads[0]) {
                echoed[0] = true;
                cts[0].Cancel();
            }
        };
        clientOne.Connected += async (o) => await clientOne.SendPayloadAsync(echoPayload);
        await clientOne.ConnectAsync(IPAddress.Loopback, port);
        try { await Task.Delay(1000, cts[0].Token); } catch (TaskCanceledException) when (cts[0].IsCancellationRequested) {}
        await clientOne.DisconnectAsync();
        clientOne.Dispose();
        clientOne = null;

        Client? clientTwo = new Client();
        clientTwo.PayloadReceived += (o, e) => {
            if (e.Payloads.Length == 1 && echoPayload == e.Payloads[0]) {
                echoed[1] = true;
                cts[1].Cancel();
            }
        };
        clientTwo.Connected += async (o) => await clientTwo.SendPayloadAsync(echoPayload);
        await clientTwo.ConnectAsync(IPAddress.Loopback, port);
        try { await Task.Delay(1000, cts[1].Token); } catch (TaskCanceledException) when (cts[1].IsCancellationRequested) {}
        await clientTwo.DisconnectAsync();
        clientTwo.Dispose();
        clientTwo = null;

        Client? clientThree = new Client();
        clientThree.PayloadReceived += (o, e) => {
            if (e.Payloads.Length == 1 && echoPayload == e.Payloads[0]) {
                echoed[2] = true;
                cts[2].Cancel();
            }
        };
        clientThree.Connected += async (o) => await clientThree.SendPayloadAsync(echoPayload);
        await clientThree.ConnectAsync(IPAddress.Loopback, port);
        try { await Task.Delay(1000, cts[2].Token); } catch (TaskCanceledException) when (cts[2].IsCancellationRequested) {}
        await clientThree.DisconnectAsync();
        clientThree.Dispose();
        clientThree = null;

        await server.StopAsync();

        Assert.True(echoed[0]);
        Assert.True(echoed[1]);
        Assert.True(echoed[2]);
    }
}
