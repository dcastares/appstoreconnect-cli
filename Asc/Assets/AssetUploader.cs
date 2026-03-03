using AppStoreConnectCli.Asc.Api;
using Serilog;

namespace AppStoreConnectCli.Asc.Assets;

/// <summary>
/// Handles the 3-step Apple asset upload flow:
///   1. Reserve  — POST /v1/gameCenterAchievementImages
///   2. Upload   — PUT to each signed S3 URL (no auth header)
///   3. Commit   — PATCH /v1/gameCenterAchievementImages/{id} with uploaded=true
///
/// If an image already exists for the localization (e.g. on re-run with --update),
/// the existing image is deleted before the new one is reserved.
/// Images are associated at the localization level per Apple's API design.
/// </summary>
public class AssetUploader
{
    private readonly AscHttpClient _client;
    private readonly HttpClient _rawHttp;

    public AssetUploader(AscHttpClient client, IHttpClientFactory httpClientFactory)
    {
        _client = client;
        // Use a separate, unauthenticated HttpClient for S3 signed URLs
        _rawHttp = httpClientFactory.CreateClient("s3upload");
    }

    public async Task UploadAsync(string localizationId, string filePath, CancellationToken ct = default)
    {
        var fileInfo = new FileInfo(filePath);
        if (!fileInfo.Exists)
            throw new FileNotFoundException($"Image file not found: {filePath}");

        var fileName = fileInfo.Name;
        var fileSize = fileInfo.Length;

        Log.Debug("Uploading image {FileName} ({Size} bytes) for localization {LocId}",
            fileName, fileSize, localizationId);

        ValidatePngDimensions(filePath);

        var reserveRequest = BuildReserveRequest(localizationId, fileName, fileSize);

        // Step 1: Reserve — if an image already exists, delete it first then retry once
        AscResponse<AscResourceObject<GameCenterAchievementImageAttributes>> reserveResponse;
        try
        {
            reserveResponse = await _client.PostAsync<
                GameCenterAchievementImageCreateRequest,
                AscResponse<AscResourceObject<GameCenterAchievementImageAttributes>>>(
                "/v1/gameCenterAchievementImages", reserveRequest, ct);
        }
        catch (AscApiException ex) when (ex.StatusCode == 409 && ex.Message.Contains("IMAGE_ALREADY_EXISTS"))
        {
            Log.Debug("Image already exists for localization {LocId} — deleting and re-uploading", localizationId);
            await DeleteExistingImageAsync(localizationId, ct);

            reserveResponse = await _client.PostAsync<
                GameCenterAchievementImageCreateRequest,
                AscResponse<AscResourceObject<GameCenterAchievementImageAttributes>>>(
                "/v1/gameCenterAchievementImages", reserveRequest, ct);
        }

        var imageId = reserveResponse?.Data?.Id
                      ?? throw new InvalidOperationException("No image ID returned from reserve step");
        var uploadOps = reserveResponse?.Data?.Attributes?.UploadOperations;

        if (uploadOps == null || uploadOps.Count == 0)
            throw new InvalidOperationException($"No upload operations returned for image {imageId}");

        // Step 2: Upload each part via signed URLs (no auth header)
        var fileBytes = await File.ReadAllBytesAsync(filePath, ct);
        foreach (var op in uploadOps)
        {
            Log.Debug("Uploading part offset={Offset} length={Length} to {Url}", op.Offset, op.Length, op.Url);
            await UploadPartAsync(op, fileBytes, ct);
        }

        // Step 3: Commit
        var commitRequest = new GameCenterAchievementImageCommitRequest
        {
            Data = new GameCenterAchievementImageCommitData
            {
                Id = imageId,
                Attributes = new GameCenterAchievementImageCommitAttributes
                {
                    Uploaded = true
                }
            }
        };

        await _client.PatchAsync<
            GameCenterAchievementImageCommitRequest,
            AscResponse<AscResourceObject<GameCenterAchievementImageAttributes>>>(
            $"/v1/gameCenterAchievementImages/{imageId}", commitRequest, ct);

        Log.Information("Image upload committed for localization {LocId}, imageId={ImageId}", localizationId, imageId);
    }

    private static void ValidatePngDimensions(string filePath)
    {
        // PNG IHDR: bytes 16-19 = width, 20-23 = height (big-endian)
        Span<byte> header = stackalloc byte[24];
        using var fs = File.OpenRead(filePath);
        if (fs.Read(header) < 24)
            throw new InvalidOperationException($"Image file is too small to be a valid PNG: {filePath}");

        // Check PNG signature
        ReadOnlySpan<byte> pngSig = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        if (!header[..8].SequenceEqual(pngSig))
            throw new InvalidOperationException($"Image file does not appear to be a PNG: {filePath}");

        var width  = (int)System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(header[16..20]);
        var height = (int)System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(header[20..24]);

        if (!((width == 512 && height == 512) || (width == 1024 && height == 1024)))
        {
            throw new InvalidOperationException(
                $"Image {Path.GetFileName(filePath)} is {width}x{height}px. " +
                $"Game Center achievement images must be 512x512 or 1024x1024 px PNG.");
        }

        Log.Debug("Image dimensions OK: {Width}x{Height}", width, height);
    }

    private async Task DeleteExistingImageAsync(string localizationId, CancellationToken ct)
    {
        var existing = await _client.GetAsync<AscResponse<AscResourceObject<GameCenterAchievementImageAttributes>>>(
            $"/v1/gameCenterAchievementLocalizations/{localizationId}/gameCenterAchievementImage", ct);

        var imageId = existing?.Data?.Id;
        if (imageId == null)
        {
            Log.Warning("IMAGE_ALREADY_EXISTS but could not find existing image for localization {LocId}", localizationId);
            return;
        }

        await _client.DeleteAsync($"/v1/gameCenterAchievementImages/{imageId}", ct);
        Log.Debug("Deleted existing image {ImageId} for localization {LocId}", imageId, localizationId);
    }

    private static GameCenterAchievementImageCreateRequest BuildReserveRequest(
        string localizationId, string fileName, long fileSize)
    {
        return new GameCenterAchievementImageCreateRequest
        {
            Data = new GameCenterAchievementImageCreateData
            {
                Attributes = new GameCenterAchievementImageCreateAttributes
                {
                    FileName = fileName,
                    FileSize = fileSize
                },
                Relationships = new GameCenterAchievementImageRelationships
                {
                    GameCenterAchievementLocalization = new AscRelationship
                    {
                        Data = new AscRelationshipData
                        {
                            Type = "gameCenterAchievementLocalizations",
                            Id = localizationId
                        }
                    }
                }
            }
        };
    }

    private async Task UploadPartAsync(UploadOperation op, byte[] fileBytes, CancellationToken ct)
    {
        var slice = fileBytes.AsMemory(op.Offset, op.Length);

        using var request = new HttpRequestMessage(new HttpMethod(op.Method), op.Url);
        request.Content = new ByteArrayContent(slice.ToArray());

        // Set headers from the operation (e.g. Content-Type)
        if (op.RequestHeaders != null)
        {
            foreach (var header in op.RequestHeaders)
            {
                // Content-* headers go on the content, not the request
                if (header.Name.StartsWith("Content-", StringComparison.OrdinalIgnoreCase))
                    request.Content.Headers.TryAddWithoutValidation(header.Name, header.Value);
                else
                    request.Headers.TryAddWithoutValidation(header.Name, header.Value);
            }
        }

        var response = await _rawHttp.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(
                $"S3 upload failed: {(int)response.StatusCode} {body}");
        }
    }
}
