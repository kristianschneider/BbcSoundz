namespace BbcSoundz.Configuration
{
    public class BbcScheduleSource
    {
        public string Name { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    public class AppSettings
    {
        public List<BbcScheduleSource> BbcScheduleSources { get; set; } = new();
    }
}
