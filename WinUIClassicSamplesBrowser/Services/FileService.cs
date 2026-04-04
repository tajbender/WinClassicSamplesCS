using System.Text;

using Newtonsoft.Json;
using WinUIClassicSamplesBrowser.Contracts.Services;

namespace WinUIClassicSamplesBrowser.Services;

public class FileService : IFileService
{
    public T Read<T>(string folderPath, string fileName)
    {
        var path = Path.Combine(folderPath, fileName);
        if (File.Exists(path))
        {
            var json = File.ReadAllText(path);
            return JsonConvert.DeserializeObject<T>(json);
        }

        return default;
    }

    public void Save<T>(string folderPath, string fileName, T content)
    {
        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }

        var fileContent = JsonConvert.SerializeObject(content);
        File.WriteAllText(Path.Combine(folderPath, fileName), fileContent, Encoding.UTF8);
    }

    public void Delete(string folderPath, string fileName)
    {
        if (fileName != null && File.Exists(Path.Combine(folderPath, fileName)))
        {
            File.Delete(Path.Combine(folderPath, fileName));
        }
    }

    public bool IsExecutable(string filePath)
    {
        if (File.Exists(filePath))
        {
            var isExecutable = string.Equals(Path.GetExtension(filePath), ".exe", StringComparison.OrdinalIgnoreCase)
                || string.Equals(Path.GetExtension(filePath), ".com", StringComparison.OrdinalIgnoreCase);

            if (isExecutable)
            {
                return true;
            }
        }

        return false;
    }

    public bool IsPeExecutable(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return false;
        }

        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        using var br = new BinaryReader(fs);

        // DOS-Header: "MZ"
        if (br.ReadUInt16() != 0x5A4D)
        {
            return false;
        }

        // PE-Header-Offset
        fs.Seek(0x3C, SeekOrigin.Begin);
        int peOffset = br.ReadInt32();

        fs.Seek(peOffset, SeekOrigin.Begin);

        // PE-Signature: "PE\0\0"
        return br.ReadUInt32() == 0x00004550;
    }
}
