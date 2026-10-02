using System;
using System.Collections.Generic;

namespace HexArch.Data.Models;

public partial class Equipo
{
    public int Id { get; set; }

    public int EmpresaId { get; set; }

    public int TipoId { get; set; }

    public string Marca { get; set; } = null!;

    public string Modelo { get; set; } = null!;

    public string NumeroSerie { get; set; } = null!;

    public int? NumeroInventario { get; set; }

    public string Visibilidad { get; set; } = null!;

    public virtual ICollection<Bitacora> Bitacoras { get; set; } = new List<Bitacora>();

    public virtual Empresa Empresa { get; set; } = null!;

    public virtual Tipo Tipo { get; set; } = null!;
}
