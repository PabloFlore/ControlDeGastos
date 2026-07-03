using System.Security.Cryptography;
using System.Text;

var privateKeyPath = "private.key";
var cmdArgs = new List<string>();
for (int i = 0; i < args.Length; i++)
{
    if ((args[i] == "--key" || args[i] == "-k") && i + 1 < args.Length)
    {
        privateKeyPath = args[++i];
    }
    else
    {
        cmdArgs.Add(args[i]);
    }
}

if (!File.Exists(privateKeyPath))
{
    Console.Error.WriteLine($"Error: No se encuentra la clave privada en '{privateKeyPath}'");
    Console.Error.WriteLine("Usa --key <ruta> o genera un par con KeyGenerator");
    return 1;
}

var privateKeyPem = File.ReadAllText(privateKeyPath);
using var ecdsa = ECDsa.Create();
ecdsa.ImportFromPem(privateKeyPem);

if (cmdArgs.Count > 0)
{
    if (cmdArgs[0] == "trial")
    {
        var dias = cmdArgs.Count > 1 && int.TryParse(cmdArgs[1], out var d) ? d : 180;
        var plan = cmdArgs.Count > 2 ? cmdArgs[2] : "local";
        var game = cmdArgs.Count > 3 ? cmdArgs[3] : "gameoff";
        Console.WriteLine(GenerarToken(ecdsa, "TRIAL", dias, plan, game));
    }
    else if (cmdArgs[0] == "forever")
    {
        var plan = cmdArgs.Count > 1 ? cmdArgs[1] : "local";
        var game = cmdArgs.Count > 2 ? cmdArgs[2] : "gameoff";
        Console.WriteLine(GenerarToken(ecdsa, "FOREVER", 0, plan, game));
    }
    else
    {
        Console.Error.WriteLine("Uso: TokenGenerator <trial|forever> [dias] [local|nube] [gameon|gameoff]");
        Console.Error.WriteLine("Ej:   TokenGenerator trial 180 nube gameon");
        Console.Error.WriteLine("      TokenGenerator forever");
        return 1;
    }

    Console.Error.WriteLine($"Usando clave: {Path.GetFullPath(privateKeyPath)}");
    return 0;
}

Console.WriteLine("=== GENERADOR DE TOKENS v2 (ECDSA) - ControlDeGastos ===");
Console.WriteLine($"Clave privada: {Path.GetFullPath(privateKeyPath)}");
Console.WriteLine();

while (true)
{
    Console.WriteLine("Selecciona tipo de licencia:");
    Console.WriteLine("  1. Trial (180 d\u00edas)");
    Console.WriteLine("  2. Trial (personalizado)");
    Console.WriteLine("  3. Para siempre (vitalicio)");
    Console.WriteLine("  0. Salir");
    Console.Write("Opci\u00f3n: ");

    var opcion = Console.ReadLine()?.Trim();
    if (opcion == "0") break;

    Console.Write("Plan incluido (local/nube): ");
    var plan = Console.ReadLine()?.Trim().ToLower() == "nube" ? "nube" : "local";

    Console.Write("Modo gamificado incluido (s/n): ");
    var game = Console.ReadLine()?.Trim().ToLower() == "s" ? "gameon" : "gameoff";

    switch (opcion)
    {
        case "1":
            var t1 = GenerarToken(ecdsa, "TRIAL", 180, plan, game);
            Console.WriteLine($"\n\u2705 Token generado (180 d\u00edas, {plan}, {game}):");
            Console.WriteLine(t1);
            Console.WriteLine();
            break;

        case "2":
            Console.Write("D\u00edas de prueba: ");
            if (int.TryParse(Console.ReadLine(), out var dias) && dias > 0)
            {
                var t2 = GenerarToken(ecdsa, "TRIAL", dias, plan, game);
                Console.WriteLine($"\n\u2705 Token generado ({dias} d\u00edas, {plan}, {game}):");
                Console.WriteLine(t2);
            }
            else
            {
                Console.WriteLine("\u274c N\u00famero inv\u00e1lido");
            }
            Console.WriteLine();
            break;

        case "3":
            var t3 = GenerarToken(ecdsa, "FOREVER", 0, plan, game);
            Console.WriteLine($"\n\u2705 Token generado (para siempre, {plan}, {game}):");
            Console.WriteLine(t3);
            Console.WriteLine();
            break;

        default:
            Console.WriteLine("\u274c Opci\u00f3n inv\u00e1lida");
            break;
    }
}

return 0;

static string GenerarToken(ECDsa ecdsa, string tipo, int dias, string plan = "local", string game = "gameoff")
{
    var expiryTicks = tipo == "FOREVER"
        ? DateTime.UtcNow.Ticks.ToString()
        : DateTime.UtcNow.AddDays(dias).Ticks.ToString();

    var planStr = plan == "nube" ? "NUBE" : "LOCAL";
    var gameStr = game == "gameon" ? "GAMEON" : "GAMEOFF";
    var contenido = $"CDGv2|{tipo}|{expiryTicks}|{planStr}|{gameStr}";

    var data = Encoding.UTF8.GetBytes(contenido);
    var sig = ecdsa.SignData(data, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
    var sigB64 = Convert.ToBase64String(sig).Replace('+', '-').Replace('/', '_').Replace("=", "");

    return $"{contenido}|{sigB64}";
}
