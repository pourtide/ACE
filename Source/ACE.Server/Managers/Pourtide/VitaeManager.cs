using ACE.Common;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Factories;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using log4net;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Timers;

namespace ACE.Server.Managers.Pourtide
{
    public static class VitaeManager
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public static readonly ConcurrentDictionary<string, int> VitaePlayerMap = new ConcurrentDictionary<string, int>();

        private static Stopwatch VitaeInterval = new Stopwatch();

        public static void Initialize()
        {
            VitaeInterval.Start();
        }

        public static void Tick()
        {
            if (VitaeInterval.ElapsedMilliseconds >= 1000 * 60 * 120)
            {
                DoWork();
                VitaeInterval.Restart();
            }

        }

        private static void DoWork()
        {
            VitaePlayerMap.Clear();
        }

        public static bool GivePlayerVitaeRemovalGem(Player player) 
        {
            if (VitaePlayerMap.TryGetValue(player.Name, out int value))
            {
                if (value >= 3)
                    return false;

                VitaePlayerMap.TryUpdate(player.Name, value + 1, value);
            } else
            {
                VitaePlayerMap.TryAdd(player.Name, 1);
            }

            var wo = WorldObjectFactory.CreateNewWorldObject(5000101);
            return player.TryAddToInventory(wo);
        }
    }
}
