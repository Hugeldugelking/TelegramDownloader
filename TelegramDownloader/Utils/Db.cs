using System.Data;
using System.Data.SQLite;
using Dapper;
using TelegramDownloader.Models;
using ConfigurationManager = System.Configuration.ConfigurationManager;

namespace TelegramDownloader.Utils;

public static class Db
{
    public static void Setup()
    {
        if (!File.Exists(GetDatabaseLocation()))
        {
            CreateDb();
        }
    }

    /// <summary>
    /// Updates all chats given.<br /><br />
    /// <c>UpdateChats</c>...<br/>
    /// - saves a chat if it isn't stored in the Database<br />
    /// - updates a chat if it is stored in the Database and there are changes<br />
    /// - deletes all matching records and saves the chat if there are multiple entries for a single chat<br />
    /// </summary>
    /// <param name="chats">List containing the chats to be updated. Use <c>ChatModel</c>s Methods to convert Telegram type chats to <c>ChatModel</c> class</param>
    public static void UpdateChats(List<ChatModel> chats)
    {
        using IDbConnection dbc = new SQLiteConnection(GetConnectionString());
        foreach (ChatModel chat in chats)
        {
            List<ChatModel> dbChats = LoadChats(default, chat.TelegramId);

            if (!dbChats.Any())
            {
                SaveChat(dbc, chat);
            }
            else
            {
                if ((dbChats.Count() == 1) && (!dbChats[0].RelevantAttributesEquals(chat)))
                {
                    chat.Id = dbChats[0].Id;
                    UpdateChat(dbc, chat);
                }
                else if(dbChats.Count() != 1)
                {
                    List<int> ids = new List<int>();
                    foreach (ChatModel dbChat in dbChats)
                    {
                        ids.Add(dbChat.Id);
                    }

                    DeleteChats(dbc, ids);
                    SaveChat(dbc, chat);
                }
            }
        }
    }

    /// <summary>
    /// Loads a list of Chats from the database
    /// </summary>
    /// <param name="enabled">Set to <b>1</b> to get all chats to be downloaded</param>
    /// <param name="telegramId">Set to a Telegram id to get all/the chat(s) matching to it</param>
    /// <returns><c>List</c> of <c>ChatModel</c>s</returns>
    public static List<ChatModel> LoadChats(int enabled = -1, long telegramId = -1)
    {
        string sql = "SELECT * FROM Chats";
        
        if ((enabled != -1) || (telegramId != -1))
            sql += " WHERE";
        if (enabled != -1)
            sql += (sql.IndexOf(" WHERE ", StringComparison.Ordinal) != -1) ? $" AND Enabled={enabled}" : $" Enabled={enabled}";
        if (telegramId != -1)
            sql += (sql.IndexOf(" WHERE ", StringComparison.Ordinal) != -1) ? $" AND TelegramId={telegramId}" : $" TelegramId={telegramId}";

        using IDbConnection dbc    = new SQLiteConnection(GetConnectionString());
        List<ChatModel>     result = dbc.Query<ChatModel>(sql).ToList();
        return result;
    }

    public static string GetSetting(string key)
    {
        string sql = $"SELECT Value FROM Settings WHERE key='{key}'";
        
        using IDbConnection dbc    = new SQLiteConnection(GetConnectionString());
        List<string>        result = dbc.Query<string>(sql).ToList();
        return result[0];
    }

    /// <summary>
    /// Update chat properties<br />
    /// <b>IMPORTANT:</b> Make sure that method parameter <c>chat</c> contains the <b>Database ID</b> of its entry!
    /// </summary>
    /// <param name="dbc"><c>IDbConnection</c> Database connection</param>
    /// <param name="chat"><c>ChatModel</c> Instance with modified attributes</param>
    /// <returns></returns>
    private static int UpdateChat(IDbConnection dbc, ChatModel chat)
    {
        string sql =
            "UPDATE Chats SET TelegramName=@TelegramName, Username=@Username, AccessHash=@AccessHash, LastDownloadedId=@LastDownloadedId " +
            "WHERE Id=@Id";

        return dbc.Execute(sql, chat);
    }

    /// <summary>
    /// Delete chats from the database
    /// </summary>
    /// <param name="dbc"><c>IDbConnection</c> Database connection</param>
    /// <param name="ids"><c>List<![CDATA[<]]>int<![CDATA[>]]></c> of Ids matching the entries to be deleted</param>
    /// <returns></returns>
    private static int DeleteChats(IDbConnection dbc, List<int> ids)
    {
        string sql = "DELETE FROM Chats WHERE Id IN @Ids";
        return dbc.Execute(sql, new { Ids = ids });
    }

    /// <summary>
    /// Saves a chat to the database
    /// </summary>
    /// <param name="dbc"><c>IDbConnection</c> Database connection</param>
    /// <param name="chat"><c>ChatModel</c> Chat to be saved</param>
    /// <returns></returns>
    private static int SaveChat(IDbConnection dbc, ChatModel chat)
    {
        string sql =
            "INSERT INTO Chats(TelegramName, Username, StorageName, TelegramId, AccessHash, LastDownloadedId, Enabled)" +
            "VALUES(@TelegramName, @Username, @StorageName, @TelegramId, @AccessHash, @LastDownloadedId, @Enabled)";
        return dbc.Execute(sql, chat);
    }

    private static int InsertSettings(IDbConnection dbc, Dictionary<string, string> settings)
    {
        string sql = "";
        foreach (KeyValuePair<string,string> setting in settings)
        {
            sql += $"INSERT INTO Settings(Key, Value) VALUES('{setting.Key}', '{setting.Value}');";
        }

        return dbc.Execute(sql);
    }

    /// <summary>
    /// Creates a database
    /// </summary>
    private static void CreateDb()
    {
        SQLiteConnection.CreateFile(GetDatabaseLocation());

        using (IDbConnection dbc = new SQLiteConnection(GetConnectionString()))
        {
            string sql = "CREATE TABLE 'Chats' ("               +
                         "'Id' INTEGER NOT NULL UNIQUE,"        +
                         "'TelegramName' TEXT NOT NULL,"        +
                         "'Username' TEXT,"                     +
                         "'StorageName' TEXT,"                  +
                         "'TelegramId' INTEGER NOT NULL,"       +
                         "'AccessHash' INTEGER NOT NULL,"       +
                         "'LastDownloadedId' INTEGER NOT NULL," +
                         "'Enabled' INTEGER NOT NULL,"          +
                         "PRIMARY KEY('Id' AUTOINCREMENT)"      +
                         ");";
            dbc.Execute(sql);

            sql = "CREATE TABLE 'Settings' (" +
                  "'Id' INTEGER NOT NULL UNIQUE," +
                  "'Key' TEXT,"            +
                  "'Value' TEXT,"+
                  "PRIMARY KEY('Id' AUTOINCREMENT )"+
                  ");";
            dbc.Execute(sql);

            Dictionary<string, string> settings = new Dictionary<string, string>
            {
                { "api_id", "" },
                { "api_hash", "" },
                { "phone_number", "" },
                { "verification_code", "" }
            };
            InsertSettings(dbc, settings);
        }
    }

    /// <summary>
    /// Gets the database connection string for SQLiteConnection()
    /// </summary>
    /// <param name="id">Connection string name</param>
    /// <returns><c>string</c> Connection string</returns>
    private static string GetConnectionString(string id = "DefaultDatabase")
    {
        return ConfigurationManager.ConnectionStrings[id].ConnectionString;
    }

    /// <summary>
    /// Extracts the database location from the connection string
    /// </summary>
    /// <param name="id">Connection string name</param>
    /// <returns><c>string</c> Database location</returns>
    private static string GetDatabaseLocation(string id = "DefaultDatabase")
    {
        string connectionString = GetConnectionString();
        int    start            = connectionString.IndexOf("=", StringComparison.Ordinal) + 1;
        int    stop             = connectionString.IndexOf(";", StringComparison.Ordinal);
        return connectionString.Substring(start, stop - start);
    }
}