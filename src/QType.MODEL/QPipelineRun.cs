using System.ComponentModel.DataAnnotations.Schema;

namespace QType.MODEL;

[Table("qpipelinerun")]
public class QPipelineRun
{
    public int Id { get; set; }
    public string Stage { get; set; }
    public int StartedAt { get; set; }
    public int FinishedAt { get; set; }
    public uint RowsProcessed { get; set; }
    public uint ErrorCount { get; set; }
    public string Notes { get; set; }
    public byte QStatus { get; set; }
}
