namespace MasterMind.Components.Wards
{
    public sealed class BlueTrinket : WardBase
    {
        public override string FriendlyName
        {
            get { return "Farsight Alteration (Blue)"; }
        }
        public override string BaseSkinName
        {
            get { return "BlueTrinket"; }
        }
        public override string DetectingBuffName
        {
            get { return "relicblueward"; }
        }
        public override string DetectingSpellCastName
        {
            get { return "TrinketOrbLvl3"; }
        }
        public override string DetectingObjectName
        {
            get { return "Global_Trinket_ItemClairvoyance_Red.troy"; }
        }
        public override WardTracker.Ward.Type Type
        {
            get { return WardTracker.Ward.Type.BlueTrinket; }
        }
    }
}
