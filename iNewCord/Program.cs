using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Audio;

using NAudio;
using NAudio.Wave;
using NAudio.CoreAudioApi;

using Newtonsoft.Json;
using System.IO;
using Newtonsoft.Json.Linq;
using System.Threading;
using System.Globalization;
using System.Net;
using Discord.Commands;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data.SqlClient;
using QueryMaster.Steam;

namespace iNewCord
{
    /*public enum iNewsekaiServers
    {
        [Description("114.35.118.5:27016")]
        iNewsekai_1 = 1,
        [Description("114.35.118.5:27017")]
        iNewsekai_2 = 2,
        [Description("104.198.124.209:27018")]
        iNewsekai_jp = 3
    }
    /// <summary>
    /// 擴充列舉一方法，藉由列舉指定 Value 反射回傳 Description 字串。
    /// </summary>
    */

    public static class Extension
    {
        public static string GetDescription(this Enum value)
        {
            Type type = value.GetType();
            string name = Enum.GetName(type, value);
            if (name != null)
            {
                System.Reflection.FieldInfo field = type.GetField(name);
                if (field != null)
                {
                    DescriptionAttribute attr =
                           Attribute.GetCustomAttribute(field,
                             typeof(DescriptionAttribute)) as DescriptionAttribute;
                    if (attr != null)
                    {
                        return attr.Description;
                    }
                }
            }
            return null;
        }
    }

    class AdminPermissionCheck : Discord.Commands.Permissions.IPermissionChecker
    {
        public bool CanRun(Command command, User user, Channel channel, out string error)
        {
            //throw new NotImplementedException();
            error = "需身為遊戲管理員，或是 Discord 管理員才能變更。";
            if (user.Roles.Any(r => r.Name == "🎩Game Administrator") || user.ServerPermissions.Administrator)
            {
                return true;
            }
            else
            {
                channel.SendMessage(":warning:" + user.Mention + "：" + error);
                return false;
            }
        }
    }

    public static class DataReaderExtensions
    {

        public static string GetStringOrNull(this System.Data.IDataReader reader, int ordinal)
        {
            return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal).Trim();
        }

        public static string GetStringOrNull(this System.Data.IDataReader reader, string columnName)
        {
            return reader.GetStringOrNull(reader.GetOrdinal(columnName));
        }
    }

    class Program
    {
        static void Main(string[] args) => new Program().Start();

        string publicIp = null;
        public static BotConfig BotConfig = new BotConfig("config.json");
        public List<iNewsekaiServerJson> inewskServers = null;

        public static DiscordClient _client;
        public static IAudioClient _vClient;
        string saysound_location = BotConfig.saysound_directory;

        private JObject saysound_data;
        private Channel set_voiceChannel;
        //public List<string> usedSaycommands = new List<string>();
        public static List<DuplicatedSaycommand> duplicatedsaycommandList = new List<DuplicatedSaycommand>();

        // Create a Timer object that knows to call our TimerCallback method once every 1000 milliseconds.
        private static System.Threading.Timer t = new System.Threading.Timer(TimerPerSecondCallback, null, 0, 1000);
        private static System.Threading.Timer t_syncDB = new System.Threading.Timer(TimerSyncDBCallback, null, 0, 1200000);
        private static DateTime lastSyncDatetime = new DateTime();

        public static List<iNewsekaiRankData> inewskRankDatas = new List<iNewsekaiRankData>();
        public static Dictionary<string, iNewsekaiRankData> inewRankSteam32IdDict = new Dictionary<string, iNewsekaiRankData>();
        

        public static AdminPermissionCheck adminCheck = new AdminPermissionCheck();

        static ulong uptime_second = 0;
        static bool isReady = false;


        public static IPEndPoint CreateIPEndPoint(string endPoint) // Handles IPv4 and IPv6 notation.
        {
            IPAddress ip = null;
            int port = -1;
            //try
            //{
            string[] ep = endPoint.Split(':');
            if (ep.Length < 2) //沒加冒號
            {
                throw new FormatException("Invalid endpoint format");
            }
            //IPAddress ip;
            if (ep.Length > 2)
            {
                if (!IPAddress.TryParse(string.Join(":", ep, 0, ep.Length - 1), out ip))
                {
                    throw new FormatException("Invalid ip-adress");
                }
            }
            else
            {
                if (!IPAddress.TryParse(ep[0], out ip))
                {
                    throw new FormatException("Invalid ip-adress");
                }
            }
            //int port;
            if (!int.TryParse(ep[ep.Length - 1], NumberStyles.None, NumberFormatInfo.CurrentInfo, out port))
            {
                throw new FormatException("Invalid port");
            }
            //}
            /*catch (FormatException fe)
            {
                Console.WriteLine(DateTime.Now.ToLongTimeString() + "#" + fe.Message);
            }
            catch (Exception anyerr)
            {
                Console.WriteLine(DateTime.Now.ToLongTimeString() + "#" + anyerr.Message);
            }*/
            return new IPEndPoint(ip, port);
        }

        private static void TimerPerSecondCallback(Object o)
        {
            // Display the date/time when this method got called.
            //Console.WriteLine("In TimerCallback: " + DateTime.Now);
            if (isReady) uptime_second++;
            //Console.WriteLine("iNewCord Uptime: " + uptime_second);
            // Force a garbage collection to occur for this demo.
            //GC.Collect();

            for (int i = 0; i < duplicatedsaycommandList.Count; i++)
            {
                if (duplicatedsaycommandList[i].Cooldown_second > 0)
                    duplicatedsaycommandList[i].Cooldown_second--;
                else
                    duplicatedsaycommandList.Remove(duplicatedsaycommandList[i]);
            }
        }


        private static void TimerSyncDBCallback(Object o)
        {
            lastSyncDatetime = DateTime.Now;
            Console.WriteLine($"[{lastSyncDatetime.ToString()}] Syncing Players Data!");
            string myConnectionString = "???";
            MySql.Data.MySqlClient.MySqlConnection conn;
            conn = new MySql.Data.MySqlClient.MySqlConnection();
            conn.ConnectionString = myConnectionString;
            try
            {
                conn.Open();
            }
            catch (MySql.Data.MySqlClient.MySqlException ex)
            {
                switch (ex.Number)
                {
                    case 0:
                        Console.WriteLine("[MySQL] 無法連線到資料庫.");
                        break;
                    case 1045:
                        Console.WriteLine("[MySQL] 使用者帳號或密碼錯誤,請再試一次.");
                        break;
                }
            }

            try
            {
                string SQL =
                    //rdr[0]_All ranking data sorted by column["rank_point"];
                    "select rank_id, rank_steamId, rank_nick, rank_point, rank_played_time/3600, rank_kill, rank_death, rank_headshot, rank_sucside, rank_bosskill_1, rank_bosskill_2, rank_bosskill_3, rank_bosskill_4 " +
                    "rank_kill, rank_kill, rank_death, rank_headshot, rank_sucside, rank_bosskill_1, rank_bosskill_2, rank_bosskill_3, rank_bosskill_4 " +
                    "from new_schema.inewsk_rank where rank_point > 0 order by rank_point desc;" +
                    //rdr[1~6]_Various achievements data:
                    "SELECT rank_steamId, rank_kill FROM new_schema.inewsk_rank ORDER BY rank_kill DESC LIMIT 3;" +//Most kills
                    "SELECT rank_steamId, rank_played_time FROM new_schema.inewsk_rank ORDER BY rank_played_time DESC LIMIT 3;" +//Most ptime
                    "SELECT rank_steamId, rank_bosskill_1/rank_kill AS killGym FROM new_schema.inewsk_rank WHERE rank_point >= 888 ORDER BY killGym DESC LIMIT 10;" +//Most Gym kills
                    "SELECT rank_steamId, rank_bosskill_2/rank_kill AS killChoco FROM new_schema.inewsk_rank WHERE rank_point >= 888 ORDER BY killChoco DESC LIMIT 10;" +//Most Choco kills
                    "SELECT rank_steamId, rank_bosskill_3/rank_kill AS killHoppo FROM new_schema.inewsk_rank WHERE rank_point >= 888 ORDER BY killHoppo DESC LIMIT 10;" +//Most Hoppo kills
                    "SELECT rank_steamId, rank_bosskill_4/rank_kill AS killCyuuBoss FROM new_schema.inewsk_rank WHERE rank_point >= 888 ORDER BY killCyuuBoss DESC LIMIT 10;";//Most CBoss kills
                /*string SQL =
                    "set @rank=0;" +
                    "select @rank:=@rank+1 AS rank, rank_id, rank_steamId, rank_nick, rank_point, rank_played_time/3600, " +
                    "rank_kill, rank_death, rank_headshot, rank_sucside " +
                    "from new_schema.inewsk_rank where rank_point > 0 order by rank_point desc";*/

                MySql.Data.MySqlClient.MySqlCommand cmd = new MySql.Data.MySqlClient.MySqlCommand(SQL, conn);
                //cmd.Parameters.AddWithValue("@rank", 0);

                MySql.Data.MySqlClient.MySqlDataReader rdr = cmd.ExecuteReader();
                
                inewskRankDatas.Clear();
                inewRankSteam32IdDict.Clear();
                if (iNewsekaiAchievement.UsersAchievement != null)
                    iNewsekaiAchievement.UsersAchievement.Clear();

                /*int index = 0;
                while (rdr.Read())
                {
                    iNewsekaiRankData insData = new iNewsekaiRankData(
                        rdr.GetInt64(rdr.GetOrdinal("rank_id")),
                        rdr.GetString(rdr.GetOrdinal("rank_steamId")),
                        rdr.GetString(rdr.GetOrdinal("rank_nick")).Replace("`", "‵"),
                        rdr.GetInt32(rdr.GetOrdinal("rank_point")),
                        rdr.GetFloat(rdr.GetOrdinal("rank_played_time/3600")),
                        rdr.GetInt32(rdr.GetOrdinal("rank_kill")),
                        rdr.GetInt32(rdr.GetOrdinal("rank_death")),
                        rdr.GetInt32(rdr.GetOrdinal("rank_headshot")),
                        rdr.GetInt32(rdr.GetOrdinal("rank_sucside")),
                        //rdr.GetInt32(rdr.GetOrdinal("rank")),
                        index+1,
                        rdr.GetInt32(rdr.GetOrdinal("rank_bosskill_1")),
                        rdr.GetInt32(rdr.GetOrdinal("rank_bosskill_2")),
                        rdr.GetInt32(rdr.GetOrdinal("rank_bosskill_3")),
                        rdr.GetInt32(rdr.GetOrdinal("rank_bosskill_4"))
                    );

                    inewskRankDatas.Add(insData);
                    inewRankSteam32IdDict.Add(insData.Steam32Id, insData);

                    index++;
                }*/ //Replace these code with new one which will return an array of DataReader obj. 030

                int rdr_i = 0;
                do
                {
                    int index = 0;
                    while (rdr.Read())
                    {
                        if (rdr_i == 0) //handle the first returned DataReader
                        {
                            iNewsekaiRankData insData = new iNewsekaiRankData(
                                rdr.GetInt64(rdr.GetOrdinal("rank_id")),
                                rdr.GetString(rdr.GetOrdinal("rank_steamId")),
                                rdr.GetString(rdr.GetOrdinal("rank_nick")).Replace("`", "‵"),
                                rdr.GetInt32(rdr.GetOrdinal("rank_point")),
                                rdr.GetFloat(rdr.GetOrdinal("rank_played_time/3600")),
                                rdr.GetInt32(rdr.GetOrdinal("rank_kill")),
                                rdr.GetInt32(rdr.GetOrdinal("rank_death")),
                                rdr.GetInt32(rdr.GetOrdinal("rank_headshot")),
                                rdr.GetInt32(rdr.GetOrdinal("rank_sucside")),
                                //rdr.GetInt32(rdr.GetOrdinal("rank")),
                                index + 1,
                                rdr.GetInt32(rdr.GetOrdinal("rank_bosskill_1")),
                                rdr.GetInt32(rdr.GetOrdinal("rank_bosskill_2")),
                                rdr.GetInt32(rdr.GetOrdinal("rank_bosskill_3")),
                                rdr.GetInt32(rdr.GetOrdinal("rank_bosskill_4"))
                            );

                            inewskRankDatas.Add(insData);
                            inewRankSteam32IdDict.Add(insData.Steam32Id, insData);

                            index++;
                        }
                        else //for achievements
                        {
                            string user = rdr.GetString(rdr.GetOrdinal("rank_steamId"));
                            //If not exist then add a key-value UserAchievement.
                            var achievementFlag = (iNewsekaiAchievement.Achievement)Enum.ToObject(typeof(iNewsekaiAchievement.Achievement), rdr_i);

                            if (iNewsekaiAchievement.UsersAchievement == null)
                                iNewsekaiAchievement.UsersAchievement = new Dictionary<string, List<iNewsekaiAchievement.Achievement>>();

                            if (!iNewsekaiAchievement.UsersAchievement.Keys.Contains<string>(user))
                            {
                                iNewsekaiAchievement.UsersAchievement.Add(user, new List<iNewsekaiAchievement.Achievement>
                                {
                                    achievementFlag
                                });
                            }
                            else
                            {
                                iNewsekaiAchievement.UsersAchievement[user].Add(achievementFlag);
                            }
                        }
                    }
                    rdr_i++;
                } while (rdr.NextResult());

                rdr.Close();
                conn.Close();
            }
            catch (MySql.Data.MySqlClient.MySqlException ex)
            {
                Console.WriteLine("[!ERR!]" + DateTime.Now.ToShortTimeString() + ex.Message);
                TimerSyncDBCallback(null);
            }

            /* 列出各個玩家的成就
            foreach (var u in iNewsekaiAchievement.UsersAchievement)
            {
                Console.Write($"\n{u.Key}: ");
                foreach (var a in u.Value) Console.Write(a);
            }*/

            /*using (SqlConnection conn = new SqlConnection(cstr))
            {
                conn.Open();

                SqlCommand cmd = new SqlCommand(
                    "select rank_id, rank_steamId, rank_nick, rank_point, rank_played_time, " +
                    "rank_kill, rank_kill, rank_death, rank_headshot, rank_sucside " +
                    "from new_schema.inewsk_rank where rank_point > 0 order by rank_point desc", conn);
                SqlDataReader rdr = cmd.ExecuteReader();

                try
                {
                    while (rdr.Read())
                    {
                        //System.Console.WriteLine("{0}", rdr.GetString(1));
                        inewskRankDatas.Add(
                            new iNewsekaiRankData(
                                rdr.GetInt64(rdr.GetOrdinal("rank_id")),
                                rdr.GetString(rdr.GetOrdinal("rank_steamId")),
                                rdr.GetString(rdr.GetOrdinal("rank_nick")),
                                rdr.GetInt32(rdr.GetOrdinal("rank_point")),
                                rdr.GetInt32(rdr.GetOrdinal("rank_playedtime")),
                                rdr.GetInt32(rdr.GetOrdinal("rank_kill")),
                                rdr.GetInt32(rdr.GetOrdinal("rank_death")),
                                rdr.GetInt32(rdr.GetOrdinal("rank_headshot")),
                                rdr.GetInt32(rdr.GetOrdinal("rank_sucside"))
                            )
                        );
                    }
                }
                catch (Exception exception)
                {
                    Console.WriteLine("[!ERR!] " + exception.Message);
                }
                
                rdr.Close();
            }*/

        }

        public void SaysoundMapping()
        {
            if (isReady)
            {
                Console.Write("[Saysound Remapping] ");
            }
            Console.WriteLine("Reading Saysound Mapping Json...");
            string saysound_json_string = File.ReadAllText(BotConfig.saysound_mapping_json);
            saysound_data = (JObject)JsonConvert.DeserializeObject(saysound_json_string);
            Console.WriteLine("Saysound mapping completed.");
            //Linq: Get Value by Key
        }

        /// <summary>
        /// 解析 JSON 字串，回傳一個 iNewsekaiServerJson 物件。
        /// </summary>
        /// <param name="jsonPath">Json 存放路徑（預設為：/iNewsekaiServers.json）</param>
        /// <returns>回傳 iNewsekaiServerJson 物件。</returns>
        public List<iNewsekaiServerJson> GetiNewsekaiServerObjFromJson(string jsonPath = "iNewsekaiServers.json")
        {
            var inewskServers = JsonConvert.DeserializeObject<List<iNewsekaiServerJson>>(File.ReadAllText(jsonPath));
            return inewskServers;
        }
        /// <summary>
        /// 將物件序列化，寫入 JSON 檔案。
        /// </summary>
        /// <param name="obj">想要序列化的物件</param>
        /// <param name="jsonPath">存檔路徑</param>
        public void WriteObjToJson(object obj, string jsonPath)
        {
            string json_string = string.Empty;
            json_string = JsonConvert.SerializeObject(obj);
            File.WriteAllText(jsonPath, json_string, Encoding.UTF8);
        }

        /// <summary>
        /// 整個 Console Program
        /// </summary>
        public void Start()
        {
            Console.OutputEncoding = System.Text.Encoding.Unicode; //讓 console 可以正常顯示 Unicode 文字。
            Console.Title = "iNewbot @Discord";

            //WebClient webClient = new WebClient();
            using (WebClient webClient = new WebClient())
            {
                publicIp = webClient.DownloadString("https://api.ipify.org");
                webClient.Dispose();
                Console.WriteLine("My public IP Address is: {0}", publicIp);
            }

            /*_client = new DiscordClient();
            _client.UsingAudio(x => // Opens an AudioConfigBuilder so we can configure our AudioService
            {
                x.Mode = AudioMode.Outgoing; // Tells the AudioService that we will only be sending audio
            });
            _client.UsingCommands(x => {
                x.PrefixChar = BotConfig.command_prefix_char;
                x.HelpMode = HelpMode.Public;
            });*/

            _client = new DiscordClient(x =>
           {
               x.AppName = "iNewbot";
               x.AppUrl = "http://steamcommunity.com/id/hollen9/";
               x.MessageCacheSize = 0;
               x.UsePermissionsCache = true;
               x.EnablePreUpdateEvents = true;
               //x.LogLevel = LogSeverity.Debug;
               //x.LogHandler = OnLogMessage;
           }).UsingCommands(x =>
           {
               x.PrefixChar = BotConfig.command_prefix_char;
               x.AllowMentionPrefix = true;
               x.HelpMode = HelpMode.Public;
               x.ExecuteHandler = OnCommandExecuted;
               //x.ErrorHandler = OnCommandError;
           })
            //.UsingModules()
            .UsingAudio(x =>
            {
                x.Mode = AudioMode.Outgoing;
                //x.EnableMultiserver = true;
                x.EnableEncryption = true;
                x.Bitrate = AudioServiceConfig.MaxBitrate;
                x.BufferLength = 10000;
            });
            //.UsingPermissionLevels(PermissionResolver);

            set_voiceChannel = _client.GetChannel(BotConfig.default_voice_channel);



            _client.Ready += (s, e) =>
            {
                Console.Write("Ready: ");
                Console.Write($"{BotConfig.bot_token.Substring(0, 10)}");
                for (int i=0; i< BotConfig.bot_token.Length - 10; i++)
                {
                    Console.Write("*");
                }
                Console.WriteLine($"\nClient State: {_client.Status}, {_client.State}");
            };

            _client.ServerUnavailable += (s, e) =>
            {
                Console.Write("ServerUnavailable");
            };

            /*_client.MessageUpdated += (s, e) =>
            {
                e.Channel.SendMessage($"{e.Before.Text}\n|修改前後|\n{e.After.Text}");
                //e.After.Text;
            };*/

            _client.MessageReceived += async (s, e) =>
            {
                _client.SetGame(game: $"{BotConfig.command_prefix_char}help");
                if (!e.Message.IsAuthor && !e.Message.User.IsBot) //檢查、確定不是自己 BOT 的訊息再做。
                {
                    if (!isExistInDuplicatedSaycommandList(e.Message.Text) && e.Message.Channel.Id == 223484813028491264) //voice_inewbot
                    { //在指定 VoiceChannel 對應的文字頻道讀取到文字就做（SAYSOUND 查詢、串流）。

                        // Finds the first VoiceChannel on the server 'Music Bot Server'
                        //Channel voiceChannel = _client.FindServers("iNewsekai チャットコミュニティ").FirstOrDefault().VoiceChannels.FirstOrDefault();
                        //Channel voiceChannel = _client.GetChannel(223391660145508353);  //general_chat
                        if (_vClient == null)
                        {
                            Console.WriteLine("VoiceClinet: First attempt to join voice channel.");
                            if (set_voiceChannel == null) set_voiceChannel = _client.GetChannel(BotConfig.default_voice_channel);
                            _vClient = await _client.GetService<AudioService>().Join(set_voiceChannel);
                        }
                        else
                        {
                            if (_vClient.State == ConnectionState.Disconnected)
                            {
                                Console.WriteLine("VoiceClinet: Disconnected State is detected! Try to reconnect to last voice channel.");
                                if (set_voiceChannel == null) set_voiceChannel = _client.GetChannel(BotConfig.default_voice_channel);
                                _vClient = await _client.GetService<AudioService>().Join(set_voiceChannel);
                            }
                        }
                        string input_saycommand = e.Message.Text.ToLower();
                        string mp3 = GetSaysoundFilepath(input_saycommand);
                        try
                        {
                            if (!isExistInDuplicatedSaycommandList(input_saycommand))
                            {
                                duplicatedsaycommandList.Add(
                                new DuplicatedSaycommand
                                {
                                    Saycommand = input_saycommand,
                                    Cooldown_second = BotConfig.saysound_cooldown_second //saysound 冷卻時間
                                });
                            }
                            if (mp3 != null)
                            {
                                await SendAudio(saysound_location + mp3);
                            }
                        }
                        catch
                        {
                            await e.Channel.SendMessage("<@" + e.User.Id + ">" + " 效果音找尋失敗。Failed to locate the sound file.");  //ECHO 回傳接收字串。
                            Console.WriteLine("Failed to locate the sound file.");
                            Console.WriteLine(saysound_location + mp3);
                        }
                    }
                    if (e.Message.Text.Length > 10 && e.Channel.Id == 223159018309287936) //TEST FEATURE (limited to BOT channel)
                    {
                        int lines = e.Message.Text.Split('\n').Length;
                        if (lines >= 5) await e.Channel.SendMessage($"{e.User.Mention} 你打太多行了 ({lines.ToString()})，有洗版之嫌～請注意自身行為，感謝。");
                        //e.User.AddRoles
                    }
                }

            };

            _client.GetService<CommandService>().CreateCommand("uptime")
                .Description("Show the time since the bot was last started.")
                .Do(async e =>
                {
                    await e.Message.Delete();
                    TimeSpan time = TimeSpan.FromSeconds(uptime_second);

                    //here backslash is must to tell that colon is
                    //not the part of format, it just a character that we want in output
                    //string strUptime = time.ToString(@"hh\:mm\:ss"); //string strUptime = time.ToString(@"hh\:mm\:ss\:fff");
                    string strUptime = time.ToString(@"dd\.hh\:mm\:ss");
                    await e.Channel.SendMessage($"{e.User.Mention} {strUptime} 日");
                });

            _client.GetService<CommandService>().CreateCommand("ip")
                .Alias(new string[] { "inewskstatus", "inewstatus", "inewsekaistatus", "status" })
                .Description("To check whether iNewsekai is online or not.")
                .Parameter("ServerID", ParameterType.Optional)
                .Do(async e =>
                {
                    string ArgServerID = e.GetArg("ServerID").ToLower();
                    var msg_wait = await e.Channel.SendMessage("`検索中... Querying...`").ConfigureAwait(false);
                    int success = 0;
                    List<QueryMaster.GameServer.ServerInfo> listSvinfo = new List<QueryMaster.GameServer.ServerInfo>();


                    await Task.Factory.StartNew(() =>
                    {

                        //string StrQueryIp, StrPort;
                        if (ArgServerID == "1" || ArgServerID == "一" || string.IsNullOrEmpty(ArgServerID))
                        {
                            //var inewsk_enum = (iNewsekaiServers)Enum.ToObject(typeof(iNewsekaiServers), 1);
                            //string ip = inewsk_enum.GetDescription();
                            var inewsk = inewskServers.Where(x => x.Server.PimaryKey == 1).FirstOrDefault();
                            string ip = inewsk.Server.Ip;
                            try
                            {
                                var inp = CreateIPEndPoint(ip);
                                listSvinfo.Add(
                                    QueryMaster.GameServer.
                                    ServerQuery.GetServerInstance(QueryMaster.Game.
                                    CounterStrike_Global_Offensive, inp, false, 500, 500, 3, false).
                                    GetInfo());
                            }
                            catch
                            {
                                e.Channel.SendMessage("1號 IP 有誤!");
                            }
                        }
                        if (ArgServerID == "2" || ArgServerID == "二" || string.IsNullOrEmpty(ArgServerID))
                        {
                            //var inewsk_enum = (iNewsekaiServers)Enum.ToObject(typeof(iNewsekaiServers), 2);
                            //string ip = inewsk_enum.GetDescription();
                            var inewsk = inewskServers.Where(x => x.Server.PimaryKey == 2).FirstOrDefault();
                            string ip = inewsk.Server.Ip;
                            try
                            {
                                var inp = CreateIPEndPoint(ip);
                                listSvinfo.Add(
                                    QueryMaster.GameServer.
                                    ServerQuery.GetServerInstance(QueryMaster.Game.
                                    CounterStrike_Global_Offensive, inp, false, 500, 500, 3, false).
                                    GetInfo());
                            }
                            catch
                            {
                                e.Channel.SendMessage("2號 IP 有誤!");
                            }
                        }
                        if (ArgServerID == "3" || ArgServerID == "jp" || ArgServerID == "japan" || ArgServerID == "日本" || string.IsNullOrEmpty(ArgServerID))
                        {
                            //var inewsk_enum = (iNewsekaiServers)Enum.ToObject(typeof(iNewsekaiServers), 3);
                            //string ip = inewsk_enum.GetDescription();
                            var inewsk = inewskServers.Where(x => x.Server.PimaryKey == 3).FirstOrDefault();
                            string ip = inewsk.Server.Ip;
                            try
                            {
                                var inp = CreateIPEndPoint(ip);
                                listSvinfo.Add(
                                    QueryMaster.GameServer.
                                    ServerQuery.GetServerInstance(QueryMaster.Game.
                                    CounterStrike_Global_Offensive, inp, false, 500, 500, 3, false).
                                    GetInfo());
                            }
                            catch
                            {
                                e.Channel.SendMessage("3號 IP 有誤!");
                            }
                        }
                    }).ContinueWith(async task =>
                    {
                        string StrResultSuccess = string.Empty;
                        string StrResultFailed = string.Empty;

                        foreach (var sv in listSvinfo.Select((value, index) => new { value, index }))
                        {
                            //int id = int.Parse(ArgServerID);
                            /*int id;
                            if (!int.TryParse(ArgServerID, out id)) //isString
                            {
                                id = 3;
                            }*/
                            int id = sv.index + 1;
                            var this_sv = inewskServers.Where(x => x.Server.PimaryKey == id).FirstOrDefault();
                            //Console.WriteLine("[id] " + id);
                            if (sv.value == null || sv.value.Ping < 0)
                            {
                                //StrResultFailed += $"`{Enum.GetName(typeof(iNewsekaiServers), id)} is offline. 応答なし！ 無回應。 (>1sec, 3 retries)`\n";
                                StrResultFailed += $"`{this_sv.Server.Name} is offline. 応答なし！ 無回應。 (>1sec, 3 retries)`\n";
                            }
                            else
                            {
                                //var inewsk_enum = (iNewsekaiServers)Enum.ToObject(typeof(iNewsekaiServers), id);
                                //string ip = inewsk_enum.GetDescription();
                                int signal_bar_num = 5 - (int)sv.value.Ping / 100; //0-100; 100-200; 200-300; 300-400; 400-500
                                string signal_bar = null;
                                for (int i = 0; i < 5; i++)
                                {
                                    if (i < signal_bar_num) signal_bar += "▆";
                                    else signal_bar += "▁";
                                }
                                StrResultSuccess += $"**{sv.value.Name}** | {sv.value.Map} | Signal: {signal_bar} | steam://connect/{this_sv.Server.Ip}\n";
                                success++;
                            }
                        }
                        await msg_wait.Delete();
                        if (success > 0) await e.Channel.SendMessage($"{e.User.Mention}** The status of iNewsekai 狀態 (´・ω・`)**\n{StrResultSuccess}\n{StrResultFailed}");
                        else
                        {
                            string srymsg =
                                $"{e.User.Mention}**您所指定之伺服器(皆)為離線中，歡迎利用 `{BotConfig.command_prefix_char}yoteibi` 命令以檢索開放日期。\n" +
                                $"The requested server(s) is offline. You can check our opening times by inputing `{BotConfig.command_prefix_char}when` command here.**";
                            await e.Channel.SendMessage(srymsg);
                        }
                    });
                });
            _client.GetService<CommandService>().CreateCommand("sosusaba")
                .Alias(new string[] { "sourceserver", "sosusaba", "srcsv", "sssb" })
                .Description("Check whether the source-engine server queried by user is online or not.")
                .Parameter("IP", ParameterType.Required)
                .Do(async e =>
                {
                    var msg_wait = await e.Channel.SendMessage("`検索中... Querying...`").ConfigureAwait(false);
                    QueryMaster.GameServer.Server sv1 = null;
                    QueryMaster.GameServer.ServerInfo info1 = null;
                    string StrQueryIp = null;
                    ReadOnlyCollection<QueryMaster.GameServer.PlayerInfo> players = null;
                    await Task.Factory.StartNew(() => //先做
                    {
                        StrQueryIp = e.GetArg("IP");
                        string[] StrQueryIpPort = StrQueryIp.Split(':');
                        if (StrQueryIpPort.Length < 2)
                        {
                            if (!StrQueryIp.Contains(':'))
                                StrQueryIp += ":";
                            StrQueryIp += "27015";
                        }
                        sv1 = QueryMaster.GameServer.ServerQuery.GetServerInstance(QueryMaster.EngineType.Source, CreateIPEndPoint(StrQueryIp), false, 500, 500, 3, false);
                        info1 = sv1.GetInfo();
                        players = sv1.GetPlayers();
                    }).ContinueWith(async task => //做完接著做
                    {
                        string StrResult = null;
                        if (info1 == null || info1.Ping == -1) StrResult = $"{e.User.Mention} There is no response from \"{StrQueryIp}\"! 応答なし！ 無回應。 (>500ms, 3 retries)";
                        else
                        {
                            string splitText = ":small_blue_diamond:";
                            string isFreeslot_emoji, isPrivate_emoji;
                            int freeslot = info1.MaxPlayers - info1.Players;
                            if (freeslot > 0) isFreeslot_emoji = ":white_check_mark:";
                            else isFreeslot_emoji = ":u6e80:";
                            isPrivate_emoji = info1.IsPrivate ? ":lock:" : String.Empty;
                            StrResult =
                                $"{e.User.Mention} :video_game: steam://connect/{StrQueryIp} \n" +
                                $"{isPrivate_emoji}{isFreeslot_emoji} **{info1.Name}**{splitText}**{info1.Description}** `({info1.Players}/{info1.MaxPlayers})` :map:{info1.Map}\n" +
                                $"```csharp\n";
                            //:accept: **﻿TAIWAN-2233非官方** | :small_blue_diamond: **Left 4 Dead 2** `(9/15)` | :map: l4d_MIC2_TrapmentD
                            foreach (QueryMaster.GameServer.PlayerInfo i in players)
                            {
                                string StrTime = null;
                                if (i.Time.TotalHours >= 24) StrTime += $"{i.Time.Days}日";
                                if (i.Time.TotalMinutes >= 60) StrTime += $"{i.Time.Hours}時";
                                if (i.Time.TotalSeconds >= 60) StrTime += $"{i.Time.Minutes}分";
                                StrTime += $"{i.Time.Seconds}.{i.Time.Minutes}秒";
                                if (i.Name != null) StrResult += $"{i.Name} | {i.Score}pt | {StrTime}\n";
                            }
                        }
                        await msg_wait.Delete();
                        await e.Channel.SendMessage($"{StrResult}\n```");
                        //Console.WriteLine("Name : " + i.Name + "\nScore : " + i.Score + "\nTime : " + i.Time);
                        sv1.Dispose();
                    });

                });
            _client.GetService<CommandService>().CreateCommand("say")
                .Alias(new string[] { "mimic" })
                .Description("Repeat a message.")
                .Parameter("msg", ParameterType.Unparsed)
                .Do(async e =>
                {
                    string msg = e.GetArg("msg");
                    await e.Message.Delete();
                    await e.Channel.SendMessage($"#{e.User.Discriminator}: {msg}");
                });
            _client.GetService<CommandService>().CreateCommand("motdimage")
                .Alias(new string[] { "mimg", "motdimg" })
                .Description("Show the first image displayed on MOTD.")
                .Do(async e =>
                {
                    string strWebContent = GetWebSourceCode("https://sites.google.com/site/inewskcsgo/home", Encoding.UTF8, 7000);
                    string strResult = String.Empty;
                    int iBodyStart = strWebContent.IndexOf("<body", 0);
                    int iDivStart = strWebContent.IndexOf("sites-tile-name-header", iBodyStart);
                    int iDivEnd = strWebContent.IndexOf("sites-tile-name-content-1", iDivStart);
                    strResult = strWebContent.Substring(iDivStart, iDivEnd - iDivStart);
                    int iImgTagStart = strResult.IndexOf("<img");
                    strResult = strResult.Substring(iImgTagStart);
                    int iImgSrcStart = strResult.IndexOf("src=\"");
                    strResult = strResult.Substring(iImgSrcStart);
                    string[] strImage = strResult.Split('"');
                    strResult = strImage[1];
                    await e.Channel.SendMessage($"**Image of the day!** {strResult}");
                });
            _client.GetService<CommandService>().CreateCommand("hug")
                .Alias(new string[] { })
                .Description("")
                .Parameter("targetUser", ParameterType.Required)
                .Parameter("customMsg", ParameterType.Optional)
                .Do(async e =>
                {
                    //bool isValidTarget;
                    await e.Message.Delete();
                    string strTargetUser = e.GetArg("targetUser");

                    //strTargetUser = strTargetUser.Substring(strTargetUser.IndexOf('<')+2, strTargetUser.Length-4);
                    int intIndexofAt = strTargetUser.IndexOf('@');
                    if (intIndexofAt >= 0 && intIndexofAt < 2)
                    {
                        if (strTargetUser[intIndexofAt + 1] == '!') strTargetUser = strTargetUser.Substring(intIndexofAt + 2, strTargetUser.Length - 4);
                        else strTargetUser = strTargetUser.Substring(intIndexofAt + 1, strTargetUser.Length - 3);

                        User userTarget = e.Server.GetUser(ulong.Parse(strTargetUser));
                        //User userTarget = _client.GetServer(e.Server.Id).FindUsers(strTargetUser).ElementAtOrDefault<User>(0);
                        //await e.Channel.SendMessage($"Mention: {userTarget.Mention}");
                        string strHugEmoji = "((((っ・ω・)っ", strHugTail = "‥", strHugSpace = "　";
                        string strHugging = strHugEmoji;
                        for (short i = 0; i < 5; i++) strHugging += strHugSpace; //((((っ・ω・)っ口口口口口
                        await Task.Factory.StartNew(async () =>
                        {
                            var msg = await e.Channel.SendMessage($"{strHugging}{userTarget.NicknameMention}").ConfigureAwait(false);
                            for (short i = 5; i >= 0; i--)
                            {
                                await Task.Delay(600).ConfigureAwait(false);
                                strHugging = strHugging.Remove(strHugging.Length - 1, 1).Insert(0, strHugTail);
                                await msg.Edit($"{strHugging}{userTarget.NicknameMention}").ConfigureAwait(false);
                                if (i == 0)
                                {
                                    Random rdm = new Random();
                                    //await Task.Delay(rdm.Next(600, 800)).ConfigureAwait(false);
                                    string middle_text = string.IsNullOrEmpty(e.GetArg("customMsg")) ? ":heart::heart::heart:" : e.GetArg("customMsg");
                                    await msg.Edit($"{e.User.NicknameMention}{middle_text}{userTarget.NicknameMention}").ConfigureAwait(false);
                                }
                            }
                        });
                        //await e.Channel.SendMessage($"OK, arg=`{e.GetArg("targetUser")}`, strTargetUser= `{strTargetUser}`");
                    }
                });
            _client.GetService<CommandService>().CreateCommand("yoteibi")
                .Alias(new string[] { "openingdate", "openday", "when", "date" })
                .Description("Show the time when iNewsekai is planned to be up. オープン予定日")
                .Do(async e =>
                {
                    string strDate = String.Empty;
                    var msg_wait = await e.Channel.SendMessage("`検索中... Querying...`").ConfigureAwait(false);
                    await Task.Factory.StartNew(() =>
                    {
                        string strWebContent = GetWebSourceCode("https://sites.google.com/site/inewskcsgo/home", Encoding.UTF8, 7000);
                        int iBodyStart = strWebContent.IndexOf("<body", 0);
                        int iDash = strWebContent.IndexOf("--", iBodyStart);
                        strDate = strWebContent.Substring(iDash - 12, 12);
                        string[] ymd = strDate.Split('/');
                        string strYear = ymd[0].Substring(ymd[0].IndexOf("20"), 4);
                        string strMonth = ymd[1];
                        string strDay = String.Empty;
                        if (ymd[2].Length > 2)
                        {
                            strDay = ymd[2].Substring(0, ymd[2].Length - ymd[2].IndexOf("--") - 1);
                        }
                        else strDay = ymd[2];
                        strDate = $"{strYear}/{strMonth}/{strDay}";
                    }).ContinueWith(task =>
                    {
                        msg_wait.Delete();
                        e.Channel.SendMessage(
                            $"{e.User.Mention} 開服預定日: __**{strDate}**__\n" +
                            $"オープン予定日: __**{strDate}**__\nIt's planned to be up on __**{strDate}**__.");
                    });
                });
            _client.GetService<CommandService>().CreateCommand("rank")
                .Alias(new string[] { "top10" })
                .Description("查詢排名資料。")
                .Parameter("q", ParameterType.Optional)
                .Do(async e =>
                {
                    string result = "";
                    string q = e.GetArg("q");
                    if (String.IsNullOrWhiteSpace(q) || q.Length < 4)
                    { //使用者輸入為頁數的形式
                        int rangeEnd = 10;
                        try
                        {
                            rangeEnd = int.Parse(q) * 10;
                            if (rangeEnd <= 0) throw new FormatException();
                        }
                        catch{/*使用者輸入值非數字，當作 top10 查詢*/}
                        for (int i = rangeEnd-10; i < rangeEnd; i++)
                        {
                            string emoji_medal = "";
                            switch (i)
                            {
                                case 0:
                                    emoji_medal = ":first_place:";
                                    break;
                                case 1:
                                    emoji_medal = ":second_place:";
                                    break;
                                case 2:
                                    emoji_medal = ":third_place:";
                                    break;
                                case 3:
                                case 4:
                                case 5:
                                case 6:
                                case 7:
                                case 8:
                                case 9:
                                    emoji_medal = ":medal:";
                                    break;
                                default:
                                    emoji_medal = inewskRankDatas[i].TotalPoint >= 888 ? ":small_orange_diamond:" : ":small_blue_diamond:";
                                    break;
                            }

                            string achievementBadges = "";
                            if (iNewsekaiAchievement.UsersAchievement.ContainsKey(inewskRankDatas[i].Steam32Id))
                            {
                                foreach (var a in iNewsekaiAchievement.UsersAchievement[inewskRankDatas[i].Steam32Id])
                                {
                                    achievementBadges += a.GetDescription();
                                }
                            }
                            
                            result +=
                                //$"**__{(i + 1).ToString("D2")}__** | **" + inewRankDict.Values.ElementAt(i).TotalPoint.ToString("D6") + $" pt.** | {inewRankDict.Values.ElementAt(i).TotalPlayedTime.ToString("0000.0")} 時{emoji_medal}`" + inewRankDict.Values.ElementAt(i).NickName.Replace("`", "‵") + "`\n";
                                $"**__{inewskRankDatas[i].RankNumber.ToString("D2")}__** | **" +
                                inewskRankDatas[i].TotalPoint.ToString("D6") +
                                $" pt.** | {inewskRankDatas[i].TotalPlayedTime.ToString("0000.0")} 時{emoji_medal}`" +
                                inewskRankDatas[i].NickName + '`' + achievementBadges + '\n';
                        }
                        await e.Channel.SendMessage(e.User.Mention + $" **iNewsekai Leaderboards ({rangeEnd-9}~{rangeEnd})**\n" + result + $"\n`Cached {lastSyncDatetime.ToString("HH:mm:ss")} (GMT+8)`");
                    } //使用者輸入為 SteamId任一種 的形式
                    else
                    {
                        bool isContainedColonAtSeventh = false;
                        try
                        {
                            isContainedColonAtSeventh = q[7] == ':';
                        }
                        catch(IndexOutOfRangeException indexofr)
                        {
                        }
                        if (isContainedColonAtSeventh)
                        {
                            q = q.ToUpper();
                        }
                        else if (q.Substring(0, 3) == "765") //接收 Steam64ID 輸入，經演算轉換成 Steam32ID
                        {
                            try
                            {
                                q = Steam_ConvertTo32Id(q); //將這邊的 q 查詢字串改成 Steam32ID;
                            }
                            catch
                            {
                                await e.Channel.SendMessage( e.User.Mention + ":x:Steam64ID -> Steam32ID 轉換失敗 Failed to convert!\n" + 
                                    "倘若你想要用 Steam64Id 查詢，那應該是數字才對。\n" +
                                    "If you'd like to use Steam64Id to query, it should be a number."
                                    );
                                return;
                            }
                        }
                        else //Steam Custom ID
                        {
                            //Console.WriteLine("Steam Custom ID: " + q);
                            SteamQuery sq = new SteamQuery(BotConfig.SteamApiKey);
                            q = Steam_ConvertTo32Id(sq.ISteamUser.ResolveVanityURL(q).ParsedResponse.SteamId.ToString());
                            //Console.WriteLine("To 32Id: " + q);
                        }
                        try
                        {
                            iNewsekaiRankData p = inewRankSteam32IdDict[q];

                            //iNewsekaiRankData p = inewskRankDatas.Where(x => x.Steam32Id == q).FirstOrDefault();

                            string achievementText = "";
                            if (iNewsekaiAchievement.UsersAchievement.ContainsKey(q))
                            {
                                achievementText += '\n';
                                foreach (var a in iNewsekaiAchievement.UsersAchievement[q])
                                {
                                    achievementText += "*__" + a.GetDescription() + a + "__*, ";
                                }
                                achievementText = achievementText.Substring(0, achievementText.Length - 2);
                            }
                            await e.Channel.SendMessage(
                                $"{e.User.Mention} **The Player's Stat on iNewsekai**\n" +
                                $":name_badge: 名前: **`{p.NickName}`**" +
                                $":military_medal: 順位: **{p.RankNumber}** 番\n" +
                                $":ideograph_advantage: {p.TotalPoint} 点\n" +
                                $":timer: {p.TotalPlayedTime} 時{achievementText}\n" +
                                $"<:nep_warau:230597074851332096> 總擊殺: __{p.TotalKill}__\n" +
                                $":skull_crossbones: 總死亡: __{p.TotalDeath}__\n" +
                                $":dart: Headshot: __{p.TotalHeadshot}__\n" +
                                $"<:megumin_giyatekora:230600076563578880> 自爆: __{p.TotalSuicide}__\n" +
                                $"<:null:271886130796953610>:cop: GYM_SEVEN 殺: __{p.TotalBossKill_GymSeven}__\n" +
                                $"<:null:271886130796953610>:bee: CHOCO 殺: __{p.TotalBossKill_Choco}__\n" +
                                $"<:null:271886130796953610>:dagger: ほっぽ 殺: __{p.TotalBossKill_Hoppo}__\n" +
                                $"<:null:271886130796953610>:cowboy: 中Boss 殺: __{p.TotalBossKill_CyuuBoss}__\n" +
                                $"`Cached {lastSyncDatetime.ToString("HH:mm:ss")} (GMT+8)`"
                            );
                        }
                        catch (KeyNotFoundException key404)
                        {
                            await e.Channel.SendMessage($"{e.User.Mention} \n資料庫找不到指定 ID 的玩家。\nThe provided ID doesn't match any of the player in the database.");
                        }
                    }
                });
            
            _client.GetService<CommandService>().CreateGroup("inewsk", cgb =>
            {
                cgb.CreateCommand("setip")
                        .Alias(new string[] { "changeip" })
                        .Description("變更 IP。用法: setip <Server Primary Key> <IP>")
                        .AddCheck(adminCheck)
                        .Parameter("sid", ParameterType.Required)
                        .Parameter("ip", ParameterType.Required)
                        .Do(async e =>
                        {
                            int sid;
                            if (int.TryParse(e.GetArg("sid"), out sid))
                            {
                                try
                                {
                                    var sv = inewskServers.Where(x => x.Server.PimaryKey == sid).FirstOrDefault().Server;
                                    sv.Ip = e.GetArg("ip");
                                    WriteObjToJson(inewskServers, BotConfig.inewsekai_server_json); //save (serealize) object to json file
                                    await e.Channel.SendMessage($"成功將 {sid} 號伺服器的 IP 變更為: `{e.GetArg("ip")}`");
                                }
                                catch (NullReferenceException nre)
                                {
                                    await e.Channel.SendMessage("找不到指定 Primary Key 的伺服器。");
                                }
                            }
                            else
                            {
                                await e.Channel.SendMessage("請輸入數字。");
                            }
                        });
            });


            _client.GetService<CommandService>().CreateGroup("bot", cgb => //BOT OWNER 最高指導
            {
                cgb.CreateCommand("prefix")
                        //.Alias(new string[] { "remap" })
                        //.Description("Ping a source-driven server.")
                        .Parameter("overwrite", ParameterType.Optional)
                        .Do(async e =>
                        {
                            if (e.User.Id == BotConfig.bot_owner_uid)
                            {
                                if (e.GetArg("overwrite") == null || e.GetArg("overwrite") == string.Empty)
                                    await e.Channel.SendMessage($"{e.User.Mention} Current command prefix is {BotConfig.command_prefix_char}.");
                                else if (e.GetArg("overwrite").Length > 1)
                                {
                                    await e.Channel.SendMessage($"{e.User.Mention} I only accept one character, instead of {e.GetArg("overwrite").Length} of them.");
                                }
                                else
                                {
                                    try
                                    {
                                        BotConfig.OverwriteValue("command_prefix_char", e.GetArg("overwrite"));
                                        await e.Channel.SendMessage($"{e.User.Mention} Changed prefix to {BotConfig.command_prefix_char}!\nNeed to restart me to take effect!");
                                        /*_client.UsingCommands(x => {
                                            x.PrefixChar = BotConfig.command_prefix_char;
                                        });*/
                                        //_client.UsingCommands(Action<CommandServiceConfigBuilder> 
                                        //Action<CommandServiceConfigBuilder> cscb;
                                        //cscb = s => s.PrefixChar = BotConfig.command_prefix_char;
                                        //cscb = s => s.HelpMode = HelpMode.Public;
                                        /*cscb = delegate (CommandServiceConfigBuilder s) {
                                            s.PrefixChar = BotConfig.command_prefix_char;
                                        };
                                        
                                        _client.UsingCommands(cscb);*/
                                    }
                                    catch (Exception anyerr)
                                    {
                                        Console.WriteLine(anyerr.ToString());
                                        //await e.Channel.SendMessage($"{e.User.Mention} {anyerr.ToString()}");
                                        //await e.User.SendMessage($"Error! {anyerr.ToString()}");
                                    }
                                }
                            }
                            else await e.Channel.SendMessage($"{e.User.Mention} You are not my master!");
                        });
                cgb.CreateCommand("remapping")
                        .Alias(new string[] { "remap" })
                        //.Description("Ping a source-driven server.")
                        //.Parameter("IP", ParameterType.Required)
                        .Do(async e =>
                        {
                            if (e.User.Id == BotConfig.bot_owner_uid)
                            {
                                SaysoundMapping();
                                await Task.Factory.StartNew(async () => //先做
                                {
                                    await e.Channel.SendMessage($"{e.User.Mention} Try to perform saysound remapping!");
                                    SaysoundMapping();
                                }).ContinueWith(async task => //做完接著做
                                {
                                    await e.Channel.SendMessage($"{e.User.Mention} Saysound remapping completed! :D");
                                });
                            }
                            else await e.Channel.SendMessage($"{e.User.Mention} You are not my master!");
                        });

                cgb.CreateCommand("exit")
                        .Alias(new string[] { "quit", "shutdown" })
                        //.Description("Greets a person.")
                        //.Parameter("GreetedPerson", ParameterType.Required)
                        .Do(async e =>
                        {
                            if (e.User.Id == BotConfig.bot_owner_uid)
                            {
                                await e.Channel.SendMessage($"{e.User.Mention} Good bye.");
                                Thread.Sleep(1000);
                                if (_vClient != null) await _vClient.Disconnect();
                                await _client.Disconnect();
                                Environment.Exit(0);
                            }
                            else await e.Channel.SendMessage($"{e.User.Mention} You are not my master!");
                        });
                cgb.CreateCommand("restart")
                        .Alias(new string[] { "forcerestart" })
                        //.Description("Greets a person.")
                        //.Parameter("GreetedPerson", ParameterType.Required)
                        .Do(async e =>
                        {
                            if (e.User.Id == BotConfig.bot_owner_uid)
                            {
                                var temp_msg = await e.Channel.SendMessage($"`Restarting...`").ConfigureAwait(false);
                                string fileName = null;
                                await Task.Factory.StartNew(async () => //先做
                                {
                                    if (_vClient != null) await _vClient.Disconnect();
                                    fileName = System.Reflection.Assembly.GetExecutingAssembly().Location;
                                    Thread.Sleep(1000);
                                    await temp_msg.Delete();
                                }).ContinueWith(task => //做完接著做
                                {
                                    Thread.Sleep(200);
                                    _client.Disconnect();
                                    System.Diagnostics.Process.Start(fileName);
                                });
                            }
                            else await e.Channel.SendMessage($"{e.User.Mention} You are not my master!");
                        });
                cgb.CreateCommand("voice")
                        .Alias(new string[] { "join", "vid", "id" })
                        //.Description("Greets a person.")
                        .Parameter("vid", ParameterType.Optional)
                        .Do(async e =>
                        {
                            if (e.User.Id == BotConfig.bot_owner_uid)
                            {
                                Console.WriteLine("join command detected!");
                                if (e.GetArg("vid") == "def" || e.GetArg("vid") == null || e.GetArg("vid") == string.Empty)
                                {
                                    Console.WriteLine("Try to join the default voice channel!");
                                    set_voiceChannel = _client.GetChannel(BotConfig.default_voice_channel);
                                    _vClient = await _client.GetService<AudioService>().Join(set_voiceChannel); // Join the Voice Channel, and return the IAudioClient.
                                }
                                else
                                {
                                    ulong vid = 0;
                                    vid = ulong.Parse(e.GetArg("vid"));
                                    Console.WriteLine("Try to join the voice channel whose id = " + vid.ToString());
                                    set_voiceChannel = _client.GetChannel(vid);
                                    _vClient = await _client.GetService<AudioService>().Join(set_voiceChannel);
                                }
                            }
                            else await e.Channel.SendMessage($"{e.User.Mention} You are not my master!");
                        });
                cgb.CreateCommand("setname")
                        .Alias(new string[] { "sn" })
                        .Parameter("after", ParameterType.Unparsed)
                        .Do(async e =>
                        {
                            string newname = e.GetArg("after");
                            string oldname = _client.CurrentUser.Name;
                            if (e.User.Id == BotConfig.bot_owner_uid)
                            {
                                await _client.CurrentUser.Edit(username: newname);
                                await e.Channel.SendMessage($"`{oldname}` --更名--> {newname}");
                            }
                            else await e.Channel.SendMessage($"{e.User.Mention} You are not my master!");
                        });
                cgb.CreateCommand("setnickname")
                        .Alias(new string[] { "snn" })
                        //.Description("Greets a person.")
                        .Parameter("after", ParameterType.Unparsed)
                        .Do(async e =>
                        {
                            string newname = e.GetArg("after");
                            if (e.User.Id == BotConfig.bot_owner_uid)
                            {
                                //await e.Server.CurrentUser.Edit(null,null,null,null,newname);
                                await e.Server.CurrentUser.Edit(nickname: newname);
                            }
                            else await e.Channel.SendMessage($"{e.User.Mention} You are not my master!");
                        });
                /*cgb.CreateCommand("countdown")
                        .Alias(new string[] { "cd" })
                        //.Description("Greets a person.")
                        .Parameter("sec", ParameterType.Optional)
                        .Do(async e =>
                        {
                            Console.WriteLine("test command detected!");
                            if (e.User.Id == BotConfig.bot_owner_uid)
                            {
                                //Console.WriteLine("test command detected!");
                                int sec = -1;
                                if (int.TryParse(e.GetArg("sec"), out sec))
                                {
                                    if (sec <= 0) sec = 5;
                                }                                
                                var task1 = await Task.Factory.StartNew(async () =>
                                {
                                    var msg = await e.Channel.SendMessage($"CountDown & Editing Test {sec}").ConfigureAwait(false);
                                    for (--sec; sec >= 0; sec--)
                                    {
                                        await Task.Delay(1000).ConfigureAwait(false);
                                        await msg.Edit($"CountDown & Editing Test {sec}").ConfigureAwait(false);
                                        if (sec == 0)
                                        {
                                            Random rdm = new Random();
                                            await Task.Delay(rdm.Next(900, 1100)).ConfigureAwait(false);
                                            await msg.Delete().ConfigureAwait(false);
                                        }
                                    }
                                });
                                
                                
                            }
                            else await e.Channel.SendMessage($"{e.User.Mention} You are not my master!");
                        });*/
                cgb.CreateCommand("getmessage")
                        //.AddCheck((cm, u, ch) => u.ServerPermissions.Administrator)
                        //.AddCheck((cmd, u, ch) => !u.Roles.Any(r => r.Id /* ID of role you want to exclude */))
                        .Alias(new string[] { "gmsg" })
                        .Parameter("id", ParameterType.Required)
                        .Do(async e =>
                        {
                            if (e.User.Id == BotConfig.bot_owner_uid)
                            {
                                ulong msgid = 0; ulong.TryParse(e.GetArg("id"), out msgid);
                                Console.WriteLine($"msg_id={msgid.ToString()}");
                                //string msg = _client.GetChannel(e.Channel.Id).GetMessage(msgid).Text;
                                string msg = "nope";
                                msg = _client.GetChannel(e.Channel.Id).GetMessage(msgid).Text;

                                //await _client.GetServer(e.Server.Id).GetChannel(e.Channel.Id).SendMessage("test");
                                await e.Channel.SendMessage($"{e.Message.Id.ToString()}, {msg}");

                            }
                            else await e.Channel.SendMessage($"{e.User.Mention} You are not my master!");
                        });

            });

            ///////////COMMANDS END///////////////
            _client.ExecuteAndWait(async () =>
            {    //
                await _client.Connect(BotConfig.bot_token, TokenType.Bot);    //iNewCord
                Console.WriteLine("ExecuteAndWait: Connected successfully!");
                SaysoundMapping();
                inewskServers = GetiNewsekaiServerObjFromJson(); //解析 JSON 字串，建立 iNewsekaiServerJson 物件  (SCOPE 為最高層級)
                isReady = true;
                Console.Write($"{_client.Servers.Count()} Servers: ");
                foreach (Server sv in _client.Servers)
                {
                    Console.Write($"{sv.Name}, ");
                }
                Console.Write("\n");
                _client.SetGame(game: $"{BotConfig.command_prefix_char}help");
            });

        }

        private void OnCommandExecuted(object sender, CommandEventArgs e)
        {
            //_client.Log.Info("Command", $"{e.Command.Text} ({e.User.Name})");
            //e.Message.Delete();
        }

        /*private int PermissionResolver(User user, Channel channel)

        {

            if (user.Id == BotConfig.bot_owner_uid)

                return (int)PermissionLevel.BotOwner;

            if (user.Server != null)

            {

                if (user == channel.Server.Owner)

                    return (int)PermissionLevel.ServerOwner;



                var serverPerms = user.ServerPermissions;

                if (serverPerms.ManageRoles)

                    return (int)PermissionLevel.ServerAdmin;

                if (serverPerms.ManageMessages && serverPerms.KickMembers && serverPerms.BanMembers)

                    return (int)PermissionLevel.ServerModerator;



                var channelPerms = user.GetPermissions(channel);

                if (channelPerms.ManagePermissions)

                    return (int)PermissionLevel.ChannelAdmin;

                if (channelPerms.ManageMessages)

                    return (int)PermissionLevel.ChannelModerator;

            }

            return (int)PermissionLevel.User;

        }*/


        public bool isExistInDuplicatedSaycommandList(string input_string)
        {
            for (int i = 0; i < duplicatedsaycommandList.Count; i++)
            {
                if (duplicatedsaycommandList[i].Saycommand == input_string)   //已經登錄過需要排除的 SAYCOMMMAND 了。
                {
                    return true;
                }
            }
            return false;
        }

        public async Task SendAudio(string filepath, Channel voiceChannel = null)
        {
            if (voiceChannel != null)
            {
                _vClient = await _client.GetService<AudioService>().Join(voiceChannel);
            }
            try
            {
                // Get the number of AudioChannels our AudioService has been configured to use.
                var channelCount = _client.GetService<AudioService>().Config.Channels;
                // Create a new Output Format, using the spec that Discord will accept, and with the number of channels that our client supports.
                var OutFormat = new WaveFormat(48000, 16, channelCount);
                // Create a new Disposable MP3FileReader, to read audio from the filePath parameter
                using (var MP3Reader = new Mp3FileReader(filepath))
                // Create a Disposable Resampler, which will convert the read MP3 data to PCM, using our Output Format
                using (var resampler = new MediaFoundationResampler(MP3Reader, OutFormat))
                {
                    // Set the quality of the resampler to 60, the highest quality
                    resampler.ResamplerQuality = 60;
                    // Establish the size of our AudioBuffer
                    int blockSize = OutFormat.AverageBytesPerSecond / 50;
                    byte[] buffer = new byte[blockSize];
                    int byteCount;
                    // Read audio into our buffer, and keep a loop open while data is present
                    while ((byteCount = resampler.Read(buffer, 0, blockSize)) > 0)
                    {
                        if (byteCount < blockSize)
                        {
                            // Incomplete Frame
                            for (int i = byteCount; i < blockSize; i++)
                                buffer[i] = 0;
                        }
                        _vClient.Send(buffer, 0, blockSize); // Send the buffer to Discord
                    }
                    //await _vClient.Disconnect();
                }
            }
            catch
            {
                Console.WriteLine("Something went wrong. :(");
            }
            //await _vClient.Disconnect();
        }

        /*public void SendAudio(string filePath)
        {
            Console.WriteLine("Start to stream \" " + filePath + " \"");
            var channelCount = _client.GetService<AudioService>().Config.Channels; 
            // Get the number of AudioChannels our AudioService has been configured to use.
            var OutFormat = new WaveFormat(48000, 16, channelCount); 
            // Create a new Output Format, using the spec that Discord will accept, and with the number of channels that our client supports.
            using (var MP3Reader = new Mp3FileReader(filePath)) 
            // Create a new Disposable MP3FileReader, to read audio from the filePath parameter
            using (var resampler = new MediaFoundationResampler(MP3Reader, OutFormat)) 
            // Create a Disposable Resampler, which will convert the read MP3 data to PCM, using our Output Format
            {
                resampler.ResamplerQuality = 60; 
                // Set the quality of the resampler to 60, the highest quality
                int blockSize = OutFormat.AverageBytesPerSecond / 50; 
                // Establish the size of our AudioBuffer
                byte[] buffer = new byte[blockSize];
                int byteCount;
                Console.WriteLine("Initial: blockSize=" + blockSize + "buffer=" + buffer);

                while ((byteCount = resampler.Read(buffer, 0, blockSize)) > 0)
                // Read audio into our buffer, and keep a loop open while data is present
                {
                    if (byteCount < blockSize)
                    {
                        // Incomplete Frame
                        Console.Write("|");
                        for (int i = byteCount; i < blockSize; i++)
                        {
                            buffer[i] = 0;
                            Console.Write(".");
                        }
                    }
                    _vClient.Send(buffer, 0, blockSize); // Send the buffer to Discord
                    Console.Write("%");
                }
                Console.WriteLine("Sending completed!");
            }
        }*/
        public string GetSaysoundFilepath(string saycommand)
        {
            try
            {
                string sayMp3 = saysound_data[saycommand].Value<string>();
                //Console.WriteLine(sayMp3);
                return sayMp3;
            }
            catch
            {
                Console.WriteLine("GetSaysoundFilepath_Err");
                return null;
            }
        }

        private string GetWebSourceCode(string Url, Encoding encoding, Int32 int32Timeout = 10000)
        {
            string strResult = String.Empty;
            try
            {
                System.Net.HttpWebRequest request = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(Url);
                //聲明一個HttpWebRequest請求
                request.Timeout = int32Timeout;
                //設置連接逾時時間
                request.Headers.Set("Pragma", "no-cache");
                System.Net.HttpWebResponse response = (System.Net.HttpWebResponse)request.GetResponse();
                Stream streamReceive = response.GetResponseStream();
                StreamReader streamReader = new StreamReader(streamReceive, encoding);
                strResult = streamReader.ReadToEnd();
            }
            catch
            {
                Console.WriteLine("ERR_GetWebSourceCode");
            }
            return strResult;
        }

        private string Steam_ConvertTo32Id(string q)
        {
            byte byte_steam64id_unitdigit = (byte)Char.GetNumericValue(q[q.Length - 1]);
            int Steam32IdArgY = (byte_steam64id_unitdigit % 2 == 1) ? 1 : 0;
            string Steam32IdArgZ = ((UInt64.Parse(q.Substring(3)) - 61197960265728 - (ulong)Steam32IdArgY) / 2).ToString();
            return "STEAM_1:" + Steam32IdArgY + ":" + Steam32IdArgZ;
            //Console.WriteLine("32ID: " + result);
            //return result;
        }
    }
}
