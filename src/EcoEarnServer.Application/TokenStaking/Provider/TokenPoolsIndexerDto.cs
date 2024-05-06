using System.Collections.Generic;

namespace EcoEarnServer.TokenStaking.Provider;

public class TokenPoolsIndexerDto
{
}

public class TokenPoolsQuery
{
    public TokenPoolsIndexerResult GetTokenPools { get; set; }
}

public class TokenPoolsIndexerResult
{
    public List<TokenPoolsIndexerDto> Data { get; set; }
}