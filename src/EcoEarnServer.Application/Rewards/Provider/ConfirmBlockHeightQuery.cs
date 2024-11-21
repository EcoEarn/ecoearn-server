using System.Collections.Generic;

namespace EcoEarnServer.Rewards.Provider;

public class ConfirmBlockHeightQuery
{
    public SyncStateDto SyncState { get; set; }
}

public class SyncStateDto
{
    public CurrentVersionDto CurrentVersion { get; set; }
}

public class CurrentVersionDto
{
    public List<SyncStateItemDto> Items { get; set; }
}

public class SyncStateItemDto
{
    public string ChainId { get; set; }
    public long LastIrreversibleBlockHeight { get; set; }
}


public enum BlockFilterType
{
    Block,
    Transaction,
    LogEvent,
}