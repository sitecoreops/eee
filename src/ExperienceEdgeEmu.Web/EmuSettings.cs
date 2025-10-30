namespace ExperienceEdgeEmu.Web;

public class EmuSettings
{
    public static readonly string Key = "Emu";
    public string DataRootPath { get; set; } = "./data";
    public string MediaHost { get; set; } = "https://localhost:5711";
    public CrawlerSettings Crawler { get; set; } = new CrawlerSettings(2);
}

public record CrawlerSettings(int MaxDegreeOfParallelism);
