using System.IO;

namespace ScreenTranslator.Services;

public static class AppLogger
{
    private static readonly string LogFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ScreenTranslator",
        "debug.log");

    private static readonly Lock LockObj = new();

    public static void Log(string message)
    {
        var line = $"[{DateTime.Now:yyyy-MM-ddTHH:mm:ss.fffzzz}] {message}";
        Console.WriteLine(line);

        try
        {
            lock (LockObj)
            {
                var dir = Path.GetDirectoryName(LogFilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                // Giới hạn dung lượng file log tối đa 2MB để tránh phình to ổ cứng
                var fileInfo = new FileInfo(LogFilePath);
                if (fileInfo.Exists && fileInfo.Length > 2 * 1024 * 1024)
                {
                    var oldPath = Path.Combine(dir!, "debug.old.log");
                    File.Move(LogFilePath, oldPath, overwrite: true);
                }

                File.AppendAllText(LogFilePath, line + Environment.NewLine);
            }
        }
        catch
        {
            // Bỏ qua nếu không ghi được file log
        }
    }
}
