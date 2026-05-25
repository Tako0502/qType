using System.ComponentModel.DataAnnotations.Schema;

namespace QType.MODEL;

[Table("qentry")]
public class QEntry
{
    public int Id { get; set; }
    public int RawIndex { get; set; }
    public string RawHeadword { get; set; }
    public string RawHtml { get; set; }
    public int SourceId { get; set; }
    public DateTime? EntryDate { get; set; }
    public int AddTime { get; set; }
    public byte QStatus { get; set; }
}
