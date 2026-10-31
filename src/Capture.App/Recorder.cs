using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading.Channels;
using Capture.Core;
using FlaUI.UIA3;
namespace Capture.App;
internal sealed record Click(int X, int Y, long Timestamp);
internal sealed record Frame(byte[] Png, Rectangle Bounds, long Timestamp);
internal sealed class Recorder : IDisposable
{
    private readonly Channel<Click> clicks = Channel.CreateBounded<Click>(new BoundedChannelOptions(128) { FullMode = BoundedChannelFullMode.Wait, SingleReader = true, SingleWriter = true });
    private readonly object frameLock = new();
    private readonly Queue<Frame> frames = new();
    private readonly Native.HookProc callback;
    private Thread? hookThread, worker, sampler;
    private uint threadId;
    private IntPtr hook;
    private volatile bool running, paused;
    private readonly ProjectStore store;
    public IntPtr MaskWindow { get; set; }
    public Guide Guide { get; } = new() { Title = "Recorded workflow " + DateTime.Now.ToString("yyyy-MM-dd HH:mm") };
    public event Action<string>? Status;
    private readonly string[] exclusions;
    public Recorder(ProjectStore store, string[] exclusions) { this.store = store; this.exclusions = exclusions; callback = OnMouse; }
    public void Start()
    {
        running = true;
        using var ready = new ManualResetEventSlim(); Exception? failure = null;
        hookThread = new Thread(() =>
        {
            threadId = Native.GetCurrentThreadId();
            Native.PeekMessage(out _,IntPtr.Zero,0,0,0);
            hook = Native.SetWindowsHookEx(14, callback, Native.GetModuleHandle(null), 0);
            if (hook == IntPtr.Zero) failure = new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            ready.Set();
            if (hook != IntPtr.Zero) { while (Native.GetMessage(out var m, IntPtr.Zero, 0, 0) > 0) { Native.TranslateMessage(ref m); Native.DispatchMessage(ref m); } Native.UnhookWindowsHookEx(hook); hook = IntPtr.Zero; }
        }) { IsBackground = true, Name = "Capture mouse hook" };
        hookThread.Start(); ready.Wait();
        if (failure is not null) { running = false; throw failure; }
        worker = new Thread(ProcessClicks) { IsBackground = true, Name = "Capture UIA MTA" };
        worker.SetApartmentState(ApartmentState.MTA); worker.Start();
        sampler = new Thread(Sample) { IsBackground = true, Name = "Capture pre-action frames" }; sampler.Start();
    }
    public void Pause(bool value) { paused = value; lock(frameLock) frames.Clear(); }
    private IntPtr OnMouse(int code, IntPtr message, IntPtr data)
    {
        if (code >= 0 && message == (IntPtr)0x201 && running && !paused)
        {
            var m = Marshal.PtrToStructure<Native.MouseData>(data);
            if ((m.Flags & 1) == 0 && !clicks.Writer.TryWrite(new(m.Point.X,m.Point.Y,Stopwatch.GetTimestamp()))) Interlocked.Increment(ref dropped);
        }
        return Native.CallNextHookEx(hook,code,message,data);
    }
    private int dropped;
    private bool IsAllowed(IntPtr window)
    {
        Native.GetWindowThreadProcessId(window,out var pid);
        if (pid == Environment.ProcessId || pid == 0) return false;
        try { using var process = Process.GetProcessById((int)pid); var name = process.ProcessName; return !exclusions.Any(e => name.Contains(e,StringComparison.OrdinalIgnoreCase)); }
        catch { return false; }
    }
    private void Sample()
    {
        while (running)
        {
            if (!paused)
            {
                try
                {
                    if (!IsAllowed(Native.GetForegroundWindow())) { lock(frameLock) frames.Clear(); Thread.Sleep(150); continue; }
                    var screen = System.Windows.Forms.Screen.FromHandle(Native.GetForegroundWindow()).Bounds;
                    // At most two PNG frames, and no more than one 4K monitor per sample.
                    if ((long)screen.Width * screen.Height > 3840L * 2160) { Status?.Invoke("Monitor exceeds prototype capture limit; use a display at 4K or below."); Thread.Sleep(500); continue; }
                    using var bitmap = new Bitmap(screen.Width,screen.Height);
                    using (var graphics = Graphics.FromImage(bitmap))
                    {
                        graphics.CopyFromScreen(screen.Location,Point.Empty,screen.Size);
                        if(MaskWindow != IntPtr.Zero && Native.GetWindowRect(MaskWindow,out var rect))
                            graphics.FillRectangle(Brushes.Black,rect.Left-screen.Left,rect.Top-screen.Top,rect.Right-rect.Left,rect.Bottom-rect.Top);
                    }
                    var timestamp = Stopwatch.GetTimestamp();
                    using var stream = new MemoryStream(); bitmap.Save(stream,ImageFormat.Png);
                    lock(frameLock) { frames.Enqueue(new(stream.ToArray(),screen,timestamp)); while (frames.Count > 2) frames.Dequeue(); }
                }
                catch { Status?.Invoke("Screenshot unavailable. Protected surfaces cannot be captured."); }
            }
            Thread.Sleep(150);
        }
    }
    private void ProcessClicks()
    {
        UIA3Automation? automation = null;
        try { automation = new UIA3Automation(); } catch { Status?.Invoke("Accessibility metadata unavailable; using click markers."); }
        using var automationLifetime = automation;
        while (clicks.Reader.WaitToReadAsync().AsTask().GetAwaiter().GetResult())
        {
            while (clicks.Reader.TryRead(out var click))
            {
                if (!IsAllowed(Native.WindowFromPoint(new() { X = click.X, Y = click.Y }))) continue;
                try
                {
                    Frame? frame;
                    lock(frameLock) frame = frames.LastOrDefault(f => f.Timestamp <= click.Timestamp && f.Bounds.Contains(click.X,click.Y));
                    if (frame is null || Stopwatch.GetElapsedTime(frame.Timestamp,click.Timestamp).TotalMilliseconds > 1000) { Status?.Invoke("A click had no recent pre-action screenshot and was skipped. Add a manual text step if needed."); continue; }
                    var step = new Step();
                    // UIA has no reliable cross-provider cancellation. This dedicated worker never blocks the hook or UI.
                    try
                    {
                        var element = (automation ?? throw new InvalidOperationException()).FromPoint(new Point(click.X,click.Y));
                        if (element.Properties.IsPassword.ValueOrDefault) { Status?.Invoke("Password control suppressed. Review nearby screenshots for sensitive information."); continue; }
                        var type = element.ControlType.ToString();
                        // Editable controls' Name can contain user content; do not persist it.
                        var name = type == "Edit" ? "" : element.Name;
                        if (!string.IsNullOrWhiteSpace(name) && name.Length <= 120)
                        {
                            step.Title = type switch { "TabItem" => $"Open the {name} tab", "MenuItem" => $"Choose {name}", "Hyperlink" => $"Open {name}", "CheckBox" => $"Change the {name} option", _ => $"Select {name}" };
                            step.Instruction = step.Title + ".";
                        }
                        else if(type == "Edit") { step.Title = "Enter the required information"; step.Instruction = "Enter the required information in the selected field."; }
                    }
                    catch { /* Safe click-marker fallback. */ }
                    var id = Guid.NewGuid().ToString(); var relative = $"assets/{id}.png";
                    var path = GuideJson.SafePath(store.DirectoryFor(Guide.Id),relative); Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllBytes(path,frame.Png);
                    Guide.Assets.Add(new(id,relative,"image/png",frame.Bounds.Width,frame.Bounds.Height,Convert.ToHexString(SHA256.HashData(frame.Png))));
                    step.AssetId = id; step.Order = Guide.Steps.Count+1;
                    step.Annotations.Add(Coordinates.Marker(click.X,click.Y,frame.Bounds.Left,frame.Bounds.Top,frame.Bounds.Width,frame.Bounds.Height));
                    // All UIA labels/timing are provisional until reviewed by a person.
                    Guide.Steps.Add(step); store.SaveAsync(Guide).GetAwaiter().GetResult();
                    Status?.Invoke($"Recording · {Guide.Steps.Count} steps" + (Volatile.Read(ref dropped) > 0 ? " · Some clicks were dropped; review the guide." : ""));
                }
                catch { Status?.Invoke("A step could not be saved. Check free disk space before continuing."); }
            }
        }
    }
    public async Task FinishAsync()
    {
        running = false; Native.PostThreadMessage(threadId,0x12,UIntPtr.Zero,IntPtr.Zero);
        clicks.Writer.TryComplete();
        // Do not silently open an incomplete guide if an external UIA provider hangs.
        await Task.Run(() => { sampler?.Join(); hookThread?.Join(); if (worker is not null && !worker.Join(TimeSpan.FromSeconds(10))) throw new TimeoutException("An application accessibility provider is not responding. Saved steps remain in Projects. Close Capture and restart it before recording again."); });
        lock(frameLock) frames.Clear();
        await store.SaveAsync(Guide);
    }
    public void Dispose() { running = false; paused = true; clicks.Writer.TryComplete(); if (threadId != 0) Native.PostThreadMessage(threadId,0x12,UIntPtr.Zero,IntPtr.Zero); }
}
