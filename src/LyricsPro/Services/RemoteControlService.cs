using LyricsPro.Models;
using LyricsPro.ViewModels;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Web;

namespace LyricsPro.Services;

public class RemoteControlService : IDisposable
{
    public int    Port      { get; } = 7890;
    public bool   IsRunning { get; private set; }
    public string LocalUrl  { get; private set; } = "";

    public event Action<string>?                          UrlChanged;
    public event Action<IEnumerable<BibleVerse>, string, int>? PresentBibleRequested;
    public event Action<LyricEntry>?                      PresentLyricRequested;

    private TcpListener?             _tcp;
    private CancellationTokenSource? _cts;
    private ProjectorViewModel?      _vm;

    // ── Start / Stop ─────────────────────────────────────────────

    public Task StartAsync(ProjectorViewModel vm)
    {
        if (IsRunning) return Task.CompletedTask;
        _vm  = vm;
        _cts = new CancellationTokenSource();
        _tcp = new TcpListener(IPAddress.Any, Port);
        _tcp.Start();

        var ip = GetLocalIp();
        LocalUrl = $"http://{ip}:{Port}";
        IsRunning = true;
        UrlChanged?.Invoke(LocalUrl);
        Task.Run(() => AcceptLoop(_cts.Token));
        return Task.CompletedTask;
    }

    public void Stop()
    {
        _cts?.Cancel();
        try { _tcp?.Stop(); } catch { }
        IsRunning = false; LocalUrl = "";
    }

    public void Dispose() => Stop();

    // ── TCP loop ─────────────────────────────────────────────────

    private async Task AcceptLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try { var c = await _tcp!.AcceptTcpClientAsync(ct); _ = Task.Run(() => Handle(c), ct); }
            catch { break; }
        }
    }

    private async Task Handle(TcpClient client)
    {
        using var _ = client;
        client.ReceiveTimeout = 3000; client.SendTimeout = 3000;
        try
        {
            var stream = client.GetStream();
            var buf = new byte[8192];
            var read = await stream.ReadAsync(buf, 0, buf.Length);
            var req  = Encoding.UTF8.GetString(buf, 0, read);

            var line   = req.Split('\n')[0].Trim();
            var parts  = line.Split(' ');
            if (parts.Length < 2) return;
            var method = parts[0].ToUpper();
            var full   = parts[1];
            var path   = full.Split('?')[0];
            var qs     = full.Contains('?') ? full[(full.IndexOf('?') + 1)..] : "";

            await Route(stream, method, path, qs);
        }
        catch { }
    }

    private async Task Route(NetworkStream s, string method, string path, string qs)
    {
        string? html = null; string? json = null;

        switch ((method, path))
        {
            case ("GET", "/"):
                html = PageHome(); break;
            case ("GET", "/state"):
                json = JsonSerializer.Serialize(GetState()); break;
            case ("GET", "/lyrics"):
                html = PageLyrics(); break;
            case ("GET", "/bible"):
                html = PageBible(); break;
            case ("GET", "/bible-chapter"):
            {
                var bookId  = int.TryParse(Param(qs, "book"),    out var b) ? b : 1;
                var chapter = int.TryParse(Param(qs, "chapter"), out var c) ? c : 1;
                html = PageBibleChapter(bookId, chapter); break;
            }
            case ("POST", "/next"):
            case ("POST", "/prev"):
            case ("POST", "/blank"):
            case ("POST", "/font+"):
            case ("POST", "/font-"):
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    if (_vm is null) return;
                    switch (path)
                    {
                        case "/next":  _vm.Next();         break;
                        case "/prev":  _vm.Previous();     break;
                        case "/blank": _vm.ToggleBlank();  break;
                        case "/font+": _vm.IncreaseFont(); break;
                        case "/font-": _vm.DecreaseFont(); break;
                    }
                });
                json = JsonSerializer.Serialize(GetState()); break;

            // ── Media player controls ──────────────────────────────
            case ("GET",  "/media-state"):
                json = JsonSerializer.Serialize(GetMediaState()); break;

            case ("POST", "/audio-toggle"):
                Avalonia.Threading.Dispatcher.UIThread.Post(
                    () => AppServices.MediaState.AudioTogglePlayPause());
                json = JsonSerializer.Serialize(GetMediaState()); break;

            case ("POST", "/audio-stop"):
                Avalonia.Threading.Dispatcher.UIThread.Post(
                    () => AppServices.MediaState.AudioStopAction());
                json = JsonSerializer.Serialize(GetMediaState()); break;

            case ("POST", "/audio-vol+"):
                Avalonia.Threading.Dispatcher.UIThread.Post(
                    () => AppServices.MediaState.AudioVolumeUp());
                json = JsonSerializer.Serialize(GetMediaState()); break;

            case ("POST", "/audio-vol-"):
                Avalonia.Threading.Dispatcher.UIThread.Post(
                    () => AppServices.MediaState.AudioVolumeDown());
                json = JsonSerializer.Serialize(GetMediaState()); break;

            case ("POST", "/video-toggle"):
                AppServices.MediaState.VideoPlayPause?.Invoke();
                json = JsonSerializer.Serialize(GetMediaState()); break;

            case ("POST", "/video-stop"):
                AppServices.MediaState.VideoStop?.Invoke();
                json = JsonSerializer.Serialize(GetMediaState()); break;

            // ── Lyrics / Bible ─────────────────────────────────────
            case ("POST", "/present-lyric"):
            {
                var id = int.TryParse(Param(qs, "id"), out var i) ? i : 0;
                var entry = id > 0 ? AppServices.Database.GetLyric(id) : null;
                if (entry is not null)
                    Avalonia.Threading.Dispatcher.UIThread.Post(
                        () => PresentLyricRequested?.Invoke(entry));
                json = """{"ok":true}"""; break;
            }

            case ("POST", "/present-bible"):
            {
                var bookId  = int.TryParse(Param(qs, "book"),    out var b) ? b : 1;
                var chapter = int.TryParse(Param(qs, "chapter"), out var c) ? c : 1;
                var version = Param(qs, "version").ToUpper();
                if (string.IsNullOrEmpty(version)) version = "ACF";

                var books  = AppServices.Database.GetBibleBooks();
                var book   = books.FirstOrDefault(x => x.Id == bookId);
                var verses = AppServices.Database.GetVerses(bookId, chapter, version);

                if (book is not null && verses.Count > 0)
                    Avalonia.Threading.Dispatcher.UIThread.Post(
                        () => PresentBibleRequested?.Invoke(verses, book.Name, chapter));

                json = """{"ok":true}"""; break;
            }

            default:
                await WriteRaw(s, "HTTP/1.1 404 Not Found\r\nContent-Length:0\r\n\r\n");
                return;
        }

        if (html is not null) await WriteBody(s, html, "text/html; charset=utf-8");
        else if (json is not null) await WriteBody(s, json, "application/json");
    }

    // ── Pages ─────────────────────────────────────────────────────

    private static string Shell(string title, string body, string backUrl = "/")
    {
        const string css =
            "*{box-sizing:border-box;margin:0;padding:0}" +
            "body{background:#0a0a0a;color:#f0f0f0;font-family:system-ui,sans-serif;min-height:100dvh}" +
            ".top{background:#111;padding:12px 16px;display:flex;align-items:center;gap:12px;" +
            "border-bottom:1px solid #222;position:sticky;top:0}" +
            ".top a{color:#D4610A;text-decoration:none;font-size:20px}" +
            ".top h1{font-size:15px;font-weight:700;color:#D4610A}" +
            ".list{padding:8px 0}" +
            ".item{display:flex;align-items:center;padding:14px 16px;border-bottom:1px solid #1a1a1a;" +
            "gap:12px;cursor:pointer;-webkit-tap-highlight-color:transparent}" +
            ".item:active{background:#1a1a1a}" +
            ".item-icon{font-size:18px;opacity:.5;flex-shrink:0}" +
            ".item-text{flex:1;overflow:hidden}" +
            ".item-title{font-size:14px;font-weight:600;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}" +
            ".item-sub{font-size:11px;color:#666;margin-top:2px}" +
            ".btn-play{background:#D4610A;border:none;color:white;border-radius:8px;" +
            "padding:8px 16px;font-size:13px;font-weight:700;cursor:pointer;flex-shrink:0}" +
            ".btn-play:active{opacity:.7}" +
            ".empty{text-align:center;padding:60px 24px;color:#333;font-size:14px}" +
            ".section{padding:10px 16px 4px;font-size:11px;font-weight:700;" +
            "color:#444;text-transform:uppercase;letter-spacing:.5px}";

        return "<!DOCTYPE html><html lang=\"pt-BR\"><head>" +
               "<meta charset=\"utf-8\"/>" +
               "<meta name=\"viewport\" content=\"width=device-width,initial-scale=1,maximum-scale=1\"/>" +
               "<title>" + title + "</title>" +
               "<style>" + css + "</style></head><body>" +
               "<div class=\"top\"><a href=\"" + backUrl + "\">&#x2190;</a><h1>" + title + "</h1></div>" +
               body +
               "</body></html>";
    }

    private string PageHome()
    {
        // Extra CSS for media section
        const string mediaStyle =
            ".media-bar{background:#1a0d00;border:1px solid #3a2000;border-radius:12px;padding:14px;display:flex;flex-direction:column;gap:10px}" +
            ".media-bar.off{display:none}" +
            ".media-title{font-size:13px;font-weight:700;color:#f0f0f0;overflow:hidden;white-space:nowrap;text-overflow:ellipsis}" +
            ".media-sub{font-size:10px;color:#D4610A;margin-top:2px}" +
            ".btn-row{display:grid;gap:6px}" +
            ".btn-row.cols3{grid-template-columns:1fr 1fr 1fr}" +
            ".btn-row.cols2{grid-template-columns:1fr 1fr}" +
            ".mbtn{border:none;border-radius:8px;padding:14px 0;font-size:18px;cursor:pointer;" +
            "color:#f0f0f0;background:#1e1e1e;font-weight:600}" +
            ".mbtn.accent{background:#D4610A;color:white}" +
            ".mbtn.danger{background:#c0392b;color:white}" +
            ".vol-row{display:flex;align-items:center;gap:8px}" +
            ".vol-row span{font-size:11px;color:#666;min-width:32px;text-align:center}" +
            ".vol-bar{flex:1;height:6px;background:#1e1e1e;border-radius:3px;overflow:hidden}" +
            ".vol-fill{height:100%;background:#D4610A;border-radius:3px;transition:width .3s}";

        var homeBody =
            "<style>" + mediaStyle + "</style>" +
            "<div style=\"padding:12px;display:flex;flex-direction:column;gap:10px\">" +

            // Nav links
            "<a href=\"/lyrics\" style=\"display:flex;align-items:center;gap:12px;background:#1a1a1a;border-radius:10px;padding:14px;text-decoration:none;color:#f0f0f0\">" +
            "<span style=\"font-size:22px\">&#x1F3B5;</span>" +
            "<div><div style=\"font-size:13px;font-weight:700\">Letras Salvas</div>" +
            "<div style=\"font-size:11px;color:#666\">Selecionar e apresentar</div></div></a>" +
            "<a href=\"/bible\" style=\"display:flex;align-items:center;gap:12px;background:#1a1a1a;border-radius:10px;padding:14px;text-decoration:none;color:#f0f0f0\">" +
            "<span style=\"font-size:22px\">&#x1F4D6;</span>" +
            "<div><div style=\"font-size:13px;font-weight:700\">Biblia</div>" +
            "<div style=\"font-size:11px;color:#666\">Navegar e apresentar</div></div></a>" +

            // Audio section
            "<div id=\"audioBar\" class=\"media-bar off\">" +
            "<div><div class=\"media-sub\">&#x266B; AUDIO</div><div id=\"audioTitle\" class=\"media-title\">-</div></div>" +
            "<div id=\"volRow\" class=\"vol-row\"><button class=\"mbtn\" style=\"width:36px;padding:8px 0\" onclick=\"mc('/audio-vol-')\">&#x2212;</button>" +
            "<div class=\"vol-bar\"><div id=\"volFill\" class=\"vol-fill\" style=\"width:100%\"></div></div>" +
            "<button class=\"mbtn\" style=\"width:36px;padding:8px 0\" onclick=\"mc('/audio-vol+')\">+</button>" +
            "<span id=\"volVal\">100%</span></div>" +
            "<div class=\"btn-row cols2\">" +
            "<button class=\"mbtn accent\" onclick=\"mc('/audio-toggle')\">&#x23EF; Play/Pause</button>" +
            "<button class=\"mbtn danger\" onclick=\"mc('/audio-stop')\">&#x23F9; Parar</button>" +
            "</div></div>" +

            // Video section
            "<div id=\"videoBar\" class=\"media-bar off\">" +
            "<div><div class=\"media-sub\">&#x25B6; VIDEO</div><div id=\"videoTitle\" class=\"media-title\">-</div></div>" +
            "<div class=\"btn-row cols2\">" +
            "<button class=\"mbtn accent\" onclick=\"mc('/video-toggle')\">&#x23EF; Play/Pause</button>" +
            "<button class=\"mbtn danger\" onclick=\"mc('/video-stop')\">&#x23F9; Fechar</button>" +
            "</div></div>" +

            // Image section
            "<div id=\"imageBar\" class=\"media-bar off\">" +
            "<div><div class=\"media-sub\">&#x1F5BC; IMAGEM</div><div id=\"imageTitle\" class=\"media-title\">-</div></div>" +
            "</div>" +

            // Slides section
            "<div style=\"background:#1a1a1a;border-radius:10px;padding:14px\">" +
            "<div style=\"font-size:11px;color:#D4610A;margin-bottom:6px\">&#x25CF; SLIDES AO VIVO</div>" +
            "<div id=\"tx\" style=\"font-size:14px;line-height:1.55;white-space:pre-wrap;min-height:50px;color:#ddd\">Carregando...</div></div>" +
            "<div class=\"btn-row cols3\">" +
            "<button class=\"mbtn\" onclick=\"sc('/prev')\">&#x2B05;</button>" +
            "<button id=\"bb\" class=\"mbtn accent\" onclick=\"sc('/blank')\">&#x2B1B; Tela Preta</button>" +
            "<button class=\"mbtn\" onclick=\"sc('/next')\">&#x27A1;</button>" +
            "</div></div>" +

            "<script>" +
            "function sc(p){fetch(p,{method:'POST'}).then(function(){pollSlide()});}" +
            "function mc(p){fetch(p,{method:'POST'}).then(function(){pollMedia()});}" +
            "function show(id,on){document.getElementById(id).className='media-bar'+(on?'':' off');}" +
            "function pollSlide(){fetch('/state').then(function(r){return r.json();}).then(function(s){" +
            "var tx=document.getElementById('tx');" +
            "tx.textContent=s.blank?'TELA PRETA':(s.text||'Sem apresentacao');" +
            "tx.style.color=s.blank?'#D4610A':'#ddd';" +
            "document.getElementById('bb').style.background=s.blank?'#c0392b':'#D4610A';" +
            "}).catch(function(){});}" +
            "function pollMedia(){fetch('/media-state').then(function(r){return r.json();}).then(function(m){" +
            "show('audioBar',m.audioPlaying);" +
            "if(m.audioPlaying){document.getElementById('audioTitle').textContent=m.audioTitle;" +
            "document.getElementById('volFill').style.width=m.audioVolume+'%';" +
            "document.getElementById('volVal').textContent=m.audioVolume+'%';}" +
            "show('videoBar',m.videoPlaying);" +
            "if(m.videoPlaying)document.getElementById('videoTitle').textContent=m.videoTitle;" +
            "show('imageBar',m.imageShowing);" +
            "if(m.imageShowing)document.getElementById('imageTitle').textContent=m.imageTitle;" +
            "}).catch(function(){});}" +
            "function pollAll(){pollSlide();pollMedia();}" +
            "setInterval(pollAll,1200);pollAll();" +
            "</script>";
        return Shell("LyricsPro Remote", homeBody, "/");
    }

    private string PageLyrics()
    {
        var lyrics = AppServices.Database.GetAllLyrics();
        if (!lyrics.Any())
            return Shell("Letras", """<div class="empty">Nenhuma letra salva.</div>""");

        var items = string.Join("", lyrics.Select(l =>
            "<div class=\"item\">" +
            "<span class=\"item-icon\">&#x1F3B5;</span>" +
            "<div class=\"item-text\">" +
            "<div class=\"item-title\">" + Esc(l.Title) + "</div>" +
            "<div class=\"item-sub\">" + Esc(l.Artist) + "</div></div>" +
            "<button class=\"btn-play\" onclick=\"present(" + l.Id + ")\">&#x25B6; Apresentar</button>" +
            "</div>"));

        var js2 = "<script>" +
            "function present(id){" +
            "fetch('/present-lyric?id='+id,{method:'POST'})" +
            ".then(function(){setTimeout(function(){window.location.href='/'},300)});}" +
            "</script>";

        return Shell("Letras Salvas",
            "<div class=\"list\">" + items + "</div>" + js2);
    }

    private string PageBible()
    {
        var books    = AppServices.Database.GetBibleBooks();
        var versions = AppServices.Database.GetDownloadedVersions();
        if (!books.Any())
            return Shell("Biblia", "<div class=\"empty\">Nenhuma Biblia baixada.</div>");

        var opts = string.Join("", versions.Select(v =>
            $"<option value=\"{v}\">{v}</option>"));
        var sel = "<select id=\"ver\" style=\"background:#1e1e1e;color:#D4610A;" +
                  "border:1px solid #333;border-radius:6px;padding:6px 10px;font-size:13px;" +
                  "margin:12px 16px 0\">" + opts + "</select>";

        string BookRows(IEnumerable<BibleBook> list) => string.Join("", list.Select(b =>
            "<div class=\"item\" onclick=\"goBook(" + b.Id + "," + b.ChapterCount + ")\">" +
            "<div class=\"item-text\"><div class=\"item-title\">" + Esc(b.Name) + "</div>" +
            "<div class=\"item-sub\">" + b.ChapterCount + " capitulos</div></div>" +
            "<span style=\"color:#D4610A;font-size:18px\">&#x203A;</span></div>"));

        var js = "<script>" +
            "function goBook(id,max){" +
            "var ver=document.getElementById('ver').value;" +
            "var ch=prompt('Capitulo (1-'+max+'):','1');" +
            "if(!ch)return;" +
            "window.location.href='/bible-chapter?book='+id+'&chapter='+ch+'&version='+ver;" +
            "}" +
            "</script>";

        var body = sel
            + "<div class=\"section\">Antigo Testamento</div>"
            + "<div class=\"list\">" + BookRows(books.Where(b => b.Testament == 1)) + "</div>"
            + "<div class=\"section\">Novo Testamento</div>"
            + "<div class=\"list\">" + BookRows(books.Where(b => b.Testament == 2)) + "</div>"
            + js;

        return Shell("Biblia", body);
    }

    private string PageBibleChapter(int bookId, int chapter)
    {
        var versions = AppServices.Database.GetDownloadedVersions();
        var version  = versions.FirstOrDefault() ?? "ACF";
        var books    = AppServices.Database.GetBibleBooks();
        var book     = books.FirstOrDefault(b => b.Id == bookId);
        var verses   = AppServices.Database.GetVerses(bookId, chapter, version);
        if (book is null || !verses.Any())
            return Shell("Biblia", "<div class=\"empty\">Capitulo nao encontrado.</div>");

        var rows = string.Join("", verses.Select(v =>
            "<div class=\"item\">" +
            "<div class=\"item-text\"><div class=\"item-title\">" +
            "<b style=\"color:#D4610A\">" + v.Verse + "</b>&nbsp;" + Esc(v.Text) +
            "</div></div>" +
            "<button class=\"btn-play\" onclick=\"present()\">&#x25B6;</button></div>"));

        var js = "<script>" +
            "var BOOK=" + bookId + ",CH=" + chapter + ",VER='" + version + "';" +
            "function present(){" +
            "fetch('/present-bible?book='+BOOK+'&chapter='+CH+'&version='+VER,{method:'POST'})" +
            ".then(function(){setTimeout(function(){window.location.href='/'},300)});}" +
            "</script>";

        var btn = "<div style=\"padding:12px 16px 4px\">" +
            "<button onclick=\"present()\" style=\"background:#D4610A;border:none;color:white;" +
            "border-radius:8px;padding:10px 20px;font-size:13px;font-weight:700;cursor:pointer;width:100%\">" +
            "&#x25B6; Apresentar Capitulo Inteiro</button></div>";

        return Shell(book.Name + " " + chapter,
            btn + "<div class=\"list\">" + rows + "</div>" + js,
            "/bible");
    }

    // ── Media state ───────────────────────────────────────────────

    private static object GetMediaState()
    {
        var ms = AppServices.MediaState;
        return new
        {
            audioPlaying = ms.AudioIsPlaying,
            audioTitle   = ms.AudioTitle,
            audioVolume  = (int)(ms.AudioVolume * 100),
            videoPlaying = ms.VideoIsPlaying,
            videoTitle   = ms.VideoTitle,
            imageShowing = ms.ImageIsShowing,
            imageTitle   = ms.ImageTitle,
            hasAnything  = ms.HasAnything
        };
    }

    // ── Helpers ───────────────────────────────────────────────────

    private object GetState()
    {
        if (_vm is null) return new { active = false };
        var s = _vm.CurrentSlide;
        return new { active=_vm.IsActive, blank=_vm.IsBlank, progress=_vm.ProgressText,
                     fontSize=_vm.FontSize, title=s?.Title??"", subtitle=s?.Subtitle??"",
                     text=s?.Text??"", index=s?.Index??0, total=s?.Total??0 };
    }

    private static string Param(string qs, string key)
    {
        foreach (var part in qs.Split('&'))
        {
            var kv = part.Split('=');
            if (kv.Length == 2 && kv[0] == key)
                return HttpUtility.UrlDecode(kv[1]);
        }
        return "";
    }

    private static string Esc(string s) =>
        s.Replace("&","&amp;").Replace("<","&lt;").Replace(">","&gt;").Replace("\"","&quot;");

    private static async Task WriteBody(NetworkStream s, string body, string ct)
    {
        var bytes  = Encoding.UTF8.GetBytes(body);
        var header = $"HTTP/1.1 200 OK\r\nContent-Type:{ct}\r\nContent-Length:{bytes.Length}\r\n" +
                     "Access-Control-Allow-Origin:*\r\nConnection:close\r\n\r\n";
        await s.WriteAsync(Encoding.UTF8.GetBytes(header));
        await s.WriteAsync(bytes);
    }

    private static async Task WriteRaw(NetworkStream s, string raw) =>
        await s.WriteAsync(Encoding.UTF8.GetBytes(raw));

    public static string GetLocalIp()
    {
        foreach (var iface in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (iface.OperationalStatus != OperationalStatus.Up) continue;
            if (iface.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
            foreach (var addr in iface.GetIPProperties().UnicastAddresses)
                if (addr.Address.AddressFamily == AddressFamily.InterNetwork)
                    return addr.Address.ToString();
        }
        return "localhost";
    }
}
