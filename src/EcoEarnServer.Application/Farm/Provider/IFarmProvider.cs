using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using AElf.ExceptionHandler;
using EcoEarnServer.Common.Dtos;
using EcoEarnServer.Common.GraphQL;
using EcoEarnServer.Common.HttpClient;
using EcoEarnServer.ExceptionHandle;
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


    [ExceptionHandler(typeof(Exception), TargetType = typeof(ExceptionHandlingService),
        ReturnDefault = ReturnDefault.New, MethodName = nameof(ExceptionHandlingService.HandleException),
        Message = "GetClaimInfoList Indexer error")]
    public async Task<List<LiquidityInfoIndexerDto>> GetLiquidityInfoAsync(List<string> liquidityIds, string address,
        LpStatus lpStatus, int skipCount, int maxResultCount)
    {
        if (!liquidityIds.Any() && string.IsNullOrEmpty(address))
        {
            return new List<LiquidityInfoIndexerDto>();
        }

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
    					removedTime,
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

    [ExceptionHandler(typeof(Exception), TargetType = typeof(ExceptionHandlingService),
        ReturnDefault = ReturnDefault.New, MethodName = nameof(ExceptionHandlingService.HandleException),
        Message = "GetAwakenLiquidityInfo error")]
    public async Task<List<LpPriceItemDto>> GetAwakenLiquidityInfoAsync(string symbol0, string symbol1)
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

    [ExceptionHandler(typeof(Exception), TargetType = typeof(ExceptionHandlingService),
        ReturnDefault = ReturnDefault.New, MethodName = nameof(ExceptionHandlingService.HandleException),
        Message = "GetLiquidityList Indexer error")]
    public async Task<LiquidityInfoListIndexerResult> GetLiquidityListAsync(List<string> liquidityIds, string address,
        LpStatus lpStatus, int skipCount, int maxResultCount)
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
    					address,
    					swapAddress,
    					tokenAddress,
    					tokenALossAmount,
    					tokenBLossAmount,
    					lpStatus,
    					removedTime,
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
}