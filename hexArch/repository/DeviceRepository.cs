using HexArch.Application.Abstractions.SecondaryPorts;
using HexArch.Application.DTOs;
using HexArch.Data.Models;
using HexArch.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HexArch.Repository;

public class DeviceRepository : IByEnterpriseRepository<DeviceEntity, DeviceDTO>
{
    private const string EnabledVisibility = "ENABLED";

    private readonly Sosv6DbContext _dbContext;

    public DeviceRepository(Sosv6DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsyncChild(DeviceEntity entity)
    {
        Equipo model = new Equipo
        {
            EmpresaId = entity.EnterpriseId!.Value,
            TipoId = entity.TypeId!.Value,
            Marca = entity.Brand!,
            Modelo = entity.Model!,
            NumeroSerie = entity.SerialNumber!,
            NumeroInventario = entity.InventoryNumber,
            Visibilidad = EnabledVisibility
        };

        await _dbContext.Equipos.AddAsync(model);
        await _dbContext.SaveChangesAsync();
    }

    public async Task<DeviceDTO> GetAsyncChild(DeviceEntity entity)
    {
        DeviceDTO? dto = await _dbContext.Equipos
            .AsNoTracking()
            .Where(equipo => equipo.Id == entity.Id)
            .Join(
                _dbContext.Tipos.AsNoTracking(),
                equipo => equipo.TipoId,
                tipo => tipo.Id,
                (equipo, tipo) => new DeviceDTO
                {
                    Id = equipo.Id,
                    EnterpriseId = equipo.EmpresaId,
                    TypeId = equipo.TipoId,
                    Brand = equipo.Marca,
                    Model = equipo.Modelo,
                    SerialNumber = equipo.NumeroSerie,
                    InventoryNumber = equipo.NumeroInventario,
                    Type = new TypeDTO
                    {
                        Type = tipo.Tipo1
                    }
                })
            .FirstOrDefaultAsync();

        if (dto is null)
        {
            throw new KeyNotFoundException("No se encontró un equipo con la información solicitada.");
        }

        return dto;
    }

    public async Task<IEnumerable<DeviceDTO>> GetAsyncChildrenByEnterForSelect(DeviceEntity entity)
    {
        return await _dbContext.Equipos
            .AsNoTracking()
            .Where(equipo => equipo.EmpresaId == entity.EnterpriseId && equipo.Visibilidad == EnabledVisibility)
            .Select(equipo => new DeviceDTO
            {
                Id = equipo.Id,
                Brand = equipo.Marca,
                SerialNumber = equipo.NumeroSerie
            })
            .ToListAsync();
    }

    public async Task<IEnumerable<DeviceDTO>> GetAsyncChildrenByEnterprise(DeviceEntity entity)
    {
        return await _dbContext.Equipos
            .AsNoTracking()
            .Where(equipo => equipo.EmpresaId == entity.EnterpriseId)
            .Join(
                _dbContext.Tipos.AsNoTracking(),
                equipo => equipo.TipoId,
                tipo => tipo.Id,
                (equipo, tipo) => new DeviceDTO
                {
                    Id = equipo.Id,
                    EnterpriseId = equipo.EmpresaId,
                    TypeId = equipo.TipoId,
                    Brand = equipo.Marca,
                    Model = equipo.Modelo,
                    SerialNumber = equipo.NumeroSerie,
                    InventoryNumber = equipo.NumeroInventario,
                    Visibility = equipo.Visibilidad,
                    Type = new TypeDTO
                    {
                        Type = tipo.Tipo1
                    }
                })
            .ToListAsync();
    }

    public async Task UpdateAsyncChild(DeviceEntity entity)
    {
        int updatedRows = await _dbContext.Equipos
            .Where(equipo => equipo.Id == entity.Id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(equipo => equipo.TipoId, entity.TypeId)
                .SetProperty(equipo => equipo.Marca, entity.Brand)
                .SetProperty(equipo => equipo.Modelo, entity.Model)
                .SetProperty(equipo => equipo.NumeroSerie, entity.SerialNumber)
                .SetProperty(equipo => equipo.NumeroInventario, entity.InventoryNumber));

        if (updatedRows == 0)
        {
            throw new KeyNotFoundException("No se pudo actualizar la información del equipo porque no existe un equipo con ese identificador.");
        }
    }

    public async Task UpdateAsyncVisibility(DeviceDTO dto)
    {
        int updatedRows = await _dbContext.Equipos
            .Where(equipo => equipo.Id == dto.Id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(equipo => equipo.Visibilidad, dto.Visibility));

        if (updatedRows == 0)
        {
            throw new KeyNotFoundException("No se pudo cambiar la visibilidad del equipo porque no existe un equipo con ese identificador.");
        }
    }
}
