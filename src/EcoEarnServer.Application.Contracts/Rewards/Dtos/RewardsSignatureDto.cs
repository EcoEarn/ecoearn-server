using Google.Protobuf;

namespace EcoEarnServer.Rewards.Dtos;

public class RewardsSignatureDto
{
    public string Seed { get; set; }
    public ByteString Signature { get; set; }
    public long ExpirationTime { get; set; }
}