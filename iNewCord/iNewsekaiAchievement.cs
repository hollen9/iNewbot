using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace iNewCord
{
    static class iNewsekaiAchievement
    {
        public enum Achievement
        {
            [Description(":zap:")]
            Overkill = 1, //MostKill
            [Description(":star_and_crescent:")]
            JikanLender = 2, //LongestPlayedTime
            [Description(":purple_heart:")]
            NanaBreaker = 3, //Gym
            [Description(":green_heart:")]
            ChocoRaider = 4, //Choco
            [Description(":cupid:")]
            HimeSlayer = 5, //Hoppo
            [Description(":mahjong:")]
            CyuuWarrior = 6, //Cyuuboss
            [Description(":dart:")]
            GendaiArcher = 7 //players with high headshot rate
        }
        public static Dictionary<string, List<Achievement>> UsersAchievement { get; set; }
    }
}
