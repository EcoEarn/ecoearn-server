using System.Collections.Generic;

namespace EcoEarnServer.Background.Provider.Dtos;

public class ReferralRecordDto
{
    public string Domain { get; set; }

    public string Referrer { get; set; }

    public string Invitee { get; set; }
}

public class ReferralRecordQuery
{
    public ReferralRecordListDto GetReferralRecordList { get; set; }
}

public class ReferralRecordListDto
{
    public List<ReferralRecordDto> Data { get; set; }
    public long TotalCount { get; set; }
}