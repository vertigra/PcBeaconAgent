using PcBeaconAgent.Contracts.Models;
using System.Threading.Tasks;

namespace PcBeaconAgent.Client.Core.Interfaces
{
    /// <summary>
    /// Client-side accessor for the server's
    /// <c>POST /api/transfer/text</c> endpoint. Created per-device by
    /// <see cref="Stores.DeviceFactory"/> and exposed on
    /// <see cref="Models.ManagedDevice.Transfer"/>.
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
    }
}
