using System.Globalization;
using System.Numerics;
using System.Text;
using Content.Client.Chat.UI;
using Content.Client.Resources;
using Content.Client.Stylesheets.Fonts;
using Content.Shared.Ashfall.Chat;
using Content.Shared.CCVar;
using Content.Shared.Chat;
using Content.Shared.Ghost.Components;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Configuration;
using Robust.Shared.IoC;
using Robust.Shared.Maths;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.Ashfall.Chat;

public sealed partial class RunechatSpeechBubble : SpeechBubble
{
    private const string SayStyle = AshfallRunechatStyles.Say;
    private const string WhisperStyle = AshfallRunechatStyles.Whisper;
    private const string RadioStyle = AshfallRunechatStyles.Radio;
    private const string EmoteStyle = AshfallRunechatStyles.Emote;
    private const string LoocStyle = AshfallRunechatStyles.Looc;

    private const int LongestText = 80;
    private const int ContinueTextLength = LongestText - 5;
    private const float SplitChunkSeconds = 4f;
    private const float SplitFinalSeconds = 6f;
    private const float MinimumRunechatScale = 0.5f;
    private const float MaximumRunechatScale = 2.5f;
    private const float DefaultLangchatWidth = 140f;
    private const float SplitLangchatWidth = 240f;

    private static readonly Color DefaultColor = Color.White;
    private static readonly Color ObserverColor = Color.FromHex("#c51fb7");
    private static readonly Color LoocColor = Color.FromHex("#48d1cc");
    private static readonly Color PainColor = Color.FromHex("#c83232");
    private static readonly Color RadioColor = Color.FromHex("#73d48f");

    /// <summary>
    /// A run of text that shares the same bold/italic formatting and optional color override.
    /// </summary>
    private readonly record struct TextRun(string Text, bool Bold, bool Italic, Color? ColorOverride = null);

    public RunechatSpeechBubble(SpeechType type, ChatMessage message, EntityUid senderEntity)
        : base(
            message,
            senderEntity,
            GetStyleClass(type, message),
            GetTextColor(type, message, senderEntity),
            GetLifetime(GetRunPages(GetRuns(message, GetStyleClass(type, message)))))
    {
        RectClipContent = false;
        VerticalOffsetAchieved = 0f;
        SetHeight = ContentSize.Y;
    }

    protected override Control BuildBubble(ChatMessage message, string speechStyleClass, Color? fontColor = null)
    {
        var runs = GetRuns(message, speechStyleClass);
        var (style, forceBold) = GetVisualStyle(message, speechStyleClass, runs);
        var pages = GetRunPages(runs);

        if (forceBold)
        {
            for (int i = 0; i < pages.Count; i++)
                pages[i] = ForceBold(pages[i]);
        }

        Texture? languageIcon = null;
        if (speechStyleClass is SayStyle or WhisperStyle)
        {
            TryGetLanguageIcon(message, out languageIcon);
        }

        return new RunechatTextControl(pages, fontColor ?? DefaultColor, style, languageIcon);
    }

    private static string GetStyleClass(SpeechType type, ChatMessage message)
    {
        return type switch
        {
            SpeechType.Emote => EmoteStyle,
            SpeechType.Say => SayStyle,
            SpeechType.Whisper => WhisperStyle,
            SpeechType.Looc => LoocStyle,
            _ => SayStyle,
        };
    }

    private static Color GetTextColor(SpeechType type, ChatMessage message, EntityUid senderEntity)
    {
        if (message.MessageColorOverride is { } color)
            return color;

        if (AshfallRunechatStyles.IsInterrupting(message.SpeechStyleClass))
            return PainColor;

        if (type == SpeechType.Looc)
            return LoocColor;

        if (message.Channel == ChatChannel.Radio)
            return RadioColor;

        var entityManager = IoCManager.Resolve<IEntityManager>();
        if (entityManager.HasComponent<GhostComponent>(senderEntity))
            return ObserverColor;

        return DefaultColor;
    }

    private static (RunechatVisualStyle Style, bool ForceBold) GetVisualStyle(
        ChatMessage message,
        string speechStyleClass,
        List<TextRun> runs)
    {
        if (message.SpeechStyleClass == AshfallRunechatStyles.Scream)
            return (RunechatVisualStyle.Scream, true);

        if (message.SpeechStyleClass == AshfallRunechatStyles.Pain)
            return (RunechatVisualStyle.Pain, true);

        if (speechStyleClass == EmoteStyle)
        {
            return IsYellEmote(RunsToPlainText(runs))
                ? (RunechatVisualStyle.EmoteYell, true)
                : (RunechatVisualStyle.Emote, false);
        }

        if (message.SpeechStyleClass == "megaphoneSpeech")
            return (RunechatVisualStyle.Announce, true);

        if (message.SpeechStyleClass == "commanderSpeech")
            return (RunechatVisualStyle.Bolded, true);

        if (speechStyleClass == SayStyle && IsWhollyBold(runs))
            return (RunechatVisualStyle.Bolded, false);

        var baseStyle = speechStyleClass switch
        {
            RadioStyle => RunechatVisualStyle.Radio,
            WhisperStyle => RunechatVisualStyle.Whisper,
            _ => RunechatVisualStyle.Normal,
        };

        return (baseStyle, false);
    }

    private static bool IsYellEmote(string text)
    {
        return text.Contains("scream", StringComparison.OrdinalIgnoreCase)
            || text.Contains("крик", StringComparison.OrdinalIgnoreCase)
            || text.Contains("pain", StringComparison.OrdinalIgnoreCase)
            || text.Contains("боль", StringComparison.OrdinalIgnoreCase)
            || text.Contains("medic", StringComparison.OrdinalIgnoreCase)
            || text.Contains("медик", StringComparison.OrdinalIgnoreCase);
    }

    private static TimeSpan GetLifetime(IReadOnlyList<List<TextRun>> pages)
    {
        if (pages.Count <= 1)
        {
            var length = pages.Count == 0 ? 0 : RunsLength(pages[0]);
            return TimeSpan.FromSeconds(length / (float)LongestText * SplitChunkSeconds + 2f);
        }

        return TimeSpan.FromSeconds((pages.Count - 1) * SplitChunkSeconds + SplitFinalSeconds);
    }

    private static List<TextRun> GetRuns(ChatMessage message, string speechStyleClass)
    {
        var raw = speechStyleClass switch
        {
            EmoteStyle => message.Message,
            SayStyle => GetBubbleContent(message),
            WhisperStyle => GetBubbleContent(message),
            RadioStyle => FormatRadioText(message),
            LoocStyle => $"LOOC: {message.Message}",
            _ => message.WrappedMessage,
        };

        if (string.IsNullOrWhiteSpace(raw))
            raw = message.Message;

        var runs = ParseFormattingRuns(raw);
        return NormalizeRuns(runs);
    }

    private static string GetBubbleContent(ChatMessage message)
    {
        return SharedChatSystem.GetStringInsideTag(message, "BubbleContent");
    }

    private static string FormatRadioText(ChatMessage message)
    {
        return message.Message;
    }

    private static List<TextRun> ParseFormattingRuns(string markupText)
    {
        var runs = new List<TextRun>();
        var boldDepth = 0;
        var italicDepth = 0;
        var colorStack = new Stack<Color>();
        var i = 0;
        var sb = new StringBuilder();

        Color? CurrentColor() => colorStack.Count > 0 ? colorStack.Peek() : (Color?)null;

        void Flush()
        {
            if (sb.Length > 0)
            {
                runs.Add(new TextRun(sb.ToString(), boldDepth > 0, italicDepth > 0, CurrentColor()));
                sb.Clear();
            }
        }

        while (i < markupText.Length)
        {
            var c = markupText[i];

            if (c == '[')
            {
                var close = markupText.IndexOf(']', i);
                if (close < 0)
                {
                    sb.Append(c);
                    i++;
                    continue;
                }

                var tag = markupText.Substring(i + 1, close - i - 1);
                var lowerTag = tag.ToLowerInvariant();

                switch (lowerTag)
                {
                    case "bold":
                        Flush();
                        boldDepth++;
                        i = close + 1;
                        continue;
                    case "/bold":
                        Flush();
                        boldDepth = Math.Max(0, boldDepth - 1);
                        i = close + 1;
                        continue;
                    case "italic":
                        Flush();
                        italicDepth++;
                        i = close + 1;
                        continue;
                    case "/italic":
                        Flush();
                        italicDepth = Math.Max(0, italicDepth - 1);
                        i = close + 1;
                        continue;
                    case "bolditalic":
                        Flush();
                        boldDepth++;
                        italicDepth++;
                        i = close + 1;
                        continue;
                    case "/bolditalic":
                        Flush();
                        boldDepth = Math.Max(0, boldDepth - 1);
                        italicDepth = Math.Max(0, italicDepth - 1);
                        i = close + 1;
                        continue;
                    case "/color":
                        Flush();
                        if (colorStack.Count > 0)
                            colorStack.Pop();
                        i = close + 1;
                        continue;
                    default:
                        var equalsIndex = tag.IndexOf('=');
                        var tagName = (equalsIndex >= 0 ? tag[..equalsIndex] : tag).Trim().ToLowerInvariant();

                        if (tagName == "color")
                        {
                            Flush();
                            var value = equalsIndex >= 0 ? tag[(equalsIndex + 1)..].Trim() : null;
                            if (!string.IsNullOrEmpty(value) && TryParseColor(value, out var parsedColor))
                                colorStack.Push(parsedColor);
                            else
                                colorStack.Push(colorStack.Count > 0 ? colorStack.Peek() : DefaultColor);

                            i = close + 1;
                            continue;
                        }

                        if (tagName is "font" or "/font")
                        {
                            i = close + 1;
                            continue;
                        }

                        sb.Append(markupText, i, close - i + 1);
                        i = close + 1;
                        continue;
                }
            }

            sb.Append(c);
            i++;
        }

        Flush();
        return runs;
    }

    private static bool TryParseColor(string value, out Color color)
    {
        value = value.Trim('"', '\'');

        if (Color.TryFromHex(value, out var hexColor))
        {
            color = hexColor;
            return true;
        }

        if (Color.TryFromName(value, out var namedColor))
        {
            color = namedColor;
            return true;
        }

        color = default;
        return false;
    }

    private static List<TextRun> NormalizeRuns(List<TextRun> runs)
    {
        var result = new List<TextRun>();
        var previousWasWhitespace = true;

        foreach (var run in runs)
        {
            var sb = new StringBuilder();
            foreach (var ch in run.Text)
            {
                if (char.IsWhiteSpace(ch))
                {
                    if (!previousWasWhitespace)
                        sb.Append(' ');

                    previousWasWhitespace = true;
                }
                else
                {
                    sb.Append(ch);
                    previousWasWhitespace = false;
                }
            }

            if (sb.Length > 0)
                result.Add(run with { Text = sb.ToString() });
        }

        return TrimRunsEnd(result);
    }

    private static int RunsLength(IReadOnlyList<TextRun> runs)
    {
        var length = 0;
        foreach (var run in runs)
            length += run.Text.Length;
        return length;
    }

    private static string RunsToPlainText(IReadOnlyList<TextRun> runs)
    {
        var sb = new StringBuilder();
        foreach (var run in runs)
            sb.Append(run.Text);
        return sb.ToString();
    }

    private static bool IsWhollyBold(IReadOnlyList<TextRun> runs)
    {
        var hasContent = false;
        foreach (var run in runs)
        {
            if (run.Text.Trim().Length == 0)
                continue;

            hasContent = true;
            if (!run.Bold)
                return false;
        }

        return hasContent;
    }

    private static List<TextRun> ForceBold(IReadOnlyList<TextRun> runs)
    {
        var result = new List<TextRun>(runs.Count);
        foreach (var run in runs)
            result.Add(run with { Bold = true });
        return result;
    }

    private static char GetCharAt(IReadOnlyList<TextRun> runs, int index)
    {
        var remaining = index;
        foreach (var run in runs)
        {
            if (remaining < run.Text.Length)
                return run.Text[remaining];

            remaining -= run.Text.Length;
        }

        throw new ArgumentOutOfRangeException(nameof(index));
    }

    private static int FindLastSpaceIndex(IReadOnlyList<TextRun> runs, int windowStart, int windowEnd)
    {
        var index = windowEnd - 1;
        while (index >= windowStart)
        {
            if (GetCharAt(runs, index) == ' ')
                return index;

            index--;
        }

        return -1;
    }

    private static (List<TextRun> Left, List<TextRun> Right) SplitRuns(IReadOnlyList<TextRun> runs, int index)
    {
        var left = new List<TextRun>();
        var right = new List<TextRun>();
        var remainingIndex = index;

        foreach (var run in runs)
        {
            if (remainingIndex <= 0)
            {
                right.Add(run);
                continue;
            }

            if (remainingIndex >= run.Text.Length)
            {
                left.Add(run);
                remainingIndex -= run.Text.Length;
                continue;
            }

            left.Add(run with { Text = run.Text[..remainingIndex] });
            right.Add(run with { Text = run.Text[remainingIndex..] });
            remainingIndex = 0;
        }

        return (left, right);
    }

    private static List<TextRun> TrimRunsEnd(List<TextRun> runs)
    {
        var result = new List<TextRun>(runs);
        while (result.Count > 0)
        {
            var last = result[^1];
            var trimmed = last.Text.TrimEnd(' ');

            if (trimmed.Length == last.Text.Length)
                break;

            if (trimmed.Length == 0)
            {
                result.RemoveAt(result.Count - 1);
                continue;
            }

            result[^1] = last with { Text = trimmed };
            break;
        }

        return result;
    }

    private static List<TextRun> TrimRunsStart(List<TextRun> runs)
    {
        var result = new List<TextRun>(runs);
        while (result.Count > 0)
        {
            var first = result[0];
            var trimmed = first.Text.TrimStart(' ');

            if (trimmed.Length == first.Text.Length)
                break;

            if (trimmed.Length == 0)
            {
                result.RemoveAt(0);
                continue;
            }

            result[0] = first with { Text = trimmed };
            break;
        }

        return result;
    }

    private static List<TextRun> PrependPlainRun(List<TextRun> runs, string text)
    {
        var result = new List<TextRun> { new(text, false, false) };
        result.AddRange(runs);
        return result;
    }

    private static List<TextRun> AppendPlainRun(List<TextRun> runs, string text)
    {
        var result = new List<TextRun>(runs) { new(text, false, false) };
        return result;
    }

    private static List<List<TextRun>> GetRunPages(List<TextRun> runs)
    {
        var pages = new List<List<TextRun>>();
        var length = RunsLength(runs);

        if (length <= LongestText)
        {
            pages.Add(runs);
            return pages;
        }

        var remaining = runs;
        while (RunsLength(remaining) > LongestText)
        {
            var split = GetRunSplitIndex(remaining);
            var (left, right) = SplitRuns(remaining, split);
            pages.Add(AppendPlainRun(TrimRunsEnd(left), "..."));
            remaining = PrependPlainRun(TrimRunsStart(right), "...");
        }

        pages.Add(remaining);
        return pages;
    }

    private static int GetRunSplitIndex(IReadOnlyList<TextRun> runs)
    {
        var length = RunsLength(runs);
        var max = Math.Min(ContinueTextLength, length);
        var split = FindLastSpaceIndex(runs, 0, max);

        return split >= LongestText / 2
            ? split
            : max;
    }

    private static int ScaleFontSize(int fontSize, float scale)
    {
        return Math.Max(7, (int)MathF.Round(fontSize * scale));
    }

    private readonly record struct RunechatVisualStyle(
        int FontSize,
        bool PrefixEmoteIcon,
        float MaxWidth,
        float LineHeightOffset = 0f,
        bool UsePanicShake = false)
    {
        public static readonly RunechatVisualStyle Normal = new(11, false, DefaultLangchatWidth);
        public static readonly RunechatVisualStyle Whisper = new(9, false, DefaultLangchatWidth, -1f);
        public static readonly RunechatVisualStyle Radio = new(10, false, SplitLangchatWidth);
        public static readonly RunechatVisualStyle Emote = new(10, true, DefaultLangchatWidth, -1f);
        public static readonly RunechatVisualStyle EmoteYell = new(12, true, DefaultLangchatWidth);
        public static readonly RunechatVisualStyle Bolded = new(12, false, DefaultLangchatWidth);
        public static readonly RunechatVisualStyle Announce = new(14, false, SplitLangchatWidth);
        public static readonly RunechatVisualStyle Pain = new(11, false, DefaultLangchatWidth);
        public static readonly RunechatVisualStyle Scream = new(12, false, DefaultLangchatWidth, UsePanicShake: true);

        public int GetScaledFontSize(float scale)
        {
            return ScaleFontSize(FontSize, scale);
        }

        public float GetScaledMaxWidth(float scale)
        {
            return MaxWidth * scale;
        }

        public float GetScaledLineHeightOffset(float scale)
        {
            return LineHeightOffset * scale;
        }
    }

    private sealed partial class RunechatTextControl : Control
    {
        private const float TextStrokeAlpha = 0.9f;
        private const float DefaultEmoteIconPixelSize = 1.4f;
        private const float PanicShakeDuration = 0.85f;
        private const float PanicShakeFrequency = 18f;
        private const float PanicShakeSize = 5f;
        private const float EmoteIconVisibleLeft = 3f;
        private const float EmoteIconVisibleRight = 8f;
        private const float EmoteIconVisibleTop = 3f;
        private const float EmoteIconVisibleBottom = 8f;
        private const float EmoteIconAlpha = 200f / 255f;

        private static readonly Color EmoteIconBlue = Color.FromHex("#3399ff");

        private static readonly string[] EmoteIcon =
        {
            ".........",
            ".........",
            ".........",
            "...B#B#B.",
            "...##B##.",
            "...BBBBB.",
            "...##B##.",
            "...B#B#B.",
            ".........",
        };

        [Dependency] private IConfigurationManager _configManager = default!;
        [Dependency] private IResourceCache _resourceCache = default!;

        private readonly IReadOnlyList<List<TextRun>> _pages;
        private readonly Color _color;
        private readonly RunechatVisualStyle _style;
        private readonly Texture? _languageIcon;
        private readonly float _scale;
        private readonly Font _regularFont;
        private readonly Font _boldFont;
        private readonly Font _italicFont;
        private readonly Font _boldItalicFont;

        private readonly List<RunechatPageLayout> _layouts = new();
        private Vector2 _cachedSize;
        private bool _layoutDirty = true;
        private int _currentPage;
        private float _pageTime;
        private float _animationTime;

        public RunechatTextControl(IReadOnlyList<List<TextRun>> pages, Color color, RunechatVisualStyle style, Texture? languageIcon = null)
        {
            IoCManager.InjectDependencies(this);

            MouseFilter = MouseFilterMode.Ignore;
            _pages = pages;
            _color = color;
            _style = style;
            _languageIcon = languageIcon;
            _scale = Math.Clamp(_configManager.GetCVar(CCVars.ChatRunechatBubbleScale), MinimumRunechatScale, MaximumRunechatScale);

            var fontSize = style.GetScaledFontSize(_scale);
            var stack = new NotoFontFamilyStack(_resourceCache);
            _regularFont = stack.GetFont(fontSize, FontKind.Regular);
            _boldFont = stack.GetFont(fontSize, FontKind.Bold);
            _italicFont = stack.GetFont(fontSize, FontKind.Italic);
            _boldItalicFont = stack.GetFont(fontSize, FontKind.BoldItalic);
        }

        protected override void FrameUpdate(FrameEventArgs args)
        {
            base.FrameUpdate(args);

            _animationTime += args.DeltaSeconds;

            if (_pages.Count <= 1 || _currentPage >= _pages.Count - 1)
                return;

            _pageTime += args.DeltaSeconds;
            while (_pageTime >= SplitChunkSeconds && _currentPage < _pages.Count - 1)
            {
                _pageTime -= SplitChunkSeconds;
                _currentPage++;
            }
        }

        protected override Vector2 MeasureOverride(Vector2 availableSize)
        {
            EnsureLayout();
            return _cachedSize;
        }

        protected override void Draw(DrawingHandleScreen handle)
        {
            base.Draw(handle);

            if (_pages.Count == 0)
                return;

            EnsureLayout();

            var layout = _layouts[Math.Min(_currentPage, _layouts.Count - 1)];
            var textOpacity = _configManager.GetCVar(CCVars.SpeechBubbleTextOpacity);
            var textColor = _color.WithAlpha(_color.A * textOpacity);
            var lineHeight = GetLineHeight();
            var y = (PixelSize.Y - layout.Height) / 2f;
            var shakeOffset = GetPanicShakeOffset();

            for (var i = 0; i < layout.Lines.Count; i++)
            {
                var line = layout.Lines[i];
                var visibleBounds = GetVisibleBounds(line);
                Texture? languageIcon = i == 0 ? _languageIcon : null;
                var languageIconWidth = languageIcon != null ? GetLanguageIconSize() : 0f;
                var iconWidth = _style.PrefixEmoteIcon && i == 0 ? GetVisibleIconWidth() : 0f;
                var iconGap = iconWidth + languageIconWidth > 0f ? GetIconGap() : 0f;

                var contentWidth = iconWidth + languageIconWidth + iconGap + visibleBounds.Width;
                var x = (PixelSize.X - contentWidth) / 2f + iconWidth + languageIconWidth + iconGap - visibleBounds.Left + shakeOffset;
                var position = new Vector2(x, y);

                if (languageIcon != null)
                {
                    var iconY = position.Y +
                                visibleBounds.Top +
                                visibleBounds.Height / 2f -
                                languageIconWidth / 2f;

                    handle.DrawTextureRect(
                        languageIcon,
                        UIBox2.FromDimensions(position.X - iconGap - languageIconWidth, iconY, languageIconWidth, languageIconWidth),
                        Color.White.WithAlpha(textOpacity));
                }

                if (_style.PrefixEmoteIcon && i == 0)
                {
                    var iconY = position.Y +
                                visibleBounds.Top +
                                visibleBounds.Height / 2f -
                                GetVisibleIconHeight() / 2f -
                                GetVisibleIconTop();

                    DrawEmoteIcon(
                        handle,
                        new Vector2(position.X - iconGap - iconWidth - GetVisibleIconLeft(), iconY),
                        textColor);
                }

                DrawOutlinedLine(handle, position, line, textColor, textOpacity);
                y += lineHeight;
            }
        }

        protected override void UIScaleChanged()
        {
            _layoutDirty = true;
            base.UIScaleChanged();
        }

        private void EnsureLayout()
        {
            if (!_layoutDirty)
                return;

            _layouts.Clear();

            var width = 0f;
            var height = 0f;
            foreach (var page in _pages)
            {
                var layout = LayoutPage(page);
                _layouts.Add(layout);
                width = MathF.Max(width, layout.Width);
                height = MathF.Max(height, layout.Height);
            }

            var horizontalPadding = (GetPadding() + GetHorizontalSafetyPadding()) * UIScale;
            var verticalPadding = GetPadding() * UIScale;
            _cachedSize = new Vector2(
                (width + horizontalPadding * 2f) / UIScale,
                (height + verticalPadding * 2f) / UIScale);
            _layoutDirty = false;
        }

        private RunechatPageLayout LayoutPage(List<TextRun> pageRuns)
        {
            var lines = new List<List<TextRun>>();
            var lineWidths = new List<float>();

            WrapRunPage(pageRuns, lines, lineWidths);

            if (lines.Count == 0)
            {
                lines.Add(new List<TextRun>());
                lineWidths.Add(0f);
            }

            var width = 0f;
            foreach (var lineWidth in lineWidths)
            {
                width = MathF.Max(width, lineWidth);
            }

            var height = GetLineHeight() * lines.Count;
            return new RunechatPageLayout(lines, lineWidths, width, height);
        }

        private void WrapRunPage(List<TextRun> pageRuns, List<List<TextRun>> lines, List<float> lineWidths)
        {
            var words = SplitIntoWords(pageRuns);
            var currentLine = new List<TextRun>();
            var currentWidth = 0f;
            var spaceWidth = MeasureRunWidth(new TextRun(" ", false, false));

            foreach (var word in words)
            {
                var wordWidth = MeasureRunsWidth(word);
                var hasContent = currentLine.Count > 0;
                var neededWidth = hasContent ? currentWidth + spaceWidth + wordWidth : wordWidth;

                if (neededWidth <= GetMaxWidth())
                {
                    if (hasContent)
                        currentLine.Add(new TextRun(" ", false, false));

                    currentLine.AddRange(word);
                    currentWidth = neededWidth;
                    continue;
                }

                if (hasContent)
                {
                    lines.Add(currentLine);
                    lineWidths.Add(currentWidth);
                    currentLine = new List<TextRun>();
                    currentWidth = 0f;
                }

                if (wordWidth <= GetMaxWidth())
                {
                    currentLine.AddRange(word);
                    currentWidth = wordWidth;
                }
                else
                {
                    var brokenLines = BreakWordAcrossLines(word);
                    for (var i = 0; i < brokenLines.Count - 1; i++)
                    {
                        lines.Add(brokenLines[i]);
                        lineWidths.Add(MeasureRunsWidth(brokenLines[i]));
                    }

                    currentLine = brokenLines[^1];
                    currentWidth = MeasureRunsWidth(currentLine);
                }
            }

            if (currentLine.Count > 0)
            {
                lines.Add(currentLine);
                lineWidths.Add(currentWidth);
            }
        }

        private static List<List<TextRun>> SplitIntoWords(IReadOnlyList<TextRun> runs)
        {
            var words = new List<List<TextRun>>();
            var current = new List<TextRun>();

            void FlushWord()
            {
                if (current.Count > 0)
                {
                    words.Add(current);
                    current = new List<TextRun>();
                }
            }

            foreach (var run in runs)
            {
                var text = run.Text;
                var start = 0;

                for (var i = 0; i < text.Length; i++)
                {
                    if (text[i] != ' ')
                        continue;

                    if (i > start)
                        current.Add(run with { Text = text[start..i] });

                    FlushWord();
                    start = i + 1;
                }

                if (start < text.Length)
                    current.Add(run with { Text = text[start..] });
            }

            FlushWord();
            return words;
        }

        private List<List<TextRun>> BreakWordAcrossLines(List<TextRun> word)
        {
            var lines = new List<List<TextRun>>();
            var current = new List<TextRun>();
            var currentWidth = 0f;

            foreach (var piece in word)
            {
                var pieceStartWidth = currentWidth;
                var builder = new StringBuilder();

                foreach (var rune in piece.Text.EnumerateRunes())
                {
                    var candidateText = builder.ToString() + rune;
                    var candidateWidth = MeasureRunWidth(new TextRun(candidateText, piece.Bold, piece.Italic, piece.ColorOverride));

                    if (builder.Length > 0 && pieceStartWidth + candidateWidth > GetMaxWidth())
                    {
                        current.Add(new TextRun(builder.ToString(), piece.Bold, piece.Italic, piece.ColorOverride));
                        lines.Add(current);

                        current = new List<TextRun>();
                        builder.Clear();
                        builder.Append(rune);
                        pieceStartWidth = 0f;
                        currentWidth = MeasureRunWidth(new TextRun(builder.ToString(), piece.Bold, piece.Italic, piece.ColorOverride));
                        continue;
                    }

                    builder.Append(rune);
                    currentWidth = pieceStartWidth + candidateWidth;
                }

                if (builder.Length > 0)
                    current.Add(new TextRun(builder.ToString(), piece.Bold, piece.Italic, piece.ColorOverride));
            }

            lines.Add(current);
            return lines;
        }

        private float GetMaxWidth()
        {
            return _style.GetScaledMaxWidth(_scale) * UIScale;
        }

        private Font GetFont(bool bold, bool italic)
        {
            if (bold && italic)
                return _boldItalicFont;
            if (bold)
                return _boldFont;
            if (italic)
                return _italicFont;
            return _regularFont;
        }

        private float MeasureRunWidth(TextRun run)
        {
            var width = 0f;
            var font = GetFont(run.Bold, run.Italic);

            foreach (var rune in run.Text.EnumerateRunes())
            {
                var metrics = font.GetCharMetrics(rune, UIScale);
                if (metrics == null)
                    continue;

                width += metrics.Value.Advance;
            }

            return width;
        }

        private float MeasureRunsWidth(IReadOnlyList<TextRun> runs)
        {
            var width = 0f;
            foreach (var run in runs)
                width += MeasureRunWidth(run);
            return width;
        }

        private (float Left, float Width, float Top, float Height) GetVisibleBounds(List<TextRun> lineRuns)
        {
            var cursor = 0f;
            var left = 0f;
            var right = 0f;
            var top = 0f;
            var bottom = 0f;
            var foundGlyph = false;

            foreach (var run in lineRuns)
            {
                var font = GetFont(run.Bold, run.Italic);
                var ascent = font.GetAscent(UIScale);

                foreach (var rune in run.Text.EnumerateRunes())
                {
                    var metrics = font.GetCharMetrics(rune, UIScale);
                    if (metrics == null)
                        continue;

                    var glyphLeft = cursor + metrics.Value.BearingX;
                    var glyphRight = glyphLeft + metrics.Value.Width;
                    var glyphTop = ascent - metrics.Value.BearingY;
                    var glyphBottom = glyphTop + metrics.Value.Height;

                    if (!foundGlyph)
                    {
                        left = glyphLeft;
                        right = glyphRight;
                        top = glyphTop;
                        bottom = glyphBottom;
                        foundGlyph = true;
                    }
                    else
                    {
                        left = MathF.Min(left, glyphLeft);
                        right = MathF.Max(right, glyphRight);
                        top = MathF.Min(top, glyphTop);
                        bottom = MathF.Max(bottom, glyphBottom);
                    }

                    cursor += metrics.Value.Advance;
                }
            }

            return foundGlyph
                ? (left, right - left, top, bottom - top)
                : (0f, 0f, 0f, 0f);
        }

        private float GetLineHeight()
        {
            return MathF.Max(1f, _regularFont.GetLineHeight(UIScale) + _style.GetScaledLineHeightOffset(_scale) * UIScale);
        }

        private float GetPadding()
        {
            return _scale * 4f;
        }

        private float GetHorizontalSafetyPadding()
        {
            return _scale * 16f;
        }

        private float GetIconPixelSize()
        {
            return DefaultEmoteIconPixelSize * _scale;
        }

        private float GetPanicShakeOffset()
        {
            if (!_style.UsePanicShake || _animationTime >= PanicShakeDuration)
                return 0f;

            var amount = PanicShakeSize * _scale * UIScale;
            return MathF.Sin(_animationTime * MathF.PI * PanicShakeFrequency) * amount;
        }

        private float GetIconGap()
        {
            return _scale * 2f * UIScale;
        }

        private float GetLanguageIconSize()
            => 14f * _scale * UIScale;

        private float GetVisibleIconLeft()
        {
            return EmoteIconVisibleLeft * GetIconPixelSize() * UIScale;
        }

        private float GetVisibleIconWidth()
        {
            return (EmoteIconVisibleRight - EmoteIconVisibleLeft) * GetIconPixelSize() * UIScale;
        }

        private float GetVisibleIconTop()
        {
            return EmoteIconVisibleTop * GetIconPixelSize() * UIScale;
        }

        private float GetVisibleIconHeight()
        {
            return (EmoteIconVisibleBottom - EmoteIconVisibleTop) * GetIconPixelSize() * UIScale;
        }

        private void DrawOutlinedLine(
            DrawingHandleScreen handle,
            Vector2 position,
            List<TextRun> lineRuns,
            Color textColor,
            float textOpacity)
        {
            var outline = new TextOutline(1.25f, Color.Black.WithAlpha(textColor.A * TextStrokeAlpha));
            var cursor = position;

            foreach (var run in lineRuns)
            {
                var font = GetFont(run.Bold, run.Italic);
                var drawColor = run.ColorOverride is { } runColor
                    ? runColor.WithAlpha(runColor.A * textOpacity)
                    : textColor;

                handle.DrawString(font, cursor, run.Text, UIScale, drawColor, outline);
                cursor += new Vector2(MeasureRunWidth(run), 0f);
            }
        }

        private void DrawEmoteIcon(
            DrawingHandleScreen handle,
            Vector2 position,
            Color textColor)
        {
            var scale = MathF.Max(1f, GetIconPixelSize() * UIScale);
            var iconOrigin = position;

            var iconAlpha = textColor.A * EmoteIconAlpha;

            for (var y = 0; y < EmoteIcon.Length; y++)
            {
                for (var x = 0; x < EmoteIcon[y].Length; x++)
                {
                    var color = EmoteIcon[y][x] switch
                    {
                        '#' => Color.Black.WithAlpha(iconAlpha),
                        'B' => EmoteIconBlue.WithAlpha(iconAlpha),
                        _ => (Color?)null,
                    };

                    if (color is { } iconColor)
                        DrawIconPixel(handle, iconOrigin, x, y, scale, iconColor);
                }
            }
        }

        private static void DrawIconPixel(
            DrawingHandleScreen handle,
            Vector2 iconOrigin,
            int x,
            int y,
            float scale,
            Color color)
        {
            var position = iconOrigin + new Vector2(x * scale, y * scale);
            var size = new Vector2(scale, scale);
            handle.DrawRect(UIBox2.FromDimensions(position, size), color);
        }

        private sealed record RunechatPageLayout(
            IReadOnlyList<List<TextRun>> Lines,
            IReadOnlyList<float> LineWidths,
            float Width,
            float Height);
    }
}
