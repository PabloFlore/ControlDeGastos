using System;

namespace ControlDeGastos.Services;

public static class CipherKeyStore
{
    private static byte[]? _key;

    public static byte[]? Key => _key;

    public static void SetKey(byte[] key)
    {
        _key = key;
    }

    public static void ClearKey()
    {
        if (_key != null)
        {
            Array.Clear(_key, 0, _key.Length);
            _key = null;
        }
    }
}
