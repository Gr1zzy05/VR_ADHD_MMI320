namespace HomeTasks
{
    public sealed class Plushie : HomeItem
    {
        protected override bool AvailableFor(HomeQuest quest) => true;
    }
}
