using System.Globalization;
using TL;
using WTelegram;

namespace TelegramDownloader.Services;

public class TelegramAutoDownloader : BackgroundService
{
    private readonly ILogger<TelegramAutoDownloader> _logger;
    private          Dictionary<string, string>      _settings;
    private          Client?                         _client;
    private          User?                           _user;

    public TelegramAutoDownloader(ILogger<TelegramAutoDownloader> logger)
    {
        _logger = logger;
        _client = null;
        _user   = null;
        _settings = new Dictionary<string, string>()
        {
            { "api_id", "" },
            { "api_hash", "" },
            { "phone_number", "" },
            { "verification_code", "" }
        };
    }

    public void UpdateSettings(Dictionary<string, string> settings)
    {
        _logger.LogInformation("Updating Settings");
        foreach (KeyValuePair<string,string> setting in settings)
        {
            if (_settings.ContainsKey(setting.Key))
                _settings[setting.Key] = setting.Value;
            else
                _settings.Add(setting.Key, setting.Value);
        }
    }

    /*public override async Task StartAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting TelegramAutoDownloader...");
        _logger.LogInformation("Performing Telegram Login. Please fill out login data in Database.");
        
        _client = new Client(TgConfig);
        _user   = await _client.LoginUserIfNeeded();
        
        _logger.LogInformation($"Logged in as {_user.username ?? _user.first_name + " " + _user.last_name} (id {_user.id})");
    }*/

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting TelegramAutoDownloader...");
        _client = await Task.Run(() => new Client(TgConfig));

        _logger.LogInformation("Performing Telegram Login. Please fill out login data in Database.");
        _user = await _client.LoginUserIfNeeded();

        //_logger.LogInformation($"Logged in as {_user.username ?? _user.first_name + " " + _user.last_name} (id {_user.id})");

        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("TelegramAutoDownloader running! {0}",
                                   DateTime.Now.ToString(CultureInfo.CurrentCulture));
            await Task.Delay(1000, stoppingToken);
        }

        return;
    }

    private string TgConfig(string setting)
    {
        switch (setting)
        {
            case "api_id":            return GetLoginData("api_id");
            case "api_hash":          return GetLoginData("api_hash");
            case "phone_number":      return GetLoginData("phone_number");
            case "verification_code": return GetLoginData("verification_code");
            case "first_name":        return "John";    // if sign-up is required
            case "last_name":         return "Doe";     // if sign-up is required
            case "password":          return "secret!"; // if user has enabled 2FA
            default:                  return null;      // let WTelegramClient decide the default config
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