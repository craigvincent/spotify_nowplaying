using Avalonia.Controls;
using Avalonia.Controls.Templates;
using TeamsNowPlaying.ViewModels;
using TeamsNowPlaying.Views;

namespace TeamsNowPlaying;

/// <summary>
/// Given a view model, returns the corresponding view if possible.
/// </summary>
public class ViewLocator : IDataTemplate
{
    public Control? Build(object? param)
    {
        if (param is null)
            return null;

        return param switch
        {
            MainWindowViewModel => new MainWindow(),
            _ => new TextBlock { Text = "Not Found: " + param.GetType().FullName }
        };
    }

    public bool Match(object? data)
    {
        return data is ViewModelBase;
    }
}
