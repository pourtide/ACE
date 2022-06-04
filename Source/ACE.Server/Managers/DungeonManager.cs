using ACE.Common;
using ACE.Entity.Enum;
using ACE.Server.Network.GameMessages.Messages;
using log4net;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Timers;

namespace ACE.Server.Managers
{
    public static class DungeonManager
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private static Timer _workerThread;

        public static readonly ConcurrentDictionary<ushort, LandblockInformation> XpLandblocks = new ConcurrentDictionary<ushort, LandblockInformation>();

        public static void Initialize()
        {
            LoadSmallPopLandblocks();
            _workerThread = new Timer(1000 * 60 * 60);
            _workerThread.Elapsed += DoWork;
            _workerThread.AutoReset = true;
            _workerThread.Start();
        }
        private static List<LandblockInformation> FetchSmallPopLandblocks = new List<LandblockInformation>()
        {
            new LandblockInformation(0x0104, "Ayan BSD"),
            new LandblockInformation(0xE454, "Fort Aimaru"),
        };

        private static List<LandblockInformation> FetchMedPopLandblocks = new List<LandblockInformation>()
        {
            new LandblockInformation(0x02F1, "Qalabar Citadel"),
        };

        private static List<LandblockInformation> FetchLargePopLandblocks = new List<LandblockInformation>()
        {
            new LandblockInformation(0x0103, "Obsidian Plains BSD"),
        };

        private static void DoWork(Object source, ElapsedEventArgs e)
        {
            var pop = PlayerManager.GetOnlineCount();

            if (pop >= 30)
            {
                LoadLargePopLandblocks();
            } else if (pop >= 15)
            {
                LoadMedPopLandblocks();
            } else
            {
                LoadSmallPopLandblocks();
            }
        }
        private static void LoadSmallPopLandblocks()
        {
            var blacklisted = FetchLargePopLandblocks.Concat(FetchMedPopLandblocks);

            foreach(var landblock in blacklisted)
            {
                XpLandblocks.TryRemove(landblock.Block, out _);
            }

            foreach(var landblock in FetchSmallPopLandblocks)
            {
                XpLandblocks.TryAdd(landblock.Block, landblock);
            }
        }

        private static void LoadMedPopLandblocks()
        {
            var blacklisted = FetchLargePopLandblocks;

            foreach(var landblock in blacklisted)
            {
                XpLandblocks.TryRemove(landblock.Block, out _);
            }

            foreach(var landblock in FetchSmallPopLandblocks.Concat(FetchMedPopLandblocks))
            {
                XpLandblocks.TryAdd(landblock.Block, landblock);
            }

        }

        private static void LoadLargePopLandblocks()
        {
            foreach(var landblock in FetchSmallPopLandblocks.Concat(FetchMedPopLandblocks).Concat(FetchLargePopLandblocks))
            {
                XpLandblocks.TryAdd(landblock.Block, landblock);
            }
        }
    }

    public class LandblockInformation
    {
        public ushort Block;
        public string Name;
        public LandblockInformation(ushort block, string name)
        {
            Block = block;
            Name = name;
        }
    }
}
