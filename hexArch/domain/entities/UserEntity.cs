using HexArch.Domain.Exceptions;

namespace HexArch.Domain.Entities;

public class UserEntity
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

    public string? Name
    {
        get => _name;
        set
        {
            if (value is not null && (value.Length < 10 || value.Length > 100))
            {
                throw new EntityException("El nombre debe tener entre 10 y 100 caracteres.");
            }
            _name = value;
        }
    }

    public string? Surname
    {
        get => _surname;
        set
        {
            if (value is not null && (value.Length < 15 || value.Length > 150))
            {
                throw new EntityException("El apellido debe tener entre 15 y 150 caracteres.");
            }
            _surname = value;
        }
    }

    public string? Nickname
    {
        get => _nickname;
        set
        {
            if (value is not null && (value.Length < 10 || value.Length > 100))
            {
                throw new EntityException("El apodo debe tener entre 10 y 100 caracteres.");
            }
            _nickname = value;
        }
    }

    public string? Password
    {
        get => _password;
        set
        {
            if (value is not null && (value.Length < 12 || value.Length > 255))
            {
                throw new EntityException("La contraseña debe tener entre 12 y 255 caracteres.");
            }
            _password = value;
        }
    }

    public string? Role
    {
        get => _role;
        set
        {
            if (value is not null && value.Length > 10)
            {
                throw new EntityException("El rol debe tener un máximo de 10 caracteres.");
            }
            _role = value;
        }
    }

    public string? Signature
    {
        get => _signature;
        set
        {
            if (value is not null && value.Length > 255)
            {
                throw new EntityException("La firma debe tener un máximo de 255 caracteres.");
            }
            _signature = value;
        }
    }

    private int? _id;
    private string? _name;
    private string? _surname;
    private string? _nickname;
    private string? _password;
    private string? _role;
    private string? _signature;
}