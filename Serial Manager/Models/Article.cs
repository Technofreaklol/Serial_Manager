using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SerialManager.Models;

public class Article
{
    public int Id { get; set; }

    public string ArticleNumber { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public int CurrentSerialNumber { get; set; } = 0;

    public bool IsActive { get; set; } = true;

    public DateTime RowVersion { get; set; }

    [NotMapped]
    public string CustomerNumber =>
        ArticleNumber.Contains('-')
            ? ArticleNumber.Split('-')[0]
            : "Sonstige";


}

