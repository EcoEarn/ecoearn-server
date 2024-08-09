using System.Collections.Generic;

namespace EcoEarnServer.Options;

public class ProjectItemOptions
{
    public List<ProjectItem> ProjectItems { get; set; }
}

public class ProjectItem
{
    public string DappName { get; set; }
    public string DappId { get; set; }
    public string ProjectOwner { get; set; }
    public string Icon { get; set; }
    public bool IsOpenStake { get; set; }
    public string PointsType { get; set; }
    public string GainUrl { get; set; }
    public string RulesText { get; set; }
}