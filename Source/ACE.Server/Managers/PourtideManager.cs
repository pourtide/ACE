using ACE.Common;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Server.Entity.Actions;
using ACE.Server.Factories;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using log4net;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace ACE.Server.Managers
{
    public static class PourtideManager
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private static Timer _workerThread;

        private static Stopwatch PourtideWorkerInterval = new Stopwatch();

        public static void Initialize()
        {
            PourtideWorkerInterval.Start();

            log.Info("Initializing HellgateManager...");
            HellgateManager.Initialize();

            log.Info("Initializing DungeonManager...");
            DungeonManager.Initialize();

            _workerThread = new Timer((_) =>
            {
                DoWork();
                _workerThread.Change(1000 * 60, Timeout.Infinite);
            }, null, 0, Timeout.Infinite);

        }

        private static void DoWork()
        {
            var elapsedMiliseconds = PourtideWorkerInterval.ElapsedMilliseconds;
            if (elapsedMiliseconds >= 1000 * 60) // tick every minute
            {
                HellgateManager.Tick();
                DungeonManager.Tick();

            }

            PourtideWorkerInterval.Restart();
        }
    }
}
