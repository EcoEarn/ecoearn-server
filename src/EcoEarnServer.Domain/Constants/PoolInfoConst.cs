using System.Collections.Generic;

namespace EcoEarnServer.Constants;

public class PoolInfoConst
{
    public static readonly Dictionary<string, string> PoolIndexSymbolDic = new()
    {
        { "1", "FirstSymbolAmount" },
        { "2", "SecondSymbolAmount" },
        { "3", "ThirdSymbolAmount" },
        { "4", "FourSymbolAmount" },
        { "5", "FiveSymbolAmount" },
        { "6", "SixSymbolAmount" },
        { "7", "SevenSymbolAmount" },
        { "8", "EightSymbolAmount" },
        { "9", "NineSymbolAmount" },
        { "10", "TenSymbolAmount" },
        { "11", "ElevenSymbolAmount" },
        { "12", "TwelveSymbolAmount" },
    };

    public static readonly Dictionary<string, string> SymbolPoolIndexDic = new()
    {
        { "FirstSymbolAmount", "XPSGR-1" },
        { "SecondSymbolAmount", "XPSGR-2" },
        { "ThirdSymbolAmount", "XPSGR-3" },
        { "FourSymbolAmount", "XPSGR-4" },
        { "FiveSymbolAmount", "XPSGR-5" },
        { "SixSymbolAmount", "XPSGR-6" },
        { "SevenSymbolAmount", "XPSGR-7" },
        { "EightSymbolAmount", "XPSGR-8" },
        { "NineSymbolAmount", "XPSGR-9" },
        { "TenSymbolAmount", "XPSGR-10" },
        { "ElevenSymbolAmount", "XPSGR-11" },
        { "TwelveSymbolAmount", "XPSGR-12" },
    };
    
    public static readonly Dictionary<string, string> ProjectOwnerDic = new()
    {
        { "d1f6bad87a9f1c4452f4233393f9b0b07e879dfb30c66eeee64cb8d3cd17cb0d", "AI-powered 404 NFT dApp" },
    };
}