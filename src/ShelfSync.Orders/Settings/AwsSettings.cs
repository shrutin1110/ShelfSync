namespace ShelfSync.Orders.Settings;

// Maps to the "AWS" section in appsettings.json
// and User Secrets
public class AwsSettings
{
    public string AccessKeyId { get; set; } = string.Empty;
    public string SecretAccessKey { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string BucketName { get; set; } = string.Empty;
}