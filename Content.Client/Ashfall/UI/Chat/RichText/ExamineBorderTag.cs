using Robust.Client.UserInterface.RichText;

namespace Content.Client.Ashfall.UI.Chat.RichText;

public sealed partial class ExamineBorderTag : IMarkupTagHandler
{
    public const string TagName = "examineborder";

    public string Name => TagName;
}
