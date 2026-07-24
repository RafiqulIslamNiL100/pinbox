using System.IO;
using System.IO.Compression;

namespace Pinbox.Services;

public static class ImportExportService
{
    public static void Export(string userId, string destinationZipPath)
    {
        var dataDir = PageStore.DataRootForExport(userId);
        if (!Directory.Exists(dataDir))
            throw new AuthException("Nothing to export yet.");

        if (File.Exists(destinationZipPath)) File.Delete(destinationZipPath);
        ZipFile.CreateFromDirectory(dataDir, destinationZipPath);
    }

    public static void Import(string userId, string sourceZipPath)
    {
        var dataDir = PageStore.DataRootForExport(userId);
        if (Directory.Exists(dataDir)) Directory.Delete(dataDir, true);
        Directory.CreateDirectory(dataDir);
        ZipFile.ExtractToDirectory(sourceZipPath, dataDir, overwriteFiles: true);
    }
}
