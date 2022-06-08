using System.Net.Sockets;
using Grpc.Core;
using Grpc.Net.Client;
using grpc_chat;

if (args.Length < 2)
{
    Console.WriteLine("Syntax: grpc-chat-client https://localhost:7042 userName");
    return;
}
var url = args[0];
var name = args[1];
Console.Clear();
using var channel = GrpcChannel.ForAddress(url);
var client = new Chatroom.ChatroomClient(channel);
var duplex = client.Join();
var uid = Guid.NewGuid().ToString();
var spkMsg = new SpeakRequest
{
    Uid = uid,
    Name = name,
    Message = "/join"
};
await duplex.RequestStream.WriteAsync(spkMsg);
int x = 0, y = 2;
void Print(string msg, ConsoleColor color = ConsoleColor.White) {
    Console.ForegroundColor = color;
    int origX = Console.CursorLeft, origY = Console.CursorTop;
    Console.SetCursorPosition(x, y);
    Console.WriteLine(msg);
    x = Console.CursorLeft;
    y = Console.CursorTop;
    Console.SetCursorPosition(origX, origY);
    Console.ResetColor();
};
var rcvTask = Task.Run(async () =>
{
    try
    {
        await foreach (var resp in duplex.ResponseStream.ReadAllAsync(CancellationToken.None))
        {
            Print($"{resp.Time.ToDateTime().ToLocalTime():HH:mm:ss} [{resp.Speaker}] {resp.Message}", 
                resp.Speaker == "system" ? ConsoleColor.Yellow : ConsoleColor.Cyan);
        }
    }
    catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable)
    {
        Print($"Connection broken", ConsoleColor.Magenta);
        Console.Clear();
        Environment.Exit(254);
    }
    catch (RpcException ex) {
        Print($"Error {ex.InnerException?.Message}", ConsoleColor.Magenta);
        Console.Clear();
        Environment.Exit(255);
    }
});


while (true)
{
    Console.SetCursorPosition(0, 0);
    Console.WriteLine(new String(' ', Console.WindowWidth) + new string('-', Console.WindowWidth));
    Console.SetCursorPosition(0, 0);
    var msg = Console.ReadLine();
    try
    {
        spkMsg.Message = msg;
        await duplex.RequestStream.WriteAsync(spkMsg);
        if (msg == "/exit") break;
    }
    catch (RpcException)
    {
        break;
    }
}
try { await duplex.RequestStream.CompleteAsync(); } catch { }
rcvTask.Wait();
Console.Clear();
