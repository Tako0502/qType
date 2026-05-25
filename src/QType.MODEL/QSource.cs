using System.ComponentModel.DataAnnotations.Schema;

namespace QType.MODEL;

[Table("qsource")]
public class QSource
{
    public int Id { get; set; }
    public string Title { get; set; }
    public int AddTime { get; set; }
    public byte QStatus { get; set; }
}
