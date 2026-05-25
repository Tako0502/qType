using System.ComponentModel.DataAnnotations.Schema;

namespace QType.MODEL;

[Table("qfrequency")]
public class QFrequency
{
    public string Form { get; set; }
    public uint Count { get; set; }
    public string Source { get; set; }
    public int UpdateTime { get; set; }
}
