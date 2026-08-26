using KazdyDzienZJezusem.Services;

namespace KazdyDzienZJezusem.Tests;

public sealed class StubBibleFileSystem : IBibleFileSystem
{
    private const string StubbedBook = "TNP/1Kor";
    private const string StubbedChapter = $"{StubbedBook}/1.yml";

    private static readonly HashSet<string> TranslationDirectories =
    [
        "KJV",
        "TNP",
        "TR",
        "UBG"
    ];

    private static readonly string[] ChapterOneLines =
        ChapterOne.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public bool DirectoryExists(string path)
    {
        var relativePath = GetBibleRelativePath(path);
        return relativePath.Length == 0
               || TranslationDirectories.Contains(relativePath)
               || relativePath == StubbedBook;
    }

    public IEnumerable<string> EnumerateFiles(string path, string searchPattern)
    {
        if (GetBibleRelativePath(path) != StubbedBook || searchPattern != "*.yml")
        {
            return [];
        }

        return Enumerable.Range(1, 16)
            .Select(chapter => Path.Combine(path, $"{chapter}.yml"));
    }

    public bool FileExists(string path) =>
        GetBibleRelativePath(path) == StubbedChapter;

    public IEnumerable<string> ReadLines(string path)
    {
        if (!FileExists(path))
        {
            throw new FileNotFoundException("The requested chapter is not part of the test stub.", path);
        }

        return ChapterOneLines;
    }

    private static string GetBibleRelativePath(string path)
    {
        var normalizedPath = path.Replace('\\', '/').TrimEnd('/');
        var bibleDirectoryIndex = normalizedPath.LastIndexOf("/Bible", StringComparison.Ordinal);

        if (bibleDirectoryIndex < 0)
        {
            return normalizedPath;
        }

        return normalizedPath[(bibleDirectoryIndex + "/Bible".Length)..]
            .TrimStart('/');
    }

    private const string ChapterOne = """
        "1Kor1,1": "Paweł, powołany apostoł Jezusa Chrystusa z woli Boga, i Sostenes, brat,"
        "1Kor1,2": "Zborowi Boga w Koryncie, do tych, którzy są uświęceni w Chrystusie Jezusie, powołanym świętym, ze wszystkimi, którzy wzywają imienia Pana naszego Jezusa Chrystusa na każdym miejscu – ich, a także i naszym."
        "1Kor1,3": "Łaska wam i pokój od Boga, Ojca naszego, i od Pana Jezusa Chrystusa."
        "1Kor1,4": "Dziękuję Bogu mojemu zawsze za was z powodu łaski Boga, która wam została dana w Chrystusie Jezusie,"
        "1Kor1,5": "Że we wszystkim zostaliście ubogaceni w Nim, we wszelkim słowie i we wszelkim poznaniu;"
        "1Kor1,6": "Tak, jak świadectwo Chrystusa zostało utwierdzone w was."
        "1Kor1,7": "Tak też nie brakuje wam żadnego daru łaski, wam, którzy oczekujecie objawienia Pana naszego Jezusa Chrystusa,"
        "1Kor1,8": "Który też utwierdzi was aż do końca, jako nienagannych w dniu Pana naszego Jezusa Chrystusa."
        "1Kor1,9": "Wierny jest Bóg, przez którego zostaliście powołani do społeczności Jego Syna Jezusa Chrystusa, Pana naszego."
        "1Kor1,10": "A wzywam was, bracia, przez imię Pana naszego Jezusa Chrystusa, abyście wszyscy mówili to samo, żeby nie było między wami rozłamów i abyście byli zespoleni jednym duchem i jedną myślą."
        "1Kor1,11": "Zostało mi bowiem oznajmione o was, moi bracia, od domowników Chloi, że wśród was są spory."
        "1Kor1,12": "A mówię o tym, bo każdy z was dobitnie mówi: Ja jestem Pawła, a ja Apollosa, a ja Kefasa, a ja Chrystusa."
        "1Kor1,13": "Rozdzielony jest Chrystus. Czy Paweł został za was przybity do krzyża lub w imię Pawła zostaliście ochrzczeni?"
        "1Kor1,14": "Dziękuję Bogu, że nie ochrzciłem nikogo z was, z wyjątkiem Kryspusa i Gajusa;"
        "1Kor1,15": "Aby ktoś nie powiedział, że chrzciłem w moje imię."
        "1Kor1,16": "Ochrzciłem też i dom Stefana; w końcu nie wiem, czy kogoś innego ochrzciłem."
        "1Kor1,17": "Bo Chrystus nie posłał mnie chrzcić, ale głosić Ewangelię, nie w mądrości słowa, by krzyż Chrystusa nie został pozbawiony swojego znaczenia."
        "1Kor1,18": "Bo Słowo o krzyżu dla tych, którzy istotnie giną, jest głupstwem; ale dla nas, którzy jesteśmy zbawiani, jest mocą Bożą."
        "1Kor1,19": "Napisano bowiem: Zniszczę mądrość mądrych, a zrozumienie rozumnych odrzucę."
        "1Kor1,20": "Gdzie jest mądry? Gdzie uczony w Piśmie? Gdzie badacz tego wieku? Czyż Bóg nie uczynił głupią mądrość tego świata?"
        "1Kor1,21": "Skoro bowiem w mądrości Boga, świat nie poznał Boga przez mądrość, upodobało się Bogu, przez głupie głoszenie zbawić wierzących."
        "1Kor1,22": "A gdy Żydzi domagają się cudu, a Grecy szukają mądrości,"
        "1Kor1,23": "My jednak głosimy Chrystusa, który jest ukrzyżowany, dla Żydów to wprawdzie zgorszenie, a dla Greków głupota,"
        "1Kor1,24": "Lecz dla samych powołanych, i Żydów, i Greków, głosimy Chrystusa – moc Boga i mądrość Boga."
        "1Kor1,25": "Gdyż to, co głupie u Boga mądrzejsze jest od ludzi, a to, co słabe u Boga mocniejsze jest od ludzi."
        "1Kor1,26": "Przypatrujcie się bowiem swojemu powołaniu, bracia, że niewielu jest mądrych według ciała, niewielu możnych, niewielu szlachetnie urodzonych;"
        "1Kor1,27": "Ale to, co głupie dla świata, wybrał Bóg, by zawstydzać mądrych; i słabych ze świata wybrał Bóg, aby zawstydzać mocnych;"
        "1Kor1,28": "I to, co jest niskiego rodu u świata, i to, co jest wzgardzone, wybrał Bóg, i co jest niczym, aby zniszczyć to, co jest czymś,"
        "1Kor1,29": "Aby przed Jego obliczem nie chlubiło się żadne ciało."
        "1Kor1,30": "Lecz przez Niego wy jesteście w Chrystusie Jezusie, który dla nas stał się mądrością od Boga, zarówno sprawiedliwością, i uświęceniem, i odkupieniem,"
        "1Kor1,31": "Aby tak, jak jest napisane: Kto się chlubi, niech się chlubi w PANU."
        """;
}
