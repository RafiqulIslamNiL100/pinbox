using System;

namespace Pinbox.Services;

// Identifies this install for the one-device-at-a-time license check. The
// id is a random GUID generated once and persisted in app-settings.json -
// not tied to hardware, so a reinstall counts as a "new" device, which is
// the simpler and more forgiving behavior for a licensing feature like this.
public static class DeviceService
{
    private static string? _cachedId;

    public static string DeviceId
    {
        get
        {
            if (_cachedId != null) return _cachedId;

            var settings = AppSettingsService.Load();
            if (string.IsNullOrEmpty(settings.DeviceId))
            {
                settings.DeviceId = Guid.NewGuid().ToString("N");
                AppSettingsService.Save(settings);
            }

            _cachedId = settings.DeviceId;
            return _cachedId;
        }
    }

    public static string DeviceLabel => Environment.MachineName;
}
