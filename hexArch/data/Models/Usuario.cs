using System;
using System.Collections.Generic;

namespace HexArch.Data.Models;

public partial class Usuario
{
    public int Id { get; set; }

    public string Nombre { get; set; } = null!;

    public string? Apellidos { get; set; }

    public string Alias { get; set; } = null!;

    public string Contrasena { get; set; } = null!;

    public string? Privilegio { get; set; }

    public string? Firma { get; set; }

    public string Visibilidad { get; set; } = null!;

    public virtual ICollection<Bitacora> Bitacoras { get; set; } = new List<Bitacora>();
}
