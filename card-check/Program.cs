using System.IO.Compression;
using System.Text.Json;
using System.Xml.Linq;
using Mojp;

bool redownload = true;

var cts = new CancellationTokenSource();

// My definitions
var cards = new HashSet<string>(40000);
await using (var xmlStream = File.OpenRead("cards.xml"))
{
    var xml = await XDocument.LoadAsync(xmlStream, LoadOptions.None, cts.Token);
    foreach (var card in xml.Descendants("card"))
    {
        if ((string?)card.Attribute("name") is string name)
            cards.Add(name);
    }
}

// Download GoatBots' definitions
const string ZipFilename = "card-definitions.zip";
if (redownload || !File.Exists(ZipFilename))
{
    using (var local = File.Create(ZipFilename))
    {
        using var http = new HttpClient();
        using var response = await http.GetAsync("https://www.goatbots.com/download/prices/card-definitions.zip", cts.Token);
        await response.Content.CopyToAsync(local, cts.Token);
    }
}
await using var zip = await ZipFile.OpenReadAsync(ZipFilename, cts.Token);
var file = zip.GetEntry("card-definitions.txt");
if (file == null)
{
    Console.WriteLine("card-definitions.txt is not found in zip.");
    return;
}
await using var fileStream = await file.OpenAsync(cts.Token);
using var json = await JsonDocument.ParseAsync(fileStream, default, cts.Token);

// Enumerate cards
int nentry = 0;
int ncard = 0;
int passed = 0;

foreach (var def in json.RootElement.EnumerateObject())
{
    nentry++;
    var card = def.Value;
    string? rarity = card.GetProperty("rarity").GetString();

    if (rarity == "Booster")
        continue;

    string? name = card.GetProperty("name").GetString();
    name = Card.NormalizeName(name);

    if (name == null || name.StartsWith("Avatar - ", StringComparison.Ordinal))
        continue;

    ncard++;
    if (cards.Contains(name))
    {
        passed++;
        continue;
    }

    if (name.Contains('/'))
    {
        foreach (string splitName in name.Split('/'))
        {
            if (!cards.Contains(splitName))
            {
                Console.WriteLine(name);
                continue;
            }
        }
        passed++;
    }
    else
        Console.WriteLine(name);
}
Console.WriteLine($"Entry: {nentry}, Card: {passed} / {ncard}");
