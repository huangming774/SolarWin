using System.Text.Json;
using System.Text.Json.Serialization;

namespace SolarWin.Models;

/// <summary>
/// Cloud file / file reference used across Drive and embedded media.
/// Core wire fields match OpenAPI SnCloudFileReferenceObject plus Drive extras.
/// </summary>
public sealed class SnCloudFile
{
    [JsonPropertyName("created_at")]
    public DateTimeOffset? CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTimeOffset? UpdatedAt { get; set; }

    [JsonPropertyName("deleted_at")]
    public DateTimeOffset? DeletedAt { get; set; }

    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("mime_type")]
    public string? MimeType { get; set; }

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("hash")]
    public string? Hash { get; set; }

    [JsonPropertyName("file_meta")]
    public Dictionary<string, JsonElement>? FileMeta { get; set; }

    [JsonPropertyName("user_meta")]
    public Dictionary<string, JsonElement>? UserMeta { get; set; }

    [JsonPropertyName("sensitive_marks")]
    public List<string>? SensitiveMarks { get; set; }

    [JsonPropertyName("has_compression")]
    public bool HasCompression { get; set; }

    [JsonPropertyName("width")]
    public int? Width { get; set; }

    [JsonPropertyName("height")]
    public int? Height { get; set; }

    [JsonPropertyName("blurhash")]
    public string? Blurhash { get; set; }

    [JsonPropertyName("usage")]
    public string? Usage { get; set; }

    [JsonPropertyName("application_type")]
    public string? ApplicationType { get; set; }

    [JsonPropertyName("parent_id")]
    public string? ParentId { get; set; }

    [JsonPropertyName("pool_id")]
    public string? PoolId { get; set; }

    [JsonPropertyName("is_folder")]
    public bool IsFolder { get; set; }

    [JsonPropertyName("indexed")]
    public bool Indexed { get; set; }

    [JsonPropertyName("recycled")]
    public bool Recycled { get; set; }

    [JsonPropertyName("account_id")]
    public Guid? AccountId { get; set; }
}
