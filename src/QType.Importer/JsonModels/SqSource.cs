using System.Text.Json.Serialization;

namespace QType.Importer.JsonModels;

public class SqSource
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; }
}
