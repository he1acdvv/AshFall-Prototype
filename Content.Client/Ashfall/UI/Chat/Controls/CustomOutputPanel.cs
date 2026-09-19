using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.RichText;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.Client.Ashfall.UI.Chat.Controls;

public sealed partial class CustomOutputPanel : Control
{
    public const string StyleClassOutputPanelScrollDownButton = "outputPanelScrollDownButton";

    [Dependency] private MarkupTagManager _tagManager = default!;
    [Dependency] private IEntityManager _entManager = default!;

    public const string StylePropertyFont = "font";
    public const string StylePropertyStyleBox = "stylebox";

    private readonly CustomRingBufferList<CustomRichTextEntry> _entries = new();
    private bool _isAtBottom = true;

    private int _totalContentHeight;
    private bool _firstLine = true;
    private StyleBox? _styleBoxOverride;
    private VScrollBar _scrollBar;
    private Button _scrollDownButton;

    public bool ShowScrollDownButton
    {
        get => _showScrollDownButton;
        set
        {
            _showScrollDownButton = value;
            _updateScrollButtonVisibility();
        }
    }
    private bool _showScrollDownButton;

    public bool ScrollFollowing { get; set; } = true;

    private bool _invalidOnVisible;

    public CustomOutputPanel()
    {
        IoCManager.InjectDependencies(this);
        MouseFilter = MouseFilterMode.Pass;
        RectClipContent = true;

        _scrollBar = new VScrollBar
        {
            Name = "_v_scroll",
            HorizontalAlignment = HAlignment.Right
        };
        AddChild(_scrollBar);

        AddChild(_scrollDownButton = new Button
        {
            Name = "scrollLiveBtn",
            StyleClasses = { StyleClassOutputPanelScrollDownButton },
            VerticalAlignment = VAlignment.Bottom,
            HorizontalAlignment = HAlignment.Center,
            Text = string.Format("⬇    {0}    ⬇", Loc.GetString("output-panel-scroll-down-button-text")),
            MaxWidth = 300,
            Visible = false,
        });

        _scrollDownButton.OnPressed += _ => ScrollToBottom();

        _scrollBar.OnValueChanged += _ =>
        {
            _isAtBottom = _scrollBar.IsAtEnd;
            _updateScrollButtonVisibility();
        };
    }

    private void _updateScrollButtonVisibility()
    {
        _scrollDownButton.Visible = ShowScrollDownButton && !_isAtBottom;
    }

    public int EntryCount => _entries.Count;

    public void UpdateLastMessage(FormattedMessage message)
    {
        if (_entries.Count == 0)
            return;

        var newEnt = new CustomRichTextEntry(message, this, _tagManager, _entManager);
        newEnt.Update(_tagManager, _getFont(), _getContentBox().Width, UIScale);
        _entries[_entries.Count - 1] = newEnt;
    }

    public StyleBox? StyleBoxOverride
    {
        get => _styleBoxOverride;
        set
        {
            _styleBoxOverride = value;
            InvalidateMeasure();
            _invalidateEntries();
        }
    }

    public void Clear()
    {
        _firstLine = true;
        foreach (var entry in _entries)
        {
            entry.RemoveControls();
        }

        _entries.Clear();
        _totalContentHeight = 0;
        _scrollBar.MaxValue = Math.Max(_scrollBar.Page, _totalContentHeight);
        _scrollBar.Value = 0;
        _updateScrollButtonVisibility();
    }

    public void RemoveEntry(Index index)
    {
        if (_entries.Count == 0)
            return;

        var entry = _entries[index];
        entry.RemoveControls();
        _entries.RemoveAt(index.GetOffset(_entries.Count));

        var font = _getFont();
        _totalContentHeight -= entry.Height + font.GetLineSeparation(UIScale);
        if (_entries.Count == 0)
        {
            Clear();
            return;
        }

        _scrollBar.MaxValue = Math.Max(_scrollBar.Page, _totalContentHeight);
        _updateScrollButtonVisibility();
    }

    public void AddText(string text)
    {
        var msg = new FormattedMessage();
        msg.AddText(text);
        AddMessage(msg, null);
    }

    public void AddMessage(FormattedMessage message)
    {
        AddMessage(message, CustomRichTextEntry.DefaultTags);
    }

    public void AddMessage(FormattedMessage message, Type[]? tagsAllowed)
    {
        var entry = new CustomRichTextEntry(message, this, _tagManager, _entManager, tagsAllowed);

        entry.Update(_tagManager, _getFont(), _getContentBox().Width, UIScale);

        _entries.Add(entry);
        var font = _getFont();
        _totalContentHeight += entry.Height;
        if (_firstLine)
        {
            _firstLine = false;
        }
        else
        {
            _totalContentHeight += font.GetLineSeparation(UIScale);
        }

        _scrollBar.MaxValue = Math.Max(_scrollBar.Page, _totalContentHeight);
        if (_isAtBottom && ScrollFollowing)
        {
            _scrollBar.MoveToEnd();
        }

        _updateScrollButtonVisibility();
    }

    public void ScrollToBottom()
    {
        _scrollBar.MoveToEnd();
        _isAtBottom = true;
        _updateScrollButtonVisibility();
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var style = _getStyleBox();
        var font = _getFont();
        var lineSeparation = font.GetLineSeparation(UIScale);
        style?.Draw(handle, PixelSizeBox, UIScale);
        var contentBox = _getContentBox();

        var entryOffset = -_scrollBar.Value;

        var context = new MarkupDrawingContext(2);

        foreach (ref var entry in _entries)
        {
            if (entryOffset + entry.Height < 0)
            {
                entry.HideControls();
                entryOffset += entry.Height + lineSeparation;
                continue;
            }

            if (entryOffset > contentBox.Height)
            {
                entry.HideControls();
                continue;
            }

            entry.Draw(_tagManager, handle, font, contentBox, entryOffset, _scrollBar.PixelSize, context, UIScale);

            entryOffset += entry.Height + lineSeparation;
        }
    }

    protected override void MouseWheel(GUIMouseWheelEventArgs args)
    {
        base.MouseWheel(args);

        if (MathHelper.CloseToPercent(0, args.Delta.Y))
        {
            return;
        }

        _scrollBar.ValueTarget -= _getScrollSpeed() * args.Delta.Y;
    }

    protected override void Resized()
    {
        base.Resized();

        var styleBoxSize = _getStyleBox()?.MinimumSize.Y ?? 0;

        _scrollBar.Page = UIScale * (Height - styleBoxSize);
        _invalidateEntries();
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        return _getStyleBox()?.MinimumSize ?? Vector2.Zero;
    }

    public void _invalidateEntries()
    {
        _totalContentHeight = 0;
        var font = _getFont();
        var sizeX = _getContentBox().Width;
        foreach (ref var entry in _entries)
        {
            entry.Update(_tagManager, font, sizeX, UIScale);
            _totalContentHeight += entry.Height + font.GetLineSeparation(UIScale);
        }

        _scrollBar.MaxValue = Math.Max(_scrollBar.Page, _totalContentHeight);
        if (_isAtBottom && ScrollFollowing)
        {
            _scrollBar.MoveToEnd();
        }

        _updateScrollButtonVisibility();
    }

    private Font _getFont()
    {
        if (TryGetStyleProperty<Font>(StylePropertyFont, out var font))
        {
            return font;
        }

        try
        {
            var res = IoCManager.Resolve<Robust.Client.ResourceManagement.IResourceCache>();
            if (res.TryGetResource<Robust.Client.ResourceManagement.FontResource>(new ResPath("/Fonts/Tahoma/tahoma.ttf"), out var fontRes))
            {
                return new VectorFont(fontRes, 12);
            }
        }
        catch
        {
            // fallback
        }

        return UserInterfaceManager.ThemeDefaults.DefaultFont;
    }

    private StyleBox? _getStyleBox()
    {
        if (StyleBoxOverride != null)
        {
            return StyleBoxOverride;
        }

        TryGetStyleProperty<StyleBox>(StylePropertyStyleBox, out var box);
        return box;
    }

    private float _getScrollSpeed()
    {
        return GetScrollSpeed(_getFont(), UIScale);
    }

    private UIBox2 _getContentBox()
    {
        var style = _getStyleBox();
        var box = style?.GetContentBox(PixelSizeBox, UIScale) ?? PixelSizeBox;
        box.Right = Math.Max(box.Left, box.Right - _scrollBar.DesiredPixelSize.X);
        return box;
    }

    protected override void UIScaleChanged()
    {
        if (!VisibleInTree)
            _invalidOnVisible = true;
        else
            _invalidateEntries();

        base.UIScaleChanged();
    }

    internal static float GetScrollSpeed(Font font, float scale)
    {
        return font.GetLineHeight(scale) * 2;
    }

    protected override void EnteredTree()
    {
        base.EnteredTree();
        _invalidateEntries();
    }

    protected override void VisibilityChanged(bool newVisible)
    {
        if (newVisible && _invalidOnVisible)
        {
            _invalidateEntries();
            _invalidOnVisible = false;
        }
    }
}
