namespace EcoEarnServer.Grains.Grain.TokenPool;

[GenerateSerializer]
public class TokenStakeUpdateWorkerDto
{
    [Id(0)]
    public string Id { get; set; }
    [Id(1)]
    public string StakeId { get; set; }
    [Id(2)]
    public string JobId { get; set; }
    [Id(3)]
    public long ExecuteTime { get; set; }
    [Id(4)]
    public long CreateTime { get; set; }
}