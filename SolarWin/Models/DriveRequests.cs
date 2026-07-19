using System.Text.Json.Serialization;

namespace SolarWin.Models;

/// <summary>POST /drive/folders (gateway for /folders).</summary>
public sealed class CreateFolderRequest
{
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("parent_id")]
    public string? ParentId { get; set; }
}

/// <summary>PATCH /drive/files/{id} rename body.</summary>
public sealed class RenameFileRequest
{
    [JsonPropertyName("name")]
    public required string Name { get; set; }
}

/// <summary>POST /drive/files/recycle/batch body.</summary>
public sealed class FileBatchIdsRequest
{
    [JsonPropertyName("file_ids")]
    public required List<string> FileIds { get; set; }
}
