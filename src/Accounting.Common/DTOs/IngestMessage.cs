using System.Text.Json;

namespace Accounting.Common.DTOs;

public class IngestMessage
{
    public string BatchId { get; set; } = default!;
    // Payload stored as raw JSON string when serialized; consumers should parse as needed.
    public JsonElement Payload { get; set; }
}

