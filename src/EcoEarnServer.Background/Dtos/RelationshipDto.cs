namespace EcoEarnServer.Background.Dtos;

public class RelationshipDto
{
    public string Address { get; set; }
    public long InviterKolFollowerNum { get; set; }
    public long KolFollowerNum { get; set; }
    public long KolFollowerInviteeNum { get; set; }
    public long InviteeNum { get; set; }
    public long SecondInviteeNum { get; set; }
}

public class KolFollowerNumDto
{
    public long KolFollowerNum { get; set; }
    public long KolFollowerInviteeNum { get; set; }
}

public class InviteeNumDto
{
    public long InviteeNum { get; set; }
    public long SecondInviteeNum { get; set; }
}