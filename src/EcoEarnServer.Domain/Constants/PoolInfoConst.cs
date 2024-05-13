using System.Collections.Generic;

namespace EcoEarnServer.Constants;

public class PoolInfoConst
{
    public static readonly Dictionary<string, string> PoolIndexSymbolDic = new()
    {
        { "XPSGR-1", "FirstSymbolAmount" },
        { "XPSGR-2", "SecondSymbolAmount" },
        { "XPSGR-3", "ThirdSymbolAmount" },
        { "XPSGR-4", "FourSymbolAmount" },
        { "XPSGR-5", "FiveSymbolAmount" },
        { "XPSGR-6", "SixSymbolAmount" },
        { "XPSGR-7", "SevenSymbolAmount" },
        { "XPSGR-8", "EightSymbolAmount" },
        { "XPSGR-9", "NineSymbolAmount" },
        { "XPSGR-10", "TenSymbolAmount" },
        { "XPSGR-11", "ElevenSymbolAmount" },
        { "XPSGR-12", "TwelveSymbolAmount" },
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
        { "d1f6bad87a9f1c4452f4233393f9b0b07e879dfb30c66eeee64cb8d3cd17cb0d", "Schrodinger" },
    };
}