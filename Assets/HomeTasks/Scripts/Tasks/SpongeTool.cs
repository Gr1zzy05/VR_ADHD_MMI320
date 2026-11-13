namespace HomeTasks
{
    public sealed class SpongeTool : HomeItem
    {
        protected override bool AvailableFor(HomeQuest quest) => quest == HomeQuest.Washing;
        public bool DesktopScrubbing { get; set; }
    }
}
