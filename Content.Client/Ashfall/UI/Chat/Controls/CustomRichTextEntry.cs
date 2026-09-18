using System.Numerics;
using System.Text;
using Content.Client.Ashfall.UI.Chat.RichText;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.RichText;
using Robust.Shared.Collections;
using Robust.Shared.Utility;

namespace Content.Client.Ashfall.UI.Chat.Controls;

public struct CustomRichTextEntry
{
    public static readonly Type[] DefaultTags =
    [
        typeof(BoldItalicTag),
        typeof(BoldTag),
        typeof(BulletTag),
        typeof(ColorTag),
        typeof(HeadingTag),
        typeof(ItalicTag),
        typeof(EntityTextureTag),
        typeof(ExamineBorderTag),
        typeof(FontTag),
    ];

    public const float OuterMarginX = 4f;
    public const float OuterMarginY = 3f;
    public const float BoxPaddingX = 8f;
    public const float BoxPaddingY = 5f;

    private readonly Color _defaultColor;
    private readonly Type[]? _tagsAllowed;
    private readonly IEntityManager _entManager;

    public readonly FormattedMessage Message;

    /// <summary>
    /// The vertical size of this entry, in pixels.
    /// </summary>
    public int Height;

    /// <summary>
    /// The horizontal size of this entry, in pixels.
    /// </summary>
    public int Width;

    /// <summary>
    /// The combined text indices in the message's text tags to put line breaks.
    /// </summary>
    public ValueList<int> LineBreaks;

    public bool IsInBox;

    public readonly Dictionary<int, Control>? Controls;

    public CustomRichTextEntry(
            FormattedMessage message,
            Control parent,
            MarkupTagManager tagMan,
            IEntityManager entMan,
            Color? defaultColor = null)
        : this(message, parent, tagMan, entMan, DefaultTags, defaultColor) {}

    public CustomRichTextEntry(
            FormattedMessage message,
            Control parent,
            MarkupTagManager tagManager,
            IEntityManager entManager,
            Type[]? tagsAllowed,
            Color? defaultColor = null)
    {
        Message = message;
        Height = 0;
        Width = 0;
        LineBreaks = default;
        IsInBox = false;
        _entManager = entManager;
        _defaultColor = defaultColor ?? new(200, 200, 200);
        _tagsAllowed = tagsAllowed;
        Dictionary<int, Control>? tagControls = null;

        var nodeIndex = -1;
        foreach (var node in Message)
        {
            nodeIndex++;

            if (node.Name == null)
                continue;

            if (node.Name == ExamineBorderTag.TagName)
                IsInBox = true;

            if (!tagManager.TryGetMarkupTagHandler(node.Name, _tagsAllowed, out var handler) || !handler.TryCreateControl(node, out var control))
                continue;

            parent.Children.Add(control);

            tagControls ??= new Dictionary<int, Control>();
            tagControls.Add(nodeIndex, control);
        }

        Controls = tagControls;
    }

    /// <summary>
    /// Remove all owned controls from their parents.
    /// </summary>
    public readonly void RemoveControls()
    {
        if (Controls == null)
            return;

        foreach (var ctrl in Controls.Values)
        {
            ctrl.Orphan();
        }
    }

    /// <summary>
    /// Recalculate line dimensions and where it has line breaks for word wrapping.
    /// </summary>
    public CustomRichTextEntry Update(MarkupTagManager tagManager, Font defaultFont, float maxSizeX, float uiScale, float lineHeightScale = 1)
    {
        Height = defaultFont.GetHeight(uiScale);
        LineBreaks.Clear();

        if (IsInBox && maxSizeX > 0)
        {
            var horizontalReduction = (OuterMarginX * 2 + BoxPaddingX * 2) * uiScale;
            maxSizeX = MathF.Max(0f, maxSizeX - horizontalReduction);
        }

        int? breakLine;
        var wordWrap = new CustomWordWrap(maxSizeX);
        var context = new MarkupDrawingContext();
        context.Font.Push(defaultFont);
        context.Color.Push(_defaultColor);

        var nodeIndex = -1;
        foreach (var node in Message)
        {
            nodeIndex++;
            var text = ProcessNode(tagManager, node, context);

            if (!context.Font.TryPeek(out var font))
                font = defaultFont;

            foreach (var rune in text.EnumerateRunes())
            {
                if (ProcessRune(ref this, rune, out breakLine))
                    continue;

                if (!font.TryGetCharMetrics(rune, uiScale, out var metrics))
                    continue;

                if (ProcessMetric(ref this, metrics, out breakLine))
                    return this;
            }

            if (Controls == null || !Controls.TryGetValue(nodeIndex, out var control))
                continue;

            control.Measure(new Vector2(maxSizeX, float.PositiveInfinity));

            var desiredSize = control.DesiredPixelSize;
            var controlMetrics = new CharMetrics(
                0, 0,
                desiredSize.X,
                desiredSize.X,
                desiredSize.Y);

            if (ProcessMetric(ref this, controlMetrics, out breakLine))
                return this;
        }

        Width = wordWrap.FinalizeText(out breakLine);
        CheckLineBreak(ref this, breakLine);

        if (IsInBox)
        {
            Height += (int)((BoxPaddingY * 2 + OuterMarginY * 2) * uiScale);
        }

        return this;

        bool ProcessRune(ref CustomRichTextEntry src, Rune rune, out int? outBreakLine)
        {
            wordWrap.NextRune(rune, out breakLine, out var breakNewLine, out var skip);
            CheckLineBreak(ref src, breakLine);
            CheckLineBreak(ref src, breakNewLine);
            outBreakLine = breakLine;
            return skip;
        }

        bool ProcessMetric(ref CustomRichTextEntry src, CharMetrics metrics, out int? outBreakLine)
        {
            wordWrap.NextMetrics(metrics, out breakLine, out var abort);
            CheckLineBreak(ref src, breakLine);
            outBreakLine = breakLine;
            return abort;
        }

        void CheckLineBreak(ref CustomRichTextEntry src, int? line)
        {
            if (line is { } l)
            {
                src.LineBreaks.Add(l);
                if (!context.Font.TryPeek(out var font))
                    font = defaultFont;

                src.Height += GetLineHeight(font, uiScale, lineHeightScale);
            }
        }
    }

    internal readonly void HideControls()
    {
        if (Controls == null)
            return;

        foreach (var control in Controls.Values)
        {
            control.Visible = false;
        }
    }

    public readonly void Draw(
        MarkupTagManager tagManager,
        DrawingHandleBase handle,
        Font defaultFont,
        UIBox2 drawBox,
        float verticalOffset,
        Vector2i scrollBarPixelSize,
        MarkupDrawingContext context,
        float uiScale,
        float lineHeightScale = 1)
    {
        var screenHandle = (DrawingHandleScreen) handle;

        if (IsInBox)
        {
            var boxLeft = drawBox.Left + OuterMarginX * uiScale;
            var boxRight = drawBox.Right - OuterMarginX * uiScale;
            var boxTop = drawBox.Top + verticalOffset + OuterMarginY * uiScale;
            var boxBottom = drawBox.Top + verticalOffset + Height - OuterMarginY * uiScale;
            var bounds = new UIBox2(boxLeft, boxTop, boxRight, boxBottom);

            // Dark graphite stylebox background
            screenHandle.DrawRect(
                bounds,
                Color.FromHex("#15181c"),
                true);

            // Subtle border outline
            screenHandle.DrawRect(
                bounds,
                Color.FromHex("#2d333b"),
                false);
        }

        DrawBoxContent(tagManager, handle, defaultFont, drawBox, verticalOffset, scrollBarPixelSize, context, uiScale, lineHeightScale);
    }

    private readonly void DrawBoxContent(
        MarkupTagManager tagManager,
        DrawingHandleBase handle,
        Font defaultFont,
        UIBox2 drawBox,
        float verticalOffset,
        Vector2i scrollBarPixelSize,
        MarkupDrawingContext context,
        float uiScale,
        float lineHeightScale = 1)
    {
        context.Clear();
        context.Color.Push(_defaultColor);
        context.Font.Push(defaultFont);

        float leftIndent = 0f;
        float topIndent = verticalOffset;
        if (IsInBox)
        {
            leftIndent = (OuterMarginX + BoxPaddingX) * uiScale;
            topIndent = verticalOffset + (OuterMarginY + BoxPaddingY) * uiScale;
        }

        var globalBreakCounter = 0;
        var lineBreakIndex = 0;
        var baseLine = drawBox.TopLeft + new Vector2(leftIndent, defaultFont.GetAscent(uiScale) + topIndent);

        var nodeIndex = -1;
        foreach (var node in Message)
        {
            nodeIndex++;
            var text = ProcessNode(tagManager, node, context);
            if (!context.Color.TryPeek(out var color) || !context.Font.TryPeek(out var font))
            {
                color = _defaultColor;
                font = defaultFont;
            }

            foreach (var rune in text.EnumerateRunes())
            {
                if (lineBreakIndex < LineBreaks.Count &&
                    LineBreaks[lineBreakIndex] == globalBreakCounter)
                {
                    baseLine = new Vector2(drawBox.Left + leftIndent, baseLine.Y + GetLineHeight(font, uiScale, lineHeightScale));
                    lineBreakIndex += 1;
                }

                var advance = font.DrawChar(handle, rune, baseLine, uiScale, color);
                baseLine.X += advance;

                globalBreakCounter += 1;
            }

            if (Controls == null || !Controls.TryGetValue(nodeIndex, out var control))
                continue;

            control.Visible = true;

            var invertedScale = 1f / uiScale;
            var pos = new Vector2(baseLine.X * invertedScale, (baseLine.Y - defaultFont.GetAscent(uiScale)) * invertedScale);
            LayoutContainer.SetPosition(control, pos);
            control.Measure(new Vector2(Width, Height));

            var advanceX = control.DesiredSize.X + control.Margin.Right;
            baseLine.X += advanceX;
        }
    }

    private readonly string ProcessNode(MarkupTagManager tagManager, MarkupNode node, MarkupDrawingContext context)
    {
        if (node.Name == null)
            return node.Value.StringValue ?? "";

        if (!tagManager.TryGetMarkupTagHandler(node.Name, _tagsAllowed, out var tag))
            return "";

        if (!node.Closing)
        {
            tag.PushDrawContext(node, context);
            return tag.TextBefore(node);
        }

        try
        {
            tag.PopDrawContext(node, context);
        }
        catch
        {
            throw new Exception($"Bad closing tag for {node.Name}");
        }
        return tag.TextAfter(node);
    }

    private static int GetLineHeight(Font font, float uiScale, float lineHeightScale)
    {
        var height = font.GetLineHeight(uiScale);
        return (int)(height * lineHeightScale);
    }
}
