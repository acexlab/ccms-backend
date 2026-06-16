using System;
using System.IO;

namespace ccms_backend.services;

public static class GenerateTestFiles
{
    public static void EnsureTestFilesExist()
    {
        var targetDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "test-files");
        if (!Directory.Exists(targetDir))
        {
            Directory.CreateDirectory(targetDir);
        }

        CreateDummyFile(Path.Combine(targetDir, "small-order.pdf"), 100 * 1024);
        CreateDummyFile(Path.Combine(targetDir, "aadhaar.jpg"), 500 * 1024);
        CreateDummyFile(Path.Combine(targetDir, "pan.png"), 1024 * 1024);
        CreateDummyFile(Path.Combine(targetDir, "malware.exe"), 4 * 1024);
        CreateDummyFile(Path.Combine(targetDir, "large-file.pdf"), 6 * 1024 * 1024);
    }

    private static void CreateDummyFile(string filePath, int sizeInBytes)
    {
        if (File.Exists(filePath) && new FileInfo(filePath).Length == sizeInBytes)
        {
            return;
        }

        byte[] dummyData = new byte[sizeInBytes];
        // Populate with dummy bytes so it's not all zeros (simple content)
        for (int i = 0; i < dummyData.Length; i++)
        {
            dummyData[i] = (byte)(i % 256);
        }
        File.WriteAllBytes(filePath, dummyData);
    }
}
