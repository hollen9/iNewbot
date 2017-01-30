using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace iNewCord
{
    public class iNewsekaiServerJson
    {
        [JsonProperty("server")]
        public iNewsekaiServer Server { get; set; }
    }
}
