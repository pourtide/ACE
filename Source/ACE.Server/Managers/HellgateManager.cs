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

namespace ACE.Server.Managers
{
    public static class HellgateManager
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private static Stopwatch HellgateDungeonInterval = new Stopwatch();

        public static int MaxPlayers = 12;

        public static HellgateState State { get; private set; } = HellgateState.Closed;

        private static ConcurrentDictionary<uint, WorldObject> PortalInstances = new ConcurrentDictionary<uint, WorldObject>();

        public static ConcurrentDictionary<string, HellGatePortal> OpenPortals = new ConcurrentDictionary<string, HellGatePortal>();

        public static ConcurrentDictionary<string, Player> PlayersInHellgate = new ConcurrentDictionary<string, Player>();

        public static void Initialize()
        {
        }

        internal static void Tick()
        {
            if (State == HellgateState.Shutdown)
                return;

            DoWork();
        }

        public static readonly List<uint> HellgatePortalWcids = new()
        {
            3000100,
            3000101,
            3000102,
            3000103,
        };

        public enum HellgateState
        {
            Open, Closed, Shutdown
        }

        private static void DoWork()
        {
            switch (State)
            {
                case HellgateState.Closed:
                    HandleIsClosed();
                    break;
                case HellgateState.Open:
                    HandleIsOpen();
                    break;
            }
        }

        public static void Shutdown()
        {
            State = HellgateState.Shutdown;
            CloseHellgate();
        }

        private static void HandleIsOpen()
        {
            var isExpired = HellgateDungeonInterval.ElapsedMilliseconds >= 1000 * 60 * 30;

            if (isExpired)
                CloseHellgate();
        }

        private static void HandleIsClosed()
        {
            var hasPlayers = PlayersInHellgate.Count > 0;
            var hasPortals = PortalInstances.Count > 0;

            if (!hasPortals && !hasPlayers)
                OpenHellgate();
        }

        private static void OpenHellgate()
        {
            State = HellgateState.Open;

            foreach (var player in PlayerManager.GetAllOnline())
            {
                if (player.IsInHellgate)
                {
                    WorldManager.ThreadSafeTeleport(player, new Position(player.Sanctuary));
                }
            }

            PlayerManager.BroadcastToAll(new GameMessageSystemChat("Hellgate is now open!", ChatMessageType.WorldBroadcast));
            DeletePortalInstances();
            CreatePortalInstances();
            PlayersInHellgate.Clear();
            HellgateDungeonInterval.Start();
        }

        private static void CloseHellgate()
        {
            State = HellgateState.Closed;

            foreach (var player in PlayerManager.GetAllOnline())
            {
                if (player.IsInHellgate)
                {
                    WorldManager.ThreadSafeTeleport(player, new Position(player.Sanctuary));
                }
            }

            PlayerManager.BroadcastToAll(new GameMessageSystemChat("Hellgate is now closing. New portals will open shortly", ChatMessageType.WorldBroadcast));
            DeletePortalInstances();
            PlayersInHellgate.Clear();
            HellgateDungeonInterval.Reset();
        }

        private static void CreatePortalInstances()
        {
            // quick and dirty shuffle
            var portals = Portals.OrderBy(i => Guid.NewGuid()).ToList();

            for (var i = 0; i < HellgatePortalWcids.Count(); i++)
            {
                var wcid = HellgatePortalWcids[i];
                var portal = portals[i];
                var wo = WorldObjectFactory.CreateNewWorldObject(wcid);
                wo.Location = WorldManager.LocToPosition(portal.Location);
                wo.Lifespan = 60 * 30; // Hellgate portals are only up for 30 minutes
                PortalInstances.TryAdd(wo.WeenieClassId, wo);
                OpenPortals.TryAdd(portal.Location.Substring(2, 8), portal);

                WorldManager.EnqueueAction(new ActionEventDelegate(() => wo.EnterWorld()));
            }

            var message = $"The current open hellgate portals are: {String.Join(", ", OpenPortals.Select(d => d.Value.TownName))}.";

            PlayerManager.BroadcastToAll(new GameMessageSystemChat(message, ChatMessageType.WorldBroadcast));
        }

        public static void RemoveOpenPortal(string locationString)
        {
            var location = locationString.Substring(2, 8); // we only need the hex value

            if (OpenPortals.TryGetValue(location, out var portal))
            {
                OpenPortals.TryRemove(location, out _);
                PlayerManager.BroadcastToAll(new GameMessageSystemChat($"Hellgate portal in {portal.TownName} has closed.", ChatMessageType.WorldBroadcast));
            }
        }

        public static void AddPlayer(Player player)
        {
            if (PlayersInHellgate.TryAdd(player.Name, player))
            {
                var message = $"Player: {player.Name} has entered the hellgate.";
                log.Info(message);
                log.Info($"Player count: {PlayersInHellgate.Count}");
                PlayerManager.BroadcastToAll(new GameMessageSystemChat(message, ChatMessageType.WorldBroadcast));
            }
        }

        public static void RemovePlayer(Player player)
        {
            if (PlayersInHellgate.TryRemove(player.Name, out player))
            {
                var message = $"Player: {player.Name} has left the hellgate.";
                log.Info(message);
                log.Info($"Player count: {PlayersInHellgate.Count}");
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

        private static void DeletePortalInstances()
        {
            foreach (var portal in PortalInstances.ToList())
            {
                WorldManager.EnqueueAction(new ActionEventDelegate(() => portal.Value.DeleteObject()));
            }

            PortalInstances.Clear();
            OpenPortals.Clear();
        }

        public static string HellgateTimeRemaining()
        {
            var span = TimeSpan.FromMilliseconds((1000 * 60 * 30) - HellgateDungeonInterval.ElapsedMilliseconds);
            return string.Format("{0}:{1:00}", (int)span.TotalMinutes, span.Seconds);
        }

        private static readonly List<HellGatePortal> Portals = new List<HellGatePortal>()
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
