using System.ComponentModel.DataAnnotations.Schema;

namespace QType.MODEL;

[Table("qetymology")]
public class QEtymology
{
    public int Id { get; set; }
    public int LemmaId { get; set; }
    public string OriginLang { get; set; }
    public string OriginForm { get; set; }
    public string Transliteration { get; set; }
    public int EntryId { get; set; }
    public int AddTime { get; set; }
}
