using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using System.Windows.Media;
using BitLifeClone.Models;
using BitLifeClone.Services;

namespace BitLifeClone.ViewModels;

/// <summary>Item de un menu (lista de botones que aparece marcado sobre la pantalla).</summary>
public sealed class OptionItem
{
    public OptionItem(string text, string? sub, ICommand command)
    {
        Text = text;
        Sub = sub;
        Command = command;
        (EmojiImage, TextClean) = EmojiService.FirstEmoji(text);
    }

    public string Text { get; }
    public string? Sub { get; }
    public ICommand Command { get; }

    // ---------------------------------------------------------------- JoyPixels
    public ImageSource? EmojiImage { get; }
    public string TextClean { get; }
}

/// <summary>
/// MVVM a mano. Pantalla: header (avatar/nombre/dinero), feed central
/// (Person.History mas nuevo arriba) y footer con barras + botones.
/// </summary>
public sealed class MainViewModel : ObservableObject
{
    private readonly DataLoader _data;
    private readonly GameEngine _engine;
    private readonly SaveService _saves;
    private readonly Random _rng = new();

    private GameState? _game;
    private Gender _startGender = Gender.Male;

    public MainViewModel()
    {
        _data = new DataLoader();
        _engine = new GameEngine(_data, new JobService());
        _saves = new SaveService();

        AgeUpCommand = new RelayCommand(AgeUp, () => HasGame && !_game!.Person.IsDead);
        OpenMenuCommand = new RelayCommand(p => OpenMenu(p as string ?? ""), _ => HasGame && !_game!.Person.IsDead);
        HideMenuCommand = new RelayCommand(HideMenu);
        StartCommand = new RelayCommand(StartNewLife, () => StartName.Trim().Length > 0);
        NewLifeCommand = new RelayCommand(NewLife);
        SetGenderCommand = new RelayCommand(p => StartGender = (Gender)p!);
        LoadSaveCommand = new RelayCommand(p => LoadSave(p as string));
        RefreshSavesCommand = new RelayCommand(RefreshSaves);
    }

    // ------------------------------------------------------------- comandos

    public ICommand AgeUpCommand { get; }
    public ICommand OpenMenuCommand { get; }
    public ICommand HideMenuCommand { get; }
    public ICommand StartCommand { get; }
    public ICommand NewLifeCommand { get; }
    public ICommand SetGenderCommand { get; }
    public ICommand LoadSaveCommand { get; }
    public ICommand RefreshSavesCommand { get; }

    // ------------------------------------------------------- estado de partida

    public bool HasGame => _game is not null;

    public GameState? Game => _game;

    public string Avatar => _game?.Person.Avatar ?? "🤰";

    public string TitleLine => _game is null
        ? "BitLifeClone"
        : $"{_game.Person.Name}, {_game.Person.Age}";

    public string SubLine => _game is null
        ? "Nueva vida que espera"
        : $"{_game.Person.Country} · Año {_game.Year}";

    public string MoneyText => _game is null ? "$0" : $"💵 ${_game.Person.Money:N0}";

    public string JobLine => _game is null
        ? ""
        : _game.Person.JobId is { } jobId && _engine.Jobs.FindJob(jobId) is { } job
            ? $"💼 {job.Title}"
            : "💼 Sin trabajo";

    /// <summary>Para el overlay de muerte (los modelos no son observables).</summary>
    public bool IsPlayerDead => _game?.Person.IsDead == true;

    // --------------------------------------------------------------- barras

    public int Happiness => _game?.Person.Stat("happiness") ?? 0;
    public int Health => _game?.Person.Stat("health") ?? 0;
    public int Smarts => _game?.Person.Stat("smarts") ?? 0;
    public int Looks => _game?.Person.Stat("looks") ?? 0;

    // ----------------------------------------------------------------- feed

    public ObservableCollection<LifeEvent> Feed { get; } = new();

    // ---------------------------------------------------------------- inicio

    public string StartName { get => _startName; set => SetProperty(ref _startName, value); }
    private string _startName = "Novato";

    public Gender StartGender
    {
        get => _startGender;
        set
        {
            if (SetProperty(ref _startGender, value))
            {
                RaiseProperty(nameof(IsFemaleSelected));
                RaiseProperty(nameof(IsMaleSelected));
            }
        }
    }

    public bool IsFemaleSelected => StartGender == Gender.Female;
    public bool IsMaleSelected => StartGender == Gender.Male;

    public List<string> Countries { get; } = new()
    {
        "República Dominicana", "Estados Unidos", "Puerto Rico", "México",
        "Colombia", "Venezuela", "Cuba", "España", "Argentina", "Chile"
    };

    public string SelectedCountry
    {
        get => _selectedCountry;
        set => SetProperty(ref _selectedCountry, value);
    }
    private string _selectedCountry = "República Dominicana";

    public sealed record SaveEntry(string FileName, string Label)
    {
        public string FileName { get; } = FileName;
        public string Label { get; } = Label;
    }

    public ObservableCollection<SaveEntry> Saves { get; } = new();

    public void RefreshSaves()
    {
        Saves.Clear();
        foreach (var s in _saves.ListSaves())
            Saves.Add(new SaveEntry(s.FileName, s.Label));
    }

    // ----------------------------------------------------------------- menús

    public bool OptionsVisible { get => _optionsVisible; set => SetProperty(ref _optionsVisible, value); }
    private bool _optionsVisible;

    public string OptionsTitle { get => _optionsTitle; set => SetProperty(ref _optionsTitle, value); }
    private string _optionsTitle = "";

    public ObservableCollection<OptionItem> Options { get; } = new();

    private void ShowOptions(string title, IEnumerable<OptionItem> items)
    {
        Options.Clear();
        foreach (var it in items)
            Options.Add(it);
        OptionsTitle = title;
        OptionsVisible = true;
    }

    private void HideMenu()
    {
        OptionsVisible = false;
    }

    private void OpenMenu(string name)
    {
        switch (name)
        {
            case "activities": ShowActivitiesMenu(); break;
            case "jobs": ShowJobsMenu(null); break;
            case "relations": ShowRelationsMenu(); break;
            case "game": ShowGameMenu(); break;
        }
    }

    private void ShowActivitiesMenu()
    {
        var p = Game!.Person;
        var items = new List<OptionItem>();

        // menus de sistemas nuevos (escuela, crimen, salud, fama, bienes, licencias)
        var isStudent = p.Age >= 6 && p.Age < 23 && !p.HasFlag("EXPELLED") && !p.HasFlag("DROPPED_OUT");
        if (isStudent)
            items.Add(new OptionItem("🏫 Escuela", "estudiar, clubes y trabajitos",
                new RelayCommand(ShowSchoolMenu)));
        if (p.HasFlag("IN_PRISON"))
            items.Add(new OptionItem("🚔 Mi vida en la prisión", null,
                new RelayCommand(() => ShowGenericMenu("prison", "Prisión"))));
        else
            items.Add(new OptionItem("🕵️ Criminalidad", null,
                new RelayCommand(() => ShowGenericMenu("crime", "Criminalidad"))));
        items.Add(new OptionItem("🏥 Clínica", null,
            new RelayCommand(() => ShowGenericMenu("health", "Clínica"))));
        if (p.Stat("fame") >= 20)
            items.Add(new OptionItem("⭐ Fama", null,
                new RelayCommand(() => ShowGenericMenu("fame", "Fama"))));
        items.Add(new OptionItem("🏠 Bienes raíces", null,
            new RelayCommand(ShowPropertiesMenu)));
        items.Add(new OptionItem("📄 Licencias", null,
            new RelayCommand(ShowLicensesMenu)));

        foreach (var a in _engine.AllActivities)
            items.Add(new OptionItem(a.Label,
                a.Cost > 0 ? $"${a.Cost}" : null,
                new RelayCommand(() => PickActivity(a))));

        items.Add(BackItem("Volver"));
        ShowOptions("Trayectoria", items);
    }

    private void PickActivity(GameEngine.Activity a)
    {
        HideMenu();
        PushEvents(new[] { _engine.RunActivity(Game!, a.Id) });
        RefreshAll();
    }

    private void ShowJobsMenu(string? categoryId)
    {
        var p = Game!.Person;
        if (categoryId is null)
        {
            var cats = _engine.Jobs.Categories
                .Where(c => _engine.EligibleJobs(Game, c.Id).Any())
                .Select(c => new OptionItem(c.Name,
                    $"{_engine.EligibleJobs(Game, c.Id).Count()} vacante(s)",
                    new RelayCommand(() => ShowJobsMenu(c.Id))));
            var list = cats.ToList();

            // universidad/posgrados se matriculan desde el menu de Trabajo
            var unis = _engine.UniversityOptions(Game);
            if (unis.Count > 0)
                list.Insert(0, new OptionItem("🎓 Matricularme en estudios",
                    "universidad y posgrados",
                    new RelayCommand(ShowUniversityMenu)));

            if (p.JobId is not null)
            {
                var cur = _engine.Jobs.FindJob(p.JobId);
                list.Insert(0, new OptionItem("✋ Renunciar",
                    cur is null ? "" : cur.Title,
                    new RelayCommand(() => QuitJobNow())));
                list.Insert(0, new OptionItem("📈 Pedir ascenso",
                    "50% + experiencia", new RelayCommand(() => PromoteNow())));
            }
            if (list.Count == 0)
                list.Add(new OptionItem("(No hay trabajos para tu estudio)", null, new RelayCommand(HideMenu)));
            list.Add(BackItem("Volver"));
            ShowOptions("Trabajo", list);
            return;
        }

        var cat = _engine.Jobs.FindCategory(categoryId);
        var jobs = _engine.EligibleJobs(Game, categoryId)
            .Select(j => new OptionItem(j.Title,
                JobDetail(j),
                new RelayCommand(() => ApplyJob(j.Id))))
            .ToList();
        if (jobs.Count == 0)
            jobs.Add(new OptionItem("(Sin vacantes en esta categoría)", null, new RelayCommand(HideMenu)));
        jobs.Add(new OptionItem("← Categorías", null, new RelayCommand(() => ShowJobsMenu(null))));
        jobs.Add(BackItem("Volver"));
        ShowOptions(cat?.Name ?? categoryId, jobs);
    }

    private void ApplyJob(string jobId)
    {
        HideMenu();
        var p = Game!.Person;
        if (_engine.ApplyForJob(Game, jobId))
        {
            var job = _engine.Jobs.FindJob(jobId)!;
            AddEvent(new LifeEvent
            {
                Age = p.Age,
                Text = $"💼 Te contrataron: {job.Title}.",
                Deltas = new() { ["happiness"] = 4 }
            });
        }
        else
        {
            AddEvent(new LifeEvent
            {
                Age = p.Age,
                Text = "Le tocaste la puerta equivocada: te rechazaron en la entrevista.",
                Deltas = new() { ["happiness"] = -5, ["stress"] = 5 }
            });
        }
        RefreshAll();
    }

    // --------------------------------------------- menus genericos (crime/health/fame/school)

    /// <summary>Menu de un catalogo de acciones genericas (crimen, clinica, fama, escuela...).</summary>
    private void ShowGenericMenu(string catalog, string title)
    {
        var actions = _engine.EligibleGenericActions(Game!, catalog);
        var items = actions
            .Select(a => new OptionItem(a.Label,
                a.Cost > 0 ? $"Coste ${a.Cost}" : null,
                new RelayCommand(() => RunGenericNow(catalog, a))))
            .ToList();
        if (items.Count == 0)
            items.Add(new OptionItem("(Nada disponible ahora mismo)", null, new RelayCommand(HideMenu)));
        items.Add(BackItem("Volver"));
        ShowOptions(title, items);
    }

    private void RunGenericNow(string catalog, DataLoader.GenericAction action)
    {
        HideMenu();
        var evts = _engine.RunGenericAction(Game!, action);
        PushEvents(evts);
        RefreshAll();
    }

    /// <summary>Menu de escuela: acciones de estudio + extracurriculares + trabajito escolar.</summary>
    private void ShowSchoolMenu()
    {
        var items = _engine.EligibleGenericActions(Game!, "school")
            .Select(a => new OptionItem(a.Label,
                a.Cost > 0 ? $"Coste ${a.Cost}" : null,
                new RelayCommand(() => RunGenericNow("school", a))))
            .ToList();
        items.Add(new OptionItem("🤾 Extracurriculares", "clubes y equipos",
            new RelayCommand(ShowSchoolXcMenu)));
        items.Add(new OptionItem("💼 Trabajito escolar", "tutor, monitor, cafeteria",
            new RelayCommand(() => ShowGenericMenu("school_jobs", "Trabajito escolar"))));
        items.Add(BackItem("Volver"));
        ShowOptions("Escuela", items);
    }

    private void ShowSchoolXcMenu()
    {
        var items = _engine.EligibleGenericActions(Game!, "school_xc")
            .Select(a => new OptionItem(a.Label,
                a.Cost > 0 ? $"Coste ${a.Cost}" : null,
                new RelayCommand(() => RunGenericNow("school_xc", a))))
            .ToList();
        if (items.Count == 0)
            items.Add(new OptionItem("(Sin actividades para tu edad)", null, new RelayCommand(HideMenu)));
        items.Add(new OptionItem("← Escuela", null, new RelayCommand(ShowSchoolMenu)));
        items.Add(BackItem("Volver"));
        ShowOptions("Extracurriculares", items);
    }

    // ------------------------------------------------------- bienes y licencias

    private void ShowPropertiesMenu()
    {
        var p = Game!.Person;
        var items = new List<OptionItem>();

        foreach (var propId in p.Properties)
        {
            var owned = _engine.Data.PropertyDefs.FirstOrDefault(x => x.Id == propId);
            if (owned is null) continue;
            var pid = propId;
            items.Add(new OptionItem($"{owned.Icon} {owned.Label} (tuya)",
                $"venta ~${(long)(owned.Price * 0.7):N0}",
                new RelayCommand(() => SellPropertyNow(pid))));
        }

        foreach (var prop in _engine.BuyableProperties(Game))
        {
            var pr = prop;
            items.Add(new OptionItem($"{pr.Icon} {pr.Label}",
                $"compra ${pr.Price:N0} · mant. ${pr.Maintenance:N0}/año",
                new RelayCommand(() => BuyPropertyNow(pr))));
        }

        if (items.Count == 0)
            items.Add(new OptionItem("(Nada disponible a tu edad)", null, new RelayCommand(HideMenu)));
        items.Add(BackItem("Volver"));
        ShowOptions("Bienes raíces", items);
    }

    private void BuyPropertyNow(DataLoader.PropertyDef prop)
    {
        HideMenu();
        var (ok, msg) = _engine.BuyProperty(Game!, prop);
        AddEvent(new LifeEvent
        {
            Age = Game!.Person.Age,
            Text = ok ? msg : $"😮 {msg}",
            Deltas = ok ? new() { ["happiness"] = 5 } : new() { ["happiness"] = -3 }
        });
        RefreshAll();
    }

    private void SellPropertyNow(string propertyId)
    {
        HideMenu();
        var price = _engine.SellProperty(Game!, propertyId);
        AddEvent(new LifeEvent
        {
            Age = Game!.Person.Age,
            Text = price > 0 ? $"💵 Vendiste una propiedad por ${price:N0}." : "No tienes esa propiedad.",
            MoneyDelta = (int)price
        });
        RefreshAll();
    }

    private void ShowLicensesMenu()
    {
        var items = _engine.EligibleLicenses(Game!)
            .Select(l => new OptionItem($"{l.Icon} {l.Label}",
                $"examen ${l.Cost} · chance {l.PassChance}%",
                new RelayCommand(() => TryLicenseNow(l))))
            .ToList();

        var owned = Game!.Person.Licenses
            .Select(id => _engine.Data.LicenseDefs.FirstOrDefault(x => x.Id == id))
            .Where(x => x is not null).ToList();
        foreach (var l in owned)
            items.Add(new OptionItem($"{l!.Icon} {l.Label} (ya la tienes)", null, new RelayCommand(HideMenu)));

        if (items.Count == owned.Count)
            items.Insert(0, new OptionItem("(Nada nuevo para tu perfil)", null, new RelayCommand(HideMenu)));
        items.Add(BackItem("Volver"));
        ShowOptions("Licencias", items);
    }

    private void TryLicenseNow(DataLoader.LicenseDef license)
    {
        HideMenu();
        var (ok, msg) = _engine.TryLicense(Game!, license);
        AddEvent(new LifeEvent
        {
            Age = Game!.Person.Age,
            Text = ok ? msg : $"😮 {msg}",
            Deltas = ok ? new() { ["happiness"] = 6 } : new() { ["happiness"] = -4, ["stress"] = 3 }
        });
        RefreshAll();
    }

    private string JobDetail(Job job)
    {
        var salary = _engine.Jobs.SalaryOf(job) is { } s ? $"$ {s:N0} / año" : "Sueldo sin datos";
        var parts = new List<string> { salary };
        if (job.Education is { } edu)
            parts.Add($"requiere {EduLabel(edu)}");
        if (job.Tier is { } tier && tier > 1)
            parts.Add($"tier {tier}");
        return string.Join(" · ", parts);
    }

    private static string EduLabel(EducationLevel level) => level switch
    {
        EducationLevel.None => "sin estudios",
        EducationLevel.PrimarySchool => "primaria",
        EducationLevel.LowerSecondary => "secundaria básica",
        EducationLevel.UpperSecondary => "secundaria alta",
        EducationLevel.HighSchool => "bachiller",
        EducationLevel.CommunityCollege => "técnico",
        EducationLevel.University => "universidad",
        EducationLevel.GraduateSchool => "posgrado",
        EducationLevel.MedicalSchool => "medicina",
        EducationLevel.LawSchool => "derecho",
        EducationLevel.VeterinarySchool => "veterinaria",
        EducationLevel.PharmacySchool => "farmacia",
        EducationLevel.NursingSchool => "enfermería",
        EducationLevel.BusinessSchool => "negocios",
        _ => level.ToString()
    };

    private void ShowUniversityMenu()
    {
        var options = _engine.UniversityOptions(Game!);
        var items = options
            .Select(o => new OptionItem(o.label,
                $"matrícula ${o.cost:N0}",
                new RelayCommand(() => EnrollNow(o.level, o.cost))))
            .ToList();
        if (items.Count == 0)
            items.Add(new OptionItem("(No hay estudios para ti ahora)", null, new RelayCommand(HideMenu)));
        items.Add(BackItem("Volver"));
        ShowOptions("Estudios", items);
    }

    private void EnrollNow(EducationLevel level, int cost)
    {
        HideMenu();
        var (ok, msg) = _engine.EnrollUniversity(Game!, level, cost);
        AddEvent(new LifeEvent
        {
            Age = Game!.Person.Age,
            Text = ok ? msg : $"😮 {msg}",
            Deltas = ok ? new() { ["happiness"] = 6 } : new() { ["happiness"] = -6, ["stress"] = 4 }
        });
        RefreshAll();
    }

    private void PromoteNow()
    {
        HideMenu();
        var (ok, msg) = _engine.TryPromote(Game!);
        AddEvent(new LifeEvent
        {
            Age = Game!.Person.Age,
            Text = ok ? $"📈 {msg}" : $"😮 {msg}",
            Deltas = ok ? new() { ["happiness"] = 5 } : new() { ["happiness"] = -2 }
        });
        RefreshAll();
    }

    private void QuitJobNow()
    {
        HideMenu();
        _engine.QuitJob(Game!);
        AddEvent(new LifeEvent
        {
            Age = Game!.Person.Age,
            Text = "✋ Renunciaste al trabajo.",
            Deltas = new() { ["happiness"] = 2, ["stress"] = -5 }
        });
        RefreshAll();
    }

    // ---------------------------------------------- menu de relaciones

    private void ShowRelationsMenu()
    {
        var items = Game!.Person.Relations
            .Select(r => new OptionItem($"{r.Avatar} {r.Name}",
                $"{r.KindLabel} · {r.Age} años · relación {r.Bar}/100",
                new RelayCommand(() => ShowRelationActionMenu(r.Id))))
            .ToList();

        items.Add(new OptionItem("➕ Presentar a un amigo", "nada",
            new RelayCommand(() => AddRelation("friend"))));
        items.Add(new OptionItem("❤️ Buscar pareja", "50% chance",
            new RelayCommand(() => AddRelation("partner"))));
        items.Add(BackItem("Volver"));
        ShowOptions("Relaciones", items);
    }

    private void ShowRelationActionMenu(string relationId)
    {
        var rel = Game!.FindRelation(relationId);
        if (rel is null) { ShowRelationsMenu(); return; }

        var items = _engine.EligibleActions(Game, rel)
            .Select(a => new OptionItem(a.Label,
                a.Cost > 0 ? $"Coste ${a.Cost}" : null,
                new RelayCommand(() => RunRelationAction(rel.Id, a))))
            .ToList();

        if (rel.Type != "mother" && rel.Type != "father")
        {
            items.Add(new OptionItem("🗑️ Cortar la relación", null,
                new RelayCommand(() => DropRelation(rel.Id))));
        }
        if (items.Count == 0)
            items.Add(new OptionItem("(Nada disponible con esta persona)", null, new RelayCommand(HideMenu)));
        items.Add(new OptionItem("← Relaciones", null, new RelayCommand(ShowRelationsMenu)));
        ShowOptions(rel.Name, items);
    }

    private void RunRelationAction(string relationId, DataLoader.RelAction action)
    {
        HideMenu();
        var rel = Game!.FindRelation(relationId);
        if (rel is null) return;
        PushEvents(_engine.RunRelationAction(Game, rel, action));
        RefreshAll();
    }

    private void DropRelation(string relationId)
    {
        HideMenu();
        var rel = Game!.FindRelation(relationId);
        if (rel is null) return;
        Game!.Person.Relations.Remove(rel);
        AddEvent(new LifeEvent { Age = Game.Person.Age, Text = $"Cortaste la relación con {rel.Name}." });
        RefreshAll();
    }

    private static readonly string[] FemaleNames = { "Carla", "Yamilex", "Laura", "Mariana", "Sofía", "Rosanna", "Daya" };
    private static readonly string[] MaleNames = { "Carlos", "José", "Luis", "Pedro", "Manuel", "Yefri", "Ramón" };

    /// <summary>Nombre para un NPC: usa Data/World/names.json si hay datos, si no el fallback.</summary>
    private string PickName(bool female)
    {
        var names = _engine.Data.Names;
        string[] pool = female
            ? (names.FemaleNames.Count > 0 ? names.FemaleNames.ToArray() : FemaleNames)
            : (names.MaleNames.Count > 0 ? names.MaleNames.ToArray() : MaleNames);
        var first = pool[_rng.Next(pool.Length)];
        if (names.LastNames.Count > 0 && _rng.Next(100) < 70)
            first += " " + names.LastNames[_rng.Next(names.LastNames.Count)];
        return first;
    }

    private void AddRelation(string type)
    {
        HideMenu();
        var p = Game!.Person;
        var female = _rng.Next(2) == 0;
        var name = PickName(female);

        var rel = new Relation
        {
            Name = name,
            Type = type,
            Gender = female ? Gender.Female : Gender.Male,
            Age = Math.Max(16, p.Age + _rng.Next(-6, 7)),
            Bar = 45 + _rng.Next(20)
        };

        // rasgos de personalidad del NPC (personality_templates.json)
        _engine.AssignNpcTraits(rel);

        var found = type switch
        {
            "partner" => "❤️ Conociste a alguien en la fiesta.",
            _ => "🤝 Hiciste un nuevo amigo."
        };
        p.Relations.Add(rel);
        AddEvent(new LifeEvent { Age = p.Age, Text = $"{found} ({rel.KindLabel})." });
        RefreshAll();
    }

    // ------------------------------------------------------------ menu del juego

    private void ShowGameMenu()
    {
        RefreshSaves();
        var list = new List<OptionItem>
        {
            new("💾 Guardar partida", null, new RelayCommand(SaveGameNow)),
            new("📂 Cargar otra", $"{Saves.Count} partida(s)", new RelayCommand(ShowLoadSaves)),
            new("🆕 Nueva vida", "abandonar esta", new RelayCommand(NewLife))
        };
        list.Add(BackItem("Volver"));
        ShowOptions("Menú", list);
    }

    private void ShowLoadSaves()
    {
        RefreshSaves();
        var items = Saves.Select(s => new OptionItem(s.Label, s.FileName,
            new RelayCommand(() => LoadSave(s.FileName)))).ToList();
        if (items.Count == 0)
            items.Add(new OptionItem("(No hay partidas guardadas)", null, new RelayCommand(HideMenu)));
        items.Add(BackItem("Volver"));
        ShowOptions("Cargar partida", items);
    }

    private void SaveGameNow()
    {
        HideMenu();
        var path = _saves.Save(Game!);
        AddEvent(new LifeEvent
        {
            Age = Game!.Person.Age,
            Text = $"💾 Partida guardada ({Path.GetFileNameWithoutExtension(path)}).",
        });
        RefreshAll();
    }

    public void LoadSave(string? fileName)
    {
        if (string.IsNullOrEmpty(fileName)) return;
        var game = _saves.Load(fileName);
        if (game is null) return;

        SetGame(game);
        OnGameChanged();
    }

    // -------------------------------------------------------------- ciclo de vida

    private void StartNewLife()
    {
        var p = new Person
        {
            Name = StartName.Trim() == "" ? "Novato" : StartName.Trim(),
            Gender = StartGender,
            Country = SelectedCountry,
            BirthYear = DateTime.Now.Year,
            Money = 0
        };
        p.AddFlag("NEW_BORN");

        // familia inicial: madre y padre
        var motherAge = 20 + _rng.Next(25);
        var fatherAge = 21 + _rng.Next(27);
        p.Relations.Add(new Relation
        {
            Name = "mamá",
            Type = "mother",
            Gender = Gender.Female,
            Age = motherAge,
            Bar = 65 + _rng.Next(30)
        });
        p.Relations.Add(new Relation
        {
            Name = "papá",
            Type = "father",
            Gender = Gender.Male,
            Age = fatherAge,
            Bar = 60 + _rng.Next(35)
        });

        var game = new GameState { Person = p, Year = p.BirthYear };
        p.History.Add(new LifeEvent
        {
            Age = 0,
            Text = $"🍼 Naciste en {p.Country} el año {p.BirthYear}. Te pusieron {p.Name}."
        });

        SetGame(game);
        OnGameChanged();
    }

    private void NewLife()
    {
        _game = null;
        Feed.Clear();
        OptionsVisible = false;
        RefreshAll();
        RefreshSaves();
    }

    private void AgeUp()
    {
        if (_game is null) return;

        var evts = _engine.AgeUp(_game);
        PushEvents(evts);

        // los nuevos también envejecen; los abuelos/papás pueden morir de viejos
        var toKill = new List<Relation>();
        foreach (var rel in _game.Person.Relations.Where(r => !r.IsDead))
        {
            rel.Age++;
            if (rel.Age >= 70 && _rng.Next(100) < (rel.Age - 60) / 3)
                toKill.Add(rel);
        }
        foreach (var rel in toKill)
        {
            rel.Flags.Add("DEAD");
            PushEvents(new[] { new LifeEvent
            {
                Age = _game.Person.Age,
                Text = $"🕯️ {rel.Name} murió a los {rel.Age} años.",
                Deltas = new() { ["happiness"] = -10 }
            } });
        }

        RefreshAll();
    }

    // ---------------------------------------------------------------- helpers

    private void SetGame(GameState? game) => _game = game;

    /// <summary>Todos losRaiseProperty del HUD tras cualquier cambio.</summary>
    private void RefreshAll()
    {
        RaiseProperty(nameof(HasGame));
        RaiseProperty(nameof(Game));
        RaiseProperty(nameof(Avatar));
        RaiseProperty(nameof(TitleLine));
        RaiseProperty(nameof(SubLine));
        RaiseProperty(nameof(MoneyText));
        RaiseProperty(nameof(JobLine));
        RaiseProperty(nameof(IsPlayerDead));
        RaiseProperty(nameof(Happiness));
        RaiseProperty(nameof(Health));
        RaiseProperty(nameof(Smarts));
        RaiseProperty(nameof(Looks));
        CommandManager.InvalidateRequerySuggested();
    }

    private void OnGameChanged()
    {
        Feed.Clear();
        if (_game is not null)
            foreach (var evt in _game.Person.History.AsEnumerable().Reverse())
                Feed.Add(evt);
        RefreshAll();
        RefreshSaves();
    }

    private void PushEvents(IEnumerable<LifeEvent> events)
    {
        foreach (var evt in events)
            if (evt is not null)
                Feed.Insert(0, evt);
    }

    private void AddEvent(LifeEvent evt)
    {
        Game?.Person.History.Add(evt);
        Feed.Insert(0, evt);
    }

    private OptionItem BackItem(string label) =>
        new(label, null, new RelayCommand(HideMenu));
}
