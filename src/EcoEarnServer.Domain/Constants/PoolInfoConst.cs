using System.Collections.Generic;

namespace EcoEarnServer.Constants;

public class PoolInfoConst
{
    public static readonly Dictionary<string, string> PoolIndexSymbolDic = new()
    {
        { "XPSG-1", "FirstSymbolAmount" },
        { "XPSG-2", "SecondSymbolAmount" },
        { "XPSG-3", "ThirdSymbolAmount" },
        { "XPSG-4", "FourSymbolAmount" },
        { "XPSG-5", "FiveSymbolAmount" },
        { "XPSG-6", "SixSymbolAmount" },
        { "XPSG-7", "SevenSymbolAmount" },
        { "XPSG-8", "EightSymbolAmount" },
        { "XPSG-9", "NineSymbolAmount" }
    };

    public static readonly Dictionary<string, string> SymbolPoolIndexDic = new()
    {
        { "FirstSymbolAmount", "XPSG-1" },
        { "SecondSymbolAmount", "XPSG-2" },
        { "ThirdSymbolAmount", "XPSG-3" },
        { "FourSymbolAmount", "XPSG-4" },
        { "FiveSymbolAmount", "XPSG-5" },
        { "SixSymbolAmount", "XPSG-6" },
        { "SevenSymbolAmount", "XPSG-7" },
        { "EightSymbolAmount", "XPSG-8" },
        { "NineSymbolAmount", "XPSG-9" }
    };
    
    public static readonly Dictionary<string, string> ProjectOwnerDic = new()
    {
        { "dappId", "Schrodinger" },
    };
}