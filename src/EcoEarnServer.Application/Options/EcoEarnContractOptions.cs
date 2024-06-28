namespace EcoEarnServer.Options;

public class EcoEarnContractOptions
{
    public string EcoEarnContractAddress { get; set; }
    public string EcoEarnRewardsContractAddress { get; set; }
    public string CAContractAddress { get; set; }
    public double MergeMilliseconds { get; set; } = 0.02083;
}