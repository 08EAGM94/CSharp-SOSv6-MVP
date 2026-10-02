using HexArch.Domain.Exceptions;

namespace HexArch.Domain.Entities;

public class ContactEntity
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

    public int? EnterpriseId
    {
        get => _enterpriseId;
        set
        {
            if (value is not null && value < 1)
            {
                throw new EntityException("El identificador de la empresa debe ser un número mayor que cero.");
            }
            _enterpriseId = value;
        }
    }

    public string? FullName
    {
        get => _fullName;
        set
        {
            if (value is not null && (value.Length < 10 || value.Length > 150))
            {
                throw new EntityException("El nombre completo debe tener entre 10 y 150 caracteres.");
            }
            _fullName = value;
        }
    }

    private int? _id;
    private int? _enterpriseId;
    private string? _fullName;
}