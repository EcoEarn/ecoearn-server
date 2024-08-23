using System.Collections.Generic;
using EcoEarnServer.Rewards.Dtos;

namespace EcoEarnServer.Options;

public class PoolInfoOptions
{
    public Dictionary<string, PoolInfoDto> PoolInfoDic { get; set; }
}

public class PoolInfoDto
{
    public string PoolName { get; set; }
    public string FilterName { get; set; }
    public PoolTypeEnums PoolType { get; set; }
    public double Sort { get; set; }
    public bool SupportEarlyStake { get; set; }
}