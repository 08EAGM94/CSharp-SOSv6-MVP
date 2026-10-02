namespace HexArch.Application.Abstractions.PrimaryPorts;

public interface ISignatureService<TDTO>
{
    Task InsertSignature(TDTO dto);

    Task<TDTO> GetSignature(TDTO dto);
}