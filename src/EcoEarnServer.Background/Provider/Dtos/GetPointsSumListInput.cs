using System;
using Volo.Abp.Application.Dtos;

namespace EcoEarnServer.Background.Provider.Dtos;

public class GetPointsSumListInput : PagedAndSortedResultRequestDto
{
    public DateTime BeforeTime { get; set; }
}