using TL;

namespace TelegramDownloader.Models;

public class ChatModel
{
    public int     Id               { get; set; }
    public string  TelegramName     { get; set; }
    public string? Username         { get; set; }
    public string? StorageName      { get; set; }
    public string  Type             { get; set; }
    public long    TelegramId       { get; set; }
    public long    AccessHash       { get; set; }
    public int     LastDownloadedId { get; set; }
    public int     Enabled          { get; set; }

    public bool AppRelevantAttributesEquals(ChatModel toCompare)
    {
        bool equals = true;

        if (LastDownloadedId != toCompare.LastDownloadedId)
            equals = false;
        else if (Enabled != toCompare.Enabled)
            equals = false;

        return equals;
    }
    
    public bool TgRelevantAttributesEquals(ChatModel toCompare)
    {
        bool equals = true;

        if (TelegramName != toCompare.TelegramName)
            equals = false;
        else if (Username != toCompare.Username)
            equals = false;
        else if (Type != toCompare.Type)
            equals = false;
        else if (AccessHash != toCompare.AccessHash)
            equals = false;

        return equals;
    }

    public static ChatModel TelegramChatToChat(ChatBase tgChat)
    {
        ChatModel chat;
        switch (tgChat)
        {
            case Chat smallGroup when (smallGroup.flags & Chat.Flags.deactivated) == 0:
                chat = new ChatModel()
                {
                    Id               = 0,
                    TelegramName     = smallGroup.title,
                    Username         = null,
                    StorageName      = null,
                    Type             = "small_group",
                    TelegramId       = smallGroup.id,
                    AccessHash       = 0,
                    LastDownloadedId = 0,
                    Enabled          = 0
                };
                return chat;
            case Channel channel when (channel.flags & Channel.Flags.broadcast) != 0:
                chat = new ChatModel()
                {
                    Id               = 0,
                    TelegramName     = channel.title,
                    Username         = channel.username,
                    StorageName      = null,
                    Type             = "channel",
                    TelegramId       = channel.id,
                    AccessHash       = channel.access_hash,
                    LastDownloadedId = 0,
                    Enabled          = 0
                };
                return chat;
            case Channel group:
                chat = new ChatModel()
                {
                    Id               = 0,
                    TelegramName     = group.title,
                    Username         = group.username,
                    StorageName      = null,
                    Type             = "group",
                    TelegramId       = group.id,
                    AccessHash       = group.access_hash,
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