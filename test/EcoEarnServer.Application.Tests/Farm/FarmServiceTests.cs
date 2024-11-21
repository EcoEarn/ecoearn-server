using System.Collections.Generic;
using System.Threading.Tasks;
using EcoEarnServer.Farm.Dtos;
using EcoEarnServer.Farm.Provider;
using EcoEarnServer.Options;
using EcoEarnServer.Rewards.Provider;
using EcoEarnServer.TokenStaking.Provider;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace EcoEarnServer.Farm;

public class FarmServiceTests : EcoEarnServerApplicationTestBase
{
    private readonly IFarmService _farmService;

    public FarmServiceTests(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
    {
        _farmService = GetRequiredService<IFarmService>();
    }

    protected override void AfterAddApplication(IServiceCollection services)
    {
        base.AfterAddApplication(services);
        services.AddSingleton(MockIFarmProvider());
        services.AddSingleton(MockIRewardsProvider());
        services.AddSingleton(MockLpPoolRateOptions());
        services.AddSingleton(MockIPriceProvider());
        services.AddSingleton(MockITokenStakingProvider());
    }

    [Fact]
    public async Task GetMyLiquidityListAsync_Test()
    {
        var result = await
            _farmService.GetMyLiquidityListAsync(new GetMyLiquidityListInput
            {
                Address = ""
            });
        result.Count.ShouldBe(2);
    }

    private static IFarmProvider MockIFarmProvider()
    {
        var mockIFarmProvider =
            new Mock<IFarmProvider>();
        mockIFarmProvider.Setup(calc => calc.GetLiquidityInfoAsync(null, null, LpStatus.Added, 0, 10)).ReturnsAsync(
            new List<LiquidityInfoIndexerDto>
            {
                new()
                {
                    LiquidityId = "1",
                    StakeId = "1",
                    Seed = "1",
                    LpAmount = 100000000,
                    LpSymbol = "SGR-ELF",
                    RewardSymbol = "SGR",
                    TokenAAmount = 100000000,
                    TokenASymbol = "SGR",
                    TokenBAmount = 100000000,
                    TokenBSymbol = "ELF",
                    TokenAddress = "A"
                },
                new()
                {
                    LiquidityId = "2",
                    StakeId = "2",
                    Seed = "2",
                    LpAmount = 100000000,
                    LpSymbol = "SGR-USD",
                    RewardSymbol = "SGR",
                    TokenAAmount = 100000000,
                    TokenASymbol = "SGR",
                    TokenBAmount = 100000000,
                    TokenBSymbol = "USD",
                    TokenAddress = "B"
                },
            });
        return mockIFarmProvider.Object;
    }

    private IRewardsProvider MockIRewardsProvider()
    {
        var mockIRewardsProvider =
            new Mock<IRewardsProvider>();
        mockIRewardsProvider.Setup(calc => calc.GetUnLockedStakeIdsAsync(null, "")).ReturnsAsync(new List<string>
        {
            "", ""
        });
        return mockIRewardsProvider.Object;
    }

    public IPriceProvider MockIPriceProvider()
    {
        var mockIPriceProvider =
            new Mock<IPriceProvider>();
        mockIPriceProvider.Setup(calc => calc.GetLpPriceAsync("", 0, "", "")).ReturnsAsync(0.5d);
        return mockIPriceProvider.Object;
    }

    private ITokenStakingProvider MockITokenStakingProvider()
    {
        var mockITokenStakingProvider =
            new Mock<ITokenStakingProvider>();
        mockITokenStakingProvider.Setup(calc => calc.GetTokenPoolsAsync(null)).ReturnsAsync(new List<TokenPoolsIndexerDto>()
        {
            new ()
            {
                
            },
            new ()
            {
                
            },
            new ()
            {
                
            }
        });
        return mockITokenStakingProvider.Object;
    }

    private static IOptionsMonitor<LpPoolRateOptions> MockLpPoolRateOptions()
    {
        var mock = new Mock<IOptionsMonitor<LpPoolRateOptions>>();
        var openAiOptions = new LpPoolRateOptions()
        {
            LpPoolRateDic = new Dictionary<string, double>
            {
                ["A"] = 0.5,
                ["b"] = 0.5
            },
            SymbolIconMappingsDic = new Dictionary<string, string>()
            {
                ["SGR"] = "SGR",
                ["ELF"] = "ELF",
                ["USDT"] = "USDT"
            }
        };
        mock.Setup(x => x.CurrentValue).Returns(openAiOptions);
        return mock.Object;
    }
}