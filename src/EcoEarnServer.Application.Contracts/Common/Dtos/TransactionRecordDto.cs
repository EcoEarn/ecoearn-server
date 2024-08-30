using EcoEarnServer.TransactionRecord;

namespace EcoEarnServer.Common.Dtos;

public class TransactionRecordDto
{
    public string Address { get; set; }
    public TransactionType TransactionType { get; set; }
    public string Amount { get; set; }
    public long CreateTime { get; set; }
}