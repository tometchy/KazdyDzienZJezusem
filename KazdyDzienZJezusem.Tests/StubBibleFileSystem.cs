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
        chapter.Split('\n', StringSplitOptions.RemoveEmptyEntries);

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
        "Rdz1,1":
          text: "Na początku Bóg stworzył niebo i ziemię."
          tags: []
        """;

    private const string KjvGenesisChapterOne = """
        "Rdz1,1":
          text: "In the beginning God created the heaven and the earth."
          tags: []
        """;

    private const string UbgRevelationChapterFifteen = """
        "Ob15,1":
          text: "Potem zobaczyłem inny znak na niebie, wielki i zadziwiający: siedmiu aniołów, którzy mieli siedem plag ostatecznych, bo przez nie dopełnił się gniew Boga."
          tags: []
        "Ob15,2":
          text: "I zobaczyłem jakby morze szklane zmieszane z ogniem i tych, którzy odnieśli zwycięstwo nad bestią, nad jej wizerunkiem, nad jej znamieniem i nad liczbą jej imienia, stojących nad szklanym morzem, mających harfy Boga."
          tags: []
        "Ob15,3":
          text: "I śpiewali pieśń Mojżesza, sługi Boga, i pieśń Baranka: Wielkie i zadziwiające są twoje dzieła, Panie Boże Wszechmogący. Sprawiedliwe i prawdziwe są twoje drogi, o Królu świętych;"
          tags: []
        "Ob15,4":
          text: "Któż by się nie bał ciebie, Panie, i nie uwielbił twego imienia? Bo ty jedynie jesteś święty, bo wszystkie narody przyjdą i oddadzą tobie pokłon, bo objawiły się twoje wyroki."
          tags: []
        "Ob15,5":
          text: "Potem zobaczyłem, a oto została otwarta świątynia Przybytku Świadectwa w niebie."
          tags: []
        "Ob15,6":
          text: "I wyszło ze świątyni siedmiu aniołów mających siedem plag, ubranych w czysty, lśniący len i przepasanych na piersi złotymi pasami."
          tags: []
        "Ob15,7":
          text: "A jedno z czterech stworzeń dało siedmiu aniołom siedem złotych czasz pełnych gniewu Boga, który żyje na wieki wieków."
          tags: []
        "Ob15,8":
          text: "I napełniła się świątynia dymem od chwały Boga i jego mocy. I nikt nie mógł wejść do świątyni, dopóki nie dopełniło się siedem plag siedmiu aniołów."
          tags: []
        """;

    private const string TnpRevelationChapterFifteen = """
        "Ob15,1":
          text: "I zobaczyłem inny znak na niebie, wielki i dziwny: siedmiu aniołów, którzy mają siedem ostatnich plag, gdyż po nich zakończy się gniew Boga."
          tags: []
        "Ob15,2":
          text: "I widziałem jakby szklane morze, zmieszane z ogniem, i zwyciężających z dzikim zwierzęciem i z jego obrazem, i z jego znamieniem, i z liczbą jego imienia, stojących na szklanym morzu, mających harfy Boga."
          tags: []
        "Ob15,3":
          text: "I śpiewają pieśń Mojżesza, sługi Boga, i pieśń Baranka, mówiąc: Wielkie i dziwne są Twoje dzieła, PANIE, Boże Wszechmogący! Sprawiedliwe i prawdziwe są Twoje drogi, o Królu świętych!"
          tags: []
        "Ob15,4":
          text: "Kto by się Ciebie nie bał, PANIE?! I nie oddał chwały Twojemu imieniu? Bo Ty jedynie jesteś święty, gdyż wszystkie narody przyjdą i oddadzą pokłon przed Tobą, bo zostały ujawnione Twoje sprawiedliwe dzieła."
          tags: []
        "Ob15,5":
          text: "I po tym zobaczyłem, a oto została otwarta świątynia namiotu świadectwa w niebie,"
          tags: []
        "Ob15,6":
          text: "I wyszło ze świątyni siedmiu aniołów, mających siedem plag, którzy są przyodziani w czysty i lśniący len oraz przepasani na piersiach złotymi pasami."
          tags: []
        "Ob15,7":
          text: "I jedno z czterech stworzeń dało siedmiu aniołom siedem złotych czasz, które są pełne gniewu Boga, żyjącego na wieki wieków."
          tags: []
        "Ob15,8":
          text: "I napełniona została świątynia dymem chwały Boga i od Jego mocy, i nikt nie mógł wejść do świątyni, póki nie skończy się siedem plag siedmiu aniołów."
          tags: []
        """;

    private const string TrRevelationChapterFifteen = """
        "Ob15,1":
          text: "Καὶ εἶδον ἄλλο σημεῖον ἐν τῷ οὐρανῷ μέγα καὶ θαυμαστόν, ἀγγέλους ἑπτὰ ἔχοντας πληγὰς ἑπτὰ τὰς ἐσχάτας, ὅτι ἐν αὐταῖς ἐτελέσθη ὁ θυμὸς τοῦ Θεοῦ."
          tags: []
        "Ob15,2":
          text: "Καὶ εἶδον ὡς θάλασσαν ὑαλίνην μεμιγμένην πυρί, καὶ τοὺς νικῶντας ἐκ τοῦ θηρίου καὶ ἐκ τῆς εἰκόνος αὐτοῦ καὶ ἐκ τοῦ χαράγματος αὐτοῦ, ἐκ τοῦ ἀριθμοῦ τοῦ ὀνόματος αὐτοῦ, ἑστῶτας ἐπὶ τὴν θάλασσαν τὴν ὑαλίνην, ἔχοντας κιθάρας τοῦ Θεοῦ."
          tags: []
        "Ob15,3":
          text: "καὶ ᾄδουσι τὴν ᾠδὴν Μωσέως τοῦ δούλου τοῦ Θεοῦ, καὶ τὴν ᾠδὴν τοῦ ἀρνίου, λέγοντες, Μεγάλα καὶ θαυμαστὰ τὰ ἔργα σου, Κύριε ὁ Θεὸς ὁ παντοκράτωρ· δίκαιαι καὶ ἀληθιναὶ αἱ ὁδοί σου, ὁ βασιλεὺς τῶν ἁγίων."
          tags: []
        "Ob15,4":
          text: "τίς οὐ μὴ φοβηθῇ σε, Κύριε, καὶ δοξάσῃ τὸ ὄνομά σου; ὅτι μόνος ὅσιος· ὅτι πάντα τὰ ἔθνη ἥξουσι καὶ προσκυνήσουσιν ἐνώπιόν σου, ὅτι τὰ δικαιώματά σου ἐφανερώθησαν."
          tags: []
        "Ob15,5":
          text: "Καὶ μετὰ ταῦτα εἶδον, καὶ ἰδού, ἠνοίγη ὁ ναὸς τῆς σκηνῆς τοῦ μαρτυρίου ἐν τῷ οὐρανῷ·"
          tags: []
        "Ob15,6":
          text: "καὶ ἐξῆλθον οἱ ἑπτὰ ἄγγελοι ἔχοντες τὰς ἑπτὰ πληγὰς ἐκ τοῦ ναοῦ, ἐνδεδυμένοι λίνον καθαρὸν καὶ λαμπρόν, καὶ περιεζωσμένοι περὶ τὰ στήθη ζώνας χρυσᾶς."
          tags: []
        "Ob15,7":
          text: "καὶ ἓν ἐκ τῶν τεσσάρων ζώων ἔδωκε τοῖς ἑπτὰ ἀγγέλοις ἑπτὰ φιάλας χρυσᾶς γεμούσας τοῦ θυμοῦ τοῦ Θεοῦ τοῦ ζῶντος εἰς τοὺς αἰῶνας τῶν αἰώνων."
          tags: []
        "Ob15,8":
          text: "καὶ ἐγεμίσθη ὁ ναὸς καπνοῦ ἐκ τῆς δόξης τοῦ Θεοῦ, καὶ ἐκ τῆς δυνάμεως αὐτοῦ· καὶ οὐδεὶς ἠδύνατο εἰσελθεῖν εἰς τὸν ναόν, ἄχρι τελεσθῶσιν αἱ ἑπτὰ πληγαὶ τῶν ἑπτὰ ἀγγέλων."
          tags: []
        """;

    private const string KjvRevelationChapterFifteen = """
        "Ob15,1":
          text: "And I saw another sign in heaven, great and marvellous, seven angels having the seven last plagues; for in them is filled up the wrath of God."
          tags: []
        "Ob15,2":
          text: "And I saw as it were a sea of glass mingled with fire: and them that had gotten the victory over the beast, and over his image, and over his mark, [and] over the number of his name, stand on the sea of glass, having the harps of God."
          tags: []
        "Ob15,3":
          text: "And they sing the song of Moses the servant of God, and the song of the Lamb, saying, Great and marvellous [are] thy works, Lord God Almighty; just and true [are] thy ways, thou King of saints."
          tags: []
        "Ob15,4":
          text: "Who shall not fear thee, O Lord, and glorify thy name? for [thou] only [art] holy: for all nations shall come and worship before thee; for thy judgments are made manifest."
          tags: []
        "Ob15,5":
          text: "And after that I looked, and, behold, the temple of the tabernacle of the testimony in heaven was opened:"
          tags: []
        "Ob15,6":
          text: "And the seven angels came out of the temple, having the seven plagues, clothed in pure and white linen, and having their breasts girded with golden girdles."
          tags: []
        "Ob15,7":
          text: "And one of the four beasts gave unto the seven angels seven golden vials full of the wrath of God, who liveth for ever and ever."
          tags: []
        "Ob15,8":
          text: "And the temple was filled with smoke from the glory of God, and from his power; and no man was able to enter into the temple, till the seven plagues of the seven angels were fulfilled."
          tags: []
        """;

    private const string ChapterOne = """
        "1Kor1,1":
          text: "Paweł, powołany apostoł Jezusa Chrystusa z woli Boga, i Sostenes, brat,"
          tags: ["Paweł"]
        "1Kor1,2":
          text: "Zborowi Boga w Koryncie, do tych, którzy są uświęceni w Chrystusie Jezusie, powołanym świętym, ze wszystkimi, którzy wzywają imienia Pana naszego Jezusa Chrystusa na każdym miejscu – ich, a także i naszym."
          tags: []
        "1Kor1,3":
          text: "Łaska wam i pokój od Boga, Ojca naszego, i od Pana Jezusa Chrystusa."
          tags: []
        "1Kor1,4":
          text: "Dziękuję Bogu mojemu zawsze za was z powodu łaski Boga, która wam została dana w Chrystusie Jezusie,"
          tags: []
        "1Kor1,5":
          text: "Że we wszystkim zostaliście ubogaceni w Nim, we wszelkim słowie i we wszelkim poznaniu;"
          tags: []
        "1Kor1,6":
          text: "Tak, jak świadectwo Chrystusa zostało utwierdzone w was."
          tags: []
        "1Kor1,7":
          text: "Tak też nie brakuje wam żadnego daru łaski, wam, którzy oczekujecie objawienia Pana naszego Jezusa Chrystusa,"
          tags: []
        "1Kor1,8":
          text: "Który też utwierdzi was aż do końca, jako nienagannych w dniu Pana naszego Jezusa Chrystusa."
          tags: []
        "1Kor1,9":
          text: "Wierny jest Bóg, przez którego zostaliście powołani do społeczności Jego Syna Jezusa Chrystusa, Pana naszego."
          tags: []
        "1Kor1,10":
          text: "A wzywam was, bracia, przez imię Pana naszego Jezusa Chrystusa, abyście wszyscy mówili to samo, żeby nie było między wami rozłamów i abyście byli zespoleni jednym duchem i jedną myślą."
          tags: []
        "1Kor1,11":
          text: "Zostało mi bowiem oznajmione o was, moi bracia, od domowników Chloi, że wśród was są spory."
          tags: []
        "1Kor1,12":
          text: "A mówię o tym, bo każdy z was dobitnie mówi: Ja jestem Pawła, a ja Apollosa, a ja Kefasa, a ja Chrystusa."
          tags: []
        "1Kor1,13":
          text: "Rozdzielony jest Chrystus. Czy Paweł został za was przybity do krzyża lub w imię Pawła zostaliście ochrzczeni?"
          tags: []
        "1Kor1,14":
          text: "Dziękuję Bogu, że nie ochrzciłem nikogo z was, z wyjątkiem Kryspusa i Gajusa;"
          tags: []
        "1Kor1,15":
          text: "Aby ktoś nie powiedział, że chrzciłem w moje imię."
          tags: []
        "1Kor1,16":
          text: "Ochrzciłem też i dom Stefana; w końcu nie wiem, czy kogoś innego ochrzciłem."
          tags: []
        "1Kor1,17":
          text: "Bo Chrystus nie posłał mnie chrzcić, ale głosić Ewangelię, nie w mądrości słowa, by krzyż Chrystusa nie został pozbawiony swojego znaczenia."
          tags: []
        "1Kor1,18":
          text: "Bo Słowo o krzyżu dla tych, którzy istotnie giną, jest głupstwem; ale dla nas, którzy jesteśmy zbawiani, jest mocą Bożą."
          tags: []
        "1Kor1,19":
          text: "Napisano bowiem: Zniszczę mądrość mądrych, a zrozumienie rozumnych odrzucę."
          tags: []
        "1Kor1,20":
          text: "Gdzie jest mądry? Gdzie uczony w Piśmie? Gdzie badacz tego wieku? Czyż Bóg nie uczynił głupią mądrość tego świata?"
          tags: []
        "1Kor1,21":
          text: "Skoro bowiem w mądrości Boga, świat nie poznał Boga przez mądrość, upodobało się Bogu, przez głupie głoszenie zbawić wierzących."
          tags: []
        "1Kor1,22":
          text: "A gdy Żydzi domagają się cudu, a Grecy szukają mądrości,"
          tags: []
        "1Kor1,23":
          text: "My jednak głosimy Chrystusa, który jest ukrzyżowany, dla Żydów to wprawdzie zgorszenie, a dla Greków głupota,"
          tags: []
        "1Kor1,24":
          text: "Lecz dla samych powołanych, i Żydów, i Greków, głosimy Chrystusa – moc Boga i mądrość Boga."
          tags: []
        "1Kor1,25":
          text: "Gdyż to, co głupie u Boga mądrzejsze jest od ludzi, a to, co słabe u Boga mocniejsze jest od ludzi."
          tags: []
        "1Kor1,26":
          text: "Przypatrujcie się bowiem swojemu powołaniu, bracia, że niewielu jest mądrych według ciała, niewielu możnych, niewielu szlachetnie urodzonych;"
          tags: []
        "1Kor1,27":
          text: "Ale to, co głupie dla świata, wybrał Bóg, by zawstydzać mądrych; i słabych ze świata wybrał Bóg, aby zawstydzać mocnych;"
          tags: []
        "1Kor1,28":
          text: "I to, co jest niskiego rodu u świata, i to, co jest wzgardzone, wybrał Bóg, i co jest niczym, aby zniszczyć to, co jest czymś,"
          tags: []
        "1Kor1,29":
          text: "Aby przed Jego obliczem nie chlubiło się żadne ciało."
          tags: []
        "1Kor1,30":
          text: "Lecz przez Niego wy jesteście w Chrystusie Jezusie, który dla nas stał się mądrością od Boga, zarówno sprawiedliwością, i uświęceniem, i odkupieniem,"
          tags: []
        "1Kor1,31":
          text: "Aby tak, jak jest napisane: Kto się chlubi, niech się chlubi w PANU."
          tags: []
        """;
}
