using System.Collections.Generic;

namespace EcoEarnServer.ContractEventHandler.Core.Application;

public class ChainOptions
{
    public Dictionary<string, ChainInfo> ChainInfos { get; set; }
}

public class ChainInfo
{
    public string ChainId { get; set; }
    public string BaseUrl { get; set; }
    public string ContractAddress { get; set; }
    public string TokenContractAddress { get; set; }
    public string CrossChainContractAddress { get; set; }
    public string PublicKey { get; set; }
    public bool IsMainChain { get; set; }
}

public class IndexOptions
{
    public int IndexDelay { get; set; }
    public long IndexInterval { get; set; }
    public long IndexSafe { get; set; }
    public long IndexBefore { get; set; }
    public long IndexAfter { get; set; }
    public long IndexTimes { get; set; }
    public int MaxRetryTimes { get; set; }
    public int MaxBucket { get; set; }
    public Dictionary<string, long> AutoSyncStartHeight { get; set; }
}

public class ContractSyncOptions
{
    public int Sync { get; set; }
}