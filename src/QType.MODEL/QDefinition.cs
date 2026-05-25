using System.ComponentModel.DataAnnotations.Schema;

namespace QType.MODEL;

[Table("qdefinition")]
public class QDefinition
{
    public int Id { get; set; }
    public int LemmaId { get; set; }
    public string Lang { get; set; }
    public byte SenseNumber { get; set; }
    public string Text { get; set; }
    public int SourceId { get; set; }
    public int EntryId { get; set; }
    public int AddTime { get; set; }
    public byte QStatus { get; set; }
}
