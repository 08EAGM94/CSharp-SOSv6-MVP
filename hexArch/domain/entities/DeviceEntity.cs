using HexArch.Domain.Exceptions;

namespace HexArch.Domain.Entities;

public class DeviceEntity
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

    public int? TypeId
    {
        get => _typeId;
        set
        {
            if (value is not null && value < 1)
            {
                throw new EntityException("El identificador del tipo debe ser un número mayor que cero.");
            }
            _typeId = value;
        }
    }

    public string? Brand
    {
        get => _brand;
        set
        {
            if (value is not null && (value.Length < 2 || value.Length > 50))
            {
                throw new EntityException("La marca debe tener entre 2 y 50 caracteres.");
            }
            _brand = value;
        }
    }

    public string? Model
    {
        get => _model;
        set
        {
            if (value is not null && (value.Length < 5 || value.Length > 100))
            {
                throw new EntityException("El modelo debe tener entre 5 y 100 caracteres.");
            }
            _model = value;
        }
    }

    public string? SerialNumber
    {
        get => _serialNumber;
        set
        {
            if (value is not null && value.Length > 150)
            {
                throw new EntityException("El número de serie debe tener un máximo de 150 caracteres.");
            }
            _serialNumber = value;
        }
    }

    public int? InventoryNumber
    {
        get => _inventoryNumber;
        set
        {
            if (value is not null && value < 1)
            {
                throw new EntityException("El número de inventario debe ser un número mayor que cero.");
            }
            _inventoryNumber = value;
        }
    }

    private int? _id;
    private int? _enterpriseId;
    private int? _typeId;
    private string? _brand;
    private string? _model;
    private string? _serialNumber;
    private int? _inventoryNumber;
}