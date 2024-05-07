using System.Collections.Generic;
using System.Threading.Tasks;
using EcoEarnServer.PointsStaking.Dtos;

namespace EcoEarnServer.PointsStaking;

public interface IPointsStakingService
{
    Task<List<ProjectItemListDto>> GetProjectItemListAsync();
    Task<List<PointsPoolsDto>> GetPointsPoolsAsync(GetPointsPoolsInput input);
    Task<ClaimAmountSignatureDto> ClaimAmountSignatureAsync(ClaimAmountSignatureInput input);
    Task<string> ClaimAsync(PointsClaimInput input);
    Task<EarlyStakeInfoDto> GetEarlyStakeInfoAsync(GetEarlyStakeInfoInput input);
}