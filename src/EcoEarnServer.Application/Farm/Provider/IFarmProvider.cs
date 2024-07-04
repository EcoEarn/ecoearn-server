using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using EcoEarnServer.Common.Dtos;
using EcoEarnServer.Common.GraphQL;
using EcoEarnServer.Common.HttpClient;
using EcoEarnServer.Farm.Dtos;
using EcoEarnServer.Options;
using EcoEarnServer.TokenStaking.Provider;
using GraphQL;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;

namespace EcoEarnServer.Farm.Provider;

public interface IFarmProvider
{
    Task<List<LiquidityInfoIndexerDto>> GetLiquidityInfoAsync(List<string> liquidityIds, string address,
        LpStatus lpStatus, int skipCount, int maxResultCount);

    Task<List<LpPriceItemDto>> GetAwakenLiquidityInfoAsync(string symbol0, string symbol1);
    Task<LiquidityInfoListIndexerResult> GetLiquidityListAsync(List<string> liquidityIds, string address,
        LpStatus lpStatus, int skipCount, int maxResultCount);
}

public class FarmProvider : IFarmProvider, ISingletonDependency
{
    private readonly IGraphQlHelper _graphQlHelper;
    private readonly ILogger<FarmProvider> _logger;
    private readonly IHttpProvider _httpProvider;
    private readonly LpPoolRateOptions _lpPoolRateOptions;

    public FarmProvider(IGraphQlHelper graphQlHelper, ILogger<FarmProvider> logger, IHttpProvider httpProvider,
        IOptionsSnapshot<LpPoolRateOptions> lpPoolRateOptions)
    {
        _graphQlHelper = graphQlHelper;
        _logger = logger;
        _httpProvider = httpProvider;
        _lpPoolRateOptions = lpPoolRateOptions.Value;
    }


    public async Task<List<LiquidityInfoIndexerDto>> GetLiquidityInfoAsync(List<string> liquidityIds, string address,
        LpStatus lpStatus, int skipCount, int maxResultCount)
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
                    liquidityIds = liquidityIds, lpStatus = lpStatus, skipCount = skipCount,
                    maxResultCount = maxResultCount, address = address
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

    public async Task<List<LpPriceItemDto>> GetAwakenLiquidityInfoAsync(string symbol0, string symbol1)
    {
        try
        {
            var resp = await _httpProvider.InvokeAsync<CommonResponseDto<LpPriceDto>>(HttpMethod.Get,
                _lpPoolRateOptions.LpPriceServer.LpPriceServerBaseUrl,
                param: new Dictionary<string, string>
                {
                    ["token0Symbol"] = symbol0,
                    ["token1Symbol"] = symbol1,
                    ["chainId"] = _lpPoolRateOptions.LpPriceServer.ChainId,
                }, header: null);
            var result = new List<LpPriceItemDto>();
            if (resp.Success && resp.Data != null)
            {
                result = resp.Data.Items;
            }

            return result;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[PriceDataProvider][GetLpPriceAsync] Parse response error.");
            return new List<LpPriceItemDto>();
        }
    }

    public async Task<LiquidityInfoListIndexerResult> GetLiquidityListAsync(List<string> liquidityIds, string address, LpStatus lpStatus, int skipCount, int maxResultCount)
    
    {
        if (string.IsNullOrEmpty(address))
        {
            return new LiquidityInfoListIndexerResult();
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
                    liquidityIds = liquidityIds, lpStatus = lpStatus, skipCount = skipCount,
                    maxResultCount = maxResultCount, address = address
                }
            });

            return indexerResult.GetLiquidityInfoList;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "getClaimInfoList Indexer error");
            return new LiquidityInfoListIndexerResult();
        }
    }
}