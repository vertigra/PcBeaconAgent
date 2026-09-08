using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using PcBeaconAgent.Contracts;
using PcBeaconAgent.Contracts.Models;
using PcBeaconAgent.Server.Core.Configuration;
using PcBeaconAgent.Server.Core.Extensions;
using PcBeaconAgent.Server.Core.Interfaces;
using PcBeaconAgent.Server.Core.Services;
using System;
using System.IO;

namespace PcBeaconAgent.Server.Core.Endpoints
{
    public static class TransferEndpointsExtensions
    {
        public static IEndpointRouteBuilder MapTransferServiceEndpoints(this IEndpointRouteBuilder app, AppSettings settings, IBeaconServerIdentity identity)
        {
            RouteGroupBuilder transferGroup = app.MapGroup("/api/transfer").RequireApiKey(identity);

            // POST /api/transfer/text — accepts a single text payload
            // from the Android client. Rate-limited (10 req/min per IP)
            // to prevent accidental flooding. The API key already
            // authenticates the caller; the rate limit is a courtesy
            // guard against a misbehaving client that spams the endpoint
            // in a loop.
            transferGroup.MapPost("/text", ([FromBody] TextTransferRequestDto? request, [FromServices] TransferController controller, HttpContext context) =>
            {
                if (request is null || string.IsNullOrWhiteSpace(request.Text))
                {
                    return Results.Json(
                        new TextTransferResponseDto(false, "Text payload is required."), ProjectJsonContext.Default.TextTransferResponseDto,
                        statusCode: StatusCodes.Status400BadRequest);
                }

                string sourceIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                var (accepted, message) = controller.ReceiveText(request.Text, sourceIp);

                return Results.Json(
                    new TextTransferResponseDto(accepted, message), ProjectJsonContext.Default.TextTransferResponseDto,
                    statusCode: accepted ? StatusCodes.Status200OK : StatusCodes.Status400BadRequest);
            }).RequireRateLimiting("transfer-text");

            // POST /api/transfer/file — accepts a single file upload via
            // multipart/form-data. The file is streamed to disk in the
            // configured save folder (see TransferSettings.SaveFolder).
            // No size cap per user decision — the API key authenticates
            // the caller, and the user trusts their own paired devices.
            // Files are streamed (not buffered) so memory usage stays
            // bounded regardless of file size. Rate-limited to 5 req/min
            // per IP — files are heavier than text, so the limit is
            // tighter.
            transferGroup.MapPost("/file", async ([FromForm] IFormFile? file, [FromServices] TransferController controller, HttpContext context) =>
            {
                if (file is null || file.Length == 0)
                {
                    return Results.Json(
                        new FileTransferResponseDto(false, "No file was provided.", string.Empty),
                        ProjectJsonContext.Default.FileTransferResponseDto,
                        statusCode: StatusCodes.Status400BadRequest);
                }

                string sourceIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                // Open the upload stream and pass it to the controller.
                // The controller is responsible for copying to disk;
                // we dispose the source stream here (using-scope).
                using Stream uploadStream = file.OpenReadStream();
                var (accepted, message, savedFileName) = controller.ReceiveFile(uploadStream, file.FileName, sourceIp);

                return Results.Json(
                    new FileTransferResponseDto(accepted, message, savedFileName),
                    ProjectJsonContext.Default.FileTransferResponseDto,
                    statusCode: accepted ? StatusCodes.Status200OK : StatusCodes.Status400BadRequest);
            }).RequireRateLimiting("transfer-file").DisableAntiforgery();

            // GET /api/transfer/download/{token} — streams an outgoing
            // file to the Android client. The token is the
            // TransferRecord.Id returned in the ReceiveFileTransfer
            // SignalR push event. The file was saved to the "outgoing"
            // subfolder of the save folder by
            // TransferController.SendFileToClientAsync. No rate limit
            // — downloads are gated by the token (only known tokens
            // resolve to files, and tokens are unguessable GUIDs).
            transferGroup.MapGet("/download/{token}", ([FromRoute] string token, [FromServices] TransferController controller) =>
            {
                if (string.IsNullOrEmpty(token))
                {
                    return Results.BadRequest(new MessageDto("Download token is required."));
                }

                string? filePath = controller.GetOutgoingFilePath(token);
                if (filePath == null || !File.Exists(filePath))
                {
                    return Results.NotFound(new MessageDto("File not found. It may have been evicted from history."));
                }

                // Stream the file. Results.File with a physical path
                // uses SendFileAsync under the hood on Kestrel, which
                // is zero-copy on Windows. The content type is
                // application/octet-stream so the browser/client treats
                // it as a download rather than trying to render it.
                // The file name in the Content-Disposition header is
                // taken from the actual file on disk (which has a GUID
                // name) — the Android client already received the
                // friendly name in the push event and uses that when
                // saving locally.
                return Results.File(filePath, contentType: "application/octet-stream", fileDownloadName: Path.GetFileName(filePath));
            });

            return app;
        }
    }
}
