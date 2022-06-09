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
using System.Timers;

namespace ACE.Server.Managers
{
    public static class HellgateManager
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private static Thread _workerThread;

        private static Stopwatch HellgateWorkerInterval;

        private static Stopwatch HellgateDungeonInterval;

        private static Stopwatch HellgatePortalLifespanInterval;

        public static void Initialize()
        {
            _workerThread = new Thread(new ThreadStart(DoWork));
            _workerThread.Start();
        }

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
                var message = $"Player: {player.Name} has entered the hellgate";
                log.Info(message);
                log.Info($"Player count: {PlayerCount}");
                PlayerManager.BroadcastToAll(new GameMessageSystemChat(message, ChatMessageType.WorldBroadcast));
            }
        }

        public static void RemovePlayer(Player player)
        {
            if (PlayersInHellgate.TryRemove(player.Name, out player))
            {
                var message = $"Player: {player.Name} has left the hellgate";
                log.Info(message);
                log.Info($"Player count: {PlayerCount}");
                WorldManager.ThreadSafeTeleport(player, new Position(player.Sanctuary));
                PlayerManager.BroadcastToAll(new GameMessageSystemChat(message, ChatMessageType.WorldBroadcast));

                if (PlayersInHellgate.IsEmpty)
                    CloseHellgate();
            }
        }

        public static bool ContainsPlayer(Player player)
        {
            return PlayersInHellgate.ContainsKey(player.Name);
        }

        public static int PlayerCount { get => PlayersInHellgate.Count; }

        public static int MaxPlayers = 12;

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

            HellgatePortalLifespanInterval.Start();
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

        private static void DoWork()
        {
            HellgateWorkerInterval = new Stopwatch();
            HellgateDungeonInterval = new Stopwatch();
            HellgatePortalLifespanInterval = new Stopwatch();
            HellgateWorkerInterval.Start();

            CloseHellgate();

            while (true)
            {
                if (HellgateWorkerInterval.ElapsedMilliseconds > 1000 * 60)
                {
                    CheckPortalLifespans();

                    switch (State)
                    {
                        case HellgateState.Closed:
                            HandleIsClosed();
                            break;
                        case HellgateState.Open:
                            HandleIsOpen();
                            break;
                    }

                    HellgateWorkerInterval.Restart();
                }
            }
        }

        private static void CheckPortalLifespans()
        {
            foreach (var portal in PortalInstances.ToList())
            {
                if (portal.IsLifespanSpent)
                    PortalInstances.Remove(portal);
            }
        }

        private static void HandleIsOpen()
        {
            log.Info("handleisopen");

            // If portals have been up for 5 minutes delete them
            if (HellgatePortalLifespanInterval.ElapsedMilliseconds > 1000 * 60 * 5)
            {
                DeletePortalInstances();
                HellgatePortalLifespanInterval.Reset();
            }

            if (HellgateDungeonInterval.ElapsedMilliseconds >= 1000 * 60 * 30)
                CloseHellgate();

            if (PortalInstances.Count < 1 && PlayersInHellgate.Count < 1)
                CloseHellgate();

        }

        private static void CloseHellgate()
        {

            PlayerManager.BroadcastToAll(new GameMessageSystemChat("Hellgate is now closing. New portals will open shortly", ChatMessageType.WorldBroadcast));
            foreach (var player in PlayerManager.GetAllOnline())
            {
                if (player.IsInHellgate)
                {
                    RemovePlayer(player);
                }
            }

            DeletePortalInstances();
            PlayersInHellgate.Clear();
            HellgateDungeonInterval.Reset();
            State = HellgateState.Closed;
        }

        private static void OpenHellgate()
        {
            PlayerManager.BroadcastToAll(new GameMessageSystemChat("Hellgate is now open!", ChatMessageType.WorldBroadcast));
            CreatePortalInstances();
            HellgateDungeonInterval.Start();
            State = HellgateState.Open;
        }

        private static void HandleIsClosed()
        {
            log.Info("handleisclosed");

            var hasPlayers = PlayersInHellgate.Count > 0;
            var hasPortals = PortalInstances.Count > 0;

            if (!hasPortals && !hasPlayers)
            {
                OpenHellgate();
            }
        }

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
