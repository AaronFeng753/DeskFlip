using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using DeskFlip.Gesture;

namespace DeskFlip.Hook;

/// <summary>
/// Installs a global <c>WH_MOUSE_LL</c> hook on a dedicated thread with its own message
/// pump. The hook callback only enqueues the sample (physical pixels) and
/// returns immediately. A watchdog reinstalls the hook when the system silently removes
/// it after LowLevelHooksTimeout.
/// </summary>
public sealed class MouseHookService : IMouseEventSource, IDisposable
{
    private const int WH_MOUSE_LL = 14;
    private const int WM_MOUSEMOVE = 0x0200;
    private const uint WM_REINSTALL_HOOK = WM_APP + 1;
    private const uint WM_APP = 0x8000;
    private const uint WM_QUIT = 0x0012;

    private const int VK_LBUTTON = 0x01;
    private const int VK_RBUTTON = 0x02;
    private const int VK_MBUTTON = 0x04;
    private const int VK_XBUTTON1 = 0x05;
    private const int VK_XBUTTON2 = 0x06;

    private static readonly TimeSpan WatchdogInterval = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan MinReinstallInterval = TimeSpan.FromSeconds(10);

    public event Action<GesturePoint, GestureButtons, DateTime>? MouseMoved;

    /// <inheritdoc />
    public event Action<string>? Trace;

    // Unbounded queue: Add never blocks, so the hook callback stays non-blocking.
    private readonly BlockingCollection<(GesturePoint Point, GestureButtons Buttons, DateTime At)> _items =
        new(new ConcurrentQueue<(GesturePoint, GestureButtons, DateTime)>());

    private readonly ManualResetEventSlim _threadReady = new(false);
    private Thread? _hookThread;
    private int _hookThreadId;
    private IntPtr _hookHandle;
    private HookProc? _hookProc; // rooted so the GC cannot collect the callback delegate
    private volatile bool _stopping;

    private Timer? _watchdog;
    private Task? _consumer;
    private long _lastHookEventTicks;
    private NativePoint _lastWatchPos;
    private DateTime _lastWatchAt;
    private DateTime _lastReinstallAt = DateTime.MinValue;

    public void Start()
    {
        if (_hookThread != null)
            throw new InvalidOperationException("MouseHookService is already started.");

        _stopping = false;
        _hookThread = new Thread(HookThreadMain) { IsBackground = true, Name = "MouseHook" };
        _hookThread.SetApartmentState(ApartmentState.STA);
        _hookThread.Start();
        _threadReady.Wait();

        _consumer = Task.Run(ConsumeLoop);
        _watchdog = new Timer(WatchdogTick, null, WatchdogInterval, WatchdogInterval);
    }

    public void Stop()
    {
        if (_hookThread == null)
            return;

        _stopping = true;
        _watchdog?.Dispose();
        _watchdog = null;

        PostThreadMessage(_hookThreadId, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
        _hookThread.Join(TimeSpan.FromSeconds(5));
        _hookThread = null;

        _items.CompleteAdding();
        try { _consumer?.Wait(TimeSpan.FromSeconds(5)); }
        catch (AggregateException) { /* consumer faults are surfaced during normal operation */ }
        _consumer = null;
    }

    public void Dispose() => Stop();

    private void ConsumeLoop()
    {
        try
        {
            foreach (var (point, buttons, at) in _items.GetConsumingEnumerable())
                MouseMoved?.Invoke(point, buttons, at);
        }
        catch (InvalidOperationException)
        {
            // CompleteAdding during Stop: normal shutdown path.
        }
    }

    private void HookThreadMain()
    {
        _hookThreadId = GetCurrentThreadId();
        // Force message-queue creation so PostThreadMessage can never race ahead of it.
        PeekMessage(out _, IntPtr.Zero, 0, 0, PM_NOREMOVE);
        InstallHook();
        _threadReady.Set();

        while (GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
        {
            if (msg.Message == WM_REINSTALL_HOOK)
                ReinstallHook();
            else
            {
                TranslateMessage(in msg);
                DispatchMessage(in msg);
            }
        }

        UninstallHook();
    }

    private void InstallHook()
    {
        _hookProc ??= HookCallback; // one delegate instance for the service lifetime
        _hookHandle = SetWindowsHookEx(WH_MOUSE_LL, _hookProc, IntPtr.Zero, 0);
        if (_hookHandle == IntPtr.Zero)
        {
            var error = Marshal.GetLastWin32Error();
            Trace?.Invoke($"SetWindowsHookEx FAILED (error {error})");
            throw new InvalidOperationException($"SetWindowsHookEx failed (error {error}).");
        }
        Trace?.Invoke("hook installed");
        Interlocked.Exchange(ref _lastHookEventTicks, DateTime.Now.Ticks);
    }

    private void UninstallHook()
    {
        if (_hookHandle != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookHandle);
            _hookHandle = IntPtr.Zero;
        }
    }

    private void ReinstallHook()
    {
        UninstallHook();
        InstallHook();
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && wParam == (IntPtr)WM_MOUSEMOVE)
        {
            var data = Marshal.PtrToStructure<MouseHookStruct>(lParam);
            _items.Add((new GesturePoint(data.Point.X, data.Point.Y), ReadButtons(), DateTime.Now));
            Interlocked.Exchange(ref _lastHookEventTicks, DateTime.Now.Ticks);
        }
        return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    private static GestureButtons ReadButtons()
    {
        var buttons = GestureButtons.None;
        if (GetAsyncKeyState(VK_LBUTTON) < 0) buttons |= GestureButtons.Left;
        if (GetAsyncKeyState(VK_RBUTTON) < 0) buttons |= GestureButtons.Right;
        if (GetAsyncKeyState(VK_MBUTTON) < 0) buttons |= GestureButtons.Middle;
        if (GetAsyncKeyState(VK_XBUTTON1) < 0) buttons |= GestureButtons.XButton1;
        if (GetAsyncKeyState(VK_XBUTTON2) < 0) buttons |= GestureButtons.XButton2;
        return buttons;
    }

    // Watchdog: if the cursor demonstrably moved since the last check but the
    // hook produced no event in the same window, the system has silently unloaded the hook.
    private void WatchdogTick(object? state)
    {
        if (_stopping)
            return;

        var now = DateTime.Now;
        if (GetCursorPos(out var pos))
        {
            var moved = pos.X != _lastWatchPos.X || pos.Y != _lastWatchPos.Y;
            var lastEvent = new DateTime(Interlocked.Read(ref _lastHookEventTicks));
            var hookSilent = lastEvent < _lastWatchAt;

            if (moved && hookSilent && now - _lastReinstallAt >= MinReinstallInterval)
            {
                _lastReinstallAt = now;
                Trace?.Invoke("watchdog: cursor moved but hook silent — reinstalling hook");
                PostThreadMessage(_hookThreadId, WM_REINSTALL_HOOK, IntPtr.Zero, IntPtr.Zero);
            }

            _lastWatchPos = pos;
        }
        _lastWatchAt = now;
    }

    private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    private const uint PM_NOREMOVE = 0x0000;

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseHookStruct
    {
        public NativePoint Point;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeMessage
    {
        public IntPtr Hwnd;
        public uint Message;
        public IntPtr WParam;
        public IntPtr LParam;
        public uint Time;
        public NativePoint Point;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint lpPoint);

    [DllImport("user32.dll")]
    private static extern int GetMessage(out NativeMessage lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PeekMessage(out NativeMessage lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TranslateMessage(in NativeMessage lpMsg);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessage(in NativeMessage lpMsg);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostThreadMessage(int idThread, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    private static extern int GetCurrentThreadId();
}
