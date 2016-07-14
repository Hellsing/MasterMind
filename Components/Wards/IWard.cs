using EloBuddy;
using SharpDX;

namespace MasterMind.Components.Wards
{
    public interface IWard
    {
        string FriendlyName { get; }
        string BaseSkinName { get; }
        string DetectingBuffName { get; }
        string DetectingSpellCastName { get; }
        string DetectingObjectName { get; }
        WardTracker.Ward.Type Type { get; }

        bool Matches(Obj_AI_Base target);
        bool MatchesBuffGain(Obj_AI_Base sender, Obj_AI_BaseBuffGainEventArgs args);
        bool MatchesSpellCast(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args);
        WardTracker.Ward CreateWard(AIHeroClient caster, Obj_AI_Base wardHandle);
        WardTracker.Ward CreateFakeWard(AIHeroClient caster, Vector3 position);
        WardTracker.Ward CreateFakeWard(GameObjectTeam team, Vector3 position);
    }
}
