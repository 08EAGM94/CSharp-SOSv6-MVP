using HexArch.Domain.Exceptions;

namespace HexArch.Domain.Entities;

public class BinnacleEntity
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

    public int? UserId
    {
        get => _userId;
        set
        {
            if (value is not null && value < 1)
            {
                throw new EntityException("El identificador del usuario debe ser un número mayor que cero.");
            }
            _userId = value;
        }
    }

    public int? ContactId
    {
        get => _contactId;
        set
        {
            if (value is not null && value < 1)
            {
                throw new EntityException("El identificador del contacto debe ser un número mayor que cero.");
            }
            _contactId = value;
        }
    }

    public string? Service
    {
        get => _service;
        set
        {
            if (value is not null && value.Length < 15)
            {
                throw new EntityException("El servicio debe tener al menos 15 caracteres.");
            }
            _service = value;
        }
    }

    public int? DeviceId
    {
        get => _deviceId;
        set
        {
            if (value is not null && value < 1)
            {
                throw new EntityException("El identificador del dispositivo debe ser un número mayor que cero.");
            }
            _deviceId = value;
        }
    }

    public float? Amount
    {
        get => _amount;
        set
        {
            if (value is not null && value < 1)
            {
                throw new EntityException("El monto debe ser un número mayor que cero.");
            }
            _amount = value;
        }
    }

    public string? ActivitiesDone
    {
        get => _activitiesDone;
        set
        {
            if (value is not null && value.Length < 15)
            {
                throw new EntityException("Las actividades realizadas deben tener al menos 15 caracteres.");
            }
            _activitiesDone = value;
        }
    }

    public string? Hints
    {
        get => _hints;
        set
        {
            if (value is not null && value.Length < 15)
            {
                throw new EntityException("Las observaciones deben tener al menos 15 caracteres.");
            }
            _hints = value;
        }
    }

    public string? CustomerSignature
    {
        get => _customerSignature;
        set
        {
            if (value is not null && value.Length > 255)
            {
                throw new EntityException("La firma del cliente debe tener un máximo de 255 caracteres.");
            }
            _customerSignature = value;
        }
    }

    public string? Status
    {
        get => _status;
        set
        {
            if (value is not null && value.Length > 20)
            {
                throw new EntityException("El estado debe tener un máximo de 20 caracteres.");
            }
            _status = value;
        }
    }

    public DateOnly? StartingDate
    {
        get => _startingDate;
        set =>
            _startingDate = value;
    }

    public DateOnly? EndDate
    {
        get => _endDate;
        set =>
            _endDate = value;
    }

    private int? _id;
    private int? _userId;
    private int? _contactId;
    private string? _service;
    private int? _deviceId;
    private float? _amount;
    private string? _activitiesDone;
    private string? _hints;
    private string? _customerSignature;
    private string? _status;
    private DateOnly? _startingDate;
    private DateOnly? _endDate;
}