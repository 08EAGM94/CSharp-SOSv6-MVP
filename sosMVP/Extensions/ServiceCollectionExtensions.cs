using HexArch.Application.Abstractions.Mapper;
using HexArch.Application.Abstractions.PrimaryPorts;
using HexArch.Application.Abstractions.SecondaryPorts;
using HexArch.Application.DTOs;
using HexArch.Application.UseCases;
using HexArch.Data.Mappers.DtoToEntity;
using HexArch.Domain.Entities;
using HexArch.Repository;
using Microsoft.Extensions.DependencyInjection;
using SosMVP.Security;

namespace SosMVP.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddUserModule(this IServiceCollection services)
    {
        services.AddScoped<UserRepository>();
        services.AddScoped<IRepository<UserEntity, UserDTO>>(
            provider => provider.GetRequiredService<UserRepository>());
        services.AddScoped<IUserRepository>(
            provider => provider.GetRequiredService<UserRepository>());

        services.AddScoped<IMapper<UserDTO, UserEntity>, UserDTOtoEntityMapper>();
        services.AddScoped<IMapper<ContactDTO, ContactEntity>, ContactDTOtoEntityMapper>();

        services.AddScoped<IUserService, UserService>();

        services.AddScoped<CommonService<UserEntity, UserDTO>>();
        services.AddScoped<ICommonService<UserDTO>>(
            provider => provider.GetRequiredService<CommonService<UserEntity, UserDTO>>());

        services.AddScoped<JwtTokenFactory>();

        return services;
    }
}
