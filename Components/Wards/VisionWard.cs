namespace MasterMind.Components.Wards
{
    public sealed class VisionWard : WardBase
    {
        public override string FriendlyName
        {
            get { return "Vision Ward (Pink)"; }
        }
        public override string BaseSkinName
        {
            get { return "JammerDevice"; }
        }
        public override string DetectingBuffName
        {
            get { return "JammerDevice"; }
        }
        public override string DetectingSpellCastName
        {
            get { return "JammerDevice"; }
        }
        public override WardTracker.Ward.Type Type
        {
            get { return WardTracker.Ward.Type.VisionWard; }
        }
    }
}
