using HexArch.Application.Abstractions.Mapper;
using HexArch.Application.Abstractions.PrimaryPorts;
using HexArch.Application.Abstractions.SecondaryPorts;

namespace HexArch.Application.UseCases;

public class SignatureService<TEntity, TDTO> : ISignatureService<TDTO>
{
    private readonly ISignatureRepository<TEntity, TDTO> _repository;
    private readonly IMapper<TDTO, TEntity> _toEntityMapper;

    public SignatureService(ISignatureRepository<TEntity, TDTO> repository, IMapper<TDTO, TEntity> toEntityMapper)
    {
        _repository = repository;
        _toEntityMapper = toEntityMapper;
    }

    public Task InsertSignature(TDTO dto)
    {
        return _repository.InsertSignature(_toEntityMapper.Map(dto));
    }

    public Task<TDTO> GetSignature(TDTO dto)
    {
        return _repository.GetSignature(_toEntityMapper.Map(dto));
    }
}