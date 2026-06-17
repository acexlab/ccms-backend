using System.IO;
using System.Threading.Tasks;

namespace CCMS.Application.Interfaces;

public interface IBlobStorageService
{
    Task<string> UploadFileAsync(Stream fileStream, string fileName, string contentType);
    Task<Stream?> DownloadFileAsync(string blobName);
}
