namespace EcoEarnServer.Background.Provider.Dtos;

public class ConfirmedBlockHeightRecord
{
    public SyncState GetConfirmedBlockHeight { get; set; }
}

public class SyncState
{
    public long ConfirmedBlockHeight { get; set; }
}

public enum BlockFilterType
{
    BLOCK,
    TRANSACTION,
    LOG_EVENT
}