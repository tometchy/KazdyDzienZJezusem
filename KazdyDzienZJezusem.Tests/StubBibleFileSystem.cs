using KazdyDzienZJezusem.Services;

namespace KazdyDzienZJezusem.Tests;

public sealed class StubBibleFileSystem : IBibleFileSystem
{
    private const string CorinthiansBook = "TNP/1Kor";
    private const string CorinthiansChapter = $"{CorinthiansBook}/1.yml";

    private static readonly HashSet<string> TranslationDirectories =
    [
        "KJV",
        "TNP",
        "TR",
        "UBG"
    ];

    private static readonly int[] CorinthiansChapters = Enumerable.Range(1, 16).ToArray();
    private static readonly int[] GenesisChapters = [1];
    private static readonly int[] RevelationChapters = Enumerable.Range(1, 22).ToArray();

    private static readonly IReadOnlyDictionary<string, int[]> ChaptersByBook =
        new Dictionary<string, int[]>(StringComparer.Ordinal)
        {
            [CorinthiansBook] = CorinthiansChapters,
            ["UBG/Rdz"] = GenesisChapters,
            ["KJV/Rdz"] = GenesisChapters,
            ["UBG/Ob"] = RevelationChapters,
            ["TNP/Ob"] = RevelationChapters,
            ["TR/Ob"] = RevelationChapters,
            ["KJV/Ob"] = RevelationChapters
        };

    private static readonly IReadOnlyDictionary<string, string[]> LinesByChapter =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            [CorinthiansChapter] = ToLines(ChapterOne),
            ["UBG/Rdz/1.yml"] = ToLines(UbgGenesisChapterOne),
            ["KJV/Rdz/1.yml"] = ToLines(KjvGenesisChapterOne),
            ["UBG/Ob/15.yml"] = ToLines(UbgRevelationChapterFifteen),
            ["TNP/Ob/15.yml"] = ToLines(TnpRevelationChapterFifteen),
            ["TR/Ob/15.yml"] = ToLines(TrRevelationChapterFifteen),
            ["KJV/Ob/15.yml"] = ToLines(KjvRevelationChapterFifteen)
        };

    public bool DirectoryExists(string path)
    {
        var relativePath = GetBibleRelativePath(path);
        return relativePath.Length == 0
               || TranslationDirectories.Contains(relativePath)
               || ChaptersByBook.ContainsKey(relativePath);
    }

    public IEnumerable<string> EnumerateFiles(string path, string searchPattern)
    {
        var relativePath = GetBibleRelativePath(path);
        if (searchPattern != "*.yml" || !ChaptersByBook.TryGetValue(relativePath, out var chapters))
        {
            return [];
        }

        return chapters
            .Select(chapter => Path.Combine(path, $"{chapter}.yml"));
    }

    public bool FileExists(string path) =>
        LinesByChapter.ContainsKey(GetBibleRelativePath(path));

    public IEnumerable<string> ReadLines(string path)
    {
        if (!LinesByChapter.TryGetValue(GetBibleRelativePath(path), out var lines))
        {
            throw new FileNotFoundException("The requested chapter is not part of the test stub.", path);
        }

        return lines;
    }

    private static string[] ToLines(string chapter) =>
        chapter.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

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

    private const string UbgGenesisChapterOne = """
        "Rdz1,1": "Na początku Bóg stworzył niebo i ziemię."
        """;

    private const string KjvGenesisChapterOne = """
        "Rdz1,1": "In the beginning God created the heaven and the earth."
        """;

    private const string UbgRevelationChapterFifteen = """
        "Ob15,1": "Potem zobaczyłem inny znak na niebie, wielki i zadziwiający: siedmiu aniołów, którzy mieli siedem plag ostatecznych, bo przez nie dopełnił się gniew Boga."
        "Ob15,2": "I zobaczyłem jakby morze szklane zmieszane z ogniem i tych, którzy odnieśli zwycięstwo nad bestią, nad jej wizerunkiem, nad jej znamieniem i nad liczbą jej imienia, stojących nad szklanym morzem, mających harfy Boga."
        "Ob15,3": "I śpiewali pieśń Mojżesza, sługi Boga, i pieśń Baranka: Wielkie i zadziwiające są twoje dzieła, Panie Boże Wszechmogący. Sprawiedliwe i prawdziwe są twoje drogi, o Królu świętych;"
        "Ob15,4": "Któż by się nie bał ciebie, Panie, i nie uwielbił twego imienia? Bo ty jedynie jesteś święty, bo wszystkie narody przyjdą i oddadzą tobie pokłon, bo objawiły się twoje wyroki."
        "Ob15,5": "Potem zobaczyłem, a oto została otwarta świątynia Przybytku Świadectwa w niebie."
        "Ob15,6": "I wyszło ze świątyni siedmiu aniołów mających siedem plag, ubranych w czysty, lśniący len i przepasanych na piersi złotymi pasami."
        "Ob15,7": "A jedno z czterech stworzeń dało siedmiu aniołom siedem złotych czasz pełnych gniewu Boga, który żyje na wieki wieków."
        "Ob15,8": "I napełniła się świątynia dymem od chwały Boga i jego mocy. I nikt nie mógł wejść do świątyni, dopóki nie dopełniło się siedem plag siedmiu aniołów."
        """;

    private const string TnpRevelationChapterFifteen = """
        "Ob15,1": "I zobaczyłem inny znak na niebie, wielki i dziwny: siedmiu aniołów, którzy mają siedem ostatnich plag, gdyż po nich zakończy się gniew Boga."
        "Ob15,2": "I widziałem jakby szklane morze, zmieszane z ogniem, i zwyciężających z dzikim zwierzęciem i z jego obrazem, i z jego znamieniem, i z liczbą jego imienia, stojących na szklanym morzu, mających harfy Boga."
        "Ob15,3": "I śpiewają pieśń Mojżesza, sługi Boga, i pieśń Baranka, mówiąc: Wielkie i dziwne są Twoje dzieła, PANIE, Boże Wszechmogący! Sprawiedliwe i prawdziwe są Twoje drogi, o Królu świętych!"
        "Ob15,4": "Kto by się Ciebie nie bał, PANIE?! I nie oddał chwały Twojemu imieniu? Bo Ty jedynie jesteś święty, gdyż wszystkie narody przyjdą i oddadzą pokłon przed Tobą, bo zostały ujawnione Twoje sprawiedliwe dzieła."
        "Ob15,5": "I po tym zobaczyłem, a oto została otwarta świątynia namiotu świadectwa w niebie,"
        "Ob15,6": "I wyszło ze świątyni siedmiu aniołów, mających siedem plag, którzy są przyodziani w czysty i lśniący len oraz przepasani na piersiach złotymi pasami."
        "Ob15,7": "I jedno z czterech stworzeń dało siedmiu aniołom siedem złotych czasz, które są pełne gniewu Boga, żyjącego na wieki wieków."
        "Ob15,8": "I napełniona została świątynia dymem chwały Boga i od Jego mocy, i nikt nie mógł wejść do świątyni, póki nie skończy się siedem plag siedmiu aniołów."
        """;

    private const string TrRevelationChapterFifteen = """
        "Ob15,1": "Καὶ εἶδον ἄλλο σημεῖον ἐν τῷ οὐρανῷ μέγα καὶ θαυμαστόν, ἀγγέλους ἑπτὰ ἔχοντας πληγὰς ἑπτὰ τὰς ἐσχάτας, ὅτι ἐν αὐταῖς ἐτελέσθη ὁ θυμὸς τοῦ Θεοῦ."
        "Ob15,2": "Καὶ εἶδον ὡς θάλασσαν ὑαλίνην μεμιγμένην πυρί, καὶ τοὺς νικῶντας ἐκ τοῦ θηρίου καὶ ἐκ τῆς εἰκόνος αὐτοῦ καὶ ἐκ τοῦ χαράγματος αὐτοῦ, ἐκ τοῦ ἀριθμοῦ τοῦ ὀνόματος αὐτοῦ, ἑστῶτας ἐπὶ τὴν θάλασσαν τὴν ὑαλίνην, ἔχοντας κιθάρας τοῦ Θεοῦ."
        "Ob15,3": "καὶ ᾄδουσι τὴν ᾠδὴν Μωσέως τοῦ δούλου τοῦ Θεοῦ, καὶ τὴν ᾠδὴν τοῦ ἀρνίου, λέγοντες, Μεγάλα καὶ θαυμαστὰ τὰ ἔργα σου, Κύριε ὁ Θεὸς ὁ παντοκράτωρ· δίκαιαι καὶ ἀληθιναὶ αἱ ὁδοί σου, ὁ βασιλεὺς τῶν ἁγίων."
        "Ob15,4": "τίς οὐ μὴ φοβηθῇ σε, Κύριε, καὶ δοξάσῃ τὸ ὄνομά σου; ὅτι μόνος ὅσιος· ὅτι πάντα τὰ ἔθνη ἥξουσι καὶ προσκυνήσουσιν ἐνώπιόν σου, ὅτι τὰ δικαιώματά σου ἐφανερώθησαν."
        "Ob15,5": "Καὶ μετὰ ταῦτα εἶδον, καὶ ἰδού, ἠνοίγη ὁ ναὸς τῆς σκηνῆς τοῦ μαρτυρίου ἐν τῷ οὐρανῷ·"
        "Ob15,6": "καὶ ἐξῆλθον οἱ ἑπτὰ ἄγγελοι ἔχοντες τὰς ἑπτὰ πληγὰς ἐκ τοῦ ναοῦ, ἐνδεδυμένοι λίνον καθαρὸν καὶ λαμπρόν, καὶ περιεζωσμένοι περὶ τὰ στήθη ζώνας χρυσᾶς."
        "Ob15,7": "καὶ ἓν ἐκ τῶν τεσσάρων ζώων ἔδωκε τοῖς ἑπτὰ ἀγγέλοις ἑπτὰ φιάλας χρυσᾶς γεμούσας τοῦ θυμοῦ τοῦ Θεοῦ τοῦ ζῶντος εἰς τοὺς αἰῶνας τῶν αἰώνων."
        "Ob15,8": "καὶ ἐγεμίσθη ὁ ναὸς καπνοῦ ἐκ τῆς δόξης τοῦ Θεοῦ, καὶ ἐκ τῆς δυνάμεως αὐτοῦ· καὶ οὐδεὶς ἠδύνατο εἰσελθεῖν εἰς τὸν ναόν, ἄχρι τελεσθῶσιν αἱ ἑπτὰ πληγαὶ τῶν ἑπτὰ ἀγγέλων."
        """;

    private const string KjvRevelationChapterFifteen = """
        "Ob15,1": "And I saw another sign in heaven, great and marvellous, seven angels having the seven last plagues; for in them is filled up the wrath of God."
        "Ob15,2": "And I saw as it were a sea of glass mingled with fire: and them that had gotten the victory over the beast, and over his image, and over his mark, [and] over the number of his name, stand on the sea of glass, having the harps of God."
        "Ob15,3": "And they sing the song of Moses the servant of God, and the song of the Lamb, saying, Great and marvellous [are] thy works, Lord God Almighty; just and true [are] thy ways, thou King of saints."
        "Ob15,4": "Who shall not fear thee, O Lord, and glorify thy name? for [thou] only [art] holy: for all nations shall come and worship before thee; for thy judgments are made manifest."
        "Ob15,5": "And after that I looked, and, behold, the temple of the tabernacle of the testimony in heaven was opened:"
        "Ob15,6": "And the seven angels came out of the temple, having the seven plagues, clothed in pure and white linen, and having their breasts girded with golden girdles."
        "Ob15,7": "And one of the four beasts gave unto the seven angels seven golden vials full of the wrath of God, who liveth for ever and ever."
        "Ob15,8": "And the temple was filled with smoke from the glory of God, and from his power; and no man was able to enter into the temple, till the seven plagues of the seven angels were fulfilled."
        """;

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
