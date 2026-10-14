using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace BitLifeClone;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // Log de crashes: si el exe publicado no abre, crash.log junto al exe dice por que.
        DispatcherUnhandledException += (s, args) =>
        {
            LogCrash(args.Exception);
            MessageBox.Show(args.Exception.ToString(), "BitLifeClone - error",
                MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            LogCrash(args.ExceptionObject as Exception ?? new Exception("excepcion desconocida"));

        base.OnStartup(e);
    }

    private static void LogCrash(Exception ex)
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "crash.log");
            File.AppendAllText(path,
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\n{ex}\n\n--------------------------------------------------\n\n");
        }
        catch
        {
            // si ni eso funciona (carpeta protegida), lo mostramos igual en el MessageBox
        }
    }
}
