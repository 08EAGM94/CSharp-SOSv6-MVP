namespace HexArch.Application.Abstractions.SecondaryPorts;

public interface ISignatureRepository<TEntity, TDTO>
{
    Task InsertSignature(TEntity entity);

    Task<TDTO> GetSignature(TEntity entity);
}