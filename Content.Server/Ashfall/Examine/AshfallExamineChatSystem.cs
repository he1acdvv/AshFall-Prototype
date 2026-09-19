using Content.Server.Chat.Managers;
using Content.Shared.Ashfall;
using Content.Shared.Ashfall.Examine;
using Content.Shared.Chat;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Server.Ashfall.Examine;

public sealed partial class AshfallExamineChatSystem : EntitySystem
{
    [Dependency] private IChatManager _chatManager = default!;
    [Dependency] private INetConfigurationManager _cfg = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MetaDataComponent, ExamineCompletedEvent>(OnExamineCompleted);
    }

    private void OnExamineCompleted(EntityUid uid, MetaDataComponent component, ref ExamineCompletedEvent args)
    {
        if (!TryComp<ActorComponent>(args.Examiner, out var actor))
            return;

        var channel = actor.PlayerSession.Channel;
        if (!_cfg.GetClientCVar(channel, AshfallCCVars.ChatLogInChat))
            return;

        var markup = args.Message.ToMarkup();
        if (string.IsNullOrWhiteSpace(markup))
            return;

        var netEnt = GetNetEntity(uid);
        var name = FormattedMessage.EscapeText(component.EntityName);
        var title = Loc.GetString("examine-present-tex",
            ("name", name),
            ("id", netEnt.Id),
            ("size", 14));

        FormattedMessage chatMsg = new();
        chatMsg.PushTag(new MarkupNode("examineborder", null, null));
        chatMsg.AddMarkupPermissive($"[color=#cfd3dc][font size=11]{title}[/font][/color]");
        chatMsg.PushNewline();
        chatMsg.PushColor(Color.FromHex("#2d333b"));
        chatMsg.AddText(Loc.GetString("examine-border-line"));
        chatMsg.PushNewline();
        chatMsg.Pop();
        chatMsg.AddMarkupPermissive($"[color=#B0B5BD]{markup}[/color]");
        chatMsg.Pop();

        var wrappedMarkup = chatMsg.ToMarkup();
        var rawMessage = FormattedMessage.RemoveMarkupPermissive(wrappedMarkup);

        _chatManager.ChatMessageToOne(
            ChatChannel.Examine,
            rawMessage,
            wrappedMarkup,
            default,
            hideChat: false,
            client: channel,
            colorOverride: Color.FromHex("#cfd3dc"));
    }
}
