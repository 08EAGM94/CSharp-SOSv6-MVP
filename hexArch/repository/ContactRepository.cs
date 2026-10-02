using HexArch.Application.Abstractions.SecondaryPorts;
using HexArch.Application.DTOs;
using HexArch.Data.Models;
using HexArch.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HexArch.Repository;

public class ContactRepository : IByEnterpriseRepository<ContactEntity, ContactDTO>, ISelectRepository<ContactDTO>
{
    private const string EnabledVisibility = "ENABLED";

    private readonly Sosv6DbContext _dbContext;

    public ContactRepository(Sosv6DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsyncChild(ContactEntity entity)
    {
        Contacto model = new Contacto
        {
            EmpresaId = entity.EnterpriseId!.Value,
            NombreCompleto = entity.FullName!,
            Visibilidad = EnabledVisibility
        };

        await _dbContext.Contactos.AddAsync(model);
        await _dbContext.SaveChangesAsync();
    }

    public async Task<ContactDTO> GetAsyncChild(ContactEntity entity)
    {
        ContactDTO? dto = await _dbContext.Contactos
            .AsNoTracking()
            .Where(contacto => contacto.Id == entity.Id)
            .Join(
                _dbContext.Empresas.AsNoTracking(),
                contacto => contacto.EmpresaId,
                empresa => empresa.Id,
                (contacto, empresa) => new ContactDTO
                {
                    Id = contacto.Id,
                    EnterpriseId = contacto.EmpresaId,
                    FullName = contacto.NombreCompleto,
                    Enterprise = new EnterpriseDTO
                    {
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
                        Email = empresa.Email
                    }
                })
            .FirstOrDefaultAsync();

        if (dto is null)
        {
            throw new KeyNotFoundException("No se encontró un contacto con la información solicitada.");
        }

        return dto;
    }

    public async Task<IEnumerable<ContactDTO>> GetAsyncChildrenByEnterForSelect(ContactEntity entity)
    {
        return await _dbContext.Contactos
            .AsNoTracking()
            .Where(contacto => contacto.EmpresaId == entity.EnterpriseId && contacto.Visibilidad == EnabledVisibility)
            .Select(contacto => new ContactDTO
            {
                Id = contacto.Id,
                EnterpriseId = contacto.EmpresaId,
                FullName = contacto.NombreCompleto,
                Visibility = contacto.Visibilidad
            })
            .ToListAsync();
    }

    public async Task<IEnumerable<ContactDTO>> GetAsyncChildrenByEnterprise(ContactEntity entity)
    {
        return await _dbContext.Contactos
            .AsNoTracking()
            .Where(contacto => contacto.EmpresaId == entity.EnterpriseId)
            .Select(contacto => new ContactDTO
            {
                Id = contacto.Id,
                EnterpriseId = contacto.EmpresaId,
                FullName = contacto.NombreCompleto,
                Visibility = contacto.Visibilidad
            })
            .ToListAsync();
    }

    public async Task UpdateAsyncChild(ContactEntity entity)
    {
        int updatedRows = await _dbContext.Contactos
            .Where(contacto => contacto.Id == entity.Id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(contacto => contacto.NombreCompleto, entity.FullName));

        if (updatedRows == 0)
        {
            throw new KeyNotFoundException("No se pudo actualizar la información del contacto porque no existe un contacto con ese identificador.");
        }
    }

    public async Task UpdateAsyncVisibility(ContactDTO dto)
    {
        int updatedRows = await _dbContext.Contactos
            .Where(contacto => contacto.Id == dto.Id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(contacto => contacto.Visibilidad, dto.Visibility));

        if (updatedRows == 0)
        {
            throw new KeyNotFoundException("No se pudo cambiar la visibilidad del contacto porque no existe un contacto con ese identificador.");
        }
    }

    public async Task<IEnumerable<ContactDTO>> GetAsyncInfoForSelects()
    {
        return await _dbContext.Contactos
            .AsNoTracking()
            .Where(contacto => contacto.Visibilidad == EnabledVisibility)
            .Select(contacto => new ContactDTO
            {
                Id = contacto.Id,
                FullName = contacto.NombreCompleto
            })
            .ToListAsync();
    }
}
