using System.ComponentModel.DataAnnotations.Schema;

namespace QType.MODEL;

[Table("qlemmaentry")]
public class QLemmaEntry
{
    public int LemmaId { get; set; }
    public int EntryId { get; set; }
    public float Weight { get; set; }
    public int AddTime { get; set; }
}
