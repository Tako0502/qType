using System.ComponentModel.DataAnnotations.Schema;

namespace QType.MODEL;

[Table("qphrase")]
public class QPhrase
{
    public int Id { get; set; }
    public int HeadLemmaId { get; set; }
    public string Text { get; set; }
    public string Definition { get; set; }
    public int SourceId { get; set; }
    public int EntryId { get; set; }
    public int AddTime { get; set; }
}
