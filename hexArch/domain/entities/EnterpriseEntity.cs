using HexArch.Domain.Exceptions;

namespace HexArch.Domain.Entities;

public class EnterpriseEntity
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

    public string? CommercialName
    {
        get => _commercialName;
        set
        {
            if (value is not null && value.Length > 150)
            {
                throw new EntityException("El nombre comercial debe tener un máximo de 150 caracteres.");
            }
            _commercialName = value;
        }
    }

    public string? TradeName
    {
        get => _tradeName;
        set
        {
            if (value is not null && value.Length > 150)
            {
                throw new EntityException("El nombre de fantasía debe tener un máximo de 150 caracteres.");
            }
            _tradeName = value;
        }
    }

    public string? StreetNumber
    {
        get => _streetNumber;
        set
        {
            if (value is not null && value.Length > 50)
            {
                throw new EntityException("El número de calle debe tener un máximo de 50 caracteres.");
            }
            _streetNumber = value;
        }
    }

    public string? BetweenStreets
    {
        get => _betweenStreets;
        set
        {
            if (value is not null && value.Length > 150)
            {
                throw new EntityException("Las calles entre las que se encuentra deben tener un máximo de 150 caracteres.");
            }
            _betweenStreets = value;
        }
    }

    public string? ContactingWith
    {
        get => _contactingWith;
        set
        {
            if (value is not null && value.Length > 150)
            {
                throw new EntityException("La referencia de contacto debe tener un máximo de 150 caracteres.");
            }
            _contactingWith = value;
        }
    }

    public string? Phones
    {
        get => _phones;
        set
        {
            if (value is not null && value.Length > 150)
            {
                throw new EntityException("Los teléfonos deben tener un máximo de 150 caracteres.");
            }
            _phones = value;
        }
    }

    public string? Schedule
    {
        get => _schedule;
        set
        {
            if (value is not null && value.Length > 150)
            {
                throw new EntityException("El horario debe tener un máximo de 150 caracteres.");
            }
            _schedule = value;
        }
    }

    public string? Atention
    {
        get => _atention;
        set
        {
            if (value is not null && value.Length > 150)
            {
                throw new EntityException("El horario de atención debe tener un máximo de 150 caracteres.");
            }
            _atention = value;
        }
    }

    public string? Neighborhood
    {
        get => _neighborhood;
        set
        {
            if (value is not null && value.Length > 50)
            {
                throw new EntityException("La colonia debe tener un máximo de 50 caracteres.");
            }
            _neighborhood = value;
        }
    }

    public string? Location
    {
        get => _location;
        set
        {
            if (value is not null && value.Length > 50)
            {
                throw new EntityException("La localidad debe tener un máximo de 50 caracteres.");
            }
            _location = value;
        }
    }

    public string? Email
    {
        get => _email;
        set
        {
            if (value is not null && value.Length > 150)
            {
                throw new EntityException("El correo electrónico debe tener un máximo de 150 caracteres.");
            }
            _email = value;
        }
    }

    private int? _id;
    private string? _commercialName;
    private string? _tradeName;
    private string? _streetNumber;
    private string? _betweenStreets;
    private string? _contactingWith;
    private string? _phones;
    private string? _schedule;
    private string? _atention;
    private string? _neighborhood;
    private string? _location;
    private string? _email;
}