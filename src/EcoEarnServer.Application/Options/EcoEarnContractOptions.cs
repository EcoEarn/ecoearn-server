namespace EcoEarnServer.Options;

public class EcoEarnContractOptions
{
    public string EcoEarnContractAddress { get; set; }
    public string EcoEarnRewardsContractAddress { get; set; }
    public string CAContractAddress { get; set; }
    public long MergeMilliseconds { get; set; } = 5 * 24 * 60 * 60 * 1000;
}