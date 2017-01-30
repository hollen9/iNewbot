using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace iNewCord.iNewsekaiDB
{
    public class TopTen
    {
        public TopTen()
        {

        }
        public int RankId { get; set; }
        public string SteamId { get; set; }
        public string Nickname { get; set; }
        public int TotalPoint { get; set; }
        public int TotalKill { get; set; }
        //public int TotalKill_Boss1 { get; set; }
        //public int TotalKill_Boss2 { get; set; }
        //public int TotalKill_Boss3 { get; set; }
        //public int TotalKill_Boss4 { get; set; }
        //public int TotalKill_BossTheme { get; set; }
        public int TotalDeath { get; set; }
        public int TotalHeadshot { get; set; }
        public int TotalSuicide { get; set; }
        //public int RankFirstTime { get; set; }
        //public int RankLastTime { get; set; }
        //public int RankLastTime { get; set; }
        public int PlayedTime { get; set; }
        //public bool ForceOuenFlag { get; set;}

    }
}
