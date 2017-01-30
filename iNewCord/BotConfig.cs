using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;


namespace iNewCord
{
    public class BotConfig
    {
        JObject config_data;
        string json_path;
        public BotConfig(string newpath = "config.json") //Contructor
        {
            json_path = newpath;
            //json_path = @"D:\dotNet\Discord\inewCord\iNewCord\bin\Debug\config.json";
            try
            {
                string json_string = File.ReadAllText(json_path);
                config_data = (JObject)JsonConvert.DeserializeObject(json_string);
                LoadConfigData();
            }
            catch(FileNotFoundException nofile_err)
            {
                Console.WriteLine(DateTime.Now.ToLongTimeString() + "|BotConfig|" + "Cannot find the json: \n" + json_path);
            }
            catch(JsonException json_err)
            {
                Console.WriteLine(DateTime.Now.ToLongTimeString() + "|BotConfig|" + "There's something wrong with \"config.json\"!");
            }
            catch(Exception any_arr)
            {
                Console.WriteLine(DateTime.Now.ToLongTimeString() + "|BotConfig|" + any_arr);
            }
        }
        private void LoadConfigData()
        {
            try
            {
                bot_owner_uid = ulong.Parse(config_data["bot_owner_uid"].Value<string>());
                bot_token = config_data["bot_token"].Value<string>();
                saysound_directory = config_data["saysound_directory"].Value<string>();
                saysound_mapping_json = config_data["saysound_mapping_json"].Value<string>();
                saysound_listened_text_channel = ulong.Parse(config_data["saysound_listened_text_channel"].Value<string>());
                default_voice_channel = ulong.Parse(config_data["default_voice_channel"].Value<string>());
                saysound_cooldown_second = config_data["saysound_cooldown_second"].Value<uint>();
                command_prefix_char = char.Parse(config_data["command_prefix_char"].Value<string>());
                inewsekai_server_json = config_data["inewsekai_server_json"].Value<string>();
                SteamApiKey = config_data["SteamApiKey"].Value<string>();
            }
            catch(Exception any_err)
            {
                Console.WriteLine(DateTime.Now.ToLongTimeString() + "|LoadConfigData|" + any_err);
            }
        }
        public void OverwriteValue(string key, char value)
        {
            config_data[key] = value;
            OverwriteJsonFile_with_ReserializeObject();
            ReloadConfigData();
        }
        public void OverwriteValue(string key, string value)
        {
            config_data[key] = value;
            OverwriteJsonFile_with_ReserializeObject();
            ReloadConfigData();
        }
        public void OverwriteValue(string key, ulong value)
        {
            config_data[key] = value;
            OverwriteJsonFile_with_ReserializeObject();
            ReloadConfigData();
        }
        public void OverwriteValue(string key, bool value)
        {
            config_data[key] = value;
            OverwriteJsonFile_with_ReserializeObject();
            ReloadConfigData();
        }
        public void OverwriteValue(string key, uint value)
        {
            config_data[key] = value;
            OverwriteJsonFile_with_ReserializeObject();
            ReloadConfigData();
        }
        private void OverwriteJsonFile_with_ReserializeObject()
        {
            string json_string = string.Empty;
            json_string = JsonConvert.SerializeObject(config_data);
            File.WriteAllText(json_path, json_string, System.Text.Encoding.UTF8);
        }
        public void ReloadConfigData()
        {
            config_data = new JObject();
            string json_string = File.ReadAllText(json_path);
            config_data = (JObject)JsonConvert.DeserializeObject(json_string);
            LoadConfigData();
        }

        public ulong bot_owner_uid { get; set; }
        public string bot_token { get; set; }
        public string saysound_directory { get; set; }
        public string saysound_mapping_json { get; set; }
        public ulong saysound_listened_text_channel { get; set; }
        public ulong default_voice_channel { get; set; }
        public uint saysound_cooldown_second { get; set; }
        public char command_prefix_char { get; set; }
        public string inewsekai_server_json { get; set; }
        public string SteamApiKey { get; set; }
    }
}
