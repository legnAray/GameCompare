using System.ComponentModel;
using System.Diagnostics;
using System.IO;

namespace GameCompare.App.Services;

public sealed class ObsidianUriLauncher
{
    public void OpenPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Obsidian 路径不能为空。", nameof(path));
        }

        var fullPath = Path.GetFullPath(path);
        var uri = $"obsidian://open?path={Uri.EscapeDataString(fullPath)}";
        try
        {
            _ = Process.Start(new ProcessStartInfo(uri)
            {
                UseShellExecute = true
            });
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
        {
            throw new InvalidOperationException(
                "无法启动 Obsidian。请确认 Obsidian 已安装，并且至少运行过一次以注册 obsidian:// 协议。",
                exception);
        }
    }
}
