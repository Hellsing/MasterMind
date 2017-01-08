namespace MasterMind.Components.Wards
{
    public sealed class Shroom : WardBase
    {
        public override string FriendlyName
        {
            get { return "Shroom"; }
        }
        public override string BaseSkinName
        {
            get { return "TeemoMushroom"; }
        }
        public override string DetectingBuffName
        {
            get { return "BantamTrap"; }
        }
        public override string DetectingSpellCastName
        {
            get { return "TeemoRCast"; }
        }
        public override WardTracker.Ward.Type Type
        {
            get { return WardTracker.Ward.Type.Shroom; }
        }
    }
}
