using Grpc.Core;
using grpc_chat_svc;
using grpc_chat;
using System.Collections.Concurrent;

namespace grpc_chat_svc.Services;

public class ChatroomService : Chatroom.ChatroomBase
{
    private readonly ILogger<ChatroomService> _logger;
    public ChatroomService(ILogger<ChatroomService> logger)
    {
        _logger = logger;
    }

    static ConcurrentDictionary<string, MsgChannel> channels =
        new ConcurrentDictionary<string, MsgChannel>();
    class MsgChannel
    {
        public string Name;
        public IServerStreamWriter<BroadcastMessage> Stream;
    }

    async Task Speak(string speaker, string message)
    {
        var msg = new BroadcastMessage
        {
            Speaker = speaker,
            Time = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.UtcNow),
            Message = message
        };
        var lostUsers = new List<string>();
        foreach (var kv in channels.ToArray())
        {
            try
            {
                await kv.Value.Stream.WriteAsync(msg);
            }
            catch
            {
                lostUsers.Add(kv.Value.Name);
                channels.TryRemove(kv.Key, out _);
            }
        }
        if (lostUsers.Any())
        {
            await Speak("system", String.Join(", ", lostUsers.ToArray()) + " disconnected");
        }
    }

    public override async Task Join(IAsyncStreamReader<SpeakRequest> requestStream, 
            IServerStreamWriter<BroadcastMessage> responseStream, ServerCallContext context)
    {
        var clientIp = context.GetHttpContext().Connection.RemoteIpAddress!.ToString();
        //_logger.LogInformation($"{clientIp}/{context.Peer} connnected");
        string uid = string.Empty;
        string name = string.Empty;
        try
        {
            await foreach (var speakReq in requestStream.ReadAllAsync(context.CancellationToken))
            {
                uid = speakReq.Uid;
                name = speakReq.Name;
                var speaker = $"{name}@{clientIp}";
                var newMember = false;
                if (!channels.TryGetValue(uid, out var channel))
                {
                    _logger.LogInformation($"{uid}/{speaker}");
                    channel = new MsgChannel { Name = name, Stream = responseStream };
                    if (!channels.TryAdd(uid, channel))
                        throw new ApplicationException("Failed to join the chatroom");
                    newMember = true;
                }
                channel.Name = name;
                if (speakReq.Message == "/exit")
                    break;
                else if (newMember)
                    await Speak("system", $"{name} joined");
                else
                    await Speak(speaker, speakReq.Message);
            }
        }
        catch (Exception ex)
        {
            await Speak("system", $"{name} {ex.Message}");
        }
        //_logger.LogInformation($"{context.Peer} disconnected");
        await Speak($"system", $"{name} left");
        if (!string.IsNullOrEmpty(uid)) channels.TryRemove(uid, out _);
    }
}
