using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO.Compression;
using System.Net;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using Capture.Core;
using Microsoft.Web.WebView2.Core;
namespace Capture.App;
public partial class MainWindow : Window
{
    private readonly ProjectStore store = new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Capture","Projects"));
    private readonly Session session = new();
    private readonly System.Windows.Forms.NotifyIcon tray = new();
    private Recorder? recorder;
    private Guide? current;
    private Window? toolbar;
    private TextBlock? recordingText;
    private bool exiting, initialized, busy, hotkeyRegistered, recordingBlocked, exporting;
    private TaskCompletionSource? flushCompletion;
    private uint hotkey = 0x52;
    private string[] exclusions = ["1password", "keepass", "bitwarden", "lastpass", "credential", "lockapp", "logonui"];
    private readonly string settingsFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Capture","settings.json");
    public MainWindow()
    {
        InitializeComponent();
        Loaded += async (_,_) => await InitializeAsync();
        Closing += (_,e) => { if (!exiting) { e.Cancel = true; Hide(); tray.ShowBalloonTip(2000,"CAPTURE","Capture is still available in the system tray. Use Exit to close it.",System.Windows.Forms.ToolTipIcon.Info); } };
        SourceInitialized += (_,_) => HwndSource.FromHwnd(new WindowInteropHelper(this).Handle)?.AddHook(WindowMessage);
    }
    private async Task InitializeAsync()
    {
        try
        {
            Directory.CreateDirectory(store.Root);
            if (File.Exists(settingsFile))
            {
                try { var settings = JsonDocument.Parse(await File.ReadAllTextAsync(settingsFile)); hotkey = settings.RootElement.GetProperty("hotkey").GetUInt32(); exclusions = settings.RootElement.GetProperty("exclusions").Deserialize<string[]>() ?? exclusions; }
                catch { MessageBox.Show("Preferences could not be read. Default exclusions and hotkey are in use.","CAPTURE"); }
            }
            tray.Icon = System.Drawing.SystemIcons.Application; tray.Text = "CAPTURE — not recording";
            var menu = new System.Windows.Forms.ContextMenuStrip();
            menu.Items.Add("Open Capture", null, (_,_) => { Show(); Activate(); });
            menu.Items.Add("Start / finish recording", null, async (_,_) => await ToggleAsync());
            menu.Items.Add("Exit", null, async (_,_) => await ExitAsync());
            tray.ContextMenuStrip = menu; tray.DoubleClick += (_,_) => { Show(); Activate(); }; tray.Visible = true;
            hotkeyRegistered = Native.RegisterHotKey(new WindowInteropHelper(this).Handle,1,0x4006,hotkey);
            if (!hotkeyRegistered) MessageBox.Show("The capture shortcut is already in use. Start recording from Projects, or select another shortcut in Settings.","Shortcut unavailable");
            var environment = await CoreWebView2Environment.CreateAsync(null,Path.Combine(Path.GetDirectoryName(settingsFile)!,"WebView2"));
            await Editor.EnsureCoreWebView2Async(environment);
            Editor.CoreWebView2.Settings.AreDevToolsEnabled = false;
            Editor.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            Editor.CoreWebView2.Settings.IsStatusBarEnabled = false;
            Editor.CoreWebView2.SetVirtualHostNameToFolderMapping("capture.local",Path.Combine(AppContext.BaseDirectory,"editor"),CoreWebView2HostResourceAccessKind.DenyCors);
            Editor.CoreWebView2.NavigationStarting += (_,e) => { if (e.Uri != "https://capture.local/index.html") e.Cancel = true; };
            Editor.CoreWebView2.NewWindowRequested += (_,e) => e.Handled = true;
            Editor.CoreWebView2.PermissionRequested += (_,e) => e.State = CoreWebView2PermissionState.Deny;
            Editor.CoreWebView2.DownloadStarting += (_,e) => e.Cancel = true;
            Editor.CoreWebView2.WebMessageReceived += HandleMessage;
            Editor.Source = new Uri("https://capture.local/index.html"); initialized = true;
        }
        catch (Exception ex) { MessageBox.Show("Capture could not start. Install Microsoft Edge WebView2 Runtime if it is missing.\n\n"+ex.Message,"CAPTURE startup"); exiting=true; tray.Dispose(); System.Windows.Application.Current.Shutdown(1); }
    }
    private IntPtr WindowMessage(IntPtr hwnd,int msg,IntPtr w,IntPtr l,ref bool handled)
    {
        if(msg == 0x312 && w == (IntPtr)1) { handled = true; _ = ToggleAsync(); }
        return IntPtr.Zero;
    }
    private async void HandleMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        if (e.Source != "https://capture.local/index.html") return;
        string? requestId = null;
        try
        {
            if (e.WebMessageAsJson.Length > 16*1024*1024) throw new InvalidDataException("Message too large.");
            using var document = JsonDocument.Parse(e.WebMessageAsJson); var r = document.RootElement;
            requestId = r.GetProperty("requestId").GetString();
            if (requestId is null || requestId.Length > 100 || r.GetProperty("protocolVersion").GetInt32() != 1) throw new InvalidDataException("Unsupported bridge request.");
            var p = r.GetProperty("payload");
            object? result = r.GetProperty("type").GetString() switch
            {
                "app.flushComplete" => CompleteFlush(p),
                "editor.ready" => new { version = "0.2.0-preview.1", guide = current, recording = session.State.ToString() },
                "settings.get" => new { hotkey = ((char)hotkey).ToString(), exclusions },
                "settings.update" => await UpdateSettingsAsync(p),
                "project.list" => await store.ListAsync(),
                "project.open" => await OpenAsync(p.GetProperty("id").GetGuid()),
                "project.save" => await SaveAsync(p),
                "project.new" => await NewAsync(),
                "project.import" => await ImportAsync(),
                "recording.toggle" => await ToggleFromBridgeAsync(),
                "export.request" => await ExportAsync(p.GetProperty("format").GetString()!),
                _ => throw new InvalidDataException("Unknown command.")
            };
            Editor.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new { requestId, ok = true, result },GuideJson.Options));
        }
        catch (Exception ex)
        {
            var message = ex is InvalidDataException or InvalidOperationException or TimeoutException ? ex.Message : "The operation failed. Your saved project is retained. Check available disk space and file permissions.";
            Editor.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new { requestId, ok = false, error = message },GuideJson.Options));
        }
    }
    private async Task<object> UpdateSettingsAsync(JsonElement p)
    {
        if (session.State != SessionState.Idle) throw new InvalidOperationException("Finish recording before changing settings.");
        var key = p.GetProperty("hotkey").GetString();
        if (key is null || key.Length != 1 || key[0] < 'A' || key[0] > 'Z') throw new InvalidDataException("Choose a letter A–Z.");
        var list = p.GetProperty("exclusions").Deserialize<string[]>() ?? [];
        if (list.Length > 100 || list.Any(s=>string.IsNullOrWhiteSpace(s)||s.Length>100)) throw new InvalidDataException("Invalid exclusion list.");
        var hwnd = new WindowInteropHelper(this).Handle;
        // Register the replacement first so a failed change preserves the old shortcut.
        if ((key[0] != hotkey || !hotkeyRegistered) && !Native.RegisterHotKey(hwnd,2,0x4006,key[0])) throw new InvalidOperationException("That shortcut is in use. Choose another letter.");
        if (key[0] != hotkey || !hotkeyRegistered) {
            Native.UnregisterHotKey(hwnd,2); Native.UnregisterHotKey(hwnd,1);
            if (!Native.RegisterHotKey(hwnd,1,0x4006,key[0])) { Native.RegisterHotKey(hwnd,1,0x4006,hotkey); throw new InvalidOperationException("Shortcut could not be changed."); }
        }
        hotkeyRegistered = true; hotkey = key[0]; exclusions = list;
        await File.WriteAllTextAsync(settingsFile,JsonSerializer.Serialize(new { hotkey, exclusions })); return new { saved = true };
    }
    private void RequireIdle() { if (exporting) throw new InvalidOperationException("Wait for the current export to finish."); if (session.State != SessionState.Idle) throw new InvalidOperationException("Finish the recording first."); }
    private Guide Select(Guide g)
    {
        current = g;
        Directory.CreateDirectory(store.DirectoryFor(g.Id));
        Editor.CoreWebView2.SetVirtualHostNameToFolderMapping("project.capture.local",store.DirectoryFor(g.Id),CoreWebView2HostResourceAccessKind.Allow);
        return g;
    }
    private async Task<Guide> OpenAsync(Guid id) { RequireIdle(); return Select(await store.LoadAsync(id)); }
    private async Task<Guide> NewAsync() { RequireIdle(); var g = new Guide(); await store.SaveAsync(g); return Select(g); }
    private async Task<Guide?> ImportAsync()
    {
        RequireIdle(); var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "Capture package|*.capture;*.zip" };
        return dialog.ShowDialog(this) == true ? Select(await store.ImportPortableAsync(dialog.FileName)) : null;
    }
    private async Task<object> SaveAsync(JsonElement payload)
    {
        RequireIdle(); if (current is null) throw new InvalidOperationException("Open a project first.");
        var g = GuideJson.Parse(payload.GetRawText());
        if (g.Id != current.Id || JsonSerializer.Serialize(g.Assets,GuideJson.Options) != JsonSerializer.Serialize(current.Assets,GuideJson.Options)) throw new InvalidDataException("Project or asset identity changed.");
        g.UpdatedAt = DateTimeOffset.UtcNow; await store.SaveAsync(g); current = g; return new { saved = true };
    }
    private async Task<object> ToggleFromBridgeAsync() { await ToggleAsync(); return new { recording = session.State.ToString() }; }
    private async Task ToggleAsync()
    {
        if (!initialized || busy || exporting) return;
        if(recordingBlocked) { MessageBox.Show("Restart Capture before recording again. Saved drafts are available in Projects."); return; }
        busy = true;
        try
        {
            if (session.State == SessionState.Idle)
            {
                if (MessageBox.Show("Capture will take screenshots of the active monitor and observe mouse clicks until you finish. Screenshots may include sensitive information. Password detection is best effort; pause before opening sensitive content. No keyboard text is collected and nothing is uploaded.\n\nUse synthetic data for this preview. Start recording?","Start recording",MessageBoxButton.OKCancel,MessageBoxImage.Warning) != MessageBoxResult.OK) return;
                await FlushEditorAsync();
                session.Start(); recorder = new Recorder(store,exclusions);
                recorder.Status += text => { if(!Dispatcher.HasShutdownStarted) Dispatcher.BeginInvoke(() => { if (recordingText is not null) recordingText.Text = text; }); };
                recorder.Start(); Hide(); CreateToolbar(); tray.Text = "CAPTURE — RECORDING";
            }
            else if (session.State is SessionState.Recording or SessionState.Paused)
            {
                session.Finish(); recordingText!.Text = "Finishing and saving…";
                await recorder!.FinishAsync(); var g = recorder.Guide; recorder.Dispose(); recorder = null;
                session.Reset(); toolbar?.Close(); toolbar = null; tray.Text = "CAPTURE — not recording";
                Select(g); Show(); Activate();
                Editor.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new { type = "project.changed", guide = g },GuideJson.Options));
            }
        }
        catch (Exception ex)
        {
            recorder?.Dispose(); session.Reset(); toolbar?.Close(); toolbar = null; Show();
            if (ex is TimeoutException) { recordingBlocked = true; MessageBox.Show(ex.Message,"Recording stopped — restart Capture"); }
            else { recorder = null; MessageBox.Show("Recording stopped. Saved drafts can be reopened from Projects.\n"+ex.Message,"CAPTURE"); }
        }
        finally { busy = false; }
    }
    private void CreateToolbar()
    {
        toolbar = new Window { Title = "CAPTURE recording", Width = 560, Height = 125, Topmost = true, ResizeMode = ResizeMode.NoResize, ShowActivated = false, WindowStyle = WindowStyle.ToolWindow };
        var panel = new StackPanel { Margin = new Thickness(12) };
        recordingText = new TextBlock { Text = "● Recording · 0 steps", TextWrapping = TextWrapping.Wrap };
        panel.Children.Add(recordingText); var actions = new StackPanel { Orientation = Orientation.Horizontal };
        var pause = new Button { Content = "Pause", Margin = new Thickness(4), Padding = new Thickness(12,4,12,4) };
        pause.Click += (_,_) => { if (busy) return; if (session.State == SessionState.Recording) { session.Pause(); recorder!.Pause(true); pause.Content="Resume"; recordingText.Text="Paused — screenshots are off"; } else if(session.State == SessionState.Paused) { session.Resume(); recorder!.Pause(false); pause.Content="Pause"; recordingText.Text="● Recording"; } };
        var finish = new Button { Content = "Finish", Margin = new Thickness(4), Padding = new Thickness(12,4,12,4) }; finish.Click += async (_,_) => await ToggleAsync();
        var cancel = new Button { Content = "Cancel", Margin = new Thickness(4), Padding = new Thickness(12,4,12,4) };
        cancel.Click += async (_,_) =>
        {
            if(busy || MessageBox.Show("Discard this recording and its saved screenshots?","Cancel recording",MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
            busy=true;
            try { await recorder!.FinishAsync(); var dir=store.DirectoryFor(recorder.Guide.Id); recorder.Dispose(); recorder=null; if(Directory.Exists(dir)) Directory.Delete(dir,true); session.Reset(); toolbar.Close(); toolbar=null; Show(); tray.Text="CAPTURE — not recording"; }
            catch(Exception ex) { recordingBlocked=true; session.Reset(); toolbar?.Close(); toolbar=null; Show(); tray.Text="CAPTURE — restart required"; MessageBox.Show("Could not finish cancellation. Use Exit in the tray, then restart Capture. A saved draft may remain.\n"+ex.Message); }
            finally { busy=false; }
        };
        actions.Children.Add(pause); actions.Children.Add(finish); actions.Children.Add(cancel); panel.Children.Add(actions); toolbar.Content=panel;
        toolbar.SourceInitialized += (_,_) => { recorder!.MaskWindow = new WindowInteropHelper(toolbar).Handle; if(!Native.SetWindowDisplayAffinity(new WindowInteropHelper(toolbar).Handle,0x11)) recordingText.Text="Toolbar exclusion unavailable; its rectangle will be masked in screenshots."; };
        toolbar.Closing += (_,e) => { if(session.State is SessionState.Recording or SessionState.Paused) e.Cancel=true; };
        toolbar.Show();
    }
    private async Task<object> ExportAsync(string format)
    {
        RequireIdle(); if(current is null) throw new InvalidOperationException("Open a guide first.");
        if (MessageBox.Show("Review all screenshots for sensitive information before exporting. This preview has no redaction tool. Export only material you are permitted to share.","Review before export",MessageBoxButton.OKCancel,MessageBoxImage.Warning) != MessageBoxResult.OK) return new { cancelled = true };
        var extension = format switch { "portable" => ".capture", "markdown" => ".zip", "docx" => ".docx", "html" => ".html", _ => throw new InvalidDataException("Unsupported export format.") };
        var label = format switch { "docx" => "Word document", "html" => "HTML guide", "markdown" => "Markdown ZIP", _ => "Capture package" };
        var dialog = new Microsoft.Win32.SaveFileDialog { FileName="guide"+extension, Filter=$"{label}|*{extension}", DefaultExt=extension, InitialDirectory=Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), OverwritePrompt=true };
        if(dialog.ShowDialog(this)!=true) return new { cancelled=true };
        // Write the complete archive next to the destination before replacing any existing export.
        var temporary = dialog.FileName + "." + Guid.NewGuid().ToString("N") + ".tmp";
        var snapshot = GuideJson.Parse(GuideJson.Serialize(current));
        exporting = true;
        try
        {
            await Task.Run(async () =>
            {
                if (format == "portable") await store.ExportPortableAsync(snapshot, temporary);
                else
                {
                    Task<ExportImage> Render(Step step) => ExportImageRenderer.RenderAsync(snapshot, step, store.DirectoryFor(snapshot.Id));
                    if (format == "docx") await DocumentExports.DocxAsync(snapshot, temporary, Render);
                    else if (format == "html") await DocumentExports.HtmlAsync(snapshot, temporary, Render);
                    else await DocumentExports.MarkdownAsync(snapshot, temporary, Render);
                }
            });
            File.Move(temporary, dialog.FileName, true);
            return new { exported = true };
        }
        finally
        {
            exporting = false;
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private object CompleteFlush(JsonElement payload)
    {
        if(payload.GetProperty("saved").GetBoolean()) flushCompletion?.TrySetResult();
        else flushCompletion?.TrySetException(new IOException("Editor changes could not be saved."));
        return new { received = true };
    }
    private async Task FlushEditorAsync()
    {
        if (!initialized) return;
        flushCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Editor.CoreWebView2.PostWebMessageAsJson("{\"type\":\"editor.flush\"}");
        await flushCompletion.Task.WaitAsync(TimeSpan.FromSeconds(15));
        flushCompletion = null;
    }
    private async Task ExitAsync()
    {
        if(busy || exporting) return;
        if(session.State is SessionState.Recording or SessionState.Paused) { await ToggleAsync(); if(session.State != SessionState.Idle) return; }
        if (MessageBox.Show("Exit Capture? Any editor changes still being saved should finish before you exit.","Exit",MessageBoxButton.OKCancel)!=MessageBoxResult.OK) return;
        try { await FlushEditorAsync(); } catch { MessageBox.Show("Editor changes could not be saved. Capture will remain open. Retry Save before exiting.","Unsaved changes"); return; }
        exiting=true; recorder?.Dispose(); Native.UnregisterHotKey(new WindowInteropHelper(this).Handle,1); tray.Dispose(); Editor.Dispose(); System.Windows.Application.Current.Shutdown();
    }
}
