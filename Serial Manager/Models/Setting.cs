using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SerialManager.Models;

public class Setting
{
    public int Id { get; set; }

    public string Key { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;
}
