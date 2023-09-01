namespace starpak;

internal class Program
{
    const string DATA_DIR = @"D:\SteamLibrary\steamapps\SteamLibrary\Starfield\Data";
    const string FILENAME = "Starfield - Interface.ba2";

    static void Main(string[] args)
    {
        using BA2File ba2 = new("Starfield - Interface.orig.ba2");

        // ba2.ExtractAll("Starfield - Interface");

        ba2.ReplaceFile("interface/fontconfig_en.txt", File.ReadAllBytes("interface/fontconfig_en.txt"));
        ba2.ReplaceFile("interface/fonts_en.gfx", File.ReadAllBytes("interface/fonts_en.gfx"));
        ba2.ReplaceFile("interface/translate_en.txt", File.ReadAllBytes("interface/translate_en.txt"));

        string filePath = Path.Combine(DATA_DIR, FILENAME);
        ba2.Write(filePath);

        Console.WriteLine("Done");
    }
}
