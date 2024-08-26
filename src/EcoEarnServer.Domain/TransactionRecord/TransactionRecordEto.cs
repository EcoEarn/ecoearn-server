using Volo.Abp.EventBus;

namespace EcoEarnServer.TransactionRecord;

[EventName("TransactionRecordEto")]
public class TransactionRecordEto
{
    public string Address { get; set; }
    public TransactionType TransactionType { get; set; }
    public string Amount { get; set; }
    public long CreateTime { get; set; }
}