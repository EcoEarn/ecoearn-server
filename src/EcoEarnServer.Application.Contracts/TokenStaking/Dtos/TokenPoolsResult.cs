using System.Collections.Generic;

namespace EcoEarnServer.TokenStaking.Dtos;

public class TokenPoolsResult
{
    public List<TokenPoolsDto> Pools { get; set; }
    public List<TextNodeDto> TextNodes { get; set; }
}

public class TextNodeDto
{
    public string TextWord { get; set; }
    public List<TextNodeDto> ChildTextNodes { get; set; }
}