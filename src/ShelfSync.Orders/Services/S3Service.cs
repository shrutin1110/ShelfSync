using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using ShelfSync.Orders.Settings;

namespace ShelfSync.Orders.Services;

public class S3Service : IS3Service
{
    private readonly IAmazonS3 _s3Client;
    private readonly AwsSettings _settings;
    private readonly ILogger<S3Service> _logger;

    // IAmazonS3 is injected from DI
    // AWSSDK.Extensions.NETCore.Setup registers it
    // when you call builder.Services.AddAWSService<IAmazonS3>()
    public S3Service(
        IAmazonS3 s3Client,
        IOptions<AwsSettings> settings,
        ILogger<S3Service> logger)
    {
        _s3Client = s3Client;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<PresignedUploadResult>
        GenerateProductImageUploadUrlAsync(
            Guid productId,
            Guid tenantId,
            string fileExtension)
    {
        // Build structured S3 key (file path inside bucket)
        // Format: products/{tenantId}/{productId}.{extension}
        // Example: products/acme-guid/shirt-guid.jpg
        //
        // Why include tenantId in path?
        // → organises files by tenant
        // → easy to find all files for one tenant
        // → easy to delete all files if tenant leaves
        var s3Key = $"products/{tenantId}/{productId}" +
                    $".{fileExtension.TrimStart('.')}";

        var expiresAt = DateTime.UtcNow.AddMinutes(15);

        var request = new GetPreSignedUrlRequest
        {
            BucketName = _settings.BucketName,

            // Key = the file path inside the bucket
            Key = s3Key,

            // PUT = this URL allows uploading a file
            // not downloading, not deleting
            Verb = HttpVerb.PUT,

            Expires = expiresAt,

            // Content type tells S3 what kind of file to expect
            // React must send matching Content-Type header
            // S3 rejects the upload if they do not match
            ContentType = GetContentType(fileExtension)
        };

        // GetPreSignedURL is a LOCAL operation — no network call
        // It generates a signed URL using your credentials
        // No file is uploaded yet — you just get the URL
        var uploadUrl = await Task.FromResult(
            _s3Client.GetPreSignedURL(request));

        _logger.LogInformation(
            "Generated presigned upload URL for {S3Key}", s3Key);

        return new PresignedUploadResult(
            UploadUrl: uploadUrl,
            S3Key: s3Key,
            ExpiresAt: expiresAt);
    }

    public async Task<string> GenerateDownloadUrlAsync(
        string s3Key,
        int expiryMinutes = 60)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _settings.BucketName,
            Key = s3Key,
            Verb = HttpVerb.GET, // GET = download/view
            Expires = DateTime.UtcNow.AddMinutes(expiryMinutes)
        };

        return await Task.FromResult(
            _s3Client.GetPreSignedURL(request));
    }

    public async Task<bool> DeleteFileAsync(string s3Key)
    {
        try
        {
            var request = new DeleteObjectRequest
            {
                BucketName = _settings.BucketName,
                Key = s3Key
            };

            await _s3Client.DeleteObjectAsync(request);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to delete S3 file {S3Key}", s3Key);
            return false;
        }
    }

    // Convert file extension to MIME type
    // S3 needs to know the content type when storing the file
    private static string GetContentType(string extension)
    {
        return extension.ToLower().TrimStart('.') switch
        {
            "jpg" or "jpeg" => "image/jpeg",
            "png"           => "image/png",
            "gif"           => "image/gif",
            "webp"          => "image/webp",
            "pdf"           => "application/pdf",
            _               => "application/octet-stream"
        };
    }
}