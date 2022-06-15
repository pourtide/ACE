using ACE.Common;
using ACE.Entity.Enum;
using ACE.Server.Network.GameMessages.Messages;
using log4net;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Timers;

namespace ACE.Server.Managers.Pourtide
{
    public static class RadiationManager
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public static void Tick()
        {
            DoWork();
        }

        private static void DoWork()
        {
            foreach (var player in PlayerManager.GetAllOnline())
            {
                // lose 50% of your health on ever tick outside of xp landblock
                if (!player.IsOnXpLandblock)
                {
                    player.TakeDamageOverTime(player.Health.MaxValue / 2, DamageType.Health); 
                }
            }
        }
    }
}
