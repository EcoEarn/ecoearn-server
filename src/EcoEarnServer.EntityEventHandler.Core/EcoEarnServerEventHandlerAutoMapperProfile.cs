using AutoMapper;
using EcoEarnServer.Entities;
using EcoEarnServer.PointsPool;
using EcoEarnServer.PointsSnapshot;
using EcoEarnServer.PointsStakeRewards;
using EcoEarnServer.Rewards;
using EcoEarnServer.Users;

namespace EcoEarnServer.EntityEventHandler.Core;

public class EcoEarnServerEventHandlerAutoMapperProfile : Profile
{
    public EcoEarnServerEventHandlerAutoMapperProfile()
    {
        CreateMap<UserGrainDto, UserIndex>();
        CreateMap<PointsSnapshotEto, PointsSnapshotIndex>();
        CreateMap<PointsPoolAddressStakeEto, PointsPoolAddressStakeIndex>();
        CreateMap<PointsPoolStakeSumEto, PointsPoolStakeSumIndex>();
        CreateMap<PointsStakeRewardsEto, PointsStakeRewardsIndex>();
        CreateMap<PointsStakeRewardsSumEto, PointsStakeRewardsSumIndex>();
        CreateMap<PointsPoolClaimRecordEto, PointsPoolClaimRecordIndex>();
        CreateMap<RewardOperationRecordEto, RewardOperationRecordIndex>();
        CreateMap<ClaimInfoDto, ClaimInfo>();
    }
}