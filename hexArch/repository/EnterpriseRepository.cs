using HexArch.Application.Abstractions.SecondaryPorts;
using HexArch.Application.DTOs;
using HexArch.Data.Models;
using HexArch.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HexArch.Repository;

public class EnterpriseRepository : IRepository<EnterpriseEntity, EnterpriseDTO>, ISelectRepository<EnterpriseDTO>
{
    private const string EnabledVisibility = "ENABLED";

    private readonly Sosv6DbContext _dbContext;

    public EnterpriseRepository(Sosv6DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsyncInfo(EnterpriseEntity entity, ContactEntity? contact = null)
    {
        await using IDbContextTransaction transaction = await _dbContext.Database.BeginTransactionAsync();

        try
        {
            Empresa model = new Empresa
            {
                NombreComercial = entity.CommercialName!,
                RazonSocial = entity.TradeName,
                CalleNumero = entity.StreetNumber,
                EntreCalles = entity.BetweenStreets,
                DirigirseCon = entity.ContactingWith,
                Telefonos = entity.Phones!,
                Horario = entity.Schedule,
                Atencion = entity.Atention,
                Colonia = entity.Neighborhood,
                Localidad = entity.Location,
                Email = entity.Email,
                Visibilidad = EnabledVisibility
            };

            await _dbContext.Empresas.AddAsync(model);
            await _dbContext.SaveChangesAsync();

            if (contact is not null)
            {
                Contacto contactModel = new Contacto
                {
                    EmpresaId = model.Id,
                    NombreCompleto = contact.FullName!,
                    Visibilidad = EnabledVisibility
                };

                await _dbContext.Contactos.AddAsync(contactModel);
            }

            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (Exception exception)
        {
            await transaction.RollbackAsync();
            throw new Exception("No se pudo guardar la información de la empresa junto con su contacto, por lo que no se realizó ningún cambio.", exception);
        }
    }

    public async Task<EnterpriseDTO> GetAsyncInfo(EnterpriseEntity entity)
    {
        EnterpriseDTO? dto = await _dbContext.Empresas
            .AsNoTracking()
            .Where(empresa => empresa.Id == entity.Id)
            .Select(empresa => new EnterpriseDTO
            {
                Id = empresa.Id,
                CommercialName = empresa.NombreComercial,
                TradeName = empresa.RazonSocial,
                StreetNumber = empresa.CalleNumero,
                BetweenStreets = empresa.EntreCalles,
                ContactingWith = empresa.DirigirseCon,
                Phones = empresa.Telefonos,
                Schedule = empresa.Horario,
                Atention = empresa.Atencion,
                Neighborhood = empresa.Colonia,
                Location = empresa.Localidad,
                Email = empresa.Email,
                Visibility = empresa.Visibilidad
            })
            .FirstOrDefaultAsync();

        if (dto is null)
        {
            throw new KeyNotFoundException("No se encontró una empresa con la información solicitada.");
        }

        return dto;
    }

    public async Task<IEnumerable<EnterpriseDTO>> GetAsyncAllInfo()
    {
        return await _dbContext.Empresas
            .AsNoTracking()
            .Select(empresa => new EnterpriseDTO
            {
                Id = empresa.Id,
                CommercialName = empresa.NombreComercial,
                TradeName = empresa.RazonSocial
            })
            .ToListAsync();
    }

    public async Task UpdateAsyncInfo(EnterpriseEntity entity)
    {
        int updatedRows = await _dbContext.Empresas
            .Where(empresa => empresa.Id == entity.Id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(empresa => empresa.NombreComercial, entity.CommercialName)
                .SetProperty(empresa => empresa.RazonSocial, entity.TradeName)
                .SetProperty(empresa => empresa.CalleNumero, entity.StreetNumber)
                .SetProperty(empresa => empresa.EntreCalles, entity.BetweenStreets)
                .SetProperty(empresa => empresa.DirigirseCon, entity.ContactingWith)
                .SetProperty(empresa => empresa.Telefonos, entity.Phones)
                .SetProperty(empresa => empresa.Horario, entity.Schedule)
                .SetProperty(empresa => empresa.Atencion, entity.Atention)
                .SetProperty(empresa => empresa.Colonia, entity.Neighborhood)
                .SetProperty(empresa => empresa.Localidad, entity.Location)
                .SetProperty(empresa => empresa.Email, entity.Email));

        if (updatedRows == 0)
        {
            throw new KeyNotFoundException("No se pudo actualizar la información de la empresa porque no existe una empresa con ese identificador.");
        }
    }

    public async Task UpdateAsyncVisibility(EnterpriseDTO dto)
    {
        int updatedRows = await _dbContext.Empresas
            .Where(empresa => empresa.Id == dto.Id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(empresa => empresa.Visibilidad, dto.Visibility));

        if (updatedRows == 0)
        {
            throw new KeyNotFoundException("No se pudo cambiar la visibilidad de la empresa porque no existe una empresa con ese identificador.");
        }
    }

    public async Task<IEnumerable<EnterpriseDTO>> GetAsyncInfoForSelects()
    {
        return await _dbContext.Empresas
            .AsNoTracking()
            .Where(empresa => empresa.Visibilidad == EnabledVisibility)
            .Select(empresa => new EnterpriseDTO
            {
                Id = empresa.Id,
                CommercialName = empresa.NombreComercial,
                TradeName = empresa.RazonSocial
            })
            .ToListAsync();
    }
}
