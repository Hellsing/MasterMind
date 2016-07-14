namespace MasterMind.Components.Wards
{
    public sealed class SightWard : WardBase
    {
        public override string FriendlyName
        {
            get { return "Sight Ward (Green)"; }
        }
        public override string BaseSkinName
        {
            get { return "SightWard"; }
        }
        public override string DetectingBuffName
        {
            get { return "sharedwardbuff"; }
        }
        public override string DetectingSpellCastName
        {
            get { return "ItemGhostWard"; }
        }
        public override WardTracker.Ward.Type Type
        {
            get { return WardTracker.Ward.Type.SightWard; }
        }
    }
}
