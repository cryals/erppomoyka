// Arcane-Start
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using Robust.Client.WebView;
using Robust.Shared.ContentPack;
using YamlDotNet.RepresentationModel;
using Robust.Client.Input;
using Robust.Client.UserInterface;

namespace Content.Client.Options.UI;

public sealed record WebUiRequest(string Event, string RequestId, YamlMappingNode? Data);

public sealed class WebUiBridge : IDisposable
{
    private const string Origin = "http://webui.local";
    private readonly string _entryUrl;
    private readonly string? _gameUrl;
    private readonly IReadOnlyDictionary<string, string>? _assets;
    private readonly ConcurrentQueue<WebUiRequest> _requests = new();
    private readonly WebViewControl _view;
    private readonly IResourceManager _resources;
    private readonly IWebViewManager _webViewManager;
    private readonly Dictionary<string, ChunkBuffer> _chunks = new();
    private bool _disposed;

    private sealed class ChunkBuffer(int total, string eventName, string requestId)
    {
        public readonly string?[] Parts = new string?[total];
        public readonly string Event = eventName;
        public readonly string RequestId = requestId;
        public readonly DateTime Started = DateTime.UtcNow;
    }

    public WebUiBridge(WebViewControl view, string page = "escape-menu", IReadOnlyDictionary<string, string>? assets = null, string? game = null)
    {
        if (page is not ("escape-menu" or "lobby" or "doom-arcade" or "arcade")) throw new ArgumentOutOfRangeException(nameof(page));
        if (game != null && (page != "arcade" || game is not ("asteroids" or "flappy_bird" or "pacman" or "prince_of_persia" or "snake_game" or "space_Invaders" or "nu_pogodi" or "firered")))
            throw new ArgumentOutOfRangeException(nameof(game));
        _gameUrl = game == null ? null : Origin + "/dist/arcade/" + game + "/index.html";
        _entryUrl = Origin + "/dist/src/interfaces/" + page + "/index.html";
        _assets = assets;
        _view = view;
        _resources = IoCManager.Resolve<IResourceManager>();
        _webViewManager = IoCManager.Resolve<IWebViewManager>();
        view.AddResourceRequestHandler(HandleRequest);
        view.AddBeforeBrowseHandler(BeforeBrowse);
        view.Url = _entryUrl;
    }

    public bool TryDequeue(out WebUiRequest request) => _requests.TryDequeue(out request!);

    /// <summary>Forward physical browser shortcuts through configured game bindings on the UI thread.</summary>
    public bool HandleNativeKey(WebUiRequest request)
    {
        if (request.Event != "native-key") return false;
        if (PropertyCount(request.Data) != 2 || !TryGetString(request.Data, "code", out var code) ||
            !TryGetString(request.Data, "modifiers", out var modifiers) || !int.TryParse(modifiers, out var mask) || mask is < 0 or > 15 ||
            !TryPhysicalKey(code, out var key))
        { SendError(request.RequestId, "invalid-payload"); return true; }
        var ui = IoCManager.Resolve<IUserInterfaceManager>();
        var input = IoCManager.Resolve<IInputManager>();
        // A queued event must never steal focus back from a console or another native window.
        if (ui.KeyboardFocused != _view)
        { SendSuccess(request.RequestId, "{}"); return true; }
        ui.ReleaseKeyboardFocus();
        var synthetic = new List<Keyboard.Key>();
        var modifierKeys = new[] { Keyboard.Key.Shift, Keyboard.Key.Control, Keyboard.Key.Alt, Keyboard.Key.LSystem };
        KeyEventArgs Args(Keyboard.Key value) => new(value, false, (mask & 4) != 0, (mask & 2) != 0,
            (mask & 1) != 0, (mask & 8) != 0, 0);
        try
        {
            for (var i = 0; i < modifierKeys.Length; i++)
            {
                if ((mask & (1 << i)) == 0 || input.IsKeyDown(modifierKeys[i])) continue;
                synthetic.Add(modifierKeys[i]);
                input.KeyDown(Args(modifierKeys[i]));
            }
            input.KeyDown(Args(key));
        }
        finally
        {
            input.KeyUp(Args(key));
            foreach (var modifier in synthetic) input.KeyUp(Args(modifier));
        }
        SendSuccess(request.RequestId, "{}");
        return true;
    }

    private static bool TryPhysicalKey(string code, out Keyboard.Key key)
    {
        key = Keyboard.Key.Unknown;
        if (code.Length == 4 && code.StartsWith("Key", StringComparison.Ordinal) && code[3] is >= 'A' and <= 'Z')
            key = (Keyboard.Key) ((int) Keyboard.Key.A + code[3] - 'A');
        else if (code.Length == 6 && code.StartsWith("Digit", StringComparison.Ordinal) && code[5] is >= '0' and <= '9')
            key = (Keyboard.Key) ((int) Keyboard.Key.Num0 + code[5] - '0');
        else if (code.Length is 2 or 3 && code[0] == 'F' && int.TryParse(code[1..], out var function) && function is >= 1 and <= 24)
            key = (Keyboard.Key) ((int) Keyboard.Key.F1 + function - 1);
        else key = code switch
        {
            "Backquote" => Keyboard.Key.Tilde, "Escape" => Keyboard.Key.Escape,
            "BracketLeft" => Keyboard.Key.LBracket, "BracketRight" => Keyboard.Key.RBracket,
            "Semicolon" => Keyboard.Key.SemiColon, "Quote" => Keyboard.Key.Apostrophe,
            "Comma" => Keyboard.Key.Comma, "Period" => Keyboard.Key.Period,
            "Slash" => Keyboard.Key.Slash, "Backslash" => Keyboard.Key.BackSlash,
            "Minus" => Keyboard.Key.Minus, "Equal" => Keyboard.Key.Equal,
            "Insert" => Keyboard.Key.Insert, "Delete" => Keyboard.Key.Delete,
            "Home" => Keyboard.Key.Home, "End" => Keyboard.Key.End,
            "PageUp" => Keyboard.Key.PageUp, "PageDown" => Keyboard.Key.PageDown,
            _ => Keyboard.Key.Unknown,
        };
        return key != Keyboard.Key.Unknown;
    }

    private static bool IsLocal(Uri uri) => uri.Scheme == Uri.UriSchemeHttp &&
        uri.Host.Equals("webui.local", StringComparison.OrdinalIgnoreCase) && uri.IsDefaultPort &&
        string.IsNullOrEmpty(uri.UserInfo);

    private void BeforeBrowse(IBeforeBrowseContext context)
    {
        if (!string.Equals(context.Url, _entryUrl, StringComparison.Ordinal) &&
            !string.Equals(context.Url, _gameUrl, StringComparison.Ordinal))
            context.DoCancel();
    }

    private void HandleRequest(IRequestHandlerContext context)
    {
        Uri uri;
        try { uri = new Uri(context.Url); }
        catch (Exception) { context.DoCancel(); return; }
        // Traversal checks apply to the path, not encoded bridge data in the query string.
        var requestPath = context.Url.Split('?')[0];
        if (_disposed || !IsLocal(uri) || context.Method != "GET" || context.IsDownload ||
            requestPath.Contains("..", StringComparison.Ordinal) || requestPath.Contains('%') &&
            (requestPath.Contains("%2e", StringComparison.OrdinalIgnoreCase) ||
             requestPath.Contains("%2f", StringComparison.OrdinalIgnoreCase) ||
             requestPath.Contains("%5c", StringComparison.OrdinalIgnoreCase)))
        {
            context.DoCancel();
            return;
        }

        if (uri.AbsolutePath.StartsWith("/__bridge__/", StringComparison.Ordinal))
        {
            HandleBridgeRequest(context, uri);
            return;
        }

        var path = uri.AbsolutePath;
        var approvedAsset = _assets != null && _assets.TryGetValue(path, out var assetPath);
        if ((!uri.AbsolutePath.StartsWith("/dist/", StringComparison.Ordinal) && !approvedAsset) || uri.AbsolutePath.Contains('\\') ||
            uri.AbsolutePath.Contains(':') || uri.AbsolutePath.Contains('%'))
        {
            context.DoCancel();
            return;
        }

        if (approvedAsset) path = _assets![path];
        var dot = path.LastIndexOf('.');
        var extension = dot < path.LastIndexOf('/') ? "" : path[(dot + 1)..];
        if (!_resources.TryContentFileRead(path, out var stream))
        {
            RespondText(context, "Not found", HttpStatusCode.NotFound);
            return;
        }

        if (!_webViewManager.TryGetResourceMimeType(extension, out var mime))
            mime = "application/octet-stream";
        mime = extension.ToLowerInvariant() switch
        {
            "webp" => "image/webp", "png" => "image/png", "jpg" or "jpeg" => "image/jpeg",
            "gif" => "image/gif", "txt" or "cfg" => "text/plain; charset=utf-8",
            "wasm" => "application/wasm", "wad" => "application/octet-stream", _ => mime,
        };
        context.DoRespondStream(stream, mime);
    }

    private void HandleBridgeRequest(IRequestHandlerContext context, Uri uri)
    {
        var eventName = uri.AbsolutePath["/__bridge__/".Length..];
        var requestId = "";
        var encoded = "";
        foreach (var part in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var pair = part.Split('=', 2);
            if (pair.Length != 2) continue;
            if (pair[0] == "requestId") requestId = Uri.UnescapeDataString(pair[1]);
            if (pair[0] == "data") encoded = Uri.UnescapeDataString(pair[1]);
        }

        if (requestId.Length is < 2 or > 80 || !requestId.All(char.IsAsciiLetterOrDigit) ||
            encoded.Length > 16384 || eventName.Length > 64)
        {
            context.DoCancel();
            return;
        }

        _requests.Enqueue(new WebUiRequest(eventName, requestId, ParsePayload(encoded)));
        context.DoRespondStream(new MemoryStream(), "text/plain", HttpStatusCode.NoContent);
    }

    private static YamlMappingNode? ParsePayload(string encoded, int maxJsonLength = 8192)
    {
        try
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
            if (json.Length > maxJsonLength || !IsFlatJsonObject(json)) return null;
            var yaml = new YamlStream();
            yaml.Load(new StringReader(json));
            return yaml.Documents.Count == 1 ? yaml.Documents[0].RootNode as YamlMappingNode : null;
        }
        catch (Exception) { return null; }
    }

    // Every request used by this menu is a flat JSON object. Reject YAML's wider syntax
    // before using the sandbox-approved YAML reader to decode quoted strings.
    private static bool IsFlatJsonObject(string json)
    {
        var index = 0;
        SkipSpace(json, ref index);
        if (index >= json.Length || json[index++] != '{') return false;
        SkipSpace(json, ref index);
        if (index < json.Length && json[index] == '}')
        {
            index++;
            SkipSpace(json, ref index);
            return index == json.Length;
        }
        while (index < json.Length)
        {
            if (!ScanJsonString(json, ref index)) return false;
            SkipSpace(json, ref index);
            if (index >= json.Length || json[index++] != ':') return false;
            SkipSpace(json, ref index);
            if (index >= json.Length) return false;
            if (json[index] == '"')
            {
                if (!ScanJsonString(json, ref index)) return false;
            }
            else
            {
                if (json[index] == '-') index++;
                if (index >= json.Length || json[index] is not (>= '0' and <= '9')) return false;
                if (json[index++] != '0')
                    while (index < json.Length && json[index] is >= '0' and <= '9') index++;
            }
            SkipSpace(json, ref index);
            if (index >= json.Length) return false;
            if (json[index] == '}')
            {
                index++;
                SkipSpace(json, ref index);
                return index == json.Length;
            }
            if (json[index++] != ',') return false;
            SkipSpace(json, ref index);
        }
        return false;
    }

    private static bool ScanJsonString(string json, ref int index)
    {
        if (index >= json.Length || json[index++] != '"') return false;
        while (index < json.Length)
        {
            var c = json[index++];
            if (c == '"') return true;
            if (c < 32) return false;
            if (c != '\\') continue;
            if (index >= json.Length) return false;
            c = json[index++];
            if (c == 'u')
            {
                if (index + 4 > json.Length) return false;
                for (var i = 0; i < 4; i++)
                    if (!char.IsAsciiHexDigit(json[index++])) return false;
            }
            else if (c is not ('"' or '\\' or '/' or 'b' or 'f' or 'n' or 'r' or 't')) return false;
        }
        return false;
    }

    private static void SkipSpace(string json, ref int index)
    {
        while (index < json.Length && json[index] is ' ' or '\t' or '\r' or '\n') index++;
    }

    public static bool TryGetString(YamlMappingNode? data, string key, out string value)
    {
        value = "";
        if (data == null) return false;
        foreach (var pair in data)
        {
            if (pair.Key is not YamlScalarNode name || name.Value != key ||
                pair.Value is not YamlScalarNode scalar || scalar.Value == null) continue;
            value = scalar.Value;
            return true;
        }
        return false;
    }

    public static int PropertyCount(YamlMappingNode? data)
    {
        if (data == null) return -1;
        var count = 0;
        foreach (var _ in data) count++;
        return count;
    }

    public bool TryReassemble(WebUiRequest request, out WebUiRequest complete)
    {
        complete = request;
        if (request.Event != "chunk-data") return true;
        var maxChunks = 32;
        var maxPartLength = 1500;
        if (PropertyCount(request.Data) != 6 || !TryGetString(request.Data, "chunkId", out var chunkId) ||
            !TryGetString(request.Data, "index", out var indexText) || !int.TryParse(indexText, out var index) ||
            !TryGetString(request.Data, "total", out var totalText) || !int.TryParse(totalText, out var total) ||
            !TryGetString(request.Data, "data", out var part) ||
            !TryGetString(request.Data, "originalEventType", out var eventName) ||
            !TryGetString(request.Data, "originalRequestId", out var requestId) ||
            total < 1 || index < 0 || chunkId.Length > 80 || requestId != request.RequestId ||
            eventName is not ("escape-menu-action" or "escape-menu-get-state" or "escape-menu-close" or
                "lobby-action" or "lobby-get-state" or "lobby-set-ui-visible" or "lobby-layout" or "lobby-chat-send" or "lobby-chat-draft" or
                "firered-save-load" or "firered-save-write"))
        {
            SendError(request.RequestId, "invalid-payload");
            return false;
        }

        if (eventName is "firered-save-load" or "firered-save-write")
        {
            maxChunks = 224;
            maxPartLength = 9000;
        }

        if (total > maxChunks || part.Length > maxPartLength)
        {
            SendError(request.RequestId, "invalid-payload");
            return false;
        }

        foreach (var (key, buffer) in _chunks.ToArray())
            if (DateTime.UtcNow - buffer.Started > TimeSpan.FromSeconds(10)) _chunks.Remove(key);
        if (!_chunks.TryGetValue(chunkId, out var chunk))
        {
            if (_chunks.Count >= 16) { SendError(requestId, "invalid-payload"); return false; }
            chunk = new ChunkBuffer(total, eventName, requestId);
            _chunks.Add(chunkId, chunk);
        }
        if (chunk.Parts.Length != total || chunk.Event != eventName || chunk.RequestId != requestId ||
            chunk.Parts[index] != null)
        {
            _chunks.Remove(chunkId);
            SendError(requestId, "invalid-payload");
            return false;
        }
        chunk.Parts[index] = part;
        if (chunk.Parts.Any(x => x == null)) return false;
        _chunks.Remove(chunkId);
        complete = new WebUiRequest(eventName, requestId, ParsePayload(string.Concat(chunk.Parts),
            eventName is "firered-save-load" or "firered-save-write" ? 1_500_000 : 8192));
        if (complete.Data != null) return true;
        SendError(requestId, "invalid-payload");
        return false;
    }

    public void SendSuccess(string requestId, string json) => SendPayload("nova:complete", requestId, json);
    public void PushEvent(string name, string json)
    {
        if (name is not ("escape-menu-state" or "lobby-state" or "lobby-chat-focus")) return;
        SendPayload("nova:" + name, null, json);
    }

    private void SendPayload(string eventName, string? requestId, string json)
    {
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
        if (encoded.Length <= 1500)
        {
            Dispatch(eventName, requestId == null ? "{\"data\":" + Quote(encoded) + "}" :
                "{\"requestId\":" + Quote(requestId) + ",\"data\":" + Quote(encoded) + "}");
            return;
        }
        var chunkId = Guid.NewGuid().ToString("N");
        var total = (encoded.Length + 1499) / 1500;
        if (requestId != null)
            DispatchEncoded("nova:response-chunk-init", "{\"chunkId\":" + Quote(chunkId) + ",\"requestId\":" + Quote(requestId) + ",\"total\":" + total + "}");
        for (var i = 0; i < total; i++)
        {
            var part = Quote(encoded.Substring(i * 1500, Math.Min(1500, encoded.Length - i * 1500)));
            var detail = "{\"chunkId\":" + Quote(chunkId) + ",\"index\":" + i + ",\"data\":" + part;
            if (requestId == null)
                detail += ",\"total\":" + total + ",\"eventType\":" + Quote(eventName["nova:".Length..]) + "}";
            else detail += "}";
            DispatchEncoded(requestId == null ? "nova:chunk-event" : "nova:response-chunk-data", detail);
        }
    }

    public void SendError(string requestId, string code) =>
        Dispatch("nova:error", "{\"requestId\":" + Quote(requestId) + ",\"data\":" + Quote(code) + "}");

    private void DispatchEncoded(string eventName, string json) =>
        Dispatch(eventName, "{\"data\":" + Quote(Convert.ToBase64String(Encoding.UTF8.GetBytes(json))) + "}");

    private void Dispatch(string name, string detail)
    {
        if (_disposed) return;
        _view.ExecuteJavaScript("window.dispatchEvent(new CustomEvent(" + Quote(name) + ",{detail:" + detail + "}));");
    }

    public static string Quote(string value)
    {
        var result = new StringBuilder(value.Length + 2).Append('"');
        foreach (var c in value)
        {
            switch (c)
            {
                case '"': result.Append("\\\""); break;
                case '\\': result.Append("\\\\"); break;
                case '\n': result.Append("\\n"); break;
                case '\r': result.Append("\\r"); break;
                case '\t': result.Append("\\t"); break;
                default:
                    if (c < 32 || c == '\u2028' || c == '\u2029') result.Append("\\u").Append(((int)c).ToString("x4"));
                    else result.Append(c);
                    break;
            }
        }
        return result.Append('"').ToString();
    }

    private static void RespondText(IRequestHandlerContext context, string text, HttpStatusCode code) =>
        context.DoRespondStream(new MemoryStream(Encoding.UTF8.GetBytes(text)), "text/plain", code);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _view.RemoveResourceRequestHandler(HandleRequest);
        _view.RemoveBeforeBrowseHandler(BeforeBrowse);
        _chunks.Clear();
        while (_requests.TryDequeue(out _)) { }
    }
}
// Arcane-End
