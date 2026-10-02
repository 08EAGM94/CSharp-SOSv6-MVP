using HexArch.Domain.Exceptions;

namespace HexArch.Domain.Entities;

public class TypeEntity
{
    public int? Id
    {
        get => _id;
        set
        {
            if (value is not null && value < 1)
            {
                throw new EntityException("El identificador debe ser un número mayor que cero.");
            }
            _id = value;
        }
    }

    public string? Type
    {
        get => _type;
        set
        {
            if (value is not null && (value.Length < 5 || value.Length > 50))
            {
                throw new EntityException("El tipo debe tener entre 5 y 50 caracteres.");
            }
            _type = value;
        }
    }

    private int? _id;
    private string? _type;
}