namespace O11yParty.Models;

public sealed class Team
{
    public Team(string name) => Name = name;
    public string Name { get; set; }
    public int Score { get; set; }
}
