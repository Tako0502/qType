using System.ComponentModel.DataAnnotations.Schema;

namespace QType.MODEL;

[Table("qlemma")]
public class QLemma
{
    public int Id { get; set; }
    public string Text { get; set; }
    public string Pos { get; set; }
    public byte IsPhrase { get; set; }
    public byte IsDialectal { get; set; }
    public byte IsLoanword { get; set; }
    public string LatinTranslit { get; set; }
    public string Notes { get; set; }
    public int AddTime { get; set; }
    public int UpdateTime { get; set; }
    public byte QStatus { get; set; }
}
