using System.Threading;

namespace EcoEarnServer.Common;

public class DeviceInfoContext
{
    private static AsyncLocal<DeviceInfo> _currentDeviceInfo = new AsyncLocal<DeviceInfo>();

    public static DeviceInfo CurrentDeviceInfo
    {
        get => _currentDeviceInfo.Value;
        set => _currentDeviceInfo.Value = value;
    }

    public static void Clear()
    {
        _currentDeviceInfo.Value = null;
    }
}

public class DeviceInfo
{
    /// <see cref="DeviceType"/>
    public string ClientType { get; set; }

    public string Version { get; set; }
}