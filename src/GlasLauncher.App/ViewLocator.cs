using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using GlasLauncher.App.ViewModels;

namespace GlasLauncher.App;

public class ViewLocator : IDataTemplate
{
    public Control? Build(object? param)
    {
        if (param is null)
        {
            return null;
        }

        var name = param.GetType().FullName!.Replace("ViewModel", "View");
        var type = Type.GetType(name);

        if (type != null)
        {
            return (Control)Activator.CreateInstance(type)!;
        }

        return new TextBlock { Text = "Vue introuvable : " + name };
    }

    public bool Match(object? data) => data is ViewModelBase;
}
