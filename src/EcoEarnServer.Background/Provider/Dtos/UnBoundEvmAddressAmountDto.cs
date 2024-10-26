using System.Collections.Generic;

namespace EcoEarnServer.Background.Provider.Dtos;

public class UnBoundEvmAddressAmountDto
{
    public List<RemainPointDto> RemainPointList { get; set; }
}

public class RemainPointDto
{
    public string Points { get; set; } 
    public string Address { get; set; } 
}