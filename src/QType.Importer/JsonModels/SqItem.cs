using System.Text.Json.Serialization;

namespace QType.Importer.JsonModels;

public class SqItem
{
    [JsonPropertyName("sq_word")]
    public string SqWord { get; set; }

    [JsonPropertyName("sq_text")]
    public string SqText { get; set; }

    [JsonPropertyName("sq_sid")]
    public int SqSid { get; set; }

    [JsonPropertyName("sq_date")]
    public string SqDate { get; set; }
}
