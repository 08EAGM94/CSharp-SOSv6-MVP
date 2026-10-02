using HexArch.Application.Abstractions.SecondaryPorts;
using HexArch.Application.DTOs;
using HexArch.Data.Models;
using HexArch.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HexArch.Repository;

public class TypeRepository : IRepository<TypeEntity, TypeDTO>, ISelectRepository<TypeDTO>
{
    private const string EnabledVisibility = "ENABLED";

    private readonly Sosv6DbContext _dbContext;

    public TypeRepository(Sosv6DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsyncInfo(TypeEntity entity, ContactEntity? contact = null)
    {
        Tipo model = new Tipo
        {
            Tipo1 = entity.Type!,
            Visibilidad = EnabledVisibility
        };

        await _dbContext.Tipos.AddAsync(model);
        await _dbContext.SaveChangesAsync();
    }

    public async Task<TypeDTO> GetAsyncInfo(TypeEntity entity)
    {
        TypeDTO? dto = await _dbContext.Tipos
            .AsNoTracking()
            .Where(tipo => tipo.Id == entity.Id)
            .Select(tipo => new TypeDTO
            {
                Id = tipo.Id,
                Type = tipo.Tipo1,
                Visibility = tipo.Visibilidad
            })
            .FirstOrDefaultAsync();

        if (dto is null)
        {
            throw new KeyNotFoundException("No se encontró un tipo con la información solicitada.");
        }

        return dto;
    }

    public async Task<IEnumerable<TypeDTO>> GetAsyncAllInfo()
    {
        return await _dbContext.Tipos
            .AsNoTracking()
            .Select(tipo => new TypeDTO
            {
                Id = tipo.Id,
                Type = tipo.Tipo1,
                Visibility = tipo.Visibilidad
            })
            .ToListAsync();
    }

    public async Task UpdateAsyncInfo(TypeEntity entity)
    {
        int updatedRows = await _dbContext.Tipos
            .Where(tipo => tipo.Id == entity.Id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(tipo => tipo.Tipo1, entity.Type));

        if (updatedRows == 0)
        {
            throw new KeyNotFoundException("No se pudo actualizar la información del tipo porque no existe un tipo con ese identificador.");
        }
    }

    public async Task UpdateAsyncVisibility(TypeDTO dto)
    {
        int updatedRows = await _dbContext.Tipos
            .Where(tipo => tipo.Id == dto.Id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(tipo => tipo.Visibilidad, dto.Visibility));

        if (updatedRows == 0)
        {
            throw new KeyNotFoundException("No se pudo cambiar la visibilidad del tipo porque no existe un tipo con ese identificador.");
        }
    }

    public async Task<IEnumerable<TypeDTO>> GetAsyncInfoForSelects()
    {
        return await _dbContext.Tipos
            .AsNoTracking()
            .Where(tipo => tipo.Visibilidad == EnabledVisibility)
            .Select(tipo => new TypeDTO
            {
                Id = tipo.Id,
                Type = tipo.Tipo1,
                Visibility = tipo.Visibilidad
            })
            .ToListAsync();
    }
}
