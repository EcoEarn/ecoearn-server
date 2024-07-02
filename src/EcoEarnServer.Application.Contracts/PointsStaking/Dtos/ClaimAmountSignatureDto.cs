using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;

namespace EcoEarnServer.PointsStaking.Dtos;

public class ClaimAmountSignatureDto
{
    public string Seed { get; set; }
    public ByteString Signature { get; set; }
    public long ExpirationTime { get; set; }
}