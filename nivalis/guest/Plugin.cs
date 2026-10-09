using System;
using System.Linq;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using Nivalis;
using Nivalis.CraftingSystem;
using NivalisModKit;
using UnityEngine;

namespace NivalisNightCity;

[BepInPlugin(Id, "Nivalis Night City Business Bridge", "0.1.0")]
[BepInDependency(ModKit.Guid, "0.6.1")]
public sealed class Plugin : BasePlugin
{
    public const string Id = "dr4lera.nivalis.nightcity";
    internal static Plugin? Active;
    internal string? SaveName;
    internal string? Pair;
    internal double LeaseUntil;
    internal IDisposable? ClockPause;
    internal bool OriginalBackground;
    private string LabSave = "";
    internal bool SandboxEnabled;
    internal bool KeepStaffHappy;
    internal bool CentralSupplies;
    internal bool PaperTest;
    internal bool SimulationFrozen;
    private float PreviousTimeScale = 1f;
    private bool AutoSetup;
    private bool Maintenance = true;

    public override void Load()
    {
        Active = this;
        LabSave = Config.Bind("Lab", "SaveName", "",
            "Exact dedicated test save allowed for mutations; normal saves remain observe-only.").Value;
        AutoSetup = Config.Bind("Sandbox", "AutoSetup", false,
            "Automatically unlock and configure the dedicated sandbox when it loads.").Value;
        KeepStaffHappy = Config.Bind("Sandbox", "KeepStaffHappy", true,
            "Keep hired sandbox staff happy and prevent quitting while preserving wage payments.").Value;
        CentralSupplies = Config.Bind("Bridge", "CentralSupplies", true,
            "Use only host-reserved bridge ingredient purchases for owned sandbox venues; prevent overlapping NPC manager spending.").Value;
        RestockPurchaser.HostPath = Config.Bind("Bridge", "CyberpunkPath", "",
            "Cyberpunk installation containing the matching host reservation journal.").Value;
        OriginalBackground = Application.runInBackground;
        GameEvents.GameLoaded += a => { Reset(); SaveName = a.SaveName; };
        GameEvents.GameEnded += Reset;
        GameEvents.GameReady += () =>
        {
            if (SaveName != LabSave) return;
            SandboxEnabled = true;
            SandboxSetup.DisableCurfew();
            SandboxSetup.CancelStory();
            StorageSetup.EnsureMenuCapacity();
            OperatingSetup.LoadHours();
            if (AutoSetup) SandboxSetup.Apply();
            Freeze();
        };
        SandboxSetup.InstallCurfewHooks();
        DevCommands.Register(Id, "nc-status", "Business bridge state (read only)", _ => Status());
        DevCommands.Register(Id, "nc-paper-test", "save=NAME enabled=true|false: isolate financial operations during service tests", a =>
        {
            RequireLab(a.Get("save", ""));
            if (RestockPurchaser.Busy) throw new InvalidOperationException("wait for the active purchase before changing test mode");
            PaperTest = a.Get("enabled", "true") != "false";
            return Status();
        });
        DevCommands.Register(Id, "nc-maintenance", "enabled=true|false: hold the lab frozen while provisioning", a =>
        {
            RequireLab(LabSave);
            Maintenance = a.Get("enabled", "true") != "false";
            if (Maintenance) { LeaseUntil = 0; Freeze(); }
            return Status();
        });
        DevCommands.Register(Id, "nc-load-lab", "Load only the configured dedicated test save", _ =>
        {
            var sm = Singleton<SerializationManager>.Instance;
            if (GameEvents.IsInGame) throw new InvalidOperationException("lab already loaded; restart Nivalis to verify a saved checkpoint safely");
            if (sm.IsInitializing) throw new InvalidOperationException("already loading");
            if (Singleton<GameSceneManager>.Instance.IsLoading || GameSceneManager.IsUnloadingGameplay)
                throw new InvalidOperationException("wait for the native title/loading transition to finish");
            if (!TitleReady()) throw new InvalidOperationException("wait for the native title menu to be ready before loading");
            if (!sm.DoesSaveExist(LabSave)) throw new InvalidOperationException("dedicated lab save missing");
            sm.Load(LabSave);
            return new { loading = LabSave };
        });
        DevCommands.Register(Id, "nc-save", "save=NAME: checkpoint only the dedicated test save", a =>
        {
            RequireLab(a.Get("save", ""));
            return new { saved = Singleton<SerializationManager>.Instance.Save(LabSave, false), save = LabSave };
        });
        DevCommands.Register(Id, "nc-venues", "All venues, real menu/stock/staff/receipts (read only)", _ => Snapshot());
        DevCommands.Register(Id, "nc-layout", "Native venue placement slots and existing furniture (read only)", _ => SandboxSetup.Layout());
        DevCommands.Register(Id, "nc-manager-action", "save=NAME venue=GUID action=price|hours|menu|staff: safely apply a manager's change and checkpoint", a =>
        {
            RequireLab(a.Get("save", ""));
            if (PaperTest || RestockPurchaser.Busy) throw new InvalidOperationException("wait for the current test or purchase to finish");
            var venue = Venues.PlayerOwned.FirstOrDefault(v => v.Venue.Guid == a.Get("venue", ""))
                ?? throw new ArgumentException("owned venue not found");
            bool previous = Maintenance;
            Maintenance = true; LeaseUntil = 0; Freeze();
            object result;
            try
            {
                switch (a.Get("action", ""))
                {
                    case "hours": result = OperatingSetup.SetHours(venue, a.GetInt("start", 8), a.GetInt("end", 20)); break;
                    case "menu": result = OperatingSetup.FocusMenu(venue, a.GetInt("count", 8)); break;
                    case "staff": result = AffordableCrew.Apply(venue); break;
                    case "price":
                        int price = a.GetInt("price");
                        if (price < 100 || price > 1_000_000) throw new ArgumentOutOfRangeException("price");
                        bool found = false;
                        foreach (var entry in venue.Menu)
                            if (entry.Meal.Guid == a.Get("dish", "")) { entry.Price = price; found = true; break; }
                        if (!found) throw new ArgumentException("dish is not on the menu");
                        venue.UpdatePopularity(); result = new { price }; break;
                    default: throw new ArgumentException("unknown manager action");
                }
                if (!Singleton<SerializationManager>.Instance.Save(LabSave, false)) throw new InvalidOperationException("manager checkpoint failed");
                Maintenance = previous;
                return new { applied = true, action = a.Get("action", ""), result };
            }
            catch { Maintenance = true; throw; }
        });
        DevCommands.Register(Id, "nc-focus-menu", "save=NAME venue=GUID count=8: choose compatible meals using native local customer preferences", a =>
        {
            RequireLab(a.Get("save", ""));
            if (!Maintenance) throw new InvalidOperationException("hold the lab before changing menus");
            var venue = Venues.PlayerOwned.FirstOrDefault(v => v.Venue.Guid == a.Get("venue", ""))
                ?? throw new ArgumentException("owned venue not found");
            return OperatingSetup.FocusMenu(venue, a.GetInt("count", 8));
        });
        DevCommands.Register(Id, "nc-service-hours", "save=NAME venue=GUID start=12 end=20: persist native opening hours with matching shifts", a =>
        {
            RequireLab(a.Get("save", ""));
            if (!Maintenance) throw new InvalidOperationException("hold the lab before changing opening hours");
            var venue = Venues.PlayerOwned.FirstOrDefault(v => v.Venue.Guid == a.Get("venue", ""))
                ?? throw new ArgumentException("owned venue not found");
            return OperatingSetup.SetHours(venue, a.GetInt("start", 12), a.GetInt("end", 20));
        });
        DevCommands.Register(Id, "nc-swap-crew", "save=NAME venue=GUID other=GUID: redistribute independent qualified crews without changing wages", a =>
        {
            RequireLab(a.Get("save", ""));
            if (!Maintenance) throw new InvalidOperationException("hold the lab before moving crews");
            var first = Venues.PlayerOwned.FirstOrDefault(v => v.Venue.Guid == a.Get("venue", ""))
                ?? throw new ArgumentException("first venue not found");
            var second = Venues.PlayerOwned.FirstOrDefault(v => v.Venue.Guid == a.Get("other", ""))
                ?? throw new ArgumentException("second venue not found");
            if (first.Pointer == second.Pointer) throw new ArgumentException("different venues required");
            return OperatingSetup.SwapCrew(first, second);
        });
        DevCommands.Register(Id, "nc-affordable-crew", "save=NAME venue=GUID: hire qualified independent applicants at native wages and train them to native caps", a =>
        {
            RequireLab(a.Get("save", ""));
            if (!Maintenance || RestockPurchaser.Busy) throw new InvalidOperationException("hold the lab before replacing crew");
            var venue = Venues.PlayerOwned.FirstOrDefault(v => v.Venue.Guid == a.Get("venue", ""))
                ?? throw new ArgumentException("owned venue not found");
            return AffordableCrew.Apply(venue);
        });
        DevCommands.Register(Id, "nc-price-profile", "save=NAME venue=GUID markup=175: include a margin above native ingredient and cooking costs", a =>
        {
            RequireLab(a.Get("save", ""));
            if (!Maintenance) throw new InvalidOperationException("hold the lab before pricing menus");
            int markup = a.GetInt("markup", 175);
            if (markup < 100 || markup > 250) throw new ArgumentOutOfRangeException("markup");
            var venue = Venues.PlayerOwned.FirstOrDefault(v => v.Venue.Guid == a.Get("venue", ""))
                ?? throw new ArgumentException("owned venue not found");
            int changed = 0;
            foreach (var entry in venue.Menu)
            {
                int price = Math.Max(entry.Price, (int)Math.Ceiling(entry.RecipeDefinition.CurrentBasePrice * markup / 100.0));
                if (price > entry.Price) { entry.Price = price; changed++; }
            }
            venue.UpdatePopularity();
            return new { changed, markup };
        });
        DevCommands.Register(Id, "nc-menu-economics", "Native menu prices and recipe ingredient/cooking costs (read only)", _ =>
        {
            var results = new System.Collections.Generic.List<object>();
            foreach (var venue in Venues.PlayerOwned)
            {
                var rows = new System.Collections.Generic.List<object>();
                foreach (var m in venue.Menu)
                    rows.Add(new { dish = Items.NameOf(m.Meal), price = m.Price,
                        basePrice = m.RecipeDefinition.CurrentBasePrice, ingredientCost = m.RecipeDefinition.IngredientsPrice,
                        cookingCost = m.RecipeDefinition.CookPrice });
                results.Add(new { venue = venue.Venue.Guid, name = Venues.NameOf(venue), menu = rows });
            }
            return results;
        });
        DevCommands.Register(Id, "nc-max-staff-skills", "save=NAME venue=GUID: train existing work skills to their native cap without XP loops", a =>
        {
            RequireLab(a.Get("save", ""));
            if (!Maintenance) throw new InvalidOperationException("hold the lab before training staff");
            var venue = Venues.PlayerOwned.FirstOrDefault(v => v.Venue.Guid == a.Get("venue", ""))
                ?? throw new ArgumentException("owned venue not found");
            var rows = new System.Collections.Generic.List<object>();
            foreach (var member in Venues.StaffOf(venue))
                foreach (var skill in new[] { StaffSkills.Cooking, StaffSkills.Serving, StaffSkills.Cleaning, StaffSkills.Managing })
                {
                    if (skill == null || !StaffSkills.Has(member.Person, skill)) continue;
                    float xp = skill.GetExperienceForLevel(skill.LevelCount);
                    if (!float.IsFinite(xp) || xp < 0) throw new InvalidOperationException("invalid native skill cap");
                    int before = StaffSkills.Level(member.Person, skill);
                    member.Person.RuntimeData.Skills[skill] = Math.Max(xp, StaffSkills.Experience(member.Person, skill));
                    int after = StaffSkills.Level(member.Person, skill);
                    if (after != skill.LevelCount) throw new InvalidOperationException("native skill cap did not match");
                    rows.Add(new { person = member.Person.Guid, skill = StaffSkills.NameOf(skill), before, after });
                }
            return rows;
        });
        DevCommands.Register(Id, "nc-align-hours", "save=NAME venue=GUID: align qualified service teams to opening hours without redundant paid shifts", a =>
        {
            RequireLab(a.Get("save", ""));
            if (!Maintenance) throw new InvalidOperationException("hold the lab before changing staffing");
            var venue = Venues.PlayerOwned.FirstOrDefault(v => v.Venue.Guid == a.Get("venue", ""))
                ?? throw new ArgumentException("owned venue not found");
            return OperatingSetup.Align(venue);
        });
        DevCommands.Register(Id, "nc-refresh-objectives", "save=NAME: reevaluate native venue setup objectives against actual state", a =>
        {
            RequireLab(a.Get("save", ""));
            var manager = Singleton<Nivalis.VenueSupplyQuest.VenueSetupManager>.Instance;
            var results = new System.Collections.Generic.List<object>();
            var quests = new System.Collections.Generic.List<Nivalis.VenueSupplyQuest.RuntimeVenueSetupQuest>();
            foreach (var entry in manager._activeSetupQuests) quests.Add(entry.Value);
            foreach (var quest in quests)
            {
                var trackers = new System.Collections.Generic.List<Nivalis.VenueSupplyQuest.VenueSupplyObjective.RuntimeTracker>();
                foreach (var tracker in quest._activeObjectives) trackers.Add(tracker);
                foreach (var tracker in trackers)
                {
                    bool met = tracker.CheckIsObjectiveAlreadyCompleted(quest.Venue);
                    if (met) tracker.TryCast<Nivalis.VenueSupplyQuest.HireStaffObjective.Tracker>()?.RefreshQuestStatus();
                    results.Add(new { venue = quest.Venue.Guid, objective = tracker.StateEntry?.GetText(),
                        met, state = tracker.State.ToString() });
                }
            }
            return results;
        });
        DevCommands.Register(Id, "nc-prune-staff", "save=NAME venue=GUID keep=comma-separated-person-GUIDs: remove conflicting staff links in held lab", a =>
        {
            RequireLab(a.Get("save", ""));
            if (!Maintenance) throw new InvalidOperationException("hold the lab before repairing roster conflicts");
            var venue = Venues.PlayerOwned.FirstOrDefault(v => v.Venue.Guid == a.Get("venue", ""))
                ?? throw new ArgumentException("owned venue not found");
            var keep = a.Get("keep", "").Split(',').ToHashSet();
            int removed = 0;
            foreach (var member in Venues.StaffOf(venue))
                if (!keep.Contains(member.Person.Guid))
                {
                    Singleton<Nivalis.GhostSystem.CustomerLoop.VenueManager>.Instance.FireStaff(venue.Venue, member.Person);
                    removed++;
                }
            return new { removed, remaining = Venues.StaffOf(venue).Count };
        });
        DevCommands.Register(Id, "nc-repair-staff", "save=NAME venue=GUID: reconnect existing staff through the native hiring manager", a =>
        {
            RequireLab(a.Get("save", ""));
            var venue = Venues.PlayerOwned.FirstOrDefault(v => v.Venue.Guid == a.Get("venue", ""))
                ?? throw new ArgumentException("owned venue not found");
            int repaired = 0;
            foreach (var member in Venues.StaffOf(venue))
            {
                if (member.Person.RuntimeData.WorksAt?.Pointer != venue.Venue.Pointer)
                {
                    Singleton<Nivalis.GhostSystem.CustomerLoop.VenueManager>.Instance.HireStaff(venue.Venue, member.Person);
                    member.Person.SetIsHired(true);
                    repaired++;
                }
                Singleton<Nivalis.GhostSystem.CustomerLoop.VenueManager>.Instance.ChangeStaffTasks(venue.Venue,
                    member.Person.RuntimeData, member.Person.RuntimeData.Tasks);
                member.Person.RuntimeData.ReportTaskChange();
            }
            venue.ReportStaffWorkingHoursChanged();
            return new { venue = venue.Venue.Guid, repaired,
                assigned = Venues.StaffOf(venue).Count(s => s.Person.RuntimeData.WorksAt?.Pointer == venue.Venue.Pointer),
                total = Venues.StaffOf(venue).Count };
        });
        DevCommands.Register(Id, "nc-shopping-list", "Quote every actual shopping-list item and available supplier (read only)", _ => RestockPlanner.ShoppingList());
        if (Config.Bind("Testing", "AllowStockConsumption", false, "Allow bounded one-ingredient consumption in the dedicated lab for refill verification.").Value)
            DevCommands.Register(Id, "nc-test-consume", "save=NAME venue=GUID item=NAME: use exactly one native ingredient to test automatic replenishment", a =>
            {
                RequireLab(a.Get("save", ""));
                if (RestockPurchaser.Busy) throw new InvalidOperationException("wait for the active refill to finish");
                var venue = Venues.PlayerOwned.FirstOrDefault(v => v.Venue.Guid == a.Get("venue", ""))
                    ?? throw new ArgumentException("owned venue not found");
                var item = Items.ByName(a.Get("item", "")) ?? throw new ArgumentException("ingredient not found");
                if (!item.IsIngredient) throw new ArgumentException("ingredient required");
                var used = new Il2CppSystem.Collections.Generic.List<Nivalis.InventorySystem.ItemInstanceData>();
                venue.JointInventory.TakeByType(item, 1, used.Cast<Il2CppSystem.Collections.Generic.IList<Nivalis.InventorySystem.ItemInstanceData>>());
                venue.UpdateSupplyStates();
                return new { consumed = used.Count, remaining = venue.JointInventory.GetItemCount(item) };
            });
        DevCommands.Register(Id, "nc-restock-receipt", "id=UUID: read a saved supply receipt without replaying purchases", a =>
        {
            return RestockPurchaser.Read(a.Get("id", ""));
        });
        DevCommands.Register(Id, "nc-cash-receipt", "id=UUID: read a saved cash settlement", a => CashLedger.Receipt(a.Get("id", "")));
        DevCommands.Register(Id, "nc-cash-settle", "save=NAME pair=UUID id=UUID budget=EDDIES: settle native business proceeds and expenses", a =>
        {
            RequireLab(a.Get("save", ""));
            if (PaperTest) throw new InvalidOperationException("financial operations held during service test");
            if (Pair != a.Get("pair", "")) throw new InvalidOperationException("cash pair must match the lease");
            return CashLedger.Settle(a.Get("id", ""), a.Get("pair", ""), a.GetInt("budget"), LabSave);
        });
        DevCommands.Register(Id, "nc-buy-shopping-list", "save=NAME pair=UUID id=UUID budget=EDDIES: purchase the complete list against a real host reservation", a =>
        {
            RequireLab(a.Get("save", ""));
            if (PaperTest) throw new InvalidOperationException("financial operations held during service test");
            if (Pair != a.Get("pair", "")) throw new InvalidOperationException("host pair does not match the lease");
            return RestockPurchaser.Buy(a.Get("id", ""), a.Get("pair", ""), a.GetInt("budget"), SaveName!);
        });
        DevCommands.Register(Id, "nc-seating", "save=NAME venue=GUID tables=COUNT: fill chairs and safely expand dining capacity in the held sandbox", a =>
        {
            RequireLab(a.Get("save", ""));
            if (!Maintenance) throw new InvalidOperationException("hold the lab before adding furniture");
            var venue = Venues.PlayerOwned.FirstOrDefault(v => v.Venue.Guid == a.Get("venue", ""))
                ?? throw new ArgumentException("owned venue GUID not found");
            return FurnitureSetup.FillSeating(venue, a.GetInt("tables", 0));
        });
        DevCommands.Register(Id, "nc-kitchen", "save=NAME venue=GUID: fill compatible authored kitchen appliance slots", a =>
        {
            RequireLab(a.Get("save", ""));
            var venue = Venues.PlayerOwned.FirstOrDefault(v => v.Venue.Guid == a.Get("venue", ""))
                ?? throw new ArgumentException("owned venue GUID not found");
            return FurnitureSetup.FillKitchen(venue);
        });
        DevCommands.Register(Id, "nc-provision", "save=NAME venue=GUID: configure one venue's authored seating and kitchen after loading", a =>
        {
            RequireLab(a.Get("save", ""));
            var venue = Venues.PlayerOwned.FirstOrDefault(v => v.Venue.Guid == a.Get("venue", ""))
                ?? throw new ArgumentException("owned venue GUID not found");
            return new { venue = venue.Venue.Guid, seating = FurnitureSetup.FillSeating(venue),
                kitchen = FurnitureSetup.FillKitchen(venue) };
        });
        DevCommands.Register(Id, "nc-menu", "save=NAME venue=GUID dish=GUID price=HUNDREDTHS: change one real menu price", a =>
        {
            RequireLab(a.Get("save", ""));
            var venue = Venues.PlayerOwned.FirstOrDefault(v => v.Venue.Guid == a.Get("venue", ""))
                ?? throw new ArgumentException("owned venue GUID not found");
            int price = a.GetInt("price");
            if (price < 100 || price > 1_000_000) throw new ArgumentOutOfRangeException("price");
            foreach (var entry in venue.Menu)
                if (entry.Meal.Guid == a.Get("dish", ""))
                {
                    entry.Price = price;
                    if (!Singleton<SerializationManager>.Instance.Save(LabSave, false))
                        throw new InvalidOperationException("menu changed but checkpoint failed");
                    return new { dish = entry.Meal.Guid, priceHundredths = entry.Price };
                }
            throw new ArgumentException("dish is not on this venue's menu");
        });
        DevCommands.Register(Id, "nc-setup", "save=NAME: max and configure acquireable venues, recipes and staff", a =>
        {
            RequireLab(a.Get("save", ""));
            SandboxEnabled = true;
            var result = SandboxSetup.Apply();
            CashLedger.Initialize();
            return result;
        });
        DevCommands.Register(Id, "nc-lease", "pair=ID save=NAME seconds=1..15: run paired lab save in background", a =>
        {
            RequireLab(a.Get("save", ""));
            if (Maintenance) throw new InvalidOperationException("sandbox held for setup; enable business operation after validation");
            var pair = a.Get("pair", "");
            if (!Guid.TryParse(pair, out _)) throw new ArgumentException("pair must be a UUID");
            if (Pair != null && Pair != pair && Time.realtimeSinceStartupAsDouble < LeaseUntil)
                throw new InvalidOperationException("different host has an active lease");
            Pair = pair;
            LeaseUntil = Time.realtimeSinceStartupAsDouble + Math.Clamp(a.GetInt("seconds", 10), 1, 15);
            if (!RestockPurchaser.Busy) Unfreeze();
            Application.runInBackground = true;
            return Status();
        });
        DevCommands.Register(Id, "nc-release", "Release host lease and pause paired business clock", _ =>
        {
            LeaseUntil = 0;
            if (SandboxEnabled) Freeze();
            Pair = null;
            Application.runInBackground = OriginalBackground;
            return Status();
        });
        DevCommands.Register(Id, "nc-recipes", "save=NAME: discover every existing recipe in the paired lab save", a =>
        {
            RequireLab(a.Get("save", ""));
            if (!Singleton<MealDatabase>.InstanceExist(out var db)) throw new InvalidOperationException("meal database not ready");
            int before = db.knownRecipes.Count;
            // Snapshot keys first: discovery can change runtime recipe maps.
            var defs = new System.Collections.Generic.List<MealRecipeDefinition>();
            foreach (var recipe in db._guidMealRecipeMap.Values) defs.Add(recipe);
            foreach (var recipe in defs)
                if (!db.knownRecipes.Contains(recipe)) db.DiscoverRecipe(recipe);
            return new { before, after = db.knownRecipes.Count, available = defs.Count };
        });
        AddComponent<LeaseWatchdog>();
        Log.LogInfo("Night City guest ready; observe-only until a dedicated lab save is explicitly paired.");
    }

    private void RequireLab(string save)
    {
        if (!GameEvents.IsInGame) throw new InvalidOperationException("load the lab save first");
        if (SaveName != save || save != LabSave)
            throw new InvalidOperationException("mutations require the currently loaded dedicated lab save");
    }

    private void Reset()
    {
        OperatingSetup.RestoreDefinitions();
        PaperTest = false;
        RestockPurchaser.Cancel();
        Unfreeze();
        SandboxEnabled = false;
        SandboxSetup.RestoreCurfewFlags();
        Pair = null; SaveName = null; LeaseUntil = 0;
        Application.runInBackground = OriginalBackground;
    }

    internal void BeforeLoad() => Reset();
    internal void HoldPurchase() => Freeze();
    internal void FinishPurchase(bool failed = false)
    {
        if (failed) Maintenance = true;
        if (!Maintenance && Time.realtimeSinceStartupAsDouble < LeaseUntil) Unfreeze();
    }

    internal void Tick()
    {
        if (SandboxEnabled && GameEvents.IsInGame)
        {
            SandboxSetup.DisableCurfew();
            SandboxSetup.CancelStory();
            if (Time.realtimeSinceStartupAsDouble >= LeaseUntil && !SimulationFrozen)
            {
                Freeze();
                Log.LogWarning("Host lease expired; paired business clock and simulation paused.");
            }
        }
    }

    private void Freeze()
    {
        ClockPause ??= GameClock.Pause(Id);
        if (SimulationFrozen) return;
        PreviousTimeScale = Time.timeScale;
        Time.timeScale = 0;
        SimulationFrozen = true;
    }

    private void Unfreeze()
    {
        ClockPause?.Dispose(); ClockPause = null;
        if (!SimulationFrozen) return;
        if (Time.timeScale == 0) Time.timeScale = PreviousTimeScale;
        SimulationFrozen = false;
    }

    private static bool TitleReady()
    {
        if (GameEvents.IsInGame || Singleton<GameSceneManager>.Instance.IsLoading || GameSceneManager.IsUnloadingGameplay) return false;
        return UnityEngine.Object.FindObjectsOfType<MainMenuUI>().Any(menu => menu.title && menu.isActiveAndEnabled && menu.newGameButton != null && menu.newGameButton.interactable);
    }

    private object Status() => new
    {
        version = "0.1.0", ready = GameEvents.IsInGame, titleReady = TitleReady(),
        loading = Singleton<GameSceneManager>.InstanceExist(out var scenes) ? scenes.IsLoading : true,
        save = SaveName, pair = Pair,
        leaseActive = Pair != null && Time.realtimeSinceStartupAsDouble < LeaseUntil,
        paused = GameClock.IsPaused, background = Application.runInBackground,
        sandbox = SandboxEnabled, simulationFrozen = SimulationFrozen,
        maintenance = Maintenance,
        paperTest = PaperTest,
        purchaseInProgress = RestockPurchaser.Busy,
        curfew = NivalisModKit.Security.IsCurfew, curfewSecurity = NivalisModKit.Security.IsSecurityActive,
        day = GameTime.Day, hour = GameTime.Hour, minute = GameTime.Minute,
        moneyHundredths = Economy.PlayerMoney,
        lastRestockDay = GameEvents.IsInGame ? SaveData.For(Id).Get("last-restock-day", -1) : -1,
        cashNetEddies = GameEvents.IsInGame ? CashLedger.Delta() : 0,
        lastCashDay = GameEvents.IsInGame ? SaveData.For(Id).Get("last-cash-day", -1) : -1,
        capabilities = new[] { "venue_snapshot", "discover_recipes", "background_lease", "sandbox_setup", "disable_curfew" }
    };

    private static string DisplayName(Nivalis.GhostSystem.CustomerLoop.VenueAreaGhost venue)
    {
        try { var name = venue.Venue.EntryName; if (!string.IsNullOrWhiteSpace(name)) return name; }
        catch { }
        return Venues.NameOf(venue);
    }

    private object Snapshot()
    {
        if (!GameEvents.IsInGame) throw new InvalidOperationException("load a save first");
        return new
        {
            status = Status(),
            venues = Venues.PlayerOwned.Select(v => new
            {
                id = v.Venue.Guid, name = DisplayName(v), owned = v.PlayerOwned,
                level = v.CurrentLevel, ready = v.IsReadyToOpen,
                seating = v.HasSeating, menuCount = v.Menu.Count, staffCount = Venues.StaffOf(v).Count,
                openStart = v.Venue.OpenTime.x, openEnd = v.Venue.OpenTime.y,
                tableLimit = v.TableLimit,
                diningTables = Enumerable.Range(0, v.Tables.Count).Count(i => v.Tables[i].Value?.ChairSpots.Count > 0),
                seats = Enumerable.Range(0, v.Tables.Count).Sum(i => v.Tables[i].Value?.Chairs.Count ?? 0),
                mealsServed = Venues.MealsServedOf(v), customers = v.TotalVisitsCount,
                receivingCustomers = v.IsReadyToReceiveCustomers,
                popularity = v.AveragePopularity, reviewScore = v.GetReviewScore(),
                craftingErrors = v.craftingErrors.ToArray(),
                menu = Venues.MenuOf(v).Select(m => new { id = m.Dish.Guid, dish = Items.NameOf(m.Dish), priceHundredths = m.Price,
                    supply = m.Recipe == null ? "definition default" : v.GetCurrentSupplyState(m.Recipe).ToString(),
                    ingredients = m.Ingredients.Select(i => new { item = Items.NameOf(i.Item), amount = i.Amount }).ToArray() }).ToArray(),
                stock = Venues.StockOf(v).Select(k => new { item = Items.NameOf(k.Key), amount = k.Value }).ToArray(),
                storage = Venues.StorageOf(v),
                staff = Venues.StaffOf(v).Select(s => new { s.Name, person = s.Person.Guid, wageHundredths = s.Wage, s.ShiftStart, s.ShiftEnd, s.Roles, s.HoursLeftToday,
                    workplace = s.Person.RuntimeData.WorksAt?.Guid,
                    enabled = s.Person.IsEnabled, bot = s.Person.StaffBot,
                    agent = s.Person.RuntimeData.MyGhost.Value?.Id,
                    action = s.Person.RuntimeData.MyGhost.Value?.CurrentAction?.Type?.name,
                    cookingLevel = StaffSkills.Level(s.Person, StaffSkills.Cooking),
                    servingLevel = StaffSkills.Level(s.Person, StaffSkills.Serving),
                    happiness = s.Person.RuntimeData.WorkSatisfaction.Happiness.ToString(), happinessValue = s.Person.RuntimeData.WorkSatisfaction.Value,
                    quitting = s.Person.RuntimeData.WorkQuit }).ToArray(),
                receipts = Venues.ReceiptsOf(v).Select(r => new { r.Type, r.Day, r.DaySeconds, amountHundredths = r.Amount, r.Count }).ToArray(),
                reviews = Venues.ReviewsOf(v).Select(r => new { r.Score, r.Reviewer, r.GameSeconds, r.OrderTaken,
                    r.AllFoodDelivered, r.ServiceQuality, r.Cleanliness, r.Comfort }).ToArray(),
                orders = Venues.OrdersOf(v).Select(o => new { o.Id, o.Price, o.Prepared, o.Delivered }).ToArray()
            }).ToArray()
        };
    }
}

public sealed class LeaseWatchdog : MonoBehaviour
{
    public LeaseWatchdog(IntPtr pointer) : base(pointer) { }
    public void Update() => Plugin.Active?.Tick();
}

