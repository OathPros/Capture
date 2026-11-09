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
    private readonly ProjectStore store = new(ScreenshotSmoke.Enabled ? Path.Combine(ScreenshotSmoke.Root,"Projects") : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Capture","Projects"));
    private readonly Session session = new();
    private readonly System.Windows.Forms.NotifyIcon tray = new();
    private Recorder? recorder;
    private Guide? current;
    private Window? toolbar;
    private TextBlock? recordingText;
    private bool exiting, initialized, busy, hotkeyRegistered, screenshotHotkeyRegistered, recordingBlocked, exporting, capturing, startupFailed;
    private TaskCompletionSource? flushCompletion;
    private uint hotkey = 0x52, screenshotHotkey = 0x53;
    private Guid? editorStepId, editorProjectId;
    private readonly List<string> startupIssues = [];
    private bool darkTheme = true;
    private string[] exclusions = ["1password", "keepass", "bitwarden", "lastpass", "credential", "lockapp", "logonui"];
    private readonly string settingsFile = ScreenshotSmoke.Enabled ? Path.Combine(ScreenshotSmoke.Root,"settings.json") : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Capture","settings.json");
    public MainWindow()
    {
        InitializeComponent();
        Loaded += async (_,_) => await InitializeAsync();
        Closing += (_,e) => { if (startupFailed) { exiting = true; System.Windows.Application.Current.Shutdown(1); return; } if (!exiting) { e.Cancel = true; Hide(); } };
        SourceInitialized += (_,_) => HwndSource.FromHwnd(new WindowInteropHelper(this).Handle)?.AddHook(WindowMessage);
    }
    private async Task InitializeAsync()
    {
        try
        {
            Directory.CreateDirectory(store.Root);
            if(ScreenshotSmoke.Enabled) current=await ScreenshotSmoke.CreateAsync(store);
            if (File.Exists(settingsFile))
            {
                try { using var settings = JsonDocument.Parse(await File.ReadAllTextAsync(settingsFile)); hotkey = settings.RootElement.GetProperty("hotkey").GetUInt32(); exclusions = settings.RootElement.GetProperty("exclusions").Deserialize<string[]>() ?? exclusions;
                    if (settings.RootElement.TryGetProperty("screenshotHotkey", out var screenshotKey)) screenshotHotkey = screenshotKey.GetUInt32();
                    else screenshotHotkey = hotkey == 0x53 ? 0x50u : 0x53u;
                    if (hotkey < 'A' || hotkey > 'Z' || screenshotHotkey < 'A' || screenshotHotkey > 'Z' || hotkey == screenshotHotkey) { hotkey = 0x52; screenshotHotkey = 0x53; }
                    if (exclusions.Length > 100 || exclusions.Any(x => string.IsNullOrWhiteSpace(x) || x.Length > 100)) throw new InvalidDataException(); }
                catch { hotkey = 0x52; screenshotHotkey = 0x53; exclusions = ["1password", "keepass", "bitwarden", "lastpass", "credential", "lockapp", "logonui"]; startupIssues.Add("Preferences could not be read. Open Settings to choose your shortcuts."); }
            }
            using (var iconStream = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/Assets/CAPTURE.ico")).Stream)
            using (var icon = new System.Drawing.Icon(iconStream, System.Windows.Forms.SystemInformation.SmallIconSize))
                tray.Icon = (System.Drawing.Icon)icon.Clone(); tray.Text = "CAPTURE — not recording";
            var menu = new System.Windows.Forms.ContextMenuStrip();
            menu.Items.Add("Open Capture", null, (_,_) => { Show(); Activate(); });
            menu.Items.Add("Start / finish recording", null, async (_,_) => await ToggleAsync());
            menu.Items.Add("Capture screenshot", null, async (_,_) => await ScreenshotFromHotkeyAsync());
            menu.Items.Add("Exit", null, async (_,_) => await ExitAsync());
            tray.ContextMenuStrip = menu; tray.DoubleClick += (_,_) => { Show(); Activate(); }; tray.Visible = !ScreenshotSmoke.Enabled;
            hotkeyRegistered = ScreenshotSmoke.Enabled || Native.RegisterHotKey(new WindowInteropHelper(this).Handle,1,0x4006,hotkey);
            screenshotHotkeyRegistered = ScreenshotSmoke.Enabled || Native.RegisterHotKey(new WindowInteropHelper(this).Handle, 3, 0x4006, screenshotHotkey);
            if (!hotkeyRegistered) startupIssues.Add("Recording shortcut is in use. Choose another in Settings.");
            if (!screenshotHotkeyRegistered) startupIssues.Add("Screenshot shortcut is in use. Choose another in Settings.");
            var environment = await CoreWebView2Environment.CreateAsync(null,Path.Combine(Path.GetDirectoryName(settingsFile)!,"WebView2"),ScreenshotSmoke.Enabled ? new CoreWebView2EnvironmentOptions("--disable-gpu") : null);
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
        catch (Exception ex) { if(ScreenshotSmoke.Enabled) { ScreenshotSmoke.Report("FAIL: "+ex.Message);exiting=true;DisposeTray();System.Windows.Application.Current.Shutdown(1);return; } startupFailed = true; DisposeTray(); Background = System.Windows.Media.Brushes.White; Foreground = System.Windows.Media.Brushes.Black;
            var panel = new StackPanel { Margin = new Thickness(40) };
            panel.Children.Add(new TextBlock { Text = "Capture could not start", FontSize = 24 });
            panel.Children.Add(new TextBlock { Text = "Check Microsoft Edge WebView2 Runtime and try again.\n\n" + ex.Message, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,20,0,20) });
            var close = new Button { Content = "Close Capture", Padding = new Thickness(16,8,16,8), HorizontalAlignment = System.Windows.HorizontalAlignment.Left };
            close.Click += (_,_) => { exiting = true; System.Windows.Application.Current.Shutdown(1); }; panel.Children.Add(close); Content = panel; }
    }
    private IntPtr WindowMessage(IntPtr hwnd,int msg,IntPtr w,IntPtr l,ref bool handled)
    {
        if(msg == 0x312 && w == (IntPtr)3) { handled = true; _ = ScreenshotFromHotkeyAsync(); }
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
                "editor.ready" => new { version = "0.5.0-preview.2", guide = current, recording = session.State.ToString(), issues = startupIssues },
                "settings.get" => new { hotkey = ((char)hotkey).ToString(), screenshotHotkey = ((char)screenshotHotkey).ToString(), hotkeyRegistered, screenshotHotkeyRegistered, exclusions },
                "settings.update" => await UpdateSettingsAsync(p),
                "app.theme" => SetTheme(p.GetProperty("theme").GetString()),
                "project.list" => await store.ListAsync(),
                "project.open" => await OpenAsync(p.GetProperty("id").GetGuid()),
                "project.save" => await SaveAsync(p),
                "asset.read" => await ReadAssetAsync(p),
                "asset.import" => await ImportImageAsync(p),
                "asset.previewReady" => PreviewReady(p),
                "asset.applyPixelEdits" => await ApplyPixelEditsAsync(p),
                "asset.applyAllPixelEdits" => await ApplyPixelEditsAsync(p, true),
                "screenshot.capture" => await CaptureScreenshotAsync(p.TryGetProperty("stepId", out var stepId) ? stepId.GetGuid() : null),
                "asset.finishCleanup" => await FinishCleanupAsync(),
                "project.new" => await NewAsync(),
                "project.import" => await ImportAsync(),
                "recording.toggle" => await ToggleFromBridgeAsync(),
                "export.request" => await ExportAsync(p.GetProperty("format").GetString()!),
                "clipboard.copy" => await CopyAsync(p.GetProperty("format").GetString()!),
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
        RequireIdle();
        var key = p.GetProperty("hotkey").GetString();
        var screenshotKey = p.GetProperty("screenshotHotkey").GetString();
        if (key is null || key.Length != 1 || key[0] < 'A' || key[0] > 'Z' || screenshotKey is null || screenshotKey.Length != 1 || screenshotKey[0] < 'A' || screenshotKey[0] > 'Z')
            throw new InvalidDataException("Choose a letter A–Z for each shortcut.");
        if (key == screenshotKey) throw new InvalidDataException("Recording and screenshot shortcuts must be different.");
        var list = p.GetProperty("exclusions").Deserialize<string[]>() ?? [];
        if (list.Length > 100 || list.Any(x => string.IsNullOrWhiteSpace(x) || x.Length > 100)) throw new InvalidDataException("Invalid exclusion list.");
        var hwnd = new WindowInteropHelper(this).Handle;
        Native.UnregisterHotKey(hwnd, 1); Native.UnregisterHotKey(hwnd, 3);
        var temporary = settingsFile + ".tmp";
        try
        {
            if (!Native.RegisterHotKey(hwnd, 1, 0x4006, key[0])) throw new InvalidOperationException("Recording shortcut is in use. Choose another letter.");
            if (!Native.RegisterHotKey(hwnd, 3, 0x4006, screenshotKey[0])) throw new InvalidOperationException("Screenshot shortcut is in use. Choose another letter.");
            await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(new { hotkey = (uint)key[0], screenshotHotkey = (uint)screenshotKey[0], exclusions = list }));
            File.Move(temporary, settingsFile, true);
            hotkey = key[0]; screenshotHotkey = screenshotKey[0]; exclusions = list;
            hotkeyRegistered = screenshotHotkeyRegistered = true;
        }
        catch
        {
            Native.UnregisterHotKey(hwnd, 1); Native.UnregisterHotKey(hwnd, 3);
            hotkeyRegistered = Native.RegisterHotKey(hwnd, 1, 0x4006, hotkey);
            screenshotHotkeyRegistered = Native.RegisterHotKey(hwnd, 3, 0x4006, screenshotHotkey);
            throw;
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
        return new { saved = true };
    }

    private void RequireIdle(bool captureSave = false) { if (capturing && !captureSave) throw new InvalidOperationException("Wait for the screenshot to finish."); if (exporting) throw new InvalidOperationException("Wait for the current export to finish."); if (session.State != SessionState.Idle) throw new InvalidOperationException("Finish the recording first."); }
    private Guide Select(Guide g)
    {
        current = g;
        Directory.CreateDirectory(store.DirectoryFor(g.Id));
        // Screenshots are served through the validated asset.read bridge, not another virtual origin.
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
        RequireIdle(true); if (current is null) throw new InvalidOperationException("Open a project first.");
        var g = GuideJson.Parse(payload.GetRawText());
        if (g.Id != current.Id || JsonSerializer.Serialize(g.Assets,GuideJson.Options) != JsonSerializer.Serialize(current.Assets,GuideJson.Options) || !g.PendingAssetDeletes.SequenceEqual(current.PendingAssetDeletes)) throw new InvalidDataException("Project or asset identity changed.");
        g.UpdatedAt = DateTimeOffset.UtcNow; await store.SaveAsync(g); current = g; return new { saved = true };
    }
    private object PreviewReady(JsonElement payload)
    {
        if(current is null || payload.GetProperty("projectId").GetGuid()!=current.Id || !current.Assets.Any(a=>a.Id==payload.GetProperty("assetId").GetString() && a.Width==payload.GetProperty("width").GetInt32() && a.Height==payload.GetProperty("height").GetInt32())) throw new InvalidDataException("Invalid screenshot preview acknowledgement.");
        if(ScreenshotSmoke.Enabled) Dispatcher.BeginInvoke(()=>
        {
            ScreenshotSmoke.Report("PASS: native asset.read, checksum validation, WebView2 bridge, CSP and SVG PNG preview decoded 640x360.");
            exiting=true;DisposeTray();Editor.Dispose();System.Windows.Application.Current.Shutdown(0);
        });
        return new { received=true };
    }
    private async Task<object> ReadAssetAsync(JsonElement payload)
    {
        if(current is null || payload.GetProperty("projectId").GetGuid()!=current.Id) throw new InvalidDataException("Open this screenshot's project first.");
        var assetId=payload.GetProperty("assetId").GetString();
        if(assetId is null || !current.Assets.Any(a=>a.Id==assetId)) throw new InvalidDataException("Screenshot not found in the current project.");
        var snapshot=current;
        var image=await ExportImageRenderer.ReadSourceAsync(snapshot,assetId,store.DirectoryFor(snapshot.Id));
        return new { url="data:image/png;base64,"+Convert.ToBase64String(image.Png), width=image.Width,height=image.Height };
    }
    private async Task<object?> ImportImageAsync(JsonElement payload)
    {
        RequireIdle();
        if (current is null || payload.GetProperty("projectId").GetGuid() != current.Id)
            throw new InvalidDataException("Open this image's project first.");
        var dialog = new Microsoft.Win32.OpenFileDialog { Title = "Choose a step image", Filter = "Images (PNG, JPEG, BMP)|*.png;*.jpg;*.jpeg;*.bmp", CheckFileExists = true, Multiselect = false };
        exporting = true;
        try
        {
            if (dialog.ShowDialog(this) != true) return null;
            var result = await Task.Run(() => ImageImport.ImportAsync(current, dialog.FileName, store));
            current = result.Guide;
            return result.Asset;
        }
        finally { exporting = false; }
    }
    private async Task<object?> ApplyPixelEditsAsync(JsonElement payload, bool all = false)
    {
        RequireIdle();if(current is null) throw new InvalidOperationException("Open a project first.");
        var assetId=all ? null : payload.GetProperty("assetId").GetString();if(!all && (assetId is null || !current.Assets.Any(a=>a.Id==assetId)))throw new InvalidDataException("Unknown screenshot.");
        exporting=true;
        try
        {
            current=await Task.Run(()=>all ? PixelEditStorage.ApplyAllAsync(current,store) : PixelEditStorage.ApplyAsync(current,assetId!,store));
            try { current=await PixelEditStorage.CleanupAsync(current,store); return new { guide=current,warning=(string?)null }; }
            catch(Exception ex) when(ex is IOException or UnauthorizedAccessException) { return new { guide=current,warning="The sanitized image is saved, but the original image could not be removed. Finish privacy cleanup before exporting." }; }
        }
        finally { exporting=false; }
    }
    private async Task<object> FinishCleanupAsync()
    {
        RequireIdle();if(current is null)throw new InvalidOperationException("Open a project first.");
        current=await PixelEditStorage.CleanupAsync(current,store);return current;
    }
    private async Task<object> ToggleFromBridgeAsync() { await ToggleAsync(); return new { recording = session.State.ToString() }; }
    private async Task ToggleAsync()
    {
        if (!initialized || busy || exporting || capturing) return;
        if(recordingBlocked) { PostError("Restart Capture before recording again. Saved drafts are available in Projects."); return; }
        busy = true;
        try
        {
            if (session.State == SessionState.Idle)
            {
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
            if (ex is TimeoutException) { recordingBlocked = true; PostError(ex.Message); }
            else { recorder = null; PostError("Recording stopped. Saved drafts can be reopened from Projects. "+ex.Message); }
        }
        finally { busy = false; }
    }
    private void CreateToolbar()
    {
        toolbar = new Window { Title = "CAPTURE recording", Icon = Icon, Width = 610, Height = 125, Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(24,27,33)), Foreground = System.Windows.Media.Brushes.White, Topmost = true, ResizeMode = ResizeMode.NoResize, ShowActivated = false, WindowStyle = WindowStyle.ToolWindow };
        var panel = new StackPanel { Margin = new Thickness(12) };
        recordingText = new TextBlock { Text = "● Recording · 0 steps", TextWrapping = TextWrapping.Wrap };
        panel.Children.Add(recordingText); var actions = new StackPanel { Orientation = Orientation.Horizontal };
        var pause = new Button { Content = "Pause", Margin = new Thickness(4), Padding = new Thickness(12,4,12,4) };
        pause.Click += (_,_) => { if (busy || exporting) return; if (session.State == SessionState.Recording) { session.Pause(); recorder!.Pause(true); pause.Content="Resume"; recordingText.Text="Paused · Manual screenshots available"; } else if(session.State == SessionState.Paused) { session.Resume(); recorder!.Pause(false); pause.Content="Pause"; recordingText.Text="● Recording"; } };
        var finish = new Button { Content = "Finish", Margin = new Thickness(4), Padding = new Thickness(12,4,12,4) }; finish.Click += async (_,_) => await ToggleAsync();
        var cancel = new Button { Content = "Discard", Margin = new Thickness(4), Padding = new Thickness(12,4,12,4) };
        cancel.Click += async (_,_) =>
        {
            if(busy || exporting || capturing) return;
            busy=true;
            try { await recorder!.FinishAsync(); var dir=store.DirectoryFor(recorder.Guide.Id); recorder.Dispose(); recorder=null; if(Directory.Exists(dir)) Directory.Delete(dir,true); session.Reset(); toolbar.Close(); toolbar=null; Show(); tray.Text="CAPTURE — not recording"; }
            catch(Exception ex) { recordingBlocked=true; session.Reset(); toolbar?.Close(); toolbar=null; Show(); tray.Text="CAPTURE — restart required"; PostError("Could not discard recording. Restart Capture. A saved draft may remain. "+ex.Message); }
            finally { busy=false; }
        };
        var screenshot = new Button { Content = "Screenshot", ToolTip = $"Ctrl + Shift + {(char)screenshotHotkey}", Margin = new Thickness(4), Padding = new Thickness(12,4,12,4) };
        screenshot.Click += async (_,_) => await ScreenshotFromHotkeyAsync();
        foreach (var button in new[] { screenshot, pause, finish, cancel })
        {
            button.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(39,44,54));
            button.Foreground = System.Windows.Media.Brushes.White;
            button.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(66,73,88));
            actions.Children.Add(button);
        } panel.Children.Add(actions); toolbar.Content=panel;
        toolbar.SourceInitialized += (_,_) => { recorder!.MaskWindow = new WindowInteropHelper(toolbar).Handle; if(!Native.SetWindowDisplayAffinity(new WindowInteropHelper(toolbar).Handle,0x11)) recordingText.Text="Toolbar exclusion unavailable; its rectangle will be masked in screenshots."; };
        toolbar.Closing += (_,e) => { if(session.State is SessionState.Recording or SessionState.Paused) e.Cancel=true; };
        toolbar.Show(); SetTheme(darkTheme ? "dark" : "light");
    }
    private void DisposeTray()
    {
        var icon = tray.Icon;
        tray.Visible = false;
        tray.Dispose();
        icon?.Dispose();
    }
    private void PostError(string message)
    {
        if (initialized) Editor.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new { type = "app.error", error = message }));
        if (recordingText is not null && session.State is SessionState.Recording or SessionState.Paused) recordingText.Text = message;
    }
    private object SetTheme(string? theme)
    {
        if (theme is not ("light" or "dark")) throw new InvalidDataException("Choose a light or dark theme.");
        darkTheme = theme == "dark";
        var enabled = darkTheme ? 1 : 0;
        Native.DwmSetWindowAttribute(new WindowInteropHelper(this).Handle, 20, ref enabled, sizeof(int));
        if (toolbar is not null)
        {
            Native.DwmSetWindowAttribute(new WindowInteropHelper(toolbar).Handle, 20, ref enabled, sizeof(int));
            toolbar.Background = new System.Windows.Media.SolidColorBrush(darkTheme ? System.Windows.Media.Color.FromRgb(24,27,33) : System.Windows.Media.Color.FromRgb(248,249,252));
            toolbar.Foreground = darkTheme ? System.Windows.Media.Brushes.White : System.Windows.Media.Brushes.Black;
            if (toolbar.Content is StackPanel panel)
                foreach (var actions in panel.Children.OfType<StackPanel>())
                    foreach (var button in actions.Children.OfType<Button>())
                    {
                        button.Background = new System.Windows.Media.SolidColorBrush(darkTheme ? System.Windows.Media.Color.FromRgb(39,44,54) : System.Windows.Media.Color.FromRgb(237,239,244));
                        button.Foreground = toolbar.Foreground;
                    }
        }
        return new { applied = true };
    }
    private async Task ScreenshotFromHotkeyAsync()
    {
        try { await CaptureScreenshotAsync(null, true); }
        catch (Exception ex) { PostError(ex is InvalidOperationException or InvalidDataException or TimeoutException ? ex.Message : "Screenshot could not be saved. Try again."); if (session.State == SessionState.Idle) Show(); }
    }
    private async Task<object> CaptureScreenshotAsync(Guid? stepId, bool fromHotkey = false)
    {
        if (!initialized || busy || exporting || capturing) throw new InvalidOperationException("Wait for the current operation to finish.");
        capturing = true;
        try { return await CaptureScreenshotCoreAsync(stepId, fromHotkey); }
        finally { capturing = false; }
    }
    private async Task<object> CaptureScreenshotCoreAsync(Guid? stepId, bool fromHotkey)
    {
        // Flush before hiding the editor. The acknowledgement supplies the current selection.
        if (session.State == SessionState.Idle) await FlushEditorAsync();
        if (fromHotkey) stepId = editorStepId;
        var snapshot = editorProjectId == current?.Id ? current : null;
        if (session.State == SessionState.Idle && stepId is not null && (snapshot is null || !snapshot.Steps.Any(s => s.Id == stepId)))
            throw new InvalidDataException("Select a step in the current guide first.");
        if (!Native.GetCursorPos(out var pointer)) throw new InvalidOperationException("Pointer position is unavailable.");
        var bounds = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point(pointer.X, pointer.Y)).Bounds;
        if ((long)bounds.Width * bounds.Height > 3840L * 2160) throw new InvalidOperationException("Use a monitor at 4K resolution or below for screenshots.");
        exporting = true;
        var wasVisible = IsVisible;
        try
        {
            if (wasVisible) Hide();
            toolbar?.Hide();
            await Task.Delay(250); // Allow the desktop compositor to remove our windows.
            var frame = await Task.Run(() =>
            {
                using var bitmap = new Bitmap(bounds.Width, bounds.Height);
                using (var graphics = Graphics.FromImage(bitmap)) graphics.CopyFromScreen(bounds.Location, System.Drawing.Point.Empty, bounds.Size);
                using var stream = new MemoryStream(); bitmap.Save(stream, ImageFormat.Png);
                return new Frame(stream.ToArray(), bounds, Stopwatch.GetTimestamp());
            });
            if (session.State is SessionState.Recording or SessionState.Paused)
            {
                await recorder!.AddScreenshotAsync(frame);
                return new { captured = true };
            }
            var target = snapshot ?? new Guide { Title = "Screenshot guide" };
            Guid selectedId = Guid.Empty;
            var result = await ImageImport.StorePngAsync(target, frame.Png, bounds.Width, bounds.Height, store, (g, a) =>
            {
                selectedId = ScreenshotSteps.Attach(g, a, stepId);
            });
            Select(result.Guide);
            Editor.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new { type = "project.changed", guide = current, stepId = selectedId }, GuideJson.Options));
            Show(); Activate();
            return new { captured = true };
        }
        finally { exporting = false; if (session.State is SessionState.Recording or SessionState.Paused) toolbar?.Show(); else if (wasVisible) Show(); }
    }
    private async Task<object> CopyAsync(string format)
    {
        RequireIdle();if(current is null)throw new InvalidOperationException("Open a guide first.");GuideJson.EnsurePrivacyApplied(current);
        if(format is not ("html" or "word"))throw new InvalidDataException("Unknown clipboard format.");
        var snapshot=GuideJson.Parse(GuideJson.Serialize(current));exporting=true;
        try
        {
            Task<ExportImage> Render(Step step)=>ExportImageRenderer.RenderAsync(snapshot,step,store.DirectoryFor(snapshot.Id));
            if(format=="html")ClipboardWriter.HtmlCode(await Task.Run(()=>DocumentExports.HtmlCodeAsync(snapshot,Render)));
            else ClipboardWriter.WordSteps(await Task.Run(()=>WordClipboard.CreateAsync(snapshot,Render)));
            return new {copied=true};
        }
        finally {exporting=false;}
    }
    private async Task<object> ExportAsync(string format)
    {
        RequireIdle(); if(current is null) throw new InvalidOperationException("Open a guide first.");
        GuideJson.EnsurePrivacyApplied(current);
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
        if(payload.GetProperty("saved").GetBoolean())
        {
            editorProjectId = payload.TryGetProperty("projectId", out var project) && project.ValueKind == JsonValueKind.String ? project.GetGuid() : null;
            editorStepId = payload.TryGetProperty("stepId", out var step) && step.ValueKind == JsonValueKind.String ? step.GetGuid() : null;
            flushCompletion?.TrySetResult();
        }
        else flushCompletion?.TrySetException(new IOException("Editor changes could not be saved."));
        return new { received = true };
    }
    private async Task FlushEditorAsync()
    {
        if (!initialized) return;
        flushCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Editor.CoreWebView2.PostWebMessageAsJson("{\"type\":\"editor.flush\"}");
        try { await flushCompletion.Task.WaitAsync(TimeSpan.FromSeconds(15)); }
        finally { flushCompletion = null; }
    }
    private async Task ExitAsync()
    {
        if(busy || exporting || capturing) return;
        if(session.State is SessionState.Recording or SessionState.Paused) { await ToggleAsync(); if(session.State != SessionState.Idle) return; }
        try { await FlushEditorAsync(); } catch { PostError("Editor changes could not be saved. Retry Save before exiting."); Show(); return; }
        exiting=true; recorder?.Dispose(); Native.UnregisterHotKey(new WindowInteropHelper(this).Handle,1); Native.UnregisterHotKey(new WindowInteropHelper(this).Handle,3); DisposeTray(); Editor.Dispose(); System.Windows.Application.Current.Shutdown();
    }
}
