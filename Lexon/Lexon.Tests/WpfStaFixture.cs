using System.Runtime.ExceptionServices;
using System.Windows.Threading;
using Lexon.SettingsUi;
using Xunit;

namespace Lexon.Tests;

[CollectionDefinition("WpfSta")]
public sealed class WpfStaCollection : ICollectionFixture<WpfStaFixture>
{
}

public sealed class WpfStaFixture : IDisposable
{
    private readonly Thread _thread;
    private readonly Dispatcher _dispatcher;

    public WpfStaFixture()
    {
        Dispatcher? dispatcher = null;
        var ready = new ManualResetEventSlim(false);
        Exception? startError = null;
        _thread = new Thread(() =>
        {
            try
            {
                WpfBootstrap.EnsureApplication();
                dispatcher = Dispatcher.CurrentDispatcher;
                ready.Set();
                Dispatcher.Run();
            }
            catch (Exception ex)
            {
                startError = ex;
                ready.Set();
            }
        });
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.IsBackground = true;
        _thread.Start();
        ready.Wait();
        if (startError != null)
        {
            ExceptionDispatchInfo.Capture(startError).Throw();
        }

        _dispatcher = dispatcher ?? throw new InvalidOperationException("WPF dispatcher did not start.");
    }

    public void Run(Action action)
    {
        Exception? error = null;
        _dispatcher.Invoke(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });
        if (error != null)
        {
            ExceptionDispatchInfo.Capture(error).Throw();
        }
    }

    public void Dispose()
    {
        _dispatcher.InvokeShutdown();
        _thread.Join(TimeSpan.FromSeconds(5));
    }
}
