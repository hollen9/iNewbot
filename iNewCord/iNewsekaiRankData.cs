using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace iNewCord
{
    public class iNewsekaiRankData
    {
        public iNewsekaiRankData(
                long id, string steam32id, string name, int pt, float playtime,
                int kill, int death, int headshot, int suicide, int rankNum,
                int kboss1, int kboss2, int kboss3, int kboss4                
            )
        {
            RankId = id;
            Steam32Id = steam32id;
            NickName = name;
            TotalPoint = pt;
            TotalPlayedTime = playtime;
            TotalKill = kill;
            TotalDeath = death;
            TotalHeadshot = headshot;
            TotalSuicide = suicide;
            RankNumber = rankNum;
            TotalBossKill_GymSeven = kboss1;
            TotalBossKill_Choco = kboss2;
            TotalBossKill_Hoppo = kboss3;
            TotalBossKill_CyuuBoss = kboss4;
        }
        //Attributes
        public long RankId { get; }
        public string Steam32Id { get; set; }
        public string NickName { get; set; }
        public int TotalPoint { get; set; }
        public float TotalPlayedTime { get; set; }
        public int TotalKill { get; set; }
        public int TotalDeath { get; set; }
        public int TotalHeadshot { get; set; }
        public int TotalSuicide { get; set; }
        public int RankNumber { get; set; }
        public int TotalBossKill_GymSeven { get; set; }
        public int TotalBossKill_Choco { get; set; }
        public int TotalBossKill_Hoppo { get; set; }
        public int TotalBossKill_CyuuBoss { get; set; }
        //Class Var
    }
}
