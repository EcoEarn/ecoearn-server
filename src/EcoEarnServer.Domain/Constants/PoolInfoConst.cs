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
        { "XPSGR-9", "NineSymbolAmount" }
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
        { "NineSymbolAmount", "XPSGR-9" }
    };
    
    public static readonly Dictionary<string, string> ProjectOwnerDic = new()
    {
        { "dappId", "Schrodinger" },
    };
}