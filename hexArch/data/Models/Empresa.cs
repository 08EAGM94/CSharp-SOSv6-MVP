using System;
using System.Collections.Generic;

namespace HexArch.Data.Models;

public partial class Empresa
{
    public int Id { get; set; }

    public string NombreComercial { get; set; } = null!;

    public string? RazonSocial { get; set; }

    public string? CalleNumero { get; set; }

    public string? EntreCalles { get; set; }

    public string? DirigirseCon { get; set; }

    public string Telefonos { get; set; } = null!;

    public string? Horario { get; set; }

    public string? Atencion { get; set; }

    public string? Colonia { get; set; }

    public string? Localidad { get; set; }

    public string? Email { get; set; }

    public string Visibilidad { get; set; } = null!;

    public virtual ICollection<Contacto> Contactos { get; set; } = new List<Contacto>();

    public virtual ICollection<Equipo> Equipos { get; set; } = new List<Equipo>();
}
