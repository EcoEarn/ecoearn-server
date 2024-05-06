using System.Collections.Generic;

namespace EcoEarnServer.PointsStaking.Provider;

public class PointsPoolsIndexerDto
{
    public string DappId { get; set; }
    public string PoolId { get; set; }
    public string PointsName { get; set; }
    public string PoolAddress { get; set; }
    public string Amount { get; set; }
    public PointsPoolConfigDto PointsPoolConfig { get; set; }
    public long CreateTime { get; set; }
}

public class PointsPoolConfigDto
{
    public string RewardToken { get; set; }
    public long StartBlockNumber { get; set; }
    public long EndBlockNumber { get; set; }
    public long RewardPerBlock { get; set; }
    public long ReleasePeriod { get; set; }
    public string UpdateAddress { get; set; }
}

public class PointsPoolsQuery
{
    public PointsPoolsIndexerResult GetPointsPoolList { get; set; }
}

public class PointsPoolsIndexerResult
{
    public List<PointsPoolsIndexerDto> Data { get; set; }
}