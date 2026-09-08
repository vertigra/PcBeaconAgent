namespace PcBeaconAgent.Contracts.Models
{
    /// <summary>
    /// Request payload for <c>POST /api/transfer/text</c>. Carries a
    /// single text payload (clipboard content, link, snippet) from the
    /// Android client to the managed PC.
    /// </summary>
    /// <param name="Text">Arbitrary Unicode text. The server enforces a
    /// size cap (see <c>TransferController.MaxTextSizeBytes</c>) and
    /// rejects empty / whitespace-only payloads.</param>
    public record TextTransferRequestDto(string Text);

    /// <summary>
    /// Response payload for <c>POST /api/transfer/text</c>. Carries the
    /// accept/reject decision and a short human-readable message.
    /// </summary>
    /// <param name="Accepted"><c>true</c> if the transfer was accepted
    /// and stored in history; <c>false</c> if rejected (size cap, empty
    /// payload, rate limit).</param>
    /// <param name="Message">Short status message suitable for showing
    /// in the client UI (e.g. "Transfer received", "Payload too
    /// large").</param>
    public record TextTransferResponseDto(bool Accepted, string Message);

    /// <summary>
    /// Response payload for <c>POST /api/transfer/file</c>. The request
    /// is <c>multipart/form-data</c> (file upload), so there is no
    /// matching request DTO — ASP.NET Core binds the file via
    /// <c>IFormFile</c>.
    /// </summary>
    /// <param name="Accepted"><c>true</c> if the file was saved to
    /// disk; <c>false</c> if rejected (empty file, save folder
    /// misconfigured, disk error).</param>
    /// <param name="Message">Short status message suitable for showing
    /// in the client UI (e.g. "File received: photo.jpg").</param>
    /// <param name="FileName">The sanitised file name that was saved.
    /// May differ from the client's original name if a collision
    /// occurred (numeric suffix appended). Empty if
    /// <paramref name="Accepted"/> is <c>false</c>.</param>
    public record FileTransferResponseDto(bool Accepted, string Message, string FileName);
}
