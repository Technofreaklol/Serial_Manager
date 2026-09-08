using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;

namespace SerialManager.Models;

public class Article
{
    public int Id { get; set; }

    public string ArticleNumber { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public int CurrentSerialNumber { get; set; } = 0;

    public bool IsActive { get; set; } = true;


}

