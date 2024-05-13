using Google.Protobuf;

namespace EcoEarnServer.PointsStaking.Dtos;

public class ClaimAmountSignatureDto
{
    public string Seed { get; set; }
    public ByteString Signature { get; set; }
}