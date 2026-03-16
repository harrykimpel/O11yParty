namespace O11yParty.Models;

public sealed class O11yPartyBoard
{
    public List<O11yPartyCategory> Categories { get; set; } = new();
}

public sealed class O11yPartyCategory
{
    public string Name { get; set; } = string.Empty;
    public List<O11yPartyQuestion> Questions { get; set; } = new();
}

public sealed class O11yPartyQuestion
{
    public int Value { get; set; }
    public string Prompt { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public bool IsDailyDouble { get; set; }

    public string CategoryName { get; set; } = string.Empty;
    public bool IsAnswered { get; set; }
}
