using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace iNewCord
{
    public class iNewsekaiServer
    {
        [JsonProperty("!pkey")]
        public int PimaryKey { get; set; }

        [JsonProperty("ip")]
        public string Ip { get; set; }
        [JsonProperty("name")]
        public string Name { get; set; }
    }
}
