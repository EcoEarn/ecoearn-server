using System.Collections.Generic;

namespace EcoEarnServer.TokenStaking.Provider;

public class TokenPoolStakedInfoDto
{
    public string PoolId { get; set; }
    public string AccTokenPerShare { get; set; }
    public string TotalStakedAmount { get; set; }
    public long LastRewardTime { get; set; }
}
public class TokenPoolStakedInfoDtoResult
{
    public List<TokenPoolStakedInfoDto> Data { get; set; }
}

public class TokenPoolStakedInfoDtoListQuery
{
    public TokenPoolStakedInfoDtoResult GetTokenPoolStakeInfoList { get; set; }
}
