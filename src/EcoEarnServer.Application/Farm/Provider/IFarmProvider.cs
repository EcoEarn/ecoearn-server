using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EcoEarnServer.Common.GraphQL;
using EcoEarnServer.Farm.Dtos;
using GraphQL;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace EcoEarnServer.Farm.Provider;

public interface IFarmProvider
{
    Task<List<LiquidityInfoIndexerDto>> GetLiquidityInfoAsync(List<string> liquidityIds, string address, LpStatus lpStatus, int skipCount, int maxResultCount);
}

public class FarmProvider : IFarmProvider, ISingletonDependency
{
    private readonly IGraphQlHelper _graphQlHelper;
    private readonly ILogger<FarmProvider> _logger;

    public FarmProvider(IGraphQlHelper graphQlHelper, ILogger<FarmProvider> logger)
    {
        _graphQlHelper = graphQlHelper;
        _logger = logger;
    }


    public async Task<List<LiquidityInfoIndexerDto>> GetLiquidityInfoAsync(List<string> liquidityIds, string address, LpStatus lpStatus, int skipCount, int maxResultCount)
    {
        if (!liquidityIds.Any() && string.IsNullOrEmpty(address))
        {
            return new List<LiquidityInfoIndexerDto>();
        }

        try
        {
            var indexerResult = await _graphQlHelper.QueryAsync<LiquidityInfoListIndexerQuery>(new GraphQLRequest
            {
                Query =
                    @"query($liquidityIds:[String!]!, $lpStatus:LpStatus!, $address:String!, $skipCount:Int!,$maxResultCount:Int!){
                    getLiquidityInfoList(input: {liquidityIds:$liquidityIds,lpStatus:$lpStatus,address:$address,skipCount:$skipCount,maxResultCount:$maxResultCount}){
                        totalCount,
                        data{
                        liquidityId,
                        stakeId,
                        seed,
                        lpAmount,
                        lpSymbol,
                        rewardSymbol,
                        tokenAAmount,
    					tokenASymbol,
    					tokenBAmount,
    					tokenBSymbol,
    					addedTime,
    					removedTime,
    					dappId,
    					swapAddress,
    					tokenAddress,
    					tokenALossAmount,
    					tokenBLossAmount,
    					lpStatus,
                    }
                }
            }",
                Variables = new
                {
                    liquidityIds = liquidityIds, lpStatus = lpStatus, skipCount = skipCount, maxResultCount = maxResultCount,
                }
            });

            return indexerResult.GetLiquidityInfoList.Data;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "getClaimInfoList Indexer error");
            return new List<LiquidityInfoIndexerDto>();
        }
    }
}