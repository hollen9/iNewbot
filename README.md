# iNewbot

You will need to create two Json file on your own to make this bot run.
Thier names are "config.json", "iNewsekaiServers.json" & "saysound.json" respectively.

##config.json (he config file that stored credential informations, and it must be stored under Bin/Debug or Bin/Realease dictionary.)
{
"bot_owner_uid":"<Owner's Discord ID>",
"bot_token":"Discord Bot Token",
"saysound_directory":"C:/Game Files/SteamLibrary/steamapps/common/Counter-Strike Global Offensive/csgo/sound/inewsk/20140918/",
"saysound_mapping_json":"C:/Users/Hollen9/saysound.json",
"saysound_listened_text_channel":"223484813028491264",
"default_voice_channel":"223391660145508353",
"saysound_cooldown_second":5,
"command_prefix_char":"^",
"inewsekai_server_json":"iNewsekaiServers.json",
"SteamApiKey":"<SteamApiKey>",
"mysql_conn_str":"server=????.rds.amazonaws.com;uid=hollen9;pwd=???;database=???"
}

##iNewsekaiServers.json
[{"server":{"!pkey":1,"ip":"114.35.118.5:27016","name":"iNewsekai #1"}},{"server":{"!pkey":2,"ip":"114.35.118.5:27017","name":"iNewsekai #2"}},{"server":{"!pkey":3,"ip":"104.198.124.209:27018","name":"iNewsekai #JP"}}]

##saysound.json
{
"<haha>":"<haha.mp3>"
}
