using System.ComponentModel.DataAnnotations.Schema;

namespace QType.MODEL;

[Table("qexample")]
public class QExample
{
    public int Id { get; set; }
    public int LemmaId { get; set; }
    public string Sentence { get; set; }
    public string Citation { get; set; }
    public int SourceId { get; set; }
    public int EntryId { get; set; }
    public int AddTime { get; set; }
}
