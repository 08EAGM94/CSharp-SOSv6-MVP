using HexArch.Application.Abstractions.SecondaryPorts;
using HexArch.Application.DTOs;
using HexArch.Data.Helpers;
using HexArch.Data.Models;
using HexArch.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HexArch.Repository;

public class UserRepository : IRepository<UserEntity, UserDTO>, ISignatureRepository<UserEntity, UserDTO>, IUserRepository
{
    private const string EnabledVisibility = "ENABLED";

    private readonly Sosv6DbContext _dbContext;

    public UserRepository(Sosv6DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsyncInfo(UserEntity entity, ContactEntity? contact = null)
    {
        Usuario model = new Usuario
        {
            Nombre = entity.Name!,
            Apellidos = entity.Surname,
            Alias = entity.Nickname!,
            Contrasena = PasswordHasher.Hash(entity.Password!),
            Privilegio = entity.Role,
            Firma = entity.Signature,
            Visibilidad = EnabledVisibility
        };

        await _dbContext.Usuarios.AddAsync(model);
        await _dbContext.SaveChangesAsync();
    }

    public async Task<UserDTO> GetAsyncInfo(UserEntity entity)
    {
        UserDTO? dto = await _dbContext.Usuarios
            .AsNoTracking()
            .Where(usuario => usuario.Id == entity.Id)
            .Select(usuario => new UserDTO
            {
                Id = usuario.Id,
                Name = usuario.Nombre,
                Surname = usuario.Apellidos,
                Nickname = usuario.Alias,
                Role = usuario.Privilegio,
                Signature = usuario.Firma
            })
            .FirstOrDefaultAsync();

        if (dto is null)
        {
            throw new KeyNotFoundException("No se encontró un usuario con la información solicitada.");
        }

        return dto;
    }

    public async Task<IEnumerable<UserDTO>> GetAsyncAllInfo()
    {
        return await _dbContext.Usuarios
            .AsNoTracking()
            .Where(usuario => usuario.Visibilidad == EnabledVisibility)
            .Select(usuario => new UserDTO
            {
                Id = usuario.Id,
                Name = usuario.Nombre,
                Surname = usuario.Apellidos,
                Nickname = usuario.Alias
            })
            .ToListAsync();
    }

    public async Task UpdateAsyncInfo(UserEntity entity)
    {
        int updatedRows = await _dbContext.Usuarios
            .Where(usuario => usuario.Id == entity.Id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(usuario => usuario.Nombre, entity.Name)
                .SetProperty(usuario => usuario.Apellidos, entity.Surname)
                .SetProperty(usuario => usuario.Alias, entity.Nickname)
                .SetProperty(usuario => usuario.Contrasena, PasswordHasher.Hash(entity.Password!))
                .SetProperty(usuario => usuario.Privilegio, entity.Role));

        if (updatedRows == 0)
        {
            throw new KeyNotFoundException("No se pudo actualizar la información del usuario porque no existe un usuario con ese identificador.");
        }
    }

    public async Task UpdateAsyncVisibility(UserDTO dto)
    {
        int updatedRows = await _dbContext.Usuarios
            .Where(usuario => usuario.Id == dto.Id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(usuario => usuario.Visibilidad, dto.Visibility));

        if (updatedRows == 0)
        {
            throw new KeyNotFoundException("No se pudo cambiar la visibilidad del usuario porque no existe un usuario con ese identificador.");
        }
    }

    public async Task InsertSignature(UserEntity entity)
    {
        int updatedRows = await _dbContext.Usuarios
            .Where(usuario => usuario.Id == entity.Id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(usuario => usuario.Firma, entity.Signature));

        if (updatedRows == 0)
        {
            throw new KeyNotFoundException("No se pudo guardar la firma del usuario porque no existe un usuario con ese identificador.");
        }
    }

    public async Task<UserDTO> GetSignature(UserEntity entity)
    {
        UserDTO? dto = await _dbContext.Usuarios
            .AsNoTracking()
            .Where(usuario => usuario.Id == entity.Id)
            .Select(usuario => new UserDTO
            {
                Signature = usuario.Firma
            })
            .FirstOrDefaultAsync();

        if (dto is null)
        {
            throw new KeyNotFoundException("No se encontró un usuario con la información solicitada.");
        }

        return dto;
    }

    public async Task<UserDTO> Login(UserEntity entity)
    {
        UserDTO storedUser = await _dbContext.Usuarios
            .AsNoTracking()
            .Where(usuario => usuario.Alias == entity.Nickname && usuario.Visibilidad == EnabledVisibility)
            .Select(usuario => new UserDTO
            {
                Password = usuario.Contrasena
            })
            .FirstOrDefaultAsync()
            ?? throw new KeyNotFoundException("No se encontró un usuario con la información solicitada.");

        if (!PasswordHasher.Verify(entity.Password!, storedUser.Password!))
        {
            throw new Exception("La contraseña escrita no corresponde a la del usuario.");
        }

        return (await _dbContext.Usuarios
            .AsNoTracking()
            .Where(usuario => usuario.Alias == entity.Nickname)
            .Select(usuario => new UserDTO
            {
                Id = usuario.Id,
                Name = usuario.Nombre,
                Surname = usuario.Apellidos,
                Nickname = usuario.Alias,
                Role = usuario.Privilegio,
                Signature = usuario.Firma
            })
            .SingleOrDefaultAsync())!;
    }

    public bool AdminPwdConfirmation(UserEntity entity)
    {
        UserDTO? storedUser = _dbContext.Usuarios
            .AsNoTracking()
            .Where(usuario => usuario.Alias == entity.Nickname && usuario.Visibilidad == EnabledVisibility)
            .Select(usuario => new UserDTO
            {
                Password = usuario.Contrasena
            })
            .FirstOrDefault();

        if (storedUser is null)
        {
            throw new KeyNotFoundException("No se encontró un usuario con la información solicitada.");
        }

        return PasswordHasher.Verify(entity.Password!, storedUser.Password!);
    }
}
