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

    public static IServiceCollection AddTypeModule(this IServiceCollection services)
    {
        services.AddScoped<TypeRepository>();
        services.AddScoped<IRepository<TypeEntity, TypeDTO>>(
            provider => provider.GetRequiredService<TypeRepository>());
        services.AddScoped<ISelectRepository<TypeDTO>>(
            provider => provider.GetRequiredService<TypeRepository>());

        services.AddScoped<IMapper<TypeDTO, TypeEntity>, TypeDTOtoEntityMapper>();

        services.AddScoped<CommonService<TypeEntity, TypeDTO>>();
        services.AddScoped<ICommonService<TypeDTO>>(
            provider => provider.GetRequiredService<CommonService<TypeEntity, TypeDTO>>());

        services.AddScoped<SelectService<TypeDTO>>();
        services.AddScoped<ISelectService<TypeDTO>>(
            provider => provider.GetRequiredService<SelectService<TypeDTO>>());

        return services;
    }

    public static IServiceCollection AddEnterpriseModule(this IServiceCollection services)
    {
        services.AddScoped<EnterpriseRepository>();
        services.AddScoped<IRepository<EnterpriseEntity, EnterpriseDTO>>(
            provider => provider.GetRequiredService<EnterpriseRepository>());
        services.AddScoped<ISelectRepository<EnterpriseDTO>>(
            provider => provider.GetRequiredService<EnterpriseRepository>());

        services.AddScoped<IMapper<EnterpriseDTO, EnterpriseEntity>, EnterpriseDTOtoEntityMapper>();

        services.AddScoped<CommonService<EnterpriseEntity, EnterpriseDTO>>();
        services.AddScoped<ICommonService<EnterpriseDTO>>(
            provider => provider.GetRequiredService<CommonService<EnterpriseEntity, EnterpriseDTO>>());

        services.AddScoped<SelectService<EnterpriseDTO>>();
        services.AddScoped<ISelectService<EnterpriseDTO>>(
            provider => provider.GetRequiredService<SelectService<EnterpriseDTO>>());

        return services;
    }
}
