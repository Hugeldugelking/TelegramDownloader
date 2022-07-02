using TelegramDownloader.Models;
using TelegramDownloader.Utils;
using TL;
using WTelegram;
using HeyRed.Mime;

namespace TelegramDownloader.Services;

public class TelegramDownloaderWorker : BackgroundService
{
    private readonly ILogger<TelegramDownloaderWorker> _logger;
    private readonly Dictionary<string, string>        _settings;
    private          List<ChatModel>                   _chats;
    private          List<ChatModel>                   _enabledChats;
    private          Client?                           _client;
    private          User                              _user;
    private          Timer?                            _refreshTimer;

    public TelegramDownloaderWorker(ILogger<TelegramDownloaderWorker> logger)
    {
        _logger       = logger;
        _client       = null;
        _user         = new User();
        _chats        = new List<ChatModel>();
        _enabledChats = new List<ChatModel>();
        _settings     = Db.LoadSettings();
    }

    public void UpdateSettings(Dictionary<string, string> settings)
    {
        _logger.LogInformation("Updating Settings");
        foreach (KeyValuePair<string, string> setting in settings)
        {
            if (_settings.ContainsKey(setting.Key))
                _settings[setting.Key] = setting.Value;
        }

        Db.UpdateSettings(_settings);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting TelegramAutoDownloader...");
        _client = await Task.Run(() => new Client(TgConfig), stoppingToken);
        _user   = await _client.LoginUserIfNeeded();

        _logger.LogInformation("Logged in as {UserFirstName} (id {UserId})",
                               _user.username ?? _user.first_name + " " + _user.last_name, _user.id);

        if (_settings["use_live_update"] == "true")
        {
            await UpdateAllChats();
            await ProcessMessages();
            _client.Update += AutoUpdate;
        }
        else
        {
            double interval = double.Parse(_settings["update_interval"]);
            _refreshTimer = new Timer(TimedUpdate, null, TimeSpan.Zero,
                                      TimeSpan.FromMinutes(interval));
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            if (_client.Disconnected)
            {
                _logger.LogInformation("Disconnected... Performing reconnect");
                await _client.ConnectAsync();
                await UpdateAllChats();
                await ProcessMessages();
            }

            await Task.Delay(1000, stoppingToken);
        }

        _refreshTimer?.Change(Timeout.Infinite, 0);
    }

    private void TimedUpdate(object? state)
    {
        if (_client is { Disconnected: true })
            return;
        _logger.LogInformation("Performing Timed Update");
        UpdateAllChats().GetAwaiter().GetResult();
        ProcessMessages().GetAwaiter().GetResult();
    }

    private void AutoUpdate(IObject arg)
    {
        if (arg is not Updates { updates: var updates } upd) return;

        foreach (var update in updates)
        {
            switch (update)
            {
                case UpdateNewMessage unm:
                    ProcessMessage(unm.message).GetAwaiter().GetResult();
                    break;
                case UpdateChannel uc:
                    UpdateAllChats().GetAwaiter().GetResult();
                    break;
                case UpdateChat uc:
                    UpdateAllChats().GetAwaiter().GetResult();
                    break;
            }
        }
    }

    private async Task UpdateAllChats()
    {
        _logger.LogInformation("Updating all chats...");
        var             chats          = await _client.Messages_GetAllChats();
        List<ChatModel> chatModelChats = ChatModel.TelegramChatsToChats(chats);
        chatModelChats.Add(new ChatModel
        {
            Id               = 0,
            TelegramName     = "Saved Messages",
            Username         = null,
            StorageName      = null,
            Type             = "self",
            TelegramId       = _user.id,
            AccessHash       = 0,
            LastDownloadedId = 0,
            Enabled          = 0
        });
        Db.UpdateChats(chatModelChats, true);

        _enabledChats = Db.LoadChats(1);
    }
    
    private async Task ProcessMessages()
    {
        _logger.LogInformation("Processing all messages since last downloaded");

        foreach (ChatModel chat in _enabledChats)
        {
            InputPeer peer;
            switch (chat.Type)
            {
                case "channel" or "group":
                    peer = new InputPeerChannel(chat.TelegramId, chat.AccessHash);
                    break;
                case "small_group":
                    peer = new InputPeerChat(chat.TelegramId);
                    break;
                case "self":
                    peer = new InputPeerSelf();
                    break;
                default:
                    continue;
            }

            bool allMessagesParsed = false;
            int  offset            = 0;
            int  lastDownloadedId  = chat.LastDownloadedId;
            while (!allMessagesParsed)
            {
                var history =
                    await _client.Messages_GetHistory(peer, default, default, offset, 25, default,
                                                      chat.LastDownloadedId);
                if (history.Messages.Length == 0)
                    break;

                foreach (MessageBase messageBase in history.Messages)
                {
                    if (messageBase.ID > lastDownloadedId)
                        lastDownloadedId = messageBase.ID;

                    switch (messageBase)
                    {
                        case Message message:
                            if (message.media is MessageMediaPhoto { photo: Photo photo })
                            {
                                await DownloadPhoto(photo, chat);
                            }
                            else if (message.media is MessageMediaDocument { document: Document document })
                            {
                                await DownloadFile(document, chat);
                            }

                            break;
                        case MessageService messageService:
                            if (messageService.action is MessageActionChannelCreate or MessageActionChatCreate)
                                allMessagesParsed = true;
                            break;
                    }
                }

                offset += history.Messages.Length;
            }

            chat.LastDownloadedId = lastDownloadedId;
        }

        Db.UpdateChats(_enabledChats);
    }

    private async Task ProcessMessage(MessageBase messageBase)
    {
        long            chatId = messageBase.Peer.ID;
        List<ChatModel> chats  = Db.LoadChats(1, chatId);
        if (!chats.Any())
            return;
        ChatModel chat = chats.First();

        switch (messageBase)
        {
            case Message message:
                if (message.media is MessageMediaPhoto { photo: Photo photo })
                {
                    await DownloadPhoto(photo, chat);
                }
                else if (message.media is MessageMediaDocument { document: Document document })
                {
                    await DownloadFile(document, chat);
                }
                break;
        }

        if (messageBase.ID > chat.LastDownloadedId)
        {
            chat.LastDownloadedId = messageBase.ID;
            Db.UpdateChats(new List<ChatModel>() {chat});
        }
    }

    private static string GetStorageLocation(ChatModel chat)
    {
        string location = "./Build/TelegramDownloads/";
        location += (String.IsNullOrEmpty(chat.StorageName)) ? chat.TelegramName : chat.StorageName;
        location += "/";
        Directory.CreateDirectory(location);

        return location;
    }

    private async Task DownloadPhoto(Photo photo, ChatModel chat)
    {
        if (_client == null)
            return;
        string location = GetStorageLocation(chat);
        string filename = photo.date.ToString("yyyy-MM-dd") +
                          " "                               + photo.id + ".jpg";

        string fullFilename  = location    + filename;
        string cacheFilename = location    + "cache/" + filename;
        Directory.CreateDirectory(location + "cache/");
        
        try
        {
            if (!File.Exists(fullFilename))
            {
                _logger.LogInformation("Downloading Photo {Filename}", fullFilename);
                await using FileStream fs   = File.Create(cacheFilename);
                var                    type = await _client.DownloadFileAsync(photo, fs);
                await fs.DisposeAsync();
                if (type is not Storage_FileType.unknown and not Storage_FileType.partial)
                    File.Move(cacheFilename, $"{location}cache/{filename}.{type}",
                              true); // rename extension
                File.Move($"{location}cache/{filename}.{type}", fullFilename, true);
            }
        }
        catch (Exception exception)
        {
            _logger.LogError("{ExceptionMessage}", exception.Message);
        }
    }

    private async Task DownloadFile(Document document, ChatModel chat)
    {
        if (_client == null)
            return;
        string location = GetStorageLocation(chat);
        string filename;
        if (String.IsNullOrEmpty(document.Filename))
        {
            filename = document.date.ToString("yyyy-MM-dd") + " ";
            filename += document.Filename ?? document.id + "." +
                        MimeTypesMap.GetExtension(document.mime_type);
        }
        else
        {
            filename = document.Filename;
        }

        string fullFilename  = location    + filename;
        string cacheFilename = location    + "cache/" + filename;
        Directory.CreateDirectory(location + "cache/");
        
        try
        {
            if (!File.Exists(fullFilename))
            {
                _logger.LogInformation("Downloading File {Filename}", fullFilename);
                await using var fs = File.Create(cacheFilename);
                await _client.DownloadFileAsync(document, fs);
                await fs.DisposeAsync();
                File.Move(cacheFilename, fullFilename, true);
            }
        }
        catch (Exception exception)
        {
            _logger.LogError("{ExceptionMessage}", exception.Message);
        }
    }


    private string TgConfig(string setting)
    {
        switch (setting)
        {
            case "api_id":            return GetLoginData("tg_api_id");
            case "api_hash":          return GetLoginData("tg_api_hash");
            case "phone_number":      return GetLoginData("tg_phone_number");
            case "verification_code": return GetLoginData("tg_verification_code");
            case "first_name":        return GetLoginData("tg_first_name"); // if sign-up is required
            case "last_name":         return GetLoginData("tg_last_name"); // if sign-up is required
            case "password":          return GetLoginData("tg_password"); // if user has enabled 2FA
            default:                  return null!; // let WTelegramClient decide the default config
        }
    }

    private string GetLoginData(string dataName)
    {
        string loginData = "";
        while (loginData == "")
        {
            string result = (_settings.ContainsKey(dataName)) ? _settings[dataName] : "";
            if (result != "")
            {
                loginData = result;
            }
            else
            {
                Thread.Sleep(500);
            }
        }

        return loginData;
    }
}