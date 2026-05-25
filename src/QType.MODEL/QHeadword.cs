using System.ComponentModel.DataAnnotations.Schema;

namespace QType.MODEL;

[Table("qheadword")]
public class QHeadword
{
    public int Id { get; set; }
    public int EntryId { get; set; }
    public string Text { get; set; }
    public string TextNormalized { get; set; }
    public byte VariantIndex { get; set; }
    public short CharCount { get; set; }
    public byte IsPhrase { get; set; }
    public byte IsDialectal { get; set; }
    public int AddTime { get; set; }
    public byte QStatus { get; set; }
}
