using System.Collections.Generic;

namespace EcoEarnServer.Options;

public class ProjectKeyPairInfoOptions
{
    public Dictionary<string, ProjectKeyPairInfo> ProjectKeyPairInfos { get; set; }
}

public class ProjectKeyPairInfo
{
    public string PrivateKey { get; set; }
}