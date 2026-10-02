using System;
using System.Collections.Generic;

namespace HexArch.Data.Models;

public partial class Bitacora
{
    public long Id { get; set; }

    public int UsuarioId { get; set; }

    public int ContactoId { get; set; }

    public string? Servicio { get; set; }

    public int? EquipoId { get; set; }

    public decimal? Monto { get; set; }

    public string? ActividadesRealizadas { get; set; }

    public string? Observaciones { get; set; }

    public DateOnly? Inicio { get; set; }

    public DateOnly? Fin { get; set; }

    public string? Estatus { get; set; }

    public string? FirmaCliente { get; set; }

    public string Visibilidad { get; set; } = null!;

    public virtual Contacto Contacto { get; set; } = null!;

    public virtual Equipo? Equipo { get; set; }

    public virtual Usuario Usuario { get; set; } = null!;
}
