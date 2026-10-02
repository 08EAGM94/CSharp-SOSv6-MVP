namespace HexArch.Domain.Exceptions;

public class EntityException : Exception
{
    public EntityException(string message) : base(message)
    {
    }
}