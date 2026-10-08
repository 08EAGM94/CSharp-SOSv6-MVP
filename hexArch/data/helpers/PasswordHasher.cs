using System.Security.Cryptography;

namespace HexArch.Data.Helpers;
/*
PasswordHasher (clase estática) genera y valida hashes de contraseñas con PBKDF2:
Constantes (líneas 7-10): SaltSize=16 bytes de salt aleatorio, KeySize=32 bytes de 
derivación, Iterations=100_000 y algoritmo SHA256.
Hash(string password) (líneas 12-19):
1. Genera un salt aleatorio criptográfico (RandomNumberGenerator.GetBytes).
2. Deriva la clave con PBKDF2 usando password + salt + 100k iteraciones + SHA256.
3. Devuelve todo como un string "iteraciones.saltBase64.keyBase64". Al usar un salt 
distinto en cada llamada, dos contraseñas iguales producen hashes distintos.
Verify(string password, string hashedPassword) (líneas 21-50):
1. Divide el hash guardado en 3 partes; si el formato no es válido (número de partes ≠ 3, 
iteraciones no numéricas, Base64 inválido) devuelve false.
2. Reconstruye el salt y la key esperada desde Base64.
3. Recalcula la clave derivando la contraseña candidata con el mismo salt/iteraciones y la 
longitud esperada (expected.Length, claveándolo al tamaño original).
4. Compara con CryptographicOperations.FixedTimeEquals, una comparación en tiempo constante 
que evita ataques de timing attack.
5. Devuelve true solo si coinciden.
Notas: Verify tolera cambios futuros de Iterations porque lee el valor del propio string 
almacenado, y la longitud de clave también se infiere del hash en lugar de usar KeySize fijo.
*/
public static class PasswordHasher
{
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const int Iterations = 100_000;
    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA256;

    public static string Hash(string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] key = Rfc2898DeriveBytes.Pbkdf2(
            password, salt, Iterations, Algorithm, KeySize);

        return $"{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(key)}";
    }

    public static bool Verify(string password, string hashedPassword)
    {
        string[] parts = hashedPassword.Split('.');
        if (parts.Length != 3)
        {
            return false;
        }

        if (!int.TryParse(parts[0], out int iterations))
        {
            return false;
        }

        byte[] salt;
        byte[] expected;
        try
        {
            salt = Convert.FromBase64String(parts[1]);
            expected = Convert.FromBase64String(parts[2]);
        }
        catch (FormatException)
        {
            return false;
        }

        byte[] actual = Rfc2898DeriveBytes.Pbkdf2(
            password, salt, iterations, Algorithm, expected.Length);

        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}