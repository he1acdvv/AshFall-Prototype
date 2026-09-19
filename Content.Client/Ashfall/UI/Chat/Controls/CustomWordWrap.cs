using System.Diagnostics.Contracts;
using System.Text;
using Robust.Client.Graphics;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Utility;

namespace Content.Client.Ashfall.UI.Chat.Controls;

/// <summary>
/// Helper utility struct for word-wrapping calculations.
/// </summary>
internal struct CustomWordWrap
{
    private readonly float _maxSizeX;
    private ISawmill _sawmill;

    public float MaxUsedWidth;
    public int BreakIndexCounter;
    public int NextBreakIndexCounter;
    public (int index, float lineSize)? WordStartBreakIndex;
    public int WordSizePixels;
    public int PosX;
    public Rune LastRune;
    public (int breakIndex, int wordSizePixels)? ForceSplitData = null;

    public CustomWordWrap(float maxSizeX)
    {
        this = default;
        _maxSizeX = maxSizeX;
        _sawmill = IoCManager.Resolve<ILogManager>().GetSawmill("custom_word_wrap");
        LastRune = new Rune('A');
    }

    public void NextRune(Rune rune, out int? breakLine, out int? breakNewLine, out bool skip)
    {
        BreakIndexCounter = NextBreakIndexCounter;
        NextBreakIndexCounter += rune.Utf16SequenceLength;

        breakLine = null;
        breakNewLine = null;
        skip = false;

        if (IsWordBoundary(LastRune, rune) || rune == new Rune('\n'))
        {
            if (PosX > _maxSizeX && LastRune != new Rune(' '))
            {
                DebugTools.Assert(WordStartBreakIndex.HasValue,
                    "wordStartBreakIndex can only be null if the word begins at a new line, in which case this branch shouldn't be reached as the word would be split due to being longer than a single line.");
                if (!WordStartBreakIndex.HasValue)
                    return;

                breakLine = WordStartBreakIndex!.Value.index;
                MaxUsedWidth = Math.Max(MaxUsedWidth, WordStartBreakIndex.Value.lineSize);
                PosX = WordSizePixels;
            }

            WordSizePixels = 0;
            WordStartBreakIndex = (BreakIndexCounter, PosX);
            ForceSplitData = null;

            if (rune == new Rune('\n'))
            {
                MaxUsedWidth = Math.Max(MaxUsedWidth, PosX);
                PosX = 0;
                WordStartBreakIndex = null;
                skip = true;
                breakNewLine = BreakIndexCounter;
            }
        }

        LastRune = rune;
    }

    public void NextMetrics(in CharMetrics metrics, out int? breakLine, out bool abort)
    {
        abort = false;
        breakLine = null;

        var oldWordSizePixels = WordSizePixels;
        WordSizePixels += metrics.Advance;
        PosX += metrics.Advance;

        if (PosX <= _maxSizeX)
            return;

        if (!ForceSplitData.HasValue)
        {
            ForceSplitData = (BreakIndexCounter, oldWordSizePixels);
        }

        if (WordSizePixels > _maxSizeX)
        {
            var (breakIndex, splitWordSize) = ForceSplitData.Value;
            if (splitWordSize == 0)
            {
                abort = true;
                return;
            }

            ForceSplitData = null;
            breakLine = breakIndex;
            WordSizePixels -= splitWordSize;
            WordStartBreakIndex = null;
            MaxUsedWidth = Math.Max(MaxUsedWidth, _maxSizeX);
            PosX = WordSizePixels;
        }
    }

    public int FinalizeText(out int? breakLine)
    {
        if (PosX > _maxSizeX)
        {
            if (!WordStartBreakIndex.HasValue)
            {
                _sawmill.Error(
                    "Assert fail inside RichTextEntry.Update, " +
                    "wordStartBreakIndex is null on method end w/ word wrap required.");
                throw new Exception(
                    "wordStartBreakIndex can only be null if the word begins at a new line," +
                    "in which case this branch shouldn't be reached as" +
                    "the word would be split due to being longer than a single line.");
            }

            breakLine = WordStartBreakIndex.Value.index;
            MaxUsedWidth = Math.Max(MaxUsedWidth, WordStartBreakIndex.Value.lineSize);
        }
        else
        {
            breakLine = null;
            MaxUsedWidth = Math.Max(MaxUsedWidth, PosX);
        }

        return (int)MaxUsedWidth;
    }

    private static bool IsWordBoundary(Rune a, Rune b)
    {
        return a == new Rune(' ') || b == new Rune(' ') || a == new Rune('-') || b == new Rune('-');
    }
}
