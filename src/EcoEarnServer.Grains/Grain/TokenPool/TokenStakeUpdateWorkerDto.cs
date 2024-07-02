namespace EcoEarnServer.Grains.Grain.TokenPool;

public class TokenStakeUpdateWorkerDto
{
    public string Id { get; set; }
    public string StakeId { get; set; }
    public string JobId { get; set; }
    public long ExecuteTime { get; set; }
    public long CreateTime { get; set; }
}