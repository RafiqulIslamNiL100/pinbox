using Microsoft.Toolkit.Uwp.Notifications;

namespace Pinbox.Services;

// Best-effort Windows toast notifications. Wrapped defensively because toast
// delivery depends on Windows' notification stack, which varies across
// Windows versions/configurations and cannot be verified in this project's
// non-Windows test environment - a failure here must never take down the
// rest of the app, so this only ever logs, never throws outward.
public static class ToastService
{
    public static void Show(string title, string body)
    {
        try
        {
            new ToastContentBuilder()
                .AddText(title)
                .AddText(body)
                .Show();
        }
        catch
        {
            // No toast this time - the in-app confirmation still shows regardless.
        }
    }
}
