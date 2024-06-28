using System.Linq;

namespace EcoEarnServer.Common;

public class LpSymbolHelper
{
    private const string Space = " ";
    private const string Separator = "-";

    public static (string, string) GetLpSymbols(string lpSymbol)
    {
        var symbols = lpSymbol.Split(Space)[1].Split(Separator);

        var symbolA = "";
        var symbolB = "";

        switch (symbols.Length)
        {
            // ABC-DEF
            case 2:
                symbolA = symbols[0];
                symbolB = symbols[1];
                break;
            // ABC-1 - DEF / ABC - DEF-1
            case 3:
                symbolA = symbols[1].All(IsValidItemIdChar)
                    ? $"{symbols[0]}-{symbols[1]}"
                    : $"{symbols[1]}-{symbols[2]}";
                symbolB = symbols[1].All(IsValidItemIdChar) ? symbols[2] : symbols[0];
                break;
            // ABC-1 - DEF-1
            case 4:
                symbolA = $"{symbols[0]}-{symbols[1]}";
                symbolB = $"{symbols[2]}-{symbols[3]}";
                break;
        }

        return (symbolA, symbolB);
    }

    private static bool IsValidItemIdChar(char character)
    {
        return character >= '0' && character <= '9';
    }
}