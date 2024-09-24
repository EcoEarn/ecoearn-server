namespace EcoEarnServer.Rewards.Provider;

public class ConfirmBlockHeightQuery
{
    public SyncStateDto SyncState { get; set; }
}

public class SyncStateDto
{
    public long ConfirmedBlockHeight { get; set; }
}


public enum BlockFilterType
{
    Block,
    Transaction,
    LogEvent,
}