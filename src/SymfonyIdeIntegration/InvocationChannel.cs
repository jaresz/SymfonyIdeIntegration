using System.Diagnostics;
using System.IO.Pipes;
using System.Security.Principal;
using System.Text.Json;

namespace SymfonyIdeIntegration;

internal sealed class InvocationChannel : IDisposable
{
    private const int MaxReportBytes = 1024 * 1024;
    private readonly CancellationTokenSource shutdown = new();
    private readonly Task listener;
    private readonly object gate = new();
    private InvocationReport? latest;
    private int disposed;

    // Separate Windows users and login sessions, and let Windows enforce the
    // same-user restriction on both ends of the local pipe.
    private static string PipeName
    {
        get
        {
            using var identity = WindowsIdentity.GetCurrent();
            using var process = Process.GetCurrentProcess();
            return $"Q3a-Symfony2ide.Invocation.{identity.User!.Value}.{process.SessionId}";
        }
    }

    internal InvocationChannel() => listener = Task.Run(ListenAsync);

    internal InvocationReport? Latest
    {
        get { lock (gate) return latest; }
    }

    internal static void Publish(InvocationReport report)
    {
        // Diagnostics are best effort. A stopped tray or unavailable pipe must
        // never prevent the independent protocol process from opening the editor.
        try
        {
            SendAsync(report).GetAwaiter().GetResult();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or
            OperationCanceledException or TimeoutException or System.Security.SecurityException or JsonException or InvalidOperationException)
        { }
    }

    private static async Task SendAsync(InvocationReport report)
    {
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(report);
        if (payload.Length > MaxReportBytes)
            return;

        using var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        using var deadline = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        await pipe.ConnectAsync(200, deadline.Token).ConfigureAwait(false);
        await pipe.WriteAsync(BitConverter.GetBytes(payload.Length), deadline.Token).ConfigureAwait(false);
        await pipe.WriteAsync(payload, deadline.Token).ConfigureAwait(false);
        // An acknowledgement ensures the tray has consumed the snapshot before
        // the short-lived protocol process closes its end of the pipe.
        await pipe.ReadExactlyAsync(new byte[1], deadline.Token).ConfigureAwait(false);
    }

    private async Task ListenAsync()
    {
        CancellationToken stopping = shutdown.Token;
        while (!stopping.IsCancellationRequested)
        {
            try
            {
                using var pipe = new NamedPipeServerStream(PipeName, PipeDirection.InOut, 1,
                    PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await pipe.WaitForConnectionAsync(stopping).ConfigureAwait(false);
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(stopping);
                deadline.CancelAfter(TimeSpan.FromSeconds(1));
                CancellationToken token = deadline.Token;
                byte[] header = new byte[sizeof(int)];
                await pipe.ReadExactlyAsync(header, token).ConfigureAwait(false);
                int length = BitConverter.ToInt32(header);
                // Bound the allocation and the read time even for malformed or
                // incomplete messages; the tray UI never waits for this listener.
                if (length <= 0 || length > MaxReportBytes)
                    continue;
                byte[] payload = new byte[length];
                await pipe.ReadExactlyAsync(payload, token).ConfigureAwait(false);
                var report = JsonSerializer.Deserialize<InvocationReport>(payload);
                if (report is not null)
                {
                    lock (gate)
                    {
                        if (latest is null || report.IsNewerThan(latest))
                            latest = report;
                    }
                }
                await pipe.WriteAsync(new byte[] { 1 }, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stopping.IsCancellationRequested) { break; }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or
                OperationCanceledException or JsonException or System.Security.SecurityException)
            {
                // Recreate the pipe after a broken client or a transient failure.
                // A short delay prevents a tight retry loop if pipe creation fails.
                try { await Task.Delay(50, stopping).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
            }
        }
    }

    public void Dispose()
    {
        // Shutdown must be idempotent: Cancel throws after its source is disposed.
        if (Interlocked.Exchange(ref disposed, 1) != 0)
            return;

        shutdown.Cancel();
        listener.GetAwaiter().GetResult();
        shutdown.Dispose();
        lock (gate) latest = null;
    }
}
