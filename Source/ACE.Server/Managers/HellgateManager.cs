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
using System.Linq;
using System.Timers;

namespace ACE.Server.Managers
{
    public static class HellgateManager
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private static Timer _workerThread;

        private static TimeSpan HellgateTimeLimit;

        private static DateTime HellgateStartTime;

        public static List<HellGatePortal> Portals = new List<HellGatePortal>()
        {
            new HellGatePortal(
                "0xA9B4001A [94.517273 25.603945 94.005005] 0.308922 0.000000 0.000000 0.951087",
                "Holtburg"),
            new HellGatePortal(
                "0xBF800036 [167.457947 143.992157 36.095345] 0.775577 0.000000 0.000000 -0.631253",
                "Lytelthorpe"),
            new HellGatePortal(
                "0xC98D0021 [108.402016 16.683725 22.004999] -0.059218 0.000000 0.000000 0.998245",
                "Rithwic"),
            new HellGatePortal(
                "0xB470001A [89.616470 34.554600 42.005001] 0.022091 0.000000 0.000000 -0.999756",
                "Yanshi"),
            new HellGatePortal(
                "0xDA55001E [94.649506 135.290161 20.004999] -0.400765 0.000000 0.000000 -0.916181",
                "Shoushi"),
            new HellGatePortal(
                "0xE63E0021 [109.439323 1.675783 82.706001] -0.015213 0.000000 0.000000 0.999884",
                "Nanto"),
            new HellGatePortal(
                "0x7D640015 [52.048855 111.858696 12.004999] -0.421327 0.000000 0.000000 -0.906909",
                "Yaraq"),
            new HellGatePortal(
                "0x977B000D [43.508862 100.376144 0.005000] 0.952885 0.000000 0.000000 -0.303332",
                "Samsur"),
            new HellGatePortal(
                "0x90580003 [12.683186 53.629936 8.948068] -0.138752 0.000000 0.000000 0.990327",
                "Al-Arqas")
        };

        private static List<uint> PortalWcids = new List<uint>()
        {
            3000100,
            3000101,
            3000102,
            3000103,
        };

        private static List<WorldObject> PortalInstances = new List<WorldObject>();

        private static  ConcurrentDictionary<string, Player> PlayersInHellgate = new ConcurrentDictionary<string, Player>();

        private enum HellgateState
        {
            Open, Closed, InProgress
        }

        private static HellgateState State = HellgateState.Closed;

        public static void AddPlayer(Player player)
        {
            if (PlayersInHellgate.TryAdd(player.Name, player))
            {
                log.Info($"Player: {player.Name} has been added to the hellgate");
                log.Info($"Player count: {PlayerCount}");
            }
        }

        public static void RemovePlayer(Player player)
        {
            if (PlayersInHellgate.TryRemove(player.Name, out player))
            {
                log.Info($"Player: {player.Name} has been removed from the hellgate");
                log.Info($"Player count: {PlayerCount}");
                WorldManager.ThreadSafeTeleport(player, new Position(player.Sanctuary));
                EndHellgate();
            }
        }

        public static bool ContainsPlayer(Player player)
        {
            return PlayersInHellgate.ContainsKey(player.Name);
        }

        public static int PlayerCount { get => PlayersInHellgate.Count; }

        public static int MaxPlayers = 6;

        public static void Initialize()
        {
            _workerThread = new Timer(1000 * 60 * 5);
            _workerThread.Elapsed += DoWork;
            _workerThread.AutoReset = true;
            _workerThread.Start();

            HandleIsClosed();
        }

        private static void DeletePortalInstances()
        {
            foreach (var portal in PortalInstances)
            {
                WorldManager.EnqueueAction(new ActionEventDelegate(() => portal.DeleteObject()));
            }

            PortalInstances.Clear();
        }

        private static void CreatePortalInstances()
        {
            DeletePortalInstances();

            // quick and dirty shuffle
            var portals = Portals.OrderBy(i => Guid.NewGuid()).ToList();

            for (var i = 0; i < PortalWcids.Count(); i++)
            {
                var wo = WorldObjectFactory.CreateNewWorldObject(PortalWcids[i]);
                wo.Location = WorldManager.LocToPosition(portals[i].Location);
                wo.Lifespan = 60 * 5; // Hellgate portals are only up for 5 minutes
                PortalInstances.Add(wo);

                WorldManager.EnqueueAction(new ActionEventDelegate(() => wo.EnterWorld())); 
            }

            var message = $"The Current hellgates spawned are: {String.Join(", ", portals.Select(d => d.TownName).Take(PortalWcids.Count))}";

            log.Info(message);
            PlayerManager.BroadcastToAll(new GameMessageSystemChat(message, ChatMessageType.WorldBroadcast));
        }

        private static void DoWork(Object source, ElapsedEventArgs e)
        {
            foreach (var portal in PortalInstances)
            {
                if (portal.IsLifespanSpent)
                {
                    PortalInstances.Remove(portal);
                }
            }

            if (State == HellgateState.InProgress)
            {
                HandleInProgress();
            } else if (State == HellgateState.Closed)
            {
                HandleIsClosed();
            } else if (State == HellgateState.Open)
            {
                HandleIsOpen();
            }
        }

        private static void HandleIsOpen()
        {
            log.Info("handleisopen");

            DeletePortalInstances();

            HellgateStartTime = DateTime.UtcNow;
            HellgateTimeLimit = TimeSpan.FromMinutes(30);
            State = HellgateState.InProgress;
        }

        private static void HandleIsClosed()
        {
            log.Info("handleisclosed");

            var isEmpty = PlayersInHellgate.IsEmpty;

            if (PortalInstances.Count < 1 && isEmpty)
            {
                CreatePortalInstances();
            }

            if (!isEmpty)
            {
                State = HellgateState.Open;
            }
        }

        public static void EndHellgate()
        {
            if (PlayersInHellgate.IsEmpty)
                HandleInProgress(true);
        }

        private static void HandleInProgress(bool force = false)
        {
            log.Info("handleinprogress");
            var hasEnded = CheckIfTimeExpired();

            if (hasEnded || force || PlayersInHellgate.IsEmpty)
            {
                foreach (var player in PlayersInHellgate)
                {
                    WorldManager.ThreadSafeTeleport(player.Value, player.Value.Sanctuary);
                }

                PlayersInHellgate.Clear();
                State = HellgateState.Closed;
            }
        }

        private static bool CheckIfTimeExpired()
        {
            var now = DateTime.UtcNow;
            var expired = (now - HellgateStartTime);
            var limit = HellgateTimeLimit;

            log.Info("Hellgate time has expired");
            log.Info(expired);
            log.Info(now);
            log.Info(expired.TotalMinutes);
            log.Info(expired.TotalMinutes > limit.TotalMinutes);
            log.Info(limit.TotalMinutes);

            if (expired.TotalMinutes > HellgateTimeLimit.TotalMinutes)
            {
                return true;
            }

            return false;
        }

        public class HellGatePortal
        {
            public string Location;
            public string TownName;
            public HellGatePortal(string location, string name)
            {
                Location = location;
                TownName = name;
            }
        }
    }


}
