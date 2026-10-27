using System.Linq;
using Content.Client.Options.UI;
using Content.Client.UserInterface.Systems.Chat;
using Content.Client.UserInterface.Systems.Chat.Controls;
using Content.Shared.Chat;

namespace Content.Arcane.Client.Lobby;

public sealed partial class LobbyWebUiController
{
    private int _chatLimit = 200;
    private string _chatDraft = "";
    private ChatUIController ChatController => UIManager.GetUIController<ChatUIController>();

    private bool FocusWebChat(ChatSelectChannel? channel)
    {
        if (!_ready || _surface == null || _gui == null) return false;
        if (_hidden) SetVisible(true);
        if (channel != null) _gui.Chat.SafelySelectChannel(channel.Value);
        _surface.FocusBrowser();
        _surface.Bridge.PushEvent("lobby-chat-focus", "{\"channel\":" + Q(_gui.Chat.SelectedChannel.ToString()) + "}");
        Invalidate();
        return true;
    }

    private string BuildChatState()
    {
        var controller = ChatController;
        var box = _gui!.Chat;
        var filter = box.ChatInput.FilterButton.Popup;
        var channels = ChannelSelectorPopup.ChannelSelectorOrder.Where(c => (controller.SelectableChannels & c) != 0)
            .Select(c => "{\"id\":" + Q(c.ToString()) + ",\"label\":" + Q(ChannelSelectorButton.ChannelSelectorName(c)) + "}");
        var filters = Enum.GetValues<ChatChannel>().Where(c => IsSingleChannel(c) && (controller.FilterableChannels & c) != 0)
            .Select(c => "{\"id\":" + Q(c.ToString()) + ",\"label\":" + Q(Loc.GetString($"hud-chatbox-channel-{c}")) + ",\"active\":" + B(filter.IsActive(c)) + "}");
        var history = controller.History;
        var start = Math.Max(0, history.Count - _chatLimit);
        var messages = history.Skip(start).Select((entry, offset) => (entry.Msg, Index: start + offset))
            .Where(entry => filter.IsActive(entry.Msg.Channel))
            .Select(entry => "{\"id\":" + entry.Index + ",\"parts\":" +
                RichTextJson(entry.Msg.WrappedMessage, entry.Msg.MessageColorOverride ?? entry.Msg.Channel.TextColor()) + "}");
        return "{\"draft\":" + Q(_chatDraft) + ",\"channel\":" + Q(box.SelectedChannel.ToString()) + ",\"maxLength\":" + controller.MaxMessageLength +
            ",\"channels\":[" + string.Join(',', channels) + "],\"filters\":[" + string.Join(',', filters) +
            "],\"messages\":[" + string.Join(',', messages) + "],\"hasOlder\":" + B(start > 0) +
            ",\"sendLabel\":" + Q(Loc.GetString("arcane-lobby-chat-send")) +
            ",\"filterLabel\":" + Q(Loc.GetString("arcane-lobby-chat-filters")) +
            ",\"olderLabel\":" + Q(Loc.GetString("arcane-lobby-chat-older")) +
            ",\"bottomLabel\":" + Q(Loc.GetString("arcane-lobby-chat-bottom")) +
            ",\"placeholder\":" + Q(Loc.GetString("arcane-lobby-chat-placeholder")) + "}";
    }

    private static bool IsSingleChannel(ChatChannel channel)
    {
        var value = (uint) channel;
        return value != 0 && (value & (value - 1)) == 0;
    }

    private bool HandleChatRequest(WebUiBridge bridge, WebUiRequest request)
    {
        if (!request.Event.StartsWith("lobby-chat-", StringComparison.Ordinal)) return false;
        var count = WebUiBridge.PropertyCount(request.Data);
        var box = _gui!.Chat;
        var controller = ChatController;
        switch (request.Event)
        {
            case "lobby-chat-send" when !_hidden && count == 2 &&
                WebUiBridge.TryGetString(request.Data, "text", out var text) &&
                WebUiBridge.TryGetString(request.Data, "channel", out var channelName):
                var channel = ChannelSelectorPopup.ChannelSelectorOrder.FirstOrDefault(c => c.ToString() == channelName);
                if (channel == ChatSelectChannel.None || (controller.SelectableChannels & channel) == 0 ||
                    string.IsNullOrWhiteSpace(text) || text.Contains('\n') || text.Contains('\r') ||
                    text.Length > controller.MaxMessageLength + 32 || controller.SplitInputContents(text).text.Length > controller.MaxMessageLength)
                {
                    bridge.SendError(request.RequestId, "invalid-payload");
                    return true;
                }
                box.SafelySelectChannel(channel);
                box.ChatInput.Input.Text = text;
                controller.SendMessage(box, channel);
                _chatDraft = "";
                break;
            case "lobby-chat-draft" when count == 1 && WebUiBridge.TryGetString(request.Data, "text", out var draft) &&
                draft.Length <= controller.MaxMessageLength && !draft.Contains('\n') && !draft.Contains('\r'):
                _chatDraft = draft;
                box.ChatInput.Input.Text = draft;
                break;
            case "lobby-chat-channel" when count == 1 && WebUiBridge.TryGetString(request.Data, "channel", out var selected):
                var choice = ChannelSelectorPopup.ChannelSelectorOrder.FirstOrDefault(c => c.ToString() == selected);
                if (choice == ChatSelectChannel.None || (controller.SelectableChannels & choice) == 0)
                { bridge.SendError(request.RequestId, "action-unavailable"); return true; }
                box.SafelySelectChannel(choice);
                break;
            case "lobby-chat-filter" when count == 2 && WebUiBridge.TryGetString(request.Data, "channel", out var filtered) &&
                WebUiBridge.TryGetString(request.Data, "active", out var active) && active is "0" or "1":
                var filter = Enum.GetValues<ChatChannel>().FirstOrDefault(c => c.ToString() == filtered && IsSingleChannel(c));
                if (filter == ChatChannel.None || (controller.FilterableChannels & filter) == 0)
                { bridge.SendError(request.RequestId, "action-unavailable"); return true; }
                box.ChatInput.FilterButton.Popup.SetActive(filter, active == "1");
                break;
            case "lobby-chat-older" when count == 0:
                _chatLimit = Math.Min(controller.History.Count, _chatLimit + 200);
                break;
            default:
                bridge.SendError(request.RequestId, "invalid-payload");
                return true;
        }
        Invalidate();
        bridge.SendSuccess(request.RequestId, "{}");
        return true;
    }

    // Emit text/style spans, never HTML. Unknown game markup is ignored, its text remains readable.
    private static string RichTextJson(string markup, Color defaultColor)
    {
        var styles = new List<(string Name, Color Color, bool Bold, bool Italic)>();
        var color = defaultColor;
        var bold = false;
        var italic = false;
        var spans = new List<string>();
        foreach (var node in FormattedMessage.FromMarkupPermissive(markup).Nodes)
        {
            if (node.IsPlainText)
            {
                spans.Add("{\"text\":" + Q(node.Value.StringValue ?? "") + ",\"color\":" + Q(color.ToHexNoAlpha()) +
                    ",\"bold\":" + B(bold) + ",\"italic\":" + B(italic) + "}");
                continue;
            }
            if (node.Name is not ("color" or "bold" or "italic")) continue;
            if (node.Closing)
            {
                var index = styles.FindLastIndex(s => s.Name == node.Name);
                if (index < 0) continue;
                (_, color, bold, italic) = styles[index];
                styles.RemoveRange(index, styles.Count - index);
            }
            else
            {
                styles.Add((node.Name, color, bold, italic));
                if (node.Name == "color" && node.Value.ColorValue is { } next) color = next;
                else if (node.Name == "color" && node.Value.StringValue is { } named && Color.TryParse(named, out var parsed)) color = parsed;
                if (node.Name == "bold") bold = true;
                if (node.Name == "italic") italic = true;
            }
        }
        return "[" + string.Join(',', spans) + "]";
    }
}
