using Microsoft.Extensions.Configuration;
using QType.COMMON;
using QType.Importer.Importers;

namespace QType.Importer;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        // appsettings.json is committed with placeholder values; appsettings.Local.json
        // (gitignored) overrides with real credentials on the developer's machine.
        var cfg = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Local.json", optional: true)
            .Build();

        var connectionString = cfg["Site:ConnectionString"];
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Console.Error.WriteLine("Site:ConnectionString missing in appsettings.json");
            return 1;
        }
        QSingleton.GetInstance().SetConnectionString(connectionString);

        var sourcesPath = ExpandPath(cfg["Files:Sources"]);
        var itemsPath = ExpandPath(cfg["Files:Items"]);
        var batchSize = int.TryParse(cfg["Import:BatchSize"], out var bs) ? bs : 500;
        var maxItems = int.TryParse(cfg["Import:MaxItems"], out var mi) ? mi : 0;

        var command = (args.Length > 0 ? args[0] : "all").ToLowerInvariant();
        try
        {
            switch (command)
            {
                case "sources":
                    await SourceImporter.RunAsync(sourcesPath);
                    break;
                case "entries":
                    await EntryImporter.RunAsync(itemsPath, batchSize, maxItems);
                    break;
                case "lemmas":
                    await LemmaExtractor.RunAsync();
                    break;
                case "pos":
                    await PosClassifier.RunAsync();
                    break;
                case "morph":
                    await MorphologyGenerator.RunAsync();
                    break;
                case "freq":
                    var freqPath = args.Length > 1 ? args[1] : ExpandPath("~/Downloads/kaz_wikipedia_2021_100K-words.txt");
                    var freqSrc = args.Length > 2 ? args[2] : "wikipedia-kk";
                    await FrequencyImporter.RunAsync(freqPath, freqSrc);
                    break;
                case "morph-test":
                    var samples = args.Length > 1
                        ? args[1..]
                        : new[] { "бала", "оқушы", "мектеп", "сөздік", "көше", "қала", "кітап", "ас" };
                    foreach (var s in samples)
                    {
                        Console.WriteLine($"\n[{s}] harmony={QType.COMMON.Morphology.KazakhPhonology.GetHarmony(s)} final={QType.COMMON.Morphology.KazakhPhonology.GetFinalClass(s)}");
                        foreach (var inf in QType.COMMON.Morphology.KazakhInflector.Inflect(s))
                            Console.WriteLine($"  {inf.Form,-20} {inf.Tag}");
                    }
                    break;
                case "verb-test":
                    var vsamples = args.Length > 1
                        ? args[1..]
                        : new[] { "жазу", "оқу", "келу", "бару", "көру", "білу", "айту" };
                    foreach (var s in vsamples)
                    {
                        Console.WriteLine($"\n[{s}]");
                        foreach (var inf in QType.COMMON.Morphology.KazakhVerb.Conjugate(s))
                            Console.WriteLine($"  {inf.Form,-25} {inf.Tag}");
                    }
                    break;
                case "lemma-test":
                    var probes = args.Length > 1
                        ? args[1..]
                        : new[] { "балам", "балалар", "балаға", "оқушылар", "мектепте", "сөздіктер", "кітабым" };
                    foreach (var p in probes)
                    {
                        Console.WriteLine($"\n[{p}]");
                        foreach (var c in QType.COMMON.Morphology.KazakhLemmatizer.Strip(p))
                            Console.WriteLine($"  → {c}");
                    }
                    break;
                case "all":
                    await SourceImporter.RunAsync(sourcesPath);
                    await EntryImporter.RunAsync(itemsPath, batchSize, maxItems);
                    await LemmaExtractor.RunAsync();
                    await MorphologyGenerator.RunAsync();
                    break;
                default:
                    Console.Error.WriteLine("Usage: QType.Importer [sources|entries|lemmas|morph|all]");
                    return 2;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ERROR: {ex}");
            return 1;
        }
        return 0;
    }

    private static string ExpandPath(string path)
    {
        if (string.IsNullOrEmpty(path)) return path;
        if (path.StartsWith("~/"))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, path[2..]);
        }
        return path;
    }
}
