using System.Collections.Generic;

namespace EcoEarnServer.Background.Options;

public class PointsPoolOptions
{
    public Dictionary<string, PointsPoolInfo> PointsPoolDictionary { get; set; }
}

public class PointsPoolInfo
{
    public string Index { get; set; }
    public string PoolId { get; set; }
    public string PoolName { get; set; }
}