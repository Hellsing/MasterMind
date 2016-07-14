using System;
using System.Linq;
using EloBuddy;
using SharpDX;

namespace MasterMind.Components.Wards
{
    public abstract class WardBase : IWard
    {
        public abstract string FriendlyName { get; }
        public abstract string BaseSkinName { get; }
        public abstract string DetectingBuffName { get; }
        public abstract string DetectingSpellCastName { get; }
        public virtual string DetectingObjectName
        {
            get { return string.Empty; }
        }
        public abstract WardTracker.Ward.Type Type { get; }

        public virtual bool Matches(Obj_AI_Base target)
        {
            return target.Type == GameObjectType.obj_AI_Minion
                   && target.BaseSkinName == BaseSkinName
                   && target.Buffs.Any(o => o.Name == DetectingBuffName);
        }

        public virtual bool MatchesBuffGain(Obj_AI_Base sender, Obj_AI_BaseBuffGainEventArgs args)
        {
            return sender.BaseSkinName == BaseSkinName
                   && args.Buff.Name == DetectingBuffName;
        }

        public virtual bool MatchesSpellCast(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            return String.Equals(args.SData.Name, DetectingSpellCastName, StringComparison.CurrentCultureIgnoreCase);
        }

        public virtual WardTracker.Ward CreateWard(AIHeroClient caster, Obj_AI_Base wardHandle)
        {
            // Wards cannot have more than 5 max health
            if (wardHandle.MaxHealth <= 5)
            {
                // Validate base skin
                if (!string.IsNullOrWhiteSpace(wardHandle.BaseSkinName) && wardHandle.BaseSkinName == BaseSkinName)
                {
                    // Return the ward object
                    return new WardTracker.Ward(caster, wardHandle, wardHandle.Position, this, GetWardDuration(caster), caster.Team);
                }
            }

            return null;
        }

        public virtual WardTracker.Ward CreateFakeWard(AIHeroClient caster, Vector3 position)
        {
            return new WardTracker.Ward(caster, null, position.GetValidCastSpot(), this, GetWardDuration(caster), caster.Team);
        }

        public virtual WardTracker.Ward CreateFakeWard(GameObjectTeam team, Vector3 position)
        {
            return new WardTracker.Ward(null, null, position.GetValidCastSpot(), this, -1, team);
        }

        public int GetWardDuration(AIHeroClient caster)
        {
            switch (Type)
            {
                case WardTracker.Ward.Type.SightWard:
                    return 150;
                case WardTracker.Ward.Type.YellowTrinket:
                    return (int) Math.Ceiling(60 + 3.5 * (caster.Level - 1));
            }

            return -1;
        }
    }
}
