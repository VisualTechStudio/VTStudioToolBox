using Microsoft.UI.Xaml;
using System;

namespace VTStudioToolBox.Helpers;

public static class WindowHelper
{
    private static Window? _window;

    public static void SetWindow(Window window)
    {
        _window = window;
    }

    public static Window GetWindow()
    {
        return _window ?? throw new InvalidOperationException("Window not initialized");
    }
}
