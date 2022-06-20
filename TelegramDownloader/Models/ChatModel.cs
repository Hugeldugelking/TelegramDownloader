using TL;

namespace TelegramDownloader.Models;

public class ChatModel
{
    public int     Id               { get; set; }
    public string  TelegramName     { get; set; }
    public string? Username         { get; set; }
    public string? StorageName      { get; set; }
    public long    TelegramId       { get; set; }
    public long    AccessHash       { get; set; }
    public long    LastDownloadedId { get; set; }
    public int     Enabled          { get; set; }

    public bool RelevantAttributesEquals(ChatModel toCompare)
    {
        bool equals = true;

        if (TelegramName != toCompare.TelegramName)
            equals = false;
        if (Username != toCompare.Username)
            equals = false;
        if (AccessHash != toCompare.AccessHash)
            equals = false;
        if (LastDownloadedId != toCompare.LastDownloadedId) 
            equals = false;

        return equals;
    }

    public static ChatModel TelegramChatToChat(ChatBase tgChat)
    {
        switch (tgChat)
        {
            case Channel channel:
                ChatModel chat = new ChatModel
                {
                    Id               = 0,
                    TelegramName     = channel.title,
                    Username         = channel.username,
                    StorageName      = null,
                    TelegramId       = channel.id,
                    AccessHash       = channel.access_hash,
                    LastDownloadedId = 0,
                    Enabled          = 0
                };
                return chat;
        }

        return new ChatModel();
    }

    public static List<ChatModel> TelegramChatsToChats(Messages_Chats chats)
    {
        List<ChatModel> chatList = new List<ChatModel>();
        foreach (KeyValuePair<long, ChatBase> tgChat in chats.chats)
        {
            chatList.Add(TelegramChatToChat(tgChat.Value));
        }

        return chatList;
    }
}