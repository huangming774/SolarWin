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

/// <summary>POST /drive/files/recycle|restore|delete/batch body.</summary>
public sealed class FileBatchIdsRequest
{
    [JsonPropertyName("file_ids")]
    public required List<string> FileIds { get; set; }
}

/// <summary>POST /drive/files/move/batch body.</summary>
public sealed class MoveFilesRequest
{
    [JsonPropertyName("file_ids")]
    public required List<string> FileIds { get; set; }

    /// <summary>Null/omit = move to root.</summary>
    [JsonPropertyName("parent_id")]
    public string? ParentId { get; set; }

    [JsonPropertyName("indexed")]
    public bool? Indexed { get; set; }
}

/// <summary>POST /drive/files/upload/create body.</summary>
public sealed class CreateUploadTaskRequest
{
    [JsonPropertyName("hash")]
    public required string Hash { get; set; }

    [JsonPropertyName("file_name")]
    public required string FileName { get; set; }

    [JsonPropertyName("file_size")]
    public long FileSize { get; set; }

    [JsonPropertyName("content_type")]
    public required string ContentType { get; set; }

    [JsonPropertyName("chunk_size")]
    public long? ChunkSize { get; set; }

    [JsonPropertyName("parent_id")]
    public string? ParentId { get; set; }

    [JsonPropertyName("index")]
    public bool? Index { get; set; } = true;
}

/// <summary>
/// Response of upload/create: either an instant CloudFile (hash hit)
/// or a chunked task descriptor.
/// </summary>
public sealed class CreateUploadTaskResponse
{
    [JsonPropertyName("task_id")]
    public string? TaskId { get; set; }

    [JsonPropertyName("chunk_size")]
    public long ChunkSize { get; set; }

    [JsonPropertyName("chunks_count")]
    public int ChunksCount { get; set; }

    /// <summary>When set (and no task_id), server already has this object.</summary>
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    public bool IsExistingFile => !string.IsNullOrWhiteSpace(Id) && string.IsNullOrWhiteSpace(TaskId);
}
