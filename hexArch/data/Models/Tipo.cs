using System;
using System.Collections.Generic;

namespace HexArch.Data.Models;

public partial class Tipo
{
    public int Id { get; set; }

    public string Tipo1 { get; set; } = null!;

    public string Visibilidad { get; set; } = null!;

    public virtual ICollection<Equipo> Equipos { get; set; } = new List<Equipo>();
}
