namespace MasterMind.Components.Wards
{
    public sealed class YellowTrinket : WardBase
    {
        public override string FriendlyName
        {
            get { return "Warding Totem (Yellow)"; }
        }
        public override string BaseSkinName
        {
            get { return "YellowTrinket"; }
        }
        public override string DetectingBuffName
        {
            get { return "sharedwardbuff"; }
        }
        public override string DetectingSpellCastName
        {
            get { return "TrinketTotemLvl1"; }
        }
        public override WardTracker.Ward.Type Type
        {
            get { return WardTracker.Ward.Type.YellowTrinket; }
        }
    }
}
