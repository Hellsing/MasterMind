namespace MasterMind.Components.Wards
{
    public sealed class ControlWard : WardBase
    {
        public override string FriendlyName
        {
            get { return "Control Ward (Pink)"; }
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
            get { return WardTracker.Ward.Type.JammerDevice; }
        }
    }
}
