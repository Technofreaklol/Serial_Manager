using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SerialManager.Models;

public class SerialHistory
{
    public int Id { get; set; }

    public string ArticleNumber { get; set; } = string.Empty;

    public string SerialNumber { get; set; } = string.Empty;

    public DateTime Created { get; set; }

    public string Machine { get; set; } = string.Empty;

    public string Operator { get; set; } = string.Empty;

    public string? Remark { get; set; }

    public bool LabelPrinted { get; set; }
}
