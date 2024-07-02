using System;
using System.Collections.Generic;
using AElf.Indexing.Elasticsearch;
using EcoEarnServer.Entities;
using Nest;

namespace EcoEarnServer.Users;

public class UserExtraIndex : AbstractEntity<Guid>, IIndexBuild
{
    [Keyword] public string UserName { get; set; }
    [Keyword] public string AelfAddress { get; set; }
    [Keyword] public string CaHash { get; set; }
    [Keyword] public string CaAddressMain { get; set; }

    [Nested(Name = "CaAddressListSide", Enabled = true, IncludeInParent = true, IncludeInRoot = true)]
    public List<UserAddress> CaAddressListSide { get; set; }

    [Keyword] public string? RegisterDomain { get; set; }
    public DateTime CreateTime { get; set; }
}