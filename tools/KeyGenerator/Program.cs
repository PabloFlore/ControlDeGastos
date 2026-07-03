using System.Security.Cryptography;

var privateKeyPath = args.Length > 0 ? args[0] : "private.key";
var publicKeyPath = args.Length > 1 ? args[1] : "public.key";

using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);

var privateKeyPem = ecdsa.ExportECPrivateKeyPem();
await File.WriteAllTextAsync(privateKeyPath, privateKeyPem);
Console.WriteLine($"Clave privada guardada: {privateKeyPath}");

var publicKeyBytes = ecdsa.ExportSubjectPublicKeyInfo();
var publicKeyPem = ecdsa.ExportSubjectPublicKeyInfoPem();
await File.WriteAllTextAsync(publicKeyPath, publicKeyPem);
Console.WriteLine($"Clave pública guardada: {publicKeyPath}");

Console.WriteLine();
Console.WriteLine("=== CLAVE PÚBLICA (PEM) ===");
Console.WriteLine(publicKeyPem);
Console.WriteLine("===========================");
Console.WriteLine();
Console.WriteLine("=== C# constant (embed en LicenciaService) ===");
var escaped = publicKeyPem.Replace("\r\n", "\\n").Replace("\n", "\\n").Replace("\"", "\"\"");
Console.WriteLine($"private const string PublicKeyPem = \"{escaped}\";");
Console.WriteLine();
Console.WriteLine("=== C# bytes array (embed en API/Program.cs si prefieres) ===");
Console.WriteLine($"private static readonly byte[] PublicKeyBytes = new byte[] {{ {string.Join(", ", publicKeyBytes)} }};");
