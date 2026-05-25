using System.ComponentModel.DataAnnotations.Schema;

namespace QType.MODEL;

[Table("qmisspelling")]
public class QMisspelling
{
    public int Id { get; set; }
    public string Typed { get; set; }
    public int LemmaId { get; set; }
    public float Weight { get; set; }
    public string Origin { get; set; }
    public int AddTime { get; set; }
}
