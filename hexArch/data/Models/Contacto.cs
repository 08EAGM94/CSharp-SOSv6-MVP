using System;
using System.Collections.Generic;

namespace HexArch.Data.Models;

public partial class Contacto
{
    public int Id { get; set; }

    public int EmpresaId { get; set; }

    public string NombreCompleto { get; set; } = null!;

    public string Visibilidad { get; set; } = null!;

    public virtual ICollection<Bitacora> Bitacoras { get; set; } = new List<Bitacora>();

    public virtual Empresa Empresa { get; set; } = null!;
}
