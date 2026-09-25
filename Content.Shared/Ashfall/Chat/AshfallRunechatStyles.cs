namespace Content.Shared.Ashfall.Chat;

public static class AshfallRunechatStyles
{
    public const string Say = "runechatSay";
    public const string Whisper = "runechatWhisper";
    public const string Radio = "runechatRadio";
    public const string Emote = "runechatEmote";
    public const string Looc = "runechatLooc";
    public const string Scream = "scream";
    public const string Pain = "pain";

    public static bool IsInterrupting(string? style)
    {
        return style == Scream || style == Pain;
    }
}
