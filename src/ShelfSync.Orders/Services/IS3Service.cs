namespace ShelfSync.Orders.Services;

public interface IS3Service
{
    // Generate a presigned URL so React can upload
    // directly to S3 without going through your server
    Task<PresignedUploadResult> GenerateProductImageUploadUrlAsync(
        Guid productId,
        Guid tenantId,
        string fileExtension);

    // Generate a URL so anyone can download/view a file
    // URL expires after the given minutes
    Task<string> GenerateDownloadUrlAsync(
        string s3Key,
        int expiryMinutes = 60);

    // Delete a file from S3
    Task<bool> DeleteFileAsync(string s3Key);
}

// What we return to React after generating the upload URL
// React needs UploadUrl to PUT the file
// Your DB needs S3Key to reference the file later
public record PresignedUploadResult(
    string UploadUrl,    // React PUTs the file to this URL
    string S3Key,        // store this in Products.S3ImageKey
    DateTime ExpiresAt); // URL expires at this time