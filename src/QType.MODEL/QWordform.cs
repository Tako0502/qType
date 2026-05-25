using System.ComponentModel.DataAnnotations.Schema;

namespace QType.MODEL;

[Table("qwordform")]
public class QWordform
{
    public int Id { get; set; }
    public int LemmaId { get; set; }
    public string Form { get; set; }
    public string Features { get; set; }
    public string Origin { get; set; }
    public int AddTime { get; set; }
    public byte QStatus { get; set; }
}
