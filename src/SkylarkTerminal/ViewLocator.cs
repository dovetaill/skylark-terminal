using System;
using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using SkylarkTerminal.ViewModels;

namespace SkylarkTerminal;

public class ViewLocator : IDataTemplate
{
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Views are preserved via TrimmerRootAssembly")]
    [UnconditionalSuppressMessage("Trimming", "IL2057", Justification = "Views are preserved via TrimmerRootAssembly")]
    [UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "Views are preserved via TrimmerRootAssembly")]
    public Control? Build(object? param)
    {
        if (param is null)
            return null;

        var name = param.GetType().FullName!.Replace("ViewModel", "View", StringComparison.Ordinal);
        var type = Type.GetType(name);

        if (type != null)
        {
            return (Control)Activator.CreateInstance(type)!;
        }

        return new TextBlock { Text = "Not Found: " + name };
    }

    public bool Match(object? data)
    {
        return data is ViewModelBase;
    }
}
