namespace EcoEarnServer.Background.Options;

public class SelfIncreaseRateOptions
{
    public long InviterKolFollowerRate { get; set; } = 9888000;
    public long KolFollowerRate { get; set; } = 4944000;
    public long KolFollowerInviteeRate { get; set; } = 1582080;
    public long InviteeRate { get; set; } = 4944000;
    public long SecondInviteeRate { get; set; } = 1582080;
}