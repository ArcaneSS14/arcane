namespace Content.Client._Arcane.Radio;

/// <summary>
///     Preferred display order of radio channels across radio UIs: Common first, then
///     station frequencies, then CentCom, Syndicate and InteQ/Freelance, then any other
///     channel by frequency.
/// </summary>
public static class RadioChannelOrder
{
    public static readonly Dictionary<string, int> Channels = new()
    {
        ["Common"] = 0,
        ["Command"] = 1,
        ["Security"] = 2,
        ["Medical"] = 3,
        ["Engineering"] = 4,
        ["Science"] = 5,
        ["Service"] = 6,
        ["Supply"] = 7,
        ["Legal"] = 8,
        ["CentCom"] = 9,
        ["Syndicate"] = 10,
        ["InteQ"] = 11,
        ["Freelance"] = 12
    };
}
