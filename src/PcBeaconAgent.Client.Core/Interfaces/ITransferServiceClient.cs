using PcBeaconAgent.Contracts.Models;
using System.Net.Http;
using System.Threading.Tasks;

namespace PcBeaconAgent.Client.Core.Interfaces
{
    /// <summary>
    /// Client-side accessor for the server's transfer endpoints
    /// (<c>POST /api/transfer/text</c> and <c>POST /api/transfer/file</c>).
    /// Created per-device by <see cref="Stores.DeviceFactory"/> and
    /// exposed on <see cref="Models.ManagedDevice.Transfer"/>.
    /// </summary>
    public interface ITransferServiceClient
    {
        /// <summary>
        /// Sends a text payload to the managed PC. The server validates
        /// (non-empty, size cap) and returns an accept/reject decision
        /// with a short message.
        /// </summary>
        /// <param name="text">The text to send. Must not be empty or
        /// whitespace-only.</param>
        /// <returns>The server's response. Check
        /// <see cref="TextTransferResponseDto.Accepted"/> to determine
        /// whether the transfer was stored.</returns>
        Task<TextTransferResponseDto> SendTextAsync(string text);

        /// <summary>
        /// Sends a file to the managed PC. The file content is streamed
        /// (not buffered) so memory usage stays bounded regardless of
        /// file size. The server saves the file to its configured save
        /// folder and returns the sanitised file name.
        /// </summary>
        /// <param name="content">The file content as an
        /// <see cref="HttpContent"/> (typically
        /// <see cref="StreamContent"/> wrapping a file stream). The
        /// caller is responsible for setting the Content-Type header
        /// if a specific type is desired.</param>
        /// <param name="fileName">The original file name. The server
        /// sanitises it (strips directory components, handles reserved
        /// Windows names, appends a numeric suffix on collision).</param>
        /// <returns>The server's response. Check
        /// <see cref="FileTransferResponseDto.Accepted"/> and
        /// <see cref="FileTransferResponseDto.FileName"/> (the actual
        /// saved name, which may differ from the input on collision).</returns>
        Task<FileTransferResponseDto> SendFileAsync(HttpContent content, string fileName);
    }
}
