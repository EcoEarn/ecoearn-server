using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace EcoEarnServer.Background.Provider.Dtos;

public class GetPointsSumListInput : PagedAndSortedResultRequestDto
{
    public DateTime EndTime { get; set; }
}

public class GetRelationShipInput
{
    public List<string> AddressList { get; set; }
    public string ChainId { get; set; }
    public string DappId { get; set; }
}