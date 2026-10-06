using HexArch.Application.Abstractions.SecondaryPorts;
using HexArch.Application.DTOs;
using HexArch.Data.Models;
using HexArch.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HexArch.Repository;

public class BinnacleRepository : IBinnacleRepository
{
    private const string EnabledVisibility = "ENABLED";
    private const string InProgressStatus = "en proceso";
    private const string PendingConfirmStatus = "falta confirmar";
    private const string CancelledStatus = "cancelado";
    private const string FinishedStatus = "finalizado";

    private readonly Sosv6DbContext _dbContext;

    public BinnacleRepository(Sosv6DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsyncInfo(BinnacleEntity entity)
    {
        Bitacora model = new Bitacora
        {
            UsuarioId = entity.UserId!.Value,
            ContactoId = entity.ContactId!.Value,
            Servicio = entity.Service,
            EquipoId = entity.DeviceId,
            Monto = entity.Amount.HasValue ? (decimal)entity.Amount.Value : null,
            ActividadesRealizadas = entity.ActivitiesDone,
            Observaciones = entity.Hints,
            Inicio = DateOnly.FromDateTime(DateTime.Now),
            Fin = entity.EndDate,
            Estatus = InProgressStatus,
            FirmaCliente = entity.CustomerSignature,
            Visibilidad = EnabledVisibility
        };

        await _dbContext.Bitacoras.AddAsync(model);
        await _dbContext.SaveChangesAsync();
    }

    public async Task<BinnacleDTO> GetAsyncInfo(BinnacleEntity entity)
    {
        BinnacleDTO serviceInfo;

        if (entity.UserId is not null)
        {
            serviceInfo = await _dbContext.Bitacoras
                .AsNoTracking()
                .Where(bitacora => bitacora.UsuarioId == entity.UserId && bitacora.Id == entity.Id)
                .Select(bitacora => new BinnacleDTO
                {
                    Service = bitacora.Servicio
                })
                .FirstOrDefaultAsync()
                ?? throw new KeyNotFoundException("No se encontró una bitácora con la información solicitada.");
        }
        else
        {
            serviceInfo = await _dbContext.Bitacoras
                .AsNoTracking()
                .Where(bitacora => bitacora.Id == entity.Id)
                .Select(bitacora => new BinnacleDTO
                {
                    Service = bitacora.Servicio
                })
                .FirstOrDefaultAsync()
                ?? throw new KeyNotFoundException("No se encontró una bitácora con la información solicitada.");
        }

        BinnacleDTO binnacleInfo;

        if (serviceInfo.Service is not null)
        {
            if (entity.UserId is not null)
            {
                binnacleInfo = (await _dbContext.Bitacoras
                    .AsNoTracking()
                    .Join(_dbContext.Usuarios.AsNoTracking(), bitacora => bitacora.UsuarioId, usuario => usuario.Id, (bitacora, usuario) => new { bitacora, usuario })
                    .Join(_dbContext.Contactos.AsNoTracking(), source => source.bitacora.ContactoId, contacto => contacto.Id, (source, contacto) => new { source.bitacora, source.usuario, contacto })
                    .Join(_dbContext.Empresas.AsNoTracking(), source => source.contacto.EmpresaId, empresa => empresa.Id, (source, empresa) => new { source.bitacora, source.usuario, source.contacto, empresa })
                    .Where(source => source.bitacora.UsuarioId == entity.UserId && source.bitacora.Id == entity.Id)
                    .Select(source => new BinnacleDTO
                    {
                        Id = (int)source.bitacora.Id,
                        UserId = source.bitacora.UsuarioId,
                        Service = source.bitacora.Servicio,
                        Amount = source.bitacora.Monto.HasValue ? (float)source.bitacora.Monto.Value : null,
                        ActivitiesDone = source.bitacora.ActividadesRealizadas,
                        Hints = source.bitacora.Observaciones,
                        StartingDate = source.bitacora.Inicio,
                        Status = source.bitacora.Estatus,
                        CustomerSignature = source.bitacora.FirmaCliente,
                        User = new UserDTO
                        {
                            Name = source.usuario.Nombre,
                            Surname = source.usuario.Apellidos,
                            Signature = source.usuario.Firma
                        },
                        Contact = new ContactDTO
                        {
                            FullName = source.contacto.NombreCompleto,
                            Enterprise = new EnterpriseDTO
                            {
                                CommercialName = source.empresa.NombreComercial,
                                TradeName = source.empresa.RazonSocial,
                                StreetNumber = source.empresa.CalleNumero,
                                BetweenStreets = source.empresa.EntreCalles,
                                ContactingWith = source.empresa.DirigirseCon,
                                Phones = source.empresa.Telefonos,
                                Schedule = source.empresa.Horario,
                                Atention = source.empresa.Atencion,
                                Neighborhood = source.empresa.Colonia,
                                Location = source.empresa.Localidad,
                                Email = source.empresa.Email
                            }
                        }
                    })
                    .FirstOrDefaultAsync())!;
            }
            else
            {
                binnacleInfo = (await _dbContext.Bitacoras
                    .AsNoTracking()
                    .Join(_dbContext.Usuarios.AsNoTracking(), bitacora => bitacora.UsuarioId, usuario => usuario.Id, (bitacora, usuario) => new { bitacora, usuario })
                    .Join(_dbContext.Contactos.AsNoTracking(), source => source.bitacora.ContactoId, contacto => contacto.Id, (source, contacto) => new { source.bitacora, source.usuario, contacto })
                    .Join(_dbContext.Empresas.AsNoTracking(), source => source.contacto.EmpresaId, empresa => empresa.Id, (source, empresa) => new { source.bitacora, source.usuario, source.contacto, empresa })
                    .Where(source => source.bitacora.Id == entity.Id)
                    .Select(source => new BinnacleDTO
                    {
                        Id = (int)source.bitacora.Id,
                        UserId = source.bitacora.UsuarioId,
                        Service = source.bitacora.Servicio,
                        Amount = source.bitacora.Monto.HasValue ? (float)source.bitacora.Monto.Value : null,
                        ActivitiesDone = source.bitacora.ActividadesRealizadas,
                        Hints = source.bitacora.Observaciones,
                        StartingDate = source.bitacora.Inicio,
                        EndDate = source.bitacora.Fin,
                        Status = source.bitacora.Estatus,
                        CustomerSignature = source.bitacora.FirmaCliente,
                        User = new UserDTO
                        {
                            Name = source.usuario.Nombre,
                            Surname = source.usuario.Apellidos,
                            Signature = source.usuario.Firma
                        },
                        Contact = new ContactDTO
                        {
                            FullName = source.contacto.NombreCompleto,
                            Enterprise = new EnterpriseDTO
                            {
                                CommercialName = source.empresa.NombreComercial,
                                TradeName = source.empresa.RazonSocial,
                                StreetNumber = source.empresa.CalleNumero,
                                BetweenStreets = source.empresa.EntreCalles,
                                ContactingWith = source.empresa.DirigirseCon,
                                Phones = source.empresa.Telefonos,
                                Schedule = source.empresa.Horario,
                                Atention = source.empresa.Atencion,
                                Neighborhood = source.empresa.Colonia,
                                Location = source.empresa.Localidad,
                                Email = source.empresa.Email
                            }
                        }
                    })
                    .FirstOrDefaultAsync())!;
            }
        }
        else
        {
            if (entity.UserId is not null)
            {
                binnacleInfo = (await _dbContext.Bitacoras
                    .AsNoTracking()
                    .Join(_dbContext.Usuarios.AsNoTracking(), bitacora => bitacora.UsuarioId, usuario => usuario.Id, (bitacora, usuario) => new { bitacora, usuario })
                    .Join(_dbContext.Contactos.AsNoTracking(), source => source.bitacora.ContactoId, contacto => contacto.Id, (source, contacto) => new { source.bitacora, source.usuario, contacto })
                    .Join(_dbContext.Empresas.AsNoTracking(), source => source.contacto.EmpresaId, empresa => empresa.Id, (source, empresa) => new { source.bitacora, source.usuario, source.contacto, empresa })
                    .Join(_dbContext.Equipos.AsNoTracking(), source => source.bitacora.EquipoId, equipo => (int?)equipo.Id, (source, equipo) => new { source.bitacora, source.usuario, source.contacto, source.empresa, equipo })
                    .Join(_dbContext.Tipos.AsNoTracking(), source => source.equipo.TipoId, tipo => tipo.Id, (source, tipo) => new { source.bitacora, source.usuario, source.contacto, source.empresa, source.equipo, tipo })
                    .Where(source => source.bitacora.UsuarioId == entity.UserId && source.bitacora.Id == entity.Id)
                    .Select(source => new BinnacleDTO
                    {
                        Id = (int)source.bitacora.Id,
                        UserId = source.bitacora.UsuarioId,
                        Amount = source.bitacora.Monto.HasValue ? (float)source.bitacora.Monto.Value : null,
                        ActivitiesDone = source.bitacora.ActividadesRealizadas,
                        Hints = source.bitacora.Observaciones,
                        StartingDate = source.bitacora.Inicio,
                        Status = source.bitacora.Estatus,
                        CustomerSignature = source.bitacora.FirmaCliente,
                        User = new UserDTO
                        {
                            Name = source.usuario.Nombre,
                            Surname = source.usuario.Apellidos,
                            Signature = source.usuario.Firma
                        },
                        Contact = new ContactDTO
                        {
                            FullName = source.contacto.NombreCompleto,
                            Enterprise = new EnterpriseDTO
                            {
                                CommercialName = source.empresa.NombreComercial,
                                TradeName = source.empresa.RazonSocial,
                                StreetNumber = source.empresa.CalleNumero,
                                BetweenStreets = source.empresa.EntreCalles,
                                ContactingWith = source.empresa.DirigirseCon,
                                Phones = source.empresa.Telefonos,
                                Schedule = source.empresa.Horario,
                                Atention = source.empresa.Atencion,
                                Neighborhood = source.empresa.Colonia,
                                Location = source.empresa.Localidad,
                                Email = source.empresa.Email
                            }
                        },
                        Device = new DeviceDTO
                        {
                            Brand = source.equipo.Marca,
                            Model = source.equipo.Modelo,
                            SerialNumber = source.equipo.NumeroSerie,
                            InventoryNumber = source.equipo.NumeroInventario,
                            Visibility = source.equipo.Visibilidad,
                            Type = new TypeDTO
                            {
                                Type = source.tipo.Tipo1
                            }
                        }
                    })
                    .FirstOrDefaultAsync())!;
            }
            else
            {
                binnacleInfo = (await _dbContext.Bitacoras
                    .AsNoTracking()
                    .Join(_dbContext.Usuarios.AsNoTracking(), bitacora => bitacora.UsuarioId, usuario => usuario.Id, (bitacora, usuario) => new { bitacora, usuario })
                    .Join(_dbContext.Contactos.AsNoTracking(), source => source.bitacora.ContactoId, contacto => contacto.Id, (source, contacto) => new { source.bitacora, source.usuario, contacto })
                    .Join(_dbContext.Empresas.AsNoTracking(), source => source.contacto.EmpresaId, empresa => empresa.Id, (source, empresa) => new { source.bitacora, source.usuario, source.contacto, empresa })
                    .Join(_dbContext.Equipos.AsNoTracking(), source => source.bitacora.EquipoId, equipo => (int?)equipo.Id, (source, equipo) => new { source.bitacora, source.usuario, source.contacto, source.empresa, equipo })
                    .Join(_dbContext.Tipos.AsNoTracking(), source => source.equipo.TipoId, tipo => tipo.Id, (source, tipo) => new { source.bitacora, source.usuario, source.contacto, source.empresa, source.equipo, tipo })
                    .Where(source => source.bitacora.Id == entity.Id)
                    .Select(source => new BinnacleDTO
                    {
                        Id = (int)source.bitacora.Id,
                        UserId = source.bitacora.UsuarioId,
                        Amount = source.bitacora.Monto.HasValue ? (float)source.bitacora.Monto.Value : null,
                        ActivitiesDone = source.bitacora.ActividadesRealizadas,
                        Hints = source.bitacora.Observaciones,
                        StartingDate = source.bitacora.Inicio,
                        EndDate = source.bitacora.Fin,
                        Status = source.bitacora.Estatus,
                        CustomerSignature = source.bitacora.FirmaCliente,
                        User = new UserDTO
                        {
                            Name = source.usuario.Nombre,
                            Surname = source.usuario.Apellidos,
                            Signature = source.usuario.Firma
                        },
                        Contact = new ContactDTO
                        {
                            FullName = source.contacto.NombreCompleto,
                            Enterprise = new EnterpriseDTO
                            {
                                CommercialName = source.empresa.NombreComercial,
                                TradeName = source.empresa.RazonSocial,
                                StreetNumber = source.empresa.CalleNumero,
                                BetweenStreets = source.empresa.EntreCalles,
                                ContactingWith = source.empresa.DirigirseCon,
                                Phones = source.empresa.Telefonos,
                                Schedule = source.empresa.Horario,
                                Atention = source.empresa.Atencion,
                                Neighborhood = source.empresa.Colonia,
                                Location = source.empresa.Localidad,
                                Email = source.empresa.Email
                            }
                        },
                        Device = new DeviceDTO
                        {
                            Brand = source.equipo.Marca,
                            Model = source.equipo.Modelo,
                            SerialNumber = source.equipo.NumeroSerie,
                            InventoryNumber = source.equipo.NumeroInventario,
                            Visibility = source.equipo.Visibilidad,
                            Type = new TypeDTO
                            {
                                Type = source.tipo.Tipo1
                            }
                        }
                    })
                    .FirstOrDefaultAsync())!;
            }
        }

        return binnacleInfo;
    }

    public async Task<PaginationResult> GetAsyncAllInfo(BinnacleEntity? entity = null, int? page = null, int? elemsKey = null, Dictionary<string, string>? binnFilter = null, string? controllerAction = null)
    {
        PaginationResult paginationResult = new PaginationResult();

        if (controllerAction == "FollowupList")
        {
            int followupRegisters = await _dbContext.Bitacoras
                .AsNoTracking()
                .CountAsync(bitacora => bitacora.UsuarioId == entity!.UserId
                    && (bitacora.Estatus == InProgressStatus || bitacora.Estatus == PendingConfirmStatus));

            List<BinnacleDTO> followupElements = await _dbContext.Bitacoras
                .AsNoTracking()
                .Join(_dbContext.Contactos.AsNoTracking(), bitacora => bitacora.ContactoId, contacto => contacto.Id, (bitacora, contacto) => new { bitacora, contacto })
                .Join(_dbContext.Empresas.AsNoTracking(), source => source.contacto.EmpresaId, empresa => empresa.Id, (source, empresa) => new { source.bitacora, source.contacto, empresa })
                .Where(source => source.bitacora.UsuarioId == entity!.UserId
                    && (source.bitacora.Estatus == InProgressStatus || source.bitacora.Estatus == PendingConfirmStatus))
                .OrderBy(source => source.bitacora.Id)
                .Skip((page!.Value - 1) * elemsKey!.Value)
                .Take(elemsKey.Value)
                .Select(source => new BinnacleDTO
                {
                    Id = (int)source.bitacora.Id,
                    Status = source.bitacora.Estatus,
                    Contact = new ContactDTO
                    {
                        Enterprise = new EnterpriseDTO
                        {
                            CommercialName = source.empresa.NombreComercial
                        }
                    }
                })
                .ToListAsync();

            int followupPages = (int)Math.Ceiling((double)followupRegisters / elemsKey!.Value);

            paginationResult = new PaginationResult
            {
                Elements = followupElements,
                ActualPage = page!.Value,
                TotalPages = followupPages,
                TotalRegisters = followupRegisters
            };
        }

        if (controllerAction == "BinnaclesReport")
        {
            int contactIdFilter = int.TryParse(binnFilter!["ContactId"], out int parsedContactId) ? parsedContactId : 0;
            int deviceIdFilter = int.TryParse(binnFilter["DeviceId"], out int parsedDeviceId) ? parsedDeviceId : 0;
            DateOnly leftDay = DateOnly.TryParse(binnFilter["LeftDay"], out DateOnly parsedLeftDay) ? parsedLeftDay : default;
            DateOnly rightDay = DateOnly.TryParse(binnFilter["RightDay"], out DateOnly parsedRightDay) ? parsedRightDay : default;

            int reportRegisters = await _dbContext.Bitacoras
                .AsNoTracking()
                .CountAsync(bitacora => (binnFilter["ContactId"] == string.Empty
                        || bitacora.ContactoId == contactIdFilter)
                    && EF.Property<object>(bitacora, binnFilter["Activity"]) != null
                    && (binnFilter["DeviceId"] == string.Empty
                        || bitacora.EquipoId == deviceIdFilter)
                    && bitacora.Estatus == binnFilter["Status"]
                    && (binnFilter["LeftDay"] == string.Empty
                        || (EF.Property<DateOnly>(bitacora, binnFilter["startedOrEnded"]) >= leftDay
                            && EF.Property<DateOnly>(bitacora, binnFilter["startedOrEnded"]) <= rightDay))
                    && bitacora.Visibilidad == binnFilter["Visibility"]);

            List<BinnacleDTO> reportElements = await _dbContext.Bitacoras
                .AsNoTracking()
                .Join(_dbContext.Usuarios.AsNoTracking(), bitacora => bitacora.UsuarioId, usuario => usuario.Id, (bitacora, usuario) => new { bitacora, usuario })
                .Join(_dbContext.Contactos.AsNoTracking(), source => source.bitacora.ContactoId, contacto => contacto.Id, (source, contacto) => new { source.bitacora, source.usuario, contacto })
                .Join(_dbContext.Empresas.AsNoTracking(), source => source.contacto.EmpresaId, empresa => empresa.Id, (source, empresa) => new { source.bitacora, source.usuario, source.contacto, empresa })
                .Where(source => (binnFilter["ContactId"] == string.Empty
                        || source.bitacora.ContactoId == contactIdFilter)
                    && EF.Property<object>(source.bitacora, binnFilter["Activity"]) != null
                    && (binnFilter["DeviceId"] == string.Empty
                        || source.bitacora.EquipoId == deviceIdFilter)
                    && source.bitacora.Estatus == binnFilter["Status"]
                    && (binnFilter["LeftDay"] == string.Empty
                        || (EF.Property<DateOnly>(source.bitacora, binnFilter["startedOrEnded"]) >= leftDay
                            && EF.Property<DateOnly>(source.bitacora, binnFilter["startedOrEnded"]) <= rightDay))
                    && source.bitacora.Visibilidad == binnFilter["Visibility"])
                .OrderBy(source => source.bitacora.Id)
                .Skip((page!.Value - 1) * elemsKey!.Value)
                .Take(elemsKey.Value)
                .Select(source => new BinnacleDTO
                {
                    Id = (int)source.bitacora.Id,
                    Visibility = source.bitacora.Visibilidad,
                    User = new UserDTO
                    {
                        Name = source.usuario.Nombre,
                        Surname = source.usuario.Apellidos
                    },
                    Contact = new ContactDTO
                    {
                        FullName = source.contacto.NombreCompleto,
                        Enterprise = new EnterpriseDTO
                        {
                            CommercialName = source.empresa.NombreComercial
                        }
                    }
                })
                .ToListAsync();

            int reportPages = (int)Math.Ceiling((double)reportRegisters / elemsKey!.Value);

            paginationResult = new PaginationResult
            {
                Elements = reportElements,
                ActualPage = page!.Value,
                TotalPages = reportPages,
                TotalRegisters = reportRegisters
            };
        }

        return paginationResult;
    }

    public async Task UpdateAsyncInfo(BinnacleEntity entity)
    {
        decimal? amount = entity.Amount.HasValue ? (decimal)entity.Amount.Value : null;

        if (entity.Status == InProgressStatus)
        {
            int updatedRows = await _dbContext.Bitacoras
                .Where(bitacora => bitacora.Id == entity.Id)
                .ExecuteUpdateAsync(setters =>
                {
                    setters.SetProperty(bitacora => bitacora.UsuarioId, entity.UserId!.Value);
                    setters.SetProperty(bitacora => bitacora.Inicio, entity.StartingDate);

                    if (entity.Service is not null)
                    {
                        setters.SetProperty(bitacora => bitacora.Servicio, entity.Service);
                    }

                    if (entity.Amount is not null)
                    {
                        setters.SetProperty(bitacora => bitacora.Monto, amount);
                    }
                });

            if (updatedRows == 0)
            {
                throw new KeyNotFoundException("No se pudo actualizar la bitácora porque no existe una bitácora con ese identificador.");
            }
        }

        if (entity.Status == PendingConfirmStatus)
        {
            int updatedRows = await _dbContext.Bitacoras
                .Where(bitacora => bitacora.Id == entity.Id)
                .ExecuteUpdateAsync(setters =>
                {
                    setters.SetProperty(bitacora => bitacora.UsuarioId, entity.UserId!.Value);
                    setters.SetProperty(bitacora => bitacora.ActividadesRealizadas, entity.ActivitiesDone);
                    setters.SetProperty(bitacora => bitacora.Observaciones, entity.Hints);
                    setters.SetProperty(bitacora => bitacora.Inicio, entity.StartingDate);

                    if (entity.Service is not null)
                    {
                        setters.SetProperty(bitacora => bitacora.Servicio, entity.Service);
                    }

                    if (entity.Amount is not null)
                    {
                        setters.SetProperty(bitacora => bitacora.Monto, amount);
                    }
                });

            if (updatedRows == 0)
            {
                throw new KeyNotFoundException("No se pudo actualizar la bitácora porque no existe una bitácora con ese identificador.");
            }
        }

        if (entity.Status == CancelledStatus)
        {
            int updatedRows = await _dbContext.Bitacoras
                .Where(bitacora => bitacora.Id == entity.Id)
                .ExecuteUpdateAsync(setters =>
                {
                    setters.SetProperty(bitacora => bitacora.Observaciones, entity.Hints);
                    setters.SetProperty(bitacora => bitacora.Inicio, entity.StartingDate);

                    if (entity.Service is not null)
                    {
                        setters.SetProperty(bitacora => bitacora.Servicio, entity.Service);
                    }
                });

            if (updatedRows == 0)
            {
                throw new KeyNotFoundException("No se pudo actualizar la bitácora porque no existe una bitácora con ese identificador.");
            }
        }

        if (entity.Status == FinishedStatus)
        {
            int updatedRows = await _dbContext.Bitacoras
                .Where(bitacora => bitacora.Id == entity.Id)
                .ExecuteUpdateAsync(setters =>
                {
                    setters.SetProperty(bitacora => bitacora.ActividadesRealizadas, entity.ActivitiesDone);
                    setters.SetProperty(bitacora => bitacora.Observaciones, entity.Hints);
                    setters.SetProperty(bitacora => bitacora.Inicio, entity.StartingDate);
                    setters.SetProperty(bitacora => bitacora.Fin, entity.EndDate);

                    if (entity.Service is not null)
                    {
                        setters.SetProperty(bitacora => bitacora.Servicio, entity.Service);
                    }

                    if (entity.Amount is not null)
                    {
                        setters.SetProperty(bitacora => bitacora.Monto, amount);
                    }
                });

            if (updatedRows == 0)
            {
                throw new KeyNotFoundException("No se pudo actualizar la bitácora porque no existe una bitácora con ese identificador.");
            }
        }
    }

    public async Task UpdateAsyncVisibility(BinnacleDTO dto)
    {
        int updatedRows = await _dbContext.Bitacoras
            .Where(bitacora => bitacora.Id == dto.Id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(bitacora => bitacora.Visibilidad, dto.Visibility));

        if (updatedRows == 0)
        {
            throw new KeyNotFoundException("No se pudo cambiar la visibilidad de la bitácora porque no existe una bitácora con ese identificador.");
        }
    }

    public async Task FollowupPartialAsync(BinnacleEntity entity)
    {
        BinnacleDTO binnacleInfo = await _dbContext.Bitacoras
            .AsNoTracking()
            .Where(bitacora => bitacora.Id == entity.Id && bitacora.UsuarioId == entity.UserId)
            .Select(bitacora => new BinnacleDTO
            {
                Status = bitacora.Estatus
            })
            .FirstOrDefaultAsync()
            ?? throw new KeyNotFoundException("No se encontró una bitácora con la información solicitada.");

        if (binnacleInfo.Status != InProgressStatus)
        {
            throw new Exception("El acceso a esta bitácora está prohibido.");
        }

        int updatedRows = await _dbContext.Bitacoras
            .Where(bitacora => bitacora.Id == entity.Id && bitacora.UsuarioId == entity.UserId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(bitacora => bitacora.ActividadesRealizadas, entity.ActivitiesDone)
                .SetProperty(bitacora => bitacora.Observaciones, entity.Hints)
                .SetProperty(bitacora => bitacora.Inicio, entity.StartingDate)
                .SetProperty(bitacora => bitacora.Estatus, PendingConfirmStatus));

        if (updatedRows == 0)
        {
            throw new KeyNotFoundException("No se pudo registrar el seguimiento de la bitácora porque no existe una bitácora con ese identificador.");
        }
    }

    public async Task ResetActivitiesAsync(BinnacleEntity entity)
    {
        BinnacleDTO binnacleInfo = await _dbContext.Bitacoras
            .AsNoTracking()
            .Where(bitacora => bitacora.Id == entity.Id && bitacora.UsuarioId == entity.UserId)
            .Select(bitacora => new BinnacleDTO
            {
                Status = bitacora.Estatus
            })
            .FirstOrDefaultAsync()
            ?? throw new KeyNotFoundException("No se encontró una bitácora con la información solicitada.");

        if (binnacleInfo.Status == CancelledStatus || binnacleInfo.Status == FinishedStatus)
        {
            throw new Exception("El acceso a esta bitácora está prohibido porque ya fue cancelada o finalizada.");
        }

        int updatedRows = await _dbContext.Bitacoras
            .Where(bitacora => bitacora.Id == entity.Id && bitacora.UsuarioId == entity.UserId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(bitacora => bitacora.ActividadesRealizadas, (string?)null)
                .SetProperty(bitacora => bitacora.Observaciones, (string?)null)
                .SetProperty(bitacora => bitacora.Estatus, InProgressStatus));

        if (updatedRows == 0)
        {
            throw new KeyNotFoundException("No se pudieron reiniciar las actividades de la bitácora porque no existe una bitácora con ese identificador.");
        }
    }

    public async Task CancelBinnacleAsync(BinnacleEntity entity)
    {
        BinnacleDTO binnacleInfo = await _dbContext.Bitacoras
            .AsNoTracking()
            .Where(bitacora => bitacora.Id == entity.Id && bitacora.UsuarioId == entity.UserId)
            .Select(bitacora => new BinnacleDTO
            {
                Status = bitacora.Estatus
            })
            .FirstOrDefaultAsync()
            ?? throw new KeyNotFoundException("No se encontró una bitácora con la información solicitada.");

        if (binnacleInfo.Status == PendingConfirmStatus || binnacleInfo.Status == FinishedStatus)
        {
            throw new Exception("El acceso a esta bitácora está prohibido.");
        }

        int updatedRows = await _dbContext.Bitacoras
            .Where(bitacora => bitacora.Id == entity.Id && bitacora.UsuarioId == entity.UserId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(bitacora => bitacora.Observaciones, entity.Hints)
                .SetProperty(bitacora => bitacora.Estatus, CancelledStatus));

        if (updatedRows == 0)
        {
            throw new KeyNotFoundException("No se pudo cancelar la bitácora porque no existe una bitácora con ese identificador.");
        }
    }

    public async Task FinishBinnacleAsync(BinnacleEntity entity)
    {
        BinnacleDTO binnacleInfo = await _dbContext.Bitacoras
            .AsNoTracking()
            .Where(bitacora => bitacora.Id == entity.Id && bitacora.UsuarioId == entity.UserId)
            .Select(bitacora => new BinnacleDTO
            {
                Status = bitacora.Estatus
            })
            .FirstOrDefaultAsync()
            ?? throw new KeyNotFoundException("No se encontró una bitácora con la información solicitada.");

        if (binnacleInfo.Status != PendingConfirmStatus)
        {
            throw new Exception("El acceso a esta bitácora está prohibido.");
        }

        int updatedRows = await _dbContext.Bitacoras
            .Where(bitacora => bitacora.Id == entity.Id && bitacora.UsuarioId == entity.UserId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(bitacora => bitacora.Estatus, FinishedStatus)
                .SetProperty(bitacora => bitacora.Fin, DateOnly.FromDateTime(DateTime.Now))
                .SetProperty(bitacora => bitacora.FirmaCliente, entity.CustomerSignature));

        if (updatedRows == 0)
        {
            throw new KeyNotFoundException("No se pudo finalizar la bitácora porque no existe una bitácora con ese identificador.");
        }
    }
}
