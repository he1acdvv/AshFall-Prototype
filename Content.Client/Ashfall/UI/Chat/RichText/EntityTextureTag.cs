using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.RichText;
using Robust.Shared.Maths;

namespace Content.Client.Ashfall.UI.Chat.RichText;

public sealed partial class EntityTextureTag : IMarkupTagHandler
{
    public const string TagName = "enttex";

    public string Name => TagName;

    public bool TryCreateControl(MarkupNode node, [NotNullWhen(true)] out Control? control)
    {
        control = null;
        if (node.Closing || !node.Attributes.TryGetValue("id", out var idParameter) || !idParameter.TryGetLong(out var id))
            return false;

        if (id < int.MinValue || id > int.MaxValue)
            return false;

        if (!node.Attributes.TryGetValue("size", out var size) || !size.TryGetLong(out var sizeValue))
        {
            sizeValue = 32;
        }

        var pixels = (int) Math.Clamp(sizeValue.Value, 8, 64);

        var spriteView = new SpriteView
        {
            OverrideDirection = Direction.South,
            SetSize = new Vector2(pixels * 2, pixels * 2),
        };
        spriteView.SetEntity(new NetEntity((int) id));

        control = spriteView;
        return true;
    }
}
