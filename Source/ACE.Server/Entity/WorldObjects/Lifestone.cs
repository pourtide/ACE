using ACE.Database.Models.Shard;
using ACE.Database.Models.World;
using ACE.DatLoader;
using ACE.DatLoader.FileTypes;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Network.GameEvent.Events;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.Network.Motion;

namespace ACE.Server.Entity.WorldObjects
{
    public class Lifestone : WorldObject
    {
        /// <summary>
        /// If biota is null, one will be created with default values for this WorldObject type.
        /// </summary>
        public Lifestone(Weenie weenie, Biota biota = null) : base(weenie, biota)
        {
            DescriptionFlags |= ObjectDescriptionFlag.LifeStone | ObjectDescriptionFlag.Stuck | ObjectDescriptionFlag.Attackable;

            SetProperty(PropertyBool.Stuck, true);
            SetProperty(PropertyBool.Attackable, true);

            SetProperty(PropertyInt.RadarBlipColor, (int)ACE.Entity.Enum.RadarColor.LifeStone);
        }

        public override void ActOnUse(ObjectGuid playerId)
        {
            // All data on a lifestone is constant -- therefore we just run in context of player
            Player player = CurrentLandblock.GetObject(playerId) as Player;
            string serverMessage = null;
            // validate within use range, taking into account the radius of the model itself, as well
            var csetup = DatManager.PortalDat.ReadFromDat<SetupModel>(SetupTableId.Value);
            float radiusSquared = (UseRadius.Value + csetup.Radius) * (UseRadius.Value + csetup.Radius);

            // Run this animation...
            // Player Enqueue:
            var motionSanctuary = new UniversalMotion(MotionStance.Standing, new MotionItem(MotionCommand.Sanctuary));

            var animationEvent = new GameMessageUpdateMotion(player.Guid,
                                                             player.Sequences.GetCurrentSequence(Network.Sequence.SequenceType.ObjectInstance),
                                                             player.Sequences, motionSanctuary);

            // This event was present for a pcap in the training dungeon.. Why? The sound comes with animationEvent...
            var soundEvent = new GameMessageSound(Guid, Sound.LifestoneOn, 1);

            if (player.Location.SquaredDistanceTo(Location) >= radiusSquared)
            {
                serverMessage = "You wandered too far to attune with the Lifestone!";
            }
            else
            {
                player.SetCharacterPosition(PositionType.Sanctuary, player.Location);

                // create the outbound server message
                serverMessage = "You have attuned your spirit to this Lifestone. You will resurrect here after you die.";
                player.HandleActionMotion(motionSanctuary);
                player.Session.Network.EnqueueSend(soundEvent);
            }

            var lifestoneBindMessage = new GameMessageSystemChat(serverMessage, ChatMessageType.Magic);
            // always send useDone event
            var sendUseDoneEvent = new GameEventUseDone(player.Session);
            player.Session.Network.EnqueueSend(lifestoneBindMessage, sendUseDoneEvent);
        }
    }
}
