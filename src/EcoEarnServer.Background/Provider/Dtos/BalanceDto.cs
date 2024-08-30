namespace EcoEarnServer.Background.Provider.Dtos;

public class BalanceDto
{
    public string Symbol { get; set; }
    public string Owner { get; set; }
    public string Balance { get; set; } = "0";
    public string Amount { get; set; } = "0";
}