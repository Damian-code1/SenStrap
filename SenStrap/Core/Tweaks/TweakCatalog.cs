using System.Globalization;
using Microsoft.Win32;

namespace SenStrap.Core.Tweaks;

public static class TweakIds
{
    public const string RobloxGpu = "roblox.gpu";
    public const string RobloxPriority = "roblox.priority";
    public const string RobloxEcoQos = "roblox.ecoqos";
    public const string RobloxFso = "roblox.fso";
    public const string RobloxDpi = "roblox.dpi";
    public const string FpsAccess = "roblox.fpsaccess";
    public const string RobloxPriorityHigh = "roblox.priorityhigh";
}

/// <summary>
/// Every optimization SenStrap offers. Nothing here turns off security features (Defender, UAC,
/// Windows Update, firewall, mitigations) and nothing here is a placebo registry value.
/// </summary>
public static class TweakCatalog
{
    private const string Cdm = @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager";
    private const string ExplorerAdvanced = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
    private const string GameBar = @"Software\Microsoft\GameBar";
    private const string Desktop = @"Control Panel\Desktop";
    private const string Mouse = @"Control Panel\Mouse";
    private const string Keyboard = @"Control Panel\Keyboard";
    private const string Accessibility = @"Control Panel\Accessibility";
    private const string Multimedia = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile";
    private const string Services = @"SYSTEM\CurrentControlSet\Services";

    private static readonly Lazy<IReadOnlyList<Tweak>> _all = new(Build);

    public static IReadOnlyList<Tweak> All => _all.Value;

    public static Tweak? Find(string id) => All.FirstOrDefault(t => t.Id == id);

    private static int WindowsBuild => Environment.OSVersion.Version.Build;

    private static bool IsWindows11 => WindowsBuild >= 22000;

    private static string LocalAppData => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    private static string LocalLow =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "LocalLow");

    private static RegValue Cu(string key, string name, object on, object? absentMeans = null, object? def = null) =>
        new(Hive.HKCU, key, name, on) { AbsentMeans = absentMeans, Default = def };

    private static RegValue CuText(string key, string name, string on, string? absentMeans = null, string? def = null) =>
        new(Hive.HKCU, key, name, on, RegistryValueKind.String) { AbsentMeans = absentMeans, Default = def };

    private static RegValue Lm(string key, string name, object on, object? absentMeans = null, object? def = null) =>
        new(Hive.HKLM, key, name, on) { AbsentMeans = absentMeans, Default = def };

    private static IReadOnlyList<Tweak> Build()
    {
        var list = new List<Tweak>();
        AddRoblox(list);
        AddGaming(list);
        AddInput(list);
        AddVisual(list);
        AddPrivacy(list);
        AddSystem(list);
        AddPower(list);
        AddServices(list);
        AddExplorer(list);
        AddCleanup(list);
        AddMore(list);
        return list;
    }

    // ---------- Roblox ----------

    private static void AddRoblox(List<Tweak> list)
    {
        list.Add(new LaunchOptionTweak
        {
            Id = TweakIds.RobloxGpu, Category = TweakCategory.Roblox, Recommended = true, Note = TweakNote.NextLaunch,
            Name = "Use the high-performance GPU",
            NameEs = "Usar la GPU de alto rendimiento",
            Description = "On laptops with two graphics cards, Roblox always runs on the dedicated one.",
            DescriptionEs = "En portátiles con dos tarjetas gráficas, Roblox siempre usa la dedicada.",
            OnChange = LaunchTweaks.Refresh,
        });
        list.Add(new LaunchOptionTweak
        {
            Id = TweakIds.RobloxPriority, Category = TweakCategory.Roblox, Recommended = true, Note = TweakNote.NextLaunch, ExclusiveGroup = "priority",
            Name = "Above-normal CPU priority",
            NameEs = "Prioridad de CPU superior a la normal",
            Description = "Roblox gets the processor first when other programs are busy in the background.",
            DescriptionEs = "Roblox usa el procesador antes que los programas que trabajan en segundo plano.",
        });
        list.Add(new LaunchOptionTweak
        {
            Id = TweakIds.RobloxEcoQos, Category = TweakCategory.Roblox, Recommended = true, Note = TweakNote.NextLaunch,
            Name = "Don't throttle Roblox in the background",
            NameEs = "No frenar Roblox en segundo plano",
            Description = "Windows won't slow Roblox down to save power when it's minimized or behind another window.",
            DescriptionEs = "Windows no ralentiza Roblox para ahorrar energía cuando está minimizado o detrás de otra ventana.",
        });
        list.Add(new LaunchOptionTweak
        {
            Id = TweakIds.RobloxFso, Category = TweakCategory.Roblox, Note = TweakNote.NextLaunch,
            Name = "Disable fullscreen optimizations",
            NameEs = "Desactivar optimizaciones de pantalla completa",
            Description = "Can reduce input lag in exclusive fullscreen on some PCs.",
            DescriptionEs = "Puede reducir el retraso de entrada en pantalla completa exclusiva en algunos PCs.",
            OnChange = LaunchTweaks.Refresh,
        });
        list.Add(new LaunchOptionTweak
        {
            Id = TweakIds.RobloxDpi, Category = TweakCategory.Roblox, Note = TweakNote.NextLaunch,
            Name = "Ignore Windows display scaling",
            NameEs = "Ignorar la escala de pantalla de Windows",
            Description = "Roblox renders at native resolution even with 125% scaling or more.",
            DescriptionEs = "Roblox se dibuja a resolución nativa aunque Windows use una escala del 125 % o más.",
            OnChange = LaunchTweaks.Refresh,
        });
        list.Add(new GroupMembershipTweak
        {
            Id = TweakIds.FpsAccess, Category = TweakCategory.Roblox, Admin = true, Note = TweakNote.SignOut,
            GroupSid = "S-1-5-32-559", HiddenUnlessApplied = true,
            Name = "FPS counter permission (removed feature)",
            NameEs = "Permiso del contador de FPS (función eliminada)",
            Description = "Left over from the FPS counter in 1.5.0. Turn it off to take your account out of Windows' Performance Log Users group again.",
            DescriptionEs = "Quedó del contador de FPS de la 1.5.0. Desactívalo para sacar tu cuenta del grupo Usuarios del registro de rendimiento de Windows.",
        });
        list.Add(new CleanupTweak
        {
            Id = "roblox.cache", Category = TweakCategory.Roblox, NeedsRobloxClosed = true,
            Name = "Clear Roblox's cache",
            NameEs = "Borrar la caché de Roblox",
            Description = "Deletes downloaded assets. Fixes textures or sounds that don't load. Games load a bit slower the first time.",
            DescriptionEs = "Borra los recursos descargados. Arregla texturas o sonidos que no cargan. Los juegos tardan un poco más la primera vez.",
            Folders = () => [Path.Combine(Path.GetTempPath(), "Roblox")],
        });
        list.Add(new CleanupTweak
        {
            Id = "roblox.logs", Category = TweakCategory.Roblox, NeedsRobloxClosed = true,
            Name = "Delete Roblox logs",
            NameEs = "Borrar los registros de Roblox",
            Description = "Roblox writes a log for every session and never deletes them.",
            DescriptionEs = "Roblox guarda un registro de cada sesión y nunca los borra.",
            Folders = () => [Path.Combine(LocalAppData, "Roblox", "logs")],
        });
    }

    // ---------- Gaming ----------

    private static void AddGaming(List<Tweak> list)
    {
        list.Add(new RegistryTweak
        {
            Id = "gaming.gamemode", Category = TweakCategory.Gaming, Recommended = true,
            Name = "Game Mode",
            NameEs = "Modo de juego",
            Description = "Windows pauses updates and gives the game priority while you play.",
            DescriptionEs = "Windows pausa las actualizaciones y da prioridad al juego mientras juegas.",
            Values = [Cu(GameBar, "AutoGameModeEnabled", 1, absentMeans: 1), Cu(GameBar, "AllowAutoGameMode", 1, absentMeans: 1)],
        });
        list.Add(new RegistryTweak
        {
            Id = "gaming.dvr", Category = TweakCategory.Gaming, Recommended = true,
            Name = "Turn off background recording",
            NameEs = "Desactivar la grabación en segundo plano",
            Description = "Stops Xbox Game Bar from recording your gameplay all the time. Saves GPU and disk.",
            DescriptionEs = "Evita que la Xbox Game Bar grabe tus partidas todo el rato. Ahorra GPU y disco.",
            Values =
            [
                Cu(@"System\GameConfigStore", "GameDVR_Enabled", 0, absentMeans: 1, def: 1),
                Cu(@"Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", 0, absentMeans: 1),
                Cu(@"Software\Microsoft\Windows\CurrentVersion\GameDVR", "HistoricalCaptureEnabled", 0, absentMeans: 0),
            ],
        });
        list.Add(new RegistryTweak
        {
            Id = "gaming.gamebartips", Category = TweakCategory.Gaming, Recommended = true,
            Name = "No Game Bar tips when a game starts",
            NameEs = "Sin consejos de la Game Bar al abrir un juego",
            Description = "Hides the \"Press Win+G\" popup.",
            DescriptionEs = "Oculta el aviso de \"Pulsa Win+G\".",
            Values = [Cu(GameBar, "ShowStartupPanel", 0, absentMeans: 1)],
        });
        list.Add(new RegistryTweak
        {
            Id = "gaming.gamebarbutton", Category = TweakCategory.Gaming,
            Name = "Xbox button doesn't open Game Bar",
            NameEs = "El botón Xbox no abre la Game Bar",
            Description = "Pressing the Xbox button on a controller stops opening Game Bar in the middle of a game.",
            DescriptionEs = "Pulsar el botón Xbox del mando deja de abrir la Game Bar en mitad de una partida.",
            Values = [Cu(GameBar, "UseNexusForGameBarEnabled", 0, absentMeans: 1)],
        });
        list.Add(new DxTokenTweak
        {
            Id = "gaming.windowed", Category = TweakCategory.Gaming, Recommended = true, Note = TweakNote.NextLaunch,
            Token = "SwapEffectUpgradeEnable",
            Name = "Optimizations for windowed games",
            NameEs = "Optimizaciones para juegos en ventana",
            Description = "Lower latency for games in a window or borderless, like Roblox. Same as Windows Graphics settings.",
            DescriptionEs = "Menos latencia en juegos en ventana o sin bordes, como Roblox. Igual que en Configuración > Gráficos.",
        });
        list.Add(new DxTokenTweak
        {
            Id = "gaming.vrr", Category = TweakCategory.Gaming, Note = TweakNote.NextLaunch,
            Token = "VRROptimizeEnable",
            Name = "Variable refresh rate optimization",
            NameEs = "Optimización de frecuencia variable",
            Description = "Lets G-Sync and FreeSync monitors work with games that don't support them on their own.",
            DescriptionEs = "Permite que los monitores G-Sync y FreeSync funcionen con juegos que no los admiten por sí mismos.",
        });
        list.Add(new RegistryTweak
        {
            Id = "gaming.hags", Category = TweakCategory.Gaming, Admin = true, Note = TweakNote.Restart,
            Name = "Hardware-accelerated GPU scheduling",
            NameEs = "Programación de GPU acelerada por hardware",
            Description = "The graphics card manages its own memory. Can lower latency on recent GPUs and drivers.",
            DescriptionEs = "La tarjeta gráfica gestiona su propia memoria. Puede bajar la latencia con GPUs y drivers recientes.",
            Values = [Lm(@"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode", 2)],
        });
    }

    // ---------- Input ----------

    private static void AddInput(List<Tweak> list)
    {
        list.Add(new RegistryTweak
        {
            Id = "input.mouseaccel", Category = TweakCategory.Input, Recommended = true,
            Name = "Turn off mouse acceleration",
            NameEs = "Desactivar la aceleración del ratón",
            Description = "Turns off \"Enhance pointer precision\": the pointer moves the same distance no matter how fast you move the mouse.",
            DescriptionEs = "Desactiva \"Mejorar la precisión del puntero\": el puntero se mueve lo mismo sin importar la velocidad del ratón.",
            Values =
            [
                CuText(Mouse, "MouseSpeed", "0", def: "1"),
                CuText(Mouse, "MouseThreshold1", "0", def: "6"),
                CuText(Mouse, "MouseThreshold2", "0", def: "10"),
            ],
            AfterChange = _ => LiveSettings.PushMouse(),
        });
        list.Add(new RegistryTweak
        {
            Id = "input.keyrepeat", Category = TweakCategory.Input,
            Name = "Fastest key repeat",
            NameEs = "Repetición de teclas más rápida",
            Description = "Shortest delay before a held key starts repeating, and the fastest repeat rate.",
            DescriptionEs = "El menor retraso antes de que una tecla mantenida se repita, y la repetición más rápida.",
            Values = [CuText(Keyboard, "KeyboardDelay", "0", def: "1"), CuText(Keyboard, "KeyboardSpeed", "31", def: "31")],
            AfterChange = _ => LiveSettings.PushKeyboard(),
        });
        list.Add(new FlagBitTweak
        {
            Id = "input.stickykeys", Category = TweakCategory.Input, Recommended = true,
            Key = $@"{Accessibility}\StickyKeys", Bit = 0x4, DefaultFlags = 510,
            Name = "No Sticky Keys popup",
            NameEs = "Sin aviso de Teclas especiales",
            Description = "Pressing Shift five times no longer opens the Sticky Keys window. Sticky Keys itself still works if you use it.",
            DescriptionEs = "Pulsar Mayús cinco veces ya no abre la ventana de Teclas especiales. Si las usas, siguen funcionando.",
            AfterChange = _ => LiveSettings.PushStickyKeys(),
        });
        list.Add(new FlagBitTweak
        {
            Id = "input.filterkeys", Category = TweakCategory.Input, Recommended = true,
            Key = $@"{Accessibility}\Keyboard Response", Bit = 0x4, DefaultFlags = 126,
            Name = "No Filter Keys popup",
            NameEs = "Sin aviso de Teclas filtro",
            Description = "Holding right Shift for 8 seconds no longer turns on Filter Keys.",
            DescriptionEs = "Mantener Mayús derecha 8 segundos ya no activa las Teclas filtro.",
            AfterChange = _ => LiveSettings.PushFilterKeys(),
        });
        list.Add(new FlagBitTweak
        {
            Id = "input.togglekeys", Category = TweakCategory.Input, Recommended = true,
            Key = $@"{Accessibility}\ToggleKeys", Bit = 0x4, DefaultFlags = 62,
            Name = "No Toggle Keys popup",
            NameEs = "Sin aviso de Teclas de alternancia",
            Description = "Holding Num Lock for 5 seconds no longer turns on Toggle Keys.",
            DescriptionEs = "Mantener Bloq Num 5 segundos ya no activa las Teclas de alternancia.",
            AfterChange = _ => LiveSettings.PushToggleKeys(),
        });
    }

    // ---------- Visual ----------

    private static void AddVisual(List<Tweak> list)
    {
        list.Add(new RegistryTweak
        {
            Id = "visual.minmax", Category = TweakCategory.Visual, Recommended = true,
            Name = "No minimize and maximize animations",
            NameEs = "Sin animaciones al minimizar y maximizar",
            Description = "Windows (and Alt+Tab out of a game) open and close instantly.",
            DescriptionEs = "Las ventanas (y salir de un juego con Alt+Tab) se abren y cierran al instante.",
            Values = [CuText($@"{Desktop}\WindowMetrics", "MinAnimate", "0", absentMeans: "1", def: "1")],
            AfterChange = _ => LiveSettings.PushMinAnimate(),
        });
        list.Add(new SpiTweak
        {
            Id = "visual.effects", Category = TweakCategory.Visual, Recommended = true,
            Settings = [(0x1002, 0x1003), (0x1004, 0x1005), (0x1006, 0x1007), (0x1014, 0x1015), (0x1016, 0x1017), (0x1042, 0x1043)],
            Name = "No fade and slide effects",
            NameEs = "Sin efectos de desvanecer y deslizar",
            Description = "Menus, tooltips, lists and buttons show up instantly instead of fading or sliding in.",
            DescriptionEs = "Los menús, la información sobre herramientas, las listas y los botones aparecen al instante.",
        });
        list.Add(new RegistryTweak
        {
            Id = "visual.menudelay", Category = TweakCategory.Visual, Recommended = true,
            Name = "Faster menus",
            NameEs = "Menús más rápidos",
            Description = "Submenus open after 100 ms instead of 400 ms.",
            DescriptionEs = "Los submenús se abren a los 100 ms en lugar de 400 ms.",
            Values = [CuText(Desktop, "MenuShowDelay", "100", absentMeans: "400", def: "400")],
            AfterChange = _ => LiveSettings.PushMenuDelay(),
        });
        list.Add(new RegistryTweak
        {
            Id = "visual.taskbaranim", Category = TweakCategory.Visual, Recommended = true, Note = TweakNote.Explorer,
            Name = "No taskbar animations",
            NameEs = "Sin animaciones en la barra de tareas",
            Description = "Taskbar buttons and thumbnails appear without animating.",
            DescriptionEs = "Los botones y miniaturas de la barra de tareas aparecen sin animación.",
            Values = [Cu(ExplorerAdvanced, "TaskbarAnimations", 0, absentMeans: 1)],
        });
        list.Add(new RegistryTweak
        {
            Id = "visual.startupdelay", Category = TweakCategory.Visual, Recommended = true,
            Name = "No startup delay",
            NameEs = "Sin retraso al iniciar",
            Description = "Apps that start with Windows open right away instead of waiting about 10 seconds.",
            DescriptionEs = "Los programas que arrancan con Windows se abren al momento en lugar de esperar unos 10 segundos.",
            Values = [Cu(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Serialize", "StartupDelayInMSec", 0)],
        });
        list.Add(new RegistryTweak
        {
            Id = "visual.transparency", Category = TweakCategory.Visual,
            Name = "Turn off transparency",
            NameEs = "Desactivar la transparencia",
            Description = "Taskbar, Start and windows become solid. Uses a little less GPU.",
            DescriptionEs = "La barra de tareas, Inicio y las ventanas pasan a ser opacos. Usa un poco menos de GPU.",
            Values = [Cu(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "EnableTransparency", 0, absentMeans: 1, def: 1)],
            AfterChange = _ => Native.BroadcastSettingChange("ImmersiveColorSet"),
        });
        list.Add(new RegistryTweak
        {
            Id = "visual.peek", Category = TweakCategory.Visual, Note = TweakNote.Explorer,
            Name = "Turn off Peek",
            NameEs = "Desactivar Asomar",
            Description = "Hovering the corner of the taskbar no longer shows the desktop.",
            DescriptionEs = "Pasar el ratón por la esquina de la barra de tareas ya no muestra el escritorio.",
            Values = [Cu(@"Software\Microsoft\Windows\DWM", "EnableAeroPeek", 0, absentMeans: 1)],
        });
        list.Add(new RegistryTweak
        {
            Id = "visual.bgapps", Category = TweakCategory.Visual, Caution = true,
            Name = "Stop Store apps running in the background",
            NameEs = "Apps de la Store sin segundo plano",
            Description = "Saves memory and CPU. Store apps like Mail or Phone Link won't notify you until you open them.",
            DescriptionEs = "Ahorra memoria y CPU. Apps de la Store como Correo o Enlace Móvil no avisan hasta que las abras.",
            Values =
            [
                Cu(@"Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications", "GlobalUserDisabled", 1, absentMeans: 0),
                Cu(@"Software\Microsoft\Windows\CurrentVersion\Search", "BackgroundAppGlobalToggle", 0, absentMeans: 1),
            ],
        });
    }

    // ---------- Privacy ----------

    private static void AddPrivacy(List<Tweak> list)
    {
        list.Add(new RegistryTweak
        {
            Id = "privacy.adid", Category = TweakCategory.Privacy, Recommended = true,
            Name = "Turn off the advertising ID",
            NameEs = "Desactivar el identificador de publicidad",
            Description = "Apps can't use an ID to show you personalized ads.",
            DescriptionEs = "Las apps no pueden usar un identificador para mostrarte anuncios personalizados.",
            Values = [Cu(@"Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled", 0, absentMeans: 1)],
        });
        list.Add(new RegistryTweak
        {
            Id = "privacy.tailored", Category = TweakCategory.Privacy, Recommended = true,
            Name = "No tailored experiences",
            NameEs = "Sin experiencias personalizadas",
            Description = "Microsoft doesn't use your diagnostic data for tips and ads.",
            DescriptionEs = "Microsoft no usa tus datos de diagnóstico para consejos y anuncios.",
            Values = [Cu(@"Software\Microsoft\Windows\CurrentVersion\Privacy", "TailoredExperiencesWithDiagnosticDataEnabled", 0, absentMeans: 1)],
        });
        list.Add(new RegistryTweak
        {
            Id = "privacy.tips", Category = TweakCategory.Privacy, Recommended = true,
            Name = "No tips and suggestions",
            NameEs = "Sin consejos ni sugerencias",
            Description = "No more \"tips and tricks\" notifications, welcome screens after updates or \"finish setting up\" reminders.",
            DescriptionEs = "Se acabaron las notificaciones de consejos, las pantallas de bienvenida tras actualizar y los avisos de \"termina de configurar\".",
            Values =
            [
                Cu(Cdm, "SubscribedContent-338389Enabled", 0, absentMeans: 1),
                Cu(Cdm, "SoftLandingEnabled", 0, absentMeans: 1),
                Cu(Cdm, "SubscribedContent-310093Enabled", 0, absentMeans: 1),
                Cu(@"Software\Microsoft\Windows\CurrentVersion\UserProfileEngagement", "ScoobeSystemSettingEnabled", 0, absentMeans: 1),
            ],
        });
        list.Add(new RegistryTweak
        {
            Id = "privacy.start", Category = TweakCategory.Privacy, Recommended = true,
            Name = "No suggestions in Start",
            NameEs = "Sin sugerencias en Inicio",
            Description = "Start stops recommending apps from the Store and promoted content.",
            DescriptionEs = "Inicio deja de recomendar apps de la Store y contenido promocionado.",
            Values =
            [
                Cu(Cdm, "SystemPaneSuggestionsEnabled", 0, absentMeans: 1),
                Cu(Cdm, "SubscribedContent-338388Enabled", 0, absentMeans: 1),
                Cu(ExplorerAdvanced, "Start_IrisRecommendations", 0, absentMeans: 1),
            ],
        });
        list.Add(new RegistryTweak
        {
            Id = "privacy.silentapps", Category = TweakCategory.Privacy, Recommended = true,
            Name = "Don't install suggested apps",
            NameEs = "No instalar apps sugeridas",
            Description = "Windows stops installing promoted apps and games on its own.",
            DescriptionEs = "Windows deja de instalar por su cuenta apps y juegos promocionados.",
            Values =
            [
                Cu(Cdm, "SilentInstalledAppsEnabled", 0, absentMeans: 1),
                Cu(Cdm, "OemPreInstalledAppsEnabled", 0, absentMeans: 1),
                Cu(Cdm, "PreInstalledAppsEnabled", 0, absentMeans: 1),
            ],
        });
        list.Add(new RegistryTweak
        {
            Id = "privacy.bing", Category = TweakCategory.Privacy, Recommended = true, Note = TweakNote.Explorer,
            Name = "No web results in Start search",
            NameEs = "Sin resultados web al buscar en Inicio",
            Description = "Searching from Start or the taskbar only looks on your PC, without Bing.",
            DescriptionEs = "Buscar desde Inicio o la barra de tareas solo busca en tu PC, sin Bing.",
            Values =
            [
                Cu(@"Software\Microsoft\Windows\CurrentVersion\Search", "BingSearchEnabled", 0, absentMeans: 1),
                Cu(@"Software\Policies\Microsoft\Windows\Explorer", "DisableSearchBoxSuggestions", 1, absentMeans: 0),
            ],
        });
        list.Add(new RegistryTweak
        {
            Id = "privacy.lockscreen", Category = TweakCategory.Privacy, Recommended = true,
            Name = "No tips on the lock screen",
            NameEs = "Sin consejos en la pantalla de bloqueo",
            Description = "Removes the \"fun facts and tips\" shown on the lock screen.",
            DescriptionEs = "Quita las curiosidades y consejos de la pantalla de bloqueo.",
            Values =
            [
                Cu(Cdm, "RotatingLockScreenOverlayEnabled", 0, absentMeans: 1),
                Cu(Cdm, "SubscribedContent-338387Enabled", 0, absentMeans: 1),
            ],
        });
        list.Add(new RegistryTweak
        {
            Id = "privacy.settingsads", Category = TweakCategory.Privacy, Recommended = true,
            Name = "No suggested content in Settings",
            NameEs = "Sin contenido sugerido en Configuración",
            Description = "Settings stops showing suggestions and promotions.",
            DescriptionEs = "Configuración deja de mostrar sugerencias y promociones.",
            Values =
            [
                Cu(Cdm, "SubscribedContent-338393Enabled", 0, absentMeans: 1),
                Cu(Cdm, "SubscribedContent-353694Enabled", 0, absentMeans: 1),
                Cu(Cdm, "SubscribedContent-353696Enabled", 0, absentMeans: 1),
            ],
        });
        list.Add(new RegistryTweak
        {
            Id = "privacy.typing", Category = TweakCategory.Privacy, Recommended = true,
            Name = "Don't send typing and inking data",
            NameEs = "No enviar datos de escritura",
            Description = "What you type and write isn't collected to build a personal dictionary.",
            DescriptionEs = "Lo que escribes no se recopila para crear un diccionario personal.",
            Values =
            [
                Cu(@"Software\Microsoft\InputPersonalization", "RestrictImplicitInkCollection", 1, absentMeans: 0),
                Cu(@"Software\Microsoft\InputPersonalization", "RestrictImplicitTextCollection", 1, absentMeans: 0),
                Cu(@"Software\Microsoft\InputPersonalization\TrainedDataStore", "HarvestContacts", 0, absentMeans: 1),
                Cu(@"Software\Microsoft\Personalization\Settings", "AcceptedPrivacyPolicy", 0, absentMeans: 1),
                Cu(@"Software\Microsoft\Input\TIPC", "Enabled", 0, absentMeans: 1),
            ],
        });
        list.Add(new RegistryTweak
        {
            Id = "privacy.feedback", Category = TweakCategory.Privacy, Recommended = true,
            Name = "No feedback requests",
            NameEs = "Sin peticiones de opinión",
            Description = "Windows stops asking you to rate it.",
            DescriptionEs = "Windows deja de pedirte que lo valores.",
            Values = [Cu(@"Software\Microsoft\Siuf\Rules", "NumberOfSIUFInPeriod", 0)],
        });
        list.Add(new RegistryTweak
        {
            Id = "privacy.langlist", Category = TweakCategory.Privacy, Recommended = true,
            Name = "Hide your language list from websites",
            NameEs = "Ocultar tus idiomas a las webs",
            Description = "Websites can't read the list of languages installed on your PC.",
            DescriptionEs = "Las webs no pueden leer la lista de idiomas instalados en tu PC.",
            Values = [Cu(@"Control Panel\International\User Profile", "HttpAcceptLanguageOptOut", 1, absentMeans: 0)],
        });
        list.Add(new RegistryTweak
        {
            Id = "privacy.recall", Category = TweakCategory.Privacy, Recommended = true,
            Name = "Turn off Recall snapshots",
            NameEs = "Desactivar las capturas de Recall",
            Description = "On Copilot+ PCs, Windows stops saving snapshots of your screen.",
            DescriptionEs = "En los PCs Copilot+, Windows deja de guardar capturas de tu pantalla.",
            Values = [Cu(@"Software\Policies\Microsoft\Windows\WindowsAI", "DisableAIDataAnalysis", 1, absentMeans: 0)],
        });
        list.Add(new RegistryTweak
        {
            Id = "privacy.copilot", Category = TweakCategory.Privacy, Note = TweakNote.Explorer,
            Name = "Turn off Copilot",
            NameEs = "Desactivar Copilot",
            Description = "Removes the Copilot button and sidebar from the taskbar.",
            DescriptionEs = "Quita el botón y la barra lateral de Copilot de la barra de tareas.",
            Values =
            [
                Cu(@"Software\Policies\Microsoft\Windows\WindowsCopilot", "TurnOffWindowsCopilot", 1, absentMeans: 0),
                Cu(ExplorerAdvanced, "ShowCopilotButton", 0, absentMeans: 1),
            ],
        });
        list.Add(new RegistryTweak
        {
            Id = "privacy.tracking", Category = TweakCategory.Privacy,
            Name = "Don't track app launches",
            NameEs = "No registrar qué apps abres",
            Description = "Windows stops keeping a list of the apps you open. Start won't show your most used apps.",
            DescriptionEs = "Windows deja de guardar qué apps abres. Inicio no mostrará las más usadas.",
            Values = [Cu(ExplorerAdvanced, "Start_TrackProgs", 0, absentMeans: 1)],
        });
        list.Add(new RegistryTweak
        {
            Id = "privacy.speech", Category = TweakCategory.Privacy,
            Name = "Turn off online speech recognition",
            NameEs = "Desactivar el reconocimiento de voz en línea",
            Description = "Your voice isn't sent to Microsoft. Dictation with Win+H stops working.",
            DescriptionEs = "Tu voz no se envía a Microsoft. El dictado con Win+H deja de funcionar.",
            Values = [Cu(@"Software\Microsoft\Speech_OneCore\Settings\OnlineSpeechPrivacy", "HasAccepted", 0, absentMeans: 0)],
        });
        list.Add(new RegistryTweak
        {
            Id = "privacy.activity", Category = TweakCategory.Privacy, Admin = true, Recommended = true,
            Name = "Turn off activity history",
            NameEs = "Desactivar el historial de actividad",
            Description = "Windows stops recording which files and websites you open.",
            DescriptionEs = "Windows deja de registrar qué archivos y webs abres.",
            Values =
            [
                Lm(@"SOFTWARE\Policies\Microsoft\Windows\System", "EnableActivityFeed", 0, absentMeans: 1),
                Lm(@"SOFTWARE\Policies\Microsoft\Windows\System", "PublishUserActivities", 0, absentMeans: 1),
                Lm(@"SOFTWARE\Policies\Microsoft\Windows\System", "UploadUserActivities", 0, absentMeans: 1),
            ],
        });
        list.Add(new RegistryTweak
        {
            Id = "privacy.telemetry", Category = TweakCategory.Privacy, Admin = true, Recommended = true,
            Name = "Minimum diagnostic data",
            NameEs = "Datos de diagnóstico mínimos",
            Description = "Windows sends the least diagnostic data your edition allows.",
            DescriptionEs = "Windows envía los mínimos datos de diagnóstico que permite tu edición.",
            Values = [Lm(@"SOFTWARE\Policies\Microsoft\Windows\DataCollection", "AllowTelemetry", 0)],
        });
        list.Add(new RegistryTweak
        {
            Id = "privacy.widgets", Category = TweakCategory.Privacy, Admin = true, Recommended = true, Note = TweakNote.Explorer,
            Name = "Turn off widgets and news",
            NameEs = "Desactivar widgets y noticias",
            Description = "Removes the weather and news panel from the taskbar. Saves memory.",
            DescriptionEs = "Quita el panel del tiempo y noticias de la barra de tareas. Ahorra memoria.",
            Values =
            [
                Lm(@"SOFTWARE\Policies\Microsoft\Dsh", "AllowNewsAndInterests", 0, absentMeans: 1),
                Lm(@"SOFTWARE\Policies\Microsoft\Windows\Windows Feeds", "EnableFeeds", 0, absentMeans: 1),
            ],
        });
        list.Add(new RegistryTweak
        {
            Id = "privacy.errorreports", Category = TweakCategory.Privacy, Admin = true,
            Name = "Turn off error reporting",
            NameEs = "Desactivar el informe de errores",
            Description = "When a program crashes, Windows doesn't send a report to Microsoft.",
            DescriptionEs = "Cuando un programa se cierra por un error, Windows no envía un informe a Microsoft.",
            Values = [Lm(@"SOFTWARE\Microsoft\Windows\Windows Error Reporting", "Disabled", 1, absentMeans: 0)],
        });
    }

    // ---------- System ----------

    private static void AddSystem(List<Tweak> list)
    {
        list.Add(new RegistryTweak
        {
            Id = "system.netthrottling", Category = TweakCategory.System, Admin = true, Recommended = true, Note = TweakNote.Restart,
            Name = "No network throttling while media plays",
            NameEs = "Sin limitar la red mientras suena música",
            Description = "Windows stops limiting network traffic when audio or video is playing, for example music while you play.",
            DescriptionEs = "Windows deja de limitar el tráfico de red cuando suena audio o vídeo, por ejemplo música mientras juegas.",
            Values = [Lm(Multimedia, "NetworkThrottlingIndex", unchecked((int)0xFFFFFFFF), absentMeans: 10, def: 10)],
        });
        list.Add(new RegistryTweak
        {
            Id = "system.responsiveness", Category = TweakCategory.System, Admin = true, Recommended = true, Note = TweakNote.Restart,
            Name = "Less CPU reserved for background tasks",
            NameEs = "Menos CPU reservada para tareas de fondo",
            Description = "Windows reserves 10% of the CPU for background tasks instead of 20%.",
            DescriptionEs = "Windows reserva un 10 % de la CPU para tareas de fondo en lugar de un 20 %.",
            Values = [Lm(Multimedia, "SystemResponsiveness", 10, absentMeans: 20, def: 20)],
        });
        list.Add(new RegistryTweak
        {
            Id = "system.deliveryopt", Category = TweakCategory.System, Admin = true, Recommended = true,
            Name = "Don't upload updates to other PCs",
            NameEs = "No compartir actualizaciones con otros PCs",
            Description = "Windows Update still works, but your connection isn't used to send updates to strangers.",
            DescriptionEs = "Windows Update sigue funcionando, pero tu conexión no se usa para enviar actualizaciones a desconocidos.",
            Values = [Lm(@"SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization", "DODownloadMode", 0)],
        });
        list.Add(new RegistryTweak
        {
            Id = "system.powerthrottling", Category = TweakCategory.System, Admin = true, Caution = true, Note = TweakNote.Restart,
            Name = "Turn off power throttling for all apps",
            NameEs = "Desactivar el ahorro de energía en todas las apps",
            Description = "No app gets slowed down in the background. Uses more battery on laptops.",
            DescriptionEs = "Ninguna app se ralentiza en segundo plano. Gasta más batería en portátiles.",
            Values = [Lm(@"SYSTEM\CurrentControlSet\Control\Power\PowerThrottling", "PowerThrottlingOff", 1, absentMeans: 0)],
        });
        list.Add(new RegistryTweak
        {
            Id = "system.faststartup", Category = TweakCategory.System, Admin = true, Note = TweakNote.Restart,
            Name = "Turn off fast startup",
            NameEs = "Desactivar el inicio rápido",
            Description = "Shutting down really shuts down. Fixes drivers and updates that only apply after a full restart. Boots a bit slower.",
            DescriptionEs = "Apagar apaga de verdad. Arregla drivers y actualizaciones que solo se aplican al reiniciar. Arranca algo más lento.",
            Values = [Lm(@"SYSTEM\CurrentControlSet\Control\Session Manager\Power", "HiberbootEnabled", 0, absentMeans: 1, def: 1)],
        });
        list.Add(new CommandTweak
        {
            Id = "system.hibernate", Category = TweakCategory.System, Admin = true, Caution = true,
            Name = "Turn off hibernation",
            NameEs = "Desactivar la hibernación",
            Description = "Frees several GB of disk (hiberfil.sys). You lose hibernate and fast startup.",
            DescriptionEs = "Libera varios GB de disco (hiberfil.sys). Pierdes la hibernación y el inicio rápido.",
            Check = () => Sys.GetValue(Hive.HKLM, @"SYSTEM\CurrentControlSet\Control\Power", "HibernateEnabled") is 0,
            TurnOn = () => RunChecked("powercfg", "/hibernate off"),
            TurnOff = () => RunChecked("powercfg", "/hibernate on"),
        });
    }

    // ---------- Power ----------

    private static void AddPower(List<Tweak> list)
    {
        list.Add(new PowerPlanTweak
        {
            Id = "power.high", Category = TweakCategory.Power, Admin = true, ExclusiveGroup = "power",
            Scheme = PowerPlans.HighPerformance,
            Name = "High performance power plan",
            NameEs = "Plan de energía de alto rendimiento",
            Description = "The CPU stays at higher speeds. Best for desktops; laptops use more battery.",
            DescriptionEs = "La CPU se mantiene a más velocidad. Ideal en sobremesa; los portátiles gastan más batería.",
        });
        list.Add(new PowerPlanTweak
        {
            Id = "power.ultimate", Category = TweakCategory.Power, Admin = true, Caution = true, ExclusiveGroup = "power",
            Scheme = PowerPlans.Ultimate,
            Name = "Ultimate Performance power plan",
            NameEs = "Plan de energía Máximo rendimiento",
            Description = "Windows' hidden plan with no power saving at all. Desktops only: more heat and power use.",
            DescriptionEs = "El plan oculto de Windows sin ningún ahorro de energía. Solo para sobremesa: más calor y consumo.",
        });
    }

    // ---------- Services ----------

    private static void AddServices(List<Tweak> list)
    {
        list.Add(Service("services.diagtrack", "DiagTrack", 4, 2, recommended: true,
            "Telemetry service", "Servicio de telemetría",
            "Connected User Experiences and Telemetry: collects usage data and sends it to Microsoft.",
            "Experiencias del usuario y telemetría asociadas: recopila datos de uso y los envía a Microsoft."));
        list.Add(Service("services.mapsbroker", "MapsBroker", 3, 2, recommended: true,
            "Offline maps service on demand", "Servicio de mapas sin conexión a demanda",
            "Downloaded Maps Manager only starts when an app needs it.",
            "El administrador de mapas descargados solo se inicia cuando una app lo necesita."));
        list.Add(Service("services.retaildemo", "RetailDemo", 4, 3, recommended: true,
            "Retail demo service", "Servicio de demostración para tiendas",
            "Only used by PCs on display in shops.",
            "Solo lo usan los PCs de exposición en tiendas."));
        list.Add(Service("services.fax", "Fax", 4, 3, recommended: true,
            "Fax service", "Servicio de fax",
            "Sends and receives faxes.",
            "Envía y recibe faxes."));
        list.Add(Service("services.sysmain", "SysMain", 4, 2, recommended: false,
            "SysMain (Superfetch)", "SysMain (Superfetch)",
            "Preloads apps into memory. Can cause disk usage spikes; keep it on if Windows is on a hard drive (HDD).",
            "Precarga apps en memoria. Puede causar picos de uso de disco; déjalo activado si Windows está en un disco duro (HDD).",
            caution: true));
        list.Add(Service("services.wsearch", "WSearch", 4, 2, recommended: false,
            "Windows Search indexing", "Indexación de Windows Search",
            "Stops indexing files in the background. Searching in Start and File Explorer gets slower.",
            "Deja de indexar archivos en segundo plano. Buscar en Inicio y en el Explorador se vuelve más lento.",
            caution: true));
        list.Add(Service("services.spooler", "Spooler", 4, 2, recommended: false,
            "Print Spooler", "Cola de impresión",
            "Only if you never print: printing and PDF printers stop working.",
            "Solo si nunca imprimes: dejan de funcionar las impresoras y la impresión a PDF.",
            caution: true));
        list.Add(Service("services.dmwappush", "dmwappushservice", 4, 3, recommended: false,
            "Device management push service", "Servicio de mensajes de administración",
            "Only if your PC isn't managed by a school or company.",
            "Solo si tu PC no lo administra un colegio o una empresa.",
            caution: true));
    }

    private static RegistryTweak Service(string id, string service, int startType, int windowsDefault, bool recommended,
        string name, string nameEs, string description, string descriptionEs, bool caution = false)
    {
        string key = $@"{Services}\{service}";
        return new RegistryTweak
        {
            Id = id, Category = TweakCategory.Services, Admin = true, Recommended = recommended, Caution = caution,
            Name = name, NameEs = nameEs, Description = description, DescriptionEs = descriptionEs,
            Values = [Lm(key, "Start", startType, def: windowsDefault)],
            Available = () => Sys.KeyExists(Hive.HKLM, key),
            AfterChange = applied =>
            {
                // The new start type applies after a restart; stop or start the service now as well
                if (applied && startType == 4) Sys.Run("sc.exe", $"stop {service}");
                else if (!applied && Sys.GetValue(Hive.HKLM, key, "Start") is 2) Sys.Run("sc.exe", $"start {service}");
            },
        };
    }

    // ---------- Explorer ----------

    private static void AddExplorer(List<Tweak> list)
    {
        list.Add(new RegistryTweak
        {
            Id = "explorer.extensions", Category = TweakCategory.Explorer, Recommended = true, Note = TweakNote.Explorer,
            Name = "Show file extensions",
            NameEs = "Mostrar las extensiones de archivo",
            Description = "See whether a file is really a .png or a disguised .exe.",
            DescriptionEs = "Para saber si un archivo es de verdad un .png o un .exe disfrazado.",
            Values = [Cu(ExplorerAdvanced, "HideFileExt", 0, absentMeans: 1, def: 1)],
            AfterChange = _ => Native.BroadcastSettingChange("TraySettings"),
        });
        list.Add(new RegistryTweak
        {
            Id = "explorer.endtask", Category = TweakCategory.Explorer, Recommended = true,
            Name = "\"End task\" on the taskbar",
            NameEs = "\"Finalizar tarea\" en la barra de tareas",
            Description = "Right-click a frozen game on the taskbar to close it without Task Manager.",
            DescriptionEs = "Clic derecho en un juego congelado de la barra de tareas para cerrarlo sin el Administrador de tareas.",
            Values = [Cu($@"{ExplorerAdvanced}\TaskbarDeveloperSettings", "TaskbarEndTask", 1, absentMeans: 0)],
            Available = () => WindowsBuild >= 22631,
        });
        list.Add(new RegistryTweak
        {
            Id = "explorer.classicmenu", Category = TweakCategory.Explorer, Note = TweakNote.Explorer,
            Name = "Classic right-click menu",
            NameEs = "Menú contextual clásico",
            Description = "Right-click shows all options right away, without \"Show more options\".",
            DescriptionEs = "El clic derecho muestra todas las opciones directamente, sin \"Mostrar más opciones\".",
            Values =
            [
                new RegValue(Hive.HKCU, @"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32", "", "",
                    RegistryValueKind.String),
            ],
            Available = () => IsWindows11,
        });
        list.Add(new RegistryTweak
        {
            Id = "explorer.numlock", Category = TweakCategory.Explorer, Note = TweakNote.SignOut,
            Name = "Num Lock on when you sign in",
            NameEs = "Bloq Num activado al iniciar sesión",
            Description = "The number pad works as soon as you sign in.",
            DescriptionEs = "El teclado numérico funciona nada más iniciar sesión.",
            Values = [CuText(Keyboard, "InitialKeyboardIndicators", "2", def: "0")],
        });
    }

    // ---------- Cleanup ----------

    private static void AddCleanup(List<Tweak> list)
    {
        list.Add(new CleanupTweak
        {
            Id = "cleanup.temp", Category = TweakCategory.Cleanup, MinAgeHours = 24,
            Name = "Temporary files",
            NameEs = "Archivos temporales",
            Description = "Deletes your temporary files older than a day. Files in use are skipped.",
            DescriptionEs = "Borra tus archivos temporales de más de un día. Los que están en uso se saltan.",
            Folders = () => [Path.GetTempPath()],
        });
        list.Add(new CleanupTweak
        {
            Id = "cleanup.wintemp", Category = TweakCategory.Cleanup, Admin = true, MinAgeHours = 24,
            Name = "Windows temporary files",
            NameEs = "Archivos temporales de Windows",
            Description = "Same as above, for Windows' own temporary folder.",
            DescriptionEs = "Lo mismo, en la carpeta temporal de Windows.",
            Folders = () => [Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp")],
        });
        list.Add(new CleanupTweak
        {
            Id = "cleanup.shaders", Category = TweakCategory.Cleanup,
            Name = "GPU shader cache",
            NameEs = "Caché de shaders de la GPU",
            Description = "Fixes stutter caused by a corrupt cache after a driver update. Games rebuild it the next time they open.",
            DescriptionEs = "Arregla tirones por una caché dañada tras actualizar drivers. Los juegos la recrean al abrirse.",
            Folders = () =>
            [
                Path.Combine(LocalAppData, "D3DSCache"),
                Path.Combine(LocalAppData, "NVIDIA", "DXCache"),
                Path.Combine(LocalAppData, "NVIDIA", "GLCache"),
                Path.Combine(LocalLow, "NVIDIA", "PerDriverVersion", "DXCache"),
                Path.Combine(LocalAppData, "AMD", "DxCache"),
                Path.Combine(LocalAppData, "AMD", "DxcCache"),
                Path.Combine(LocalAppData, "AMD", "GLCache"),
                Path.Combine(LocalAppData, "AMD", "VkCache"),
                Path.Combine(LocalLow, "Intel", "ShaderCache"),
            ],
        });
        list.Add(new CleanupTweak
        {
            Id = "cleanup.crashdumps", Category = TweakCategory.Cleanup,
            Name = "Crash dumps and error reports",
            NameEs = "Volcados de memoria e informes de error",
            Description = "Files Windows saves when a program crashes. They can take several GB.",
            DescriptionEs = "Archivos que Windows guarda cuando un programa falla. Pueden ocupar varios GB.",
            Folders = () =>
            [
                Path.Combine(LocalAppData, "CrashDumps"),
                Path.Combine(LocalAppData, "Microsoft", "Windows", "WER", "ReportArchive"),
                Path.Combine(LocalAppData, "Microsoft", "Windows", "WER", "ReportQueue"),
            ],
        });
        list.Add(new CleanupTweak
        {
            Id = "cleanup.wer", Category = TweakCategory.Cleanup, Admin = true,
            Name = "System error reports",
            NameEs = "Informes de error del sistema",
            Description = "Error reports saved for all users.",
            DescriptionEs = "Informes de error guardados para todos los usuarios.",
            Folders = () =>
            [
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Microsoft", "Windows", "WER", "ReportArchive"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Microsoft", "Windows", "WER", "ReportQueue"),
            ],
        });
        list.Add(new CleanupTweak
        {
            Id = "cleanup.dns", Category = TweakCategory.Cleanup,
            Name = "Flush the DNS cache",
            NameEs = "Vaciar la caché DNS",
            Description = "Fixes sites or servers that won't connect after a network change.",
            DescriptionEs = "Arregla webs o servidores que no conectan tras un cambio de red.",
            Extra = () =>
            {
                if (!Native.DnsFlushResolverCache()) throw new Exception("DnsFlushResolverCache failed");
            },
        });
    }

    // ---------- 2.0 additions ----------

    private static void AddMore(List<Tweak> list)
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string Explorer = @"Software\Microsoft\Windows\CurrentVersion\Explorer";

        list.Add(new LaunchOptionTweak
        {
            Id = TweakIds.RobloxPriorityHigh, Category = TweakCategory.Roblox, Note = TweakNote.NextLaunch, Caution = true, ExclusiveGroup = "priority",
            Name = "High CPU priority",
            NameEs = "Prioridad de CPU alta",
            Description = "Stronger than above-normal. Roblox gets the processor before almost everything else; other apps can feel slower while you play.",
            DescriptionEs = "Más fuerte que superior a la normal. Roblox usa el procesador antes que casi todo; otras apps pueden ir más lentas mientras juegas.",
        });

        // Gaming
        list.Add(new RegistryTweak
        {
            Id = "gaming.ducking", Category = TweakCategory.Gaming, Recommended = true,
            Name = "Don't lower game volume during calls",
            NameEs = "No bajar el volumen del juego en llamadas",
            Description = "Windows stops turning Roblox down by 80% when Discord or another app uses the microphone.",
            DescriptionEs = "Windows deja de bajar Roblox un 80 % cuando Discord u otra app usa el micrófono.",
            Values = [Cu(@"Software\Microsoft\Multimedia\Audio", "UserDuckingPreference", 3, absentMeans: 1)],
        });
        list.Add(new RegistryTweak
        {
            Id = "gaming.fsoglobal", Category = TweakCategory.Gaming, Caution = true, Note = TweakNote.NextLaunch,
            Name = "Disable fullscreen optimizations for all games",
            NameEs = "Desactivar optimizaciones de pantalla completa en todos los juegos",
            Description = "Games in exclusive fullscreen use real exclusive mode. Can lower input lag; Alt+Tab gets slower.",
            DescriptionEs = "Los juegos en pantalla completa exclusiva usan el modo exclusivo real. Puede bajar el retraso; Alt+Tab va más lento.",
            Values =
            [
                Cu(@"System\GameConfigStore", "GameDVR_FSEBehaviorMode", 2, absentMeans: 0),
                Cu(@"System\GameConfigStore", "GameDVR_FSEBehavior", 2, absentMeans: 0),
                Cu(@"System\GameConfigStore", "GameDVR_HonorUserFSEBehaviorMode", 1, absentMeans: 0),
                Cu(@"System\GameConfigStore", "GameDVR_DXGIHonorFSEWindowsCompatible", 1, absentMeans: 0),
            ],
        });

        // Mouse and keyboard
        list.Add(new RegistryTweak
        {
            Id = "input.langhotkey", Category = TweakCategory.Input, Recommended = true, Note = TweakNote.SignOut,
            Name = "No keyboard layout switch with Alt+Shift",
            NameEs = "Sin cambio de distribución con Alt+Mayús",
            Description = "Alt+Shift and Ctrl+Shift no longer switch the keyboard language in the middle of a game.",
            DescriptionEs = "Alt+Mayús y Ctrl+Mayús ya no cambian el idioma del teclado en mitad de una partida.",
            Values =
            [
                CuText(@"Keyboard Layout\Toggle", "Hotkey", "3", def: "1"),
                CuText(@"Keyboard Layout\Toggle", "Language Hotkey", "3", def: "1"),
                CuText(@"Keyboard Layout\Toggle", "Layout Hotkey", "3", def: "2"),
            ],
        });
        list.Add(new FlagBitTweak
        {
            Id = "input.highcontrast", Category = TweakCategory.Input, Recommended = true,
            Key = $@"{Accessibility}\HighContrast", Bit = 0x4, DefaultFlags = 126,
            Name = "No High Contrast shortcut",
            NameEs = "Sin atajo de Contraste alto",
            Description = "Left Alt + Left Shift + Print Screen no longer switches the screen to high contrast.",
            DescriptionEs = "Alt izq. + Mayús izq. + Impr Pant ya no cambia la pantalla a contraste alto.",
        });
        list.Add(new FlagBitTweak
        {
            Id = "input.mousekeys", Category = TweakCategory.Input, Recommended = true,
            Key = $@"{Accessibility}\MouseKeys", Bit = 0x4, DefaultFlags = 62,
            Name = "No Mouse Keys shortcut",
            NameEs = "Sin atajo de Teclas del mouse",
            Description = "Left Alt + Left Shift + Num Lock no longer turns on Mouse Keys.",
            DescriptionEs = "Alt izq. + Mayús izq. + Bloq Num ya no activa las Teclas del mouse.",
        });

        // Visual effects
        list.Add(new RegistryTweak
        {
            Id = "visual.hover", Category = TweakCategory.Visual, Note = TweakNote.SignOut,
            Name = "Faster hover popups",
            NameEs = "Ventanas emergentes más rápidas",
            Description = "Taskbar previews and tooltips appear after 100 ms instead of 400 ms.",
            DescriptionEs = "Las vistas previas de la barra de tareas y las ayudas aparecen a los 100 ms en lugar de 400 ms.",
            Values = [CuText(Mouse, "MouseHoverTime", "100", absentMeans: "400", def: "400")],
        });
        list.Add(new RegistryTweak
        {
            Id = "visual.shake", Category = TweakCategory.Visual, Note = TweakNote.Explorer,
            Name = "Turn off Aero Shake",
            NameEs = "Desactivar Aero Shake",
            Description = "Shaking a window by its title bar no longer minimizes all the others.",
            DescriptionEs = "Agitar una ventana por su barra de título ya no minimiza todas las demás.",
            Values = [Cu(ExplorerAdvanced, "DisallowShaking", 1, absentMeans: 0)],
        });
        list.Add(new RegistryTweak
        {
            Id = "visual.snapassist", Category = TweakCategory.Visual,
            Name = "No Snap Assist suggestions",
            NameEs = "Sin sugerencias de Snap Assist",
            Description = "Snapping a window to one side no longer shows other windows to fill the rest.",
            DescriptionEs = "Acoplar una ventana a un lado ya no muestra otras ventanas para llenar el resto.",
            Values = [Cu(ExplorerAdvanced, "SnapAssist", 0, absentMeans: 1)],
        });

        // Privacy and ads
        list.Add(new RegistryTweak
        {
            Id = "privacy.recentdocs", Category = TweakCategory.Privacy, Note = TweakNote.Explorer,
            Name = "Don't show recent files in Start",
            NameEs = "No mostrar archivos recientes en Inicio",
            Description = "Start and Jump Lists stop listing the files you opened recently.",
            DescriptionEs = "Inicio y las listas de accesos rápidos dejan de mostrar los archivos que abriste.",
            Values = [Cu(ExplorerAdvanced, "Start_TrackDocs", 0, absentMeans: 1)],
        });
        list.Add(new RegistryTweak
        {
            Id = "privacy.quickaccess", Category = TweakCategory.Privacy, Note = TweakNote.Explorer,
            Name = "No recent items in Quick Access",
            NameEs = "Sin elementos recientes en Acceso rápido",
            Description = "File Explorer's Home stops showing recent files and frequent folders.",
            DescriptionEs = "El inicio del Explorador deja de mostrar archivos recientes y carpetas frecuentes.",
            Values = [Cu(Explorer, "ShowRecent", 0, absentMeans: 1), Cu(Explorer, "ShowFrequent", 0, absentMeans: 1)],
        });
        list.Add(new RegistryTweak
        {
            Id = "privacy.cloudclipboard", Category = TweakCategory.Privacy,
            Name = "Don't sync the clipboard to the cloud",
            NameEs = "No sincronizar el portapapeles en la nube",
            Description = "What you copy isn't uploaded to share with your other devices.",
            DescriptionEs = "Lo que copias no se sube para compartirlo con tus otros dispositivos.",
            Values = [Cu(@"Software\Microsoft\Clipboard", "EnableCloudClipboard", 0, absentMeans: 0)],
        });
        list.Add(new RegistryTweak
        {
            Id = "privacy.location", Category = TweakCategory.Privacy, Caution = true,
            Name = "Turn off location for apps",
            NameEs = "Desactivar la ubicación para las apps",
            Description = "Apps can't use your location. Weather, maps and Find my device won't know where you are.",
            DescriptionEs = "Las apps no pueden usar tu ubicación. El tiempo, los mapas y Buscar mi dispositivo no sabrán dónde estás.",
            Values = [CuText(@"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\location", "Value", "Deny", absentMeans: "Allow", def: "Allow")],
        });
        list.Add(new RegistryTweak
        {
            Id = "privacy.onedrive", Category = TweakCategory.Privacy, Caution = true,
            Name = "Don't start OneDrive with Windows",
            NameEs = "No iniciar OneDrive con Windows",
            Description = "Saves memory and bandwidth while you play. Files only sync after you open OneDrive yourself.",
            DescriptionEs = "Ahorra memoria y conexión mientras juegas. Los archivos solo se sincronizan cuando abras OneDrive.",
            Values = [new RegValue(Hive.HKCU, RunKey, "OneDrive", RegValue.Delete, RegistryValueKind.String)],
            Available = () => Sys.GetValue(Hive.HKCU, RunKey, "OneDrive") is not null || TweakStore.Load().Backups.ContainsKey("privacy.onedrive"),
        });
        list.Add(new RegistryTweak
        {
            Id = "privacy.edge", Category = TweakCategory.Privacy, Admin = true, Caution = true,
            Name = "Stop Edge running in the background",
            NameEs = "Que Edge no se ejecute en segundo plano",
            Description = "Edge no longer preloads at startup or keeps running after you close it. Saves memory. Edge will say it's managed by your organization.",
            DescriptionEs = "Edge deja de precargarse al iniciar y de seguir abierto al cerrarlo. Ahorra memoria. Edge dirá que lo administra tu organización.",
            Values =
            [
                Lm(@"SOFTWARE\Policies\Microsoft\Edge", "StartupBoostEnabled", 0),
                Lm(@"SOFTWARE\Policies\Microsoft\Edge", "BackgroundModeEnabled", 0),
            ],
        });
        list.Add(new RegistryTweak
        {
            Id = "privacy.cortana", Category = TweakCategory.Privacy, Admin = true, Recommended = true,
            Name = "Turn off Cortana",
            NameEs = "Desactivar Cortana",
            Description = "Cortana stops running in the background (Windows 10).",
            DescriptionEs = "Cortana deja de ejecutarse en segundo plano (Windows 10).",
            Values = [Lm(@"SOFTWARE\Policies\Microsoft\Windows\Windows Search", "AllowCortana", 0, absentMeans: 1)],
        });

        // File Explorer and taskbar
        list.Add(new RegistryTweak
        {
            Id = "explorer.hidden", Category = TweakCategory.Explorer, Note = TweakNote.Explorer,
            Name = "Show hidden files",
            NameEs = "Mostrar archivos ocultos",
            Description = "File Explorer shows hidden files and folders, like AppData.",
            DescriptionEs = "El Explorador muestra los archivos y carpetas ocultos, como AppData.",
            Values = [Cu(ExplorerAdvanced, "Hidden", 1, absentMeans: 2, def: 2)],
        });
        list.Add(new RegistryTweak
        {
            Id = "explorer.thispc", Category = TweakCategory.Explorer,
            Name = "Open File Explorer to This PC",
            NameEs = "Abrir el Explorador en Este equipo",
            Description = "File Explorer opens on your drives instead of Home or Quick Access.",
            DescriptionEs = "El Explorador se abre en tus unidades en lugar de Inicio o Acceso rápido.",
            Values = [Cu(ExplorerAdvanced, "LaunchTo", 1, absentMeans: 2)],
        });
        list.Add(new RegistryTweak
        {
            Id = "explorer.searchbox", Category = TweakCategory.Explorer, Note = TweakNote.Explorer,
            Name = "Hide the taskbar search box",
            NameEs = "Ocultar el cuadro de búsqueda de la barra de tareas",
            Description = "More room on the taskbar. Press Windows and type to search.",
            DescriptionEs = "Más espacio en la barra de tareas. Pulsa Windows y escribe para buscar.",
            Values = [Cu(@"Software\Microsoft\Windows\CurrentVersion\Search", "SearchboxTaskbarMode", 0, absentMeans: 2)],
        });
        list.Add(new RegistryTweak
        {
            Id = "explorer.taskview", Category = TweakCategory.Explorer, Note = TweakNote.Explorer,
            Name = "Hide the Task View button",
            NameEs = "Ocultar el botón Vista de tareas",
            Description = "Win+Tab still works.",
            DescriptionEs = "Win+Tab sigue funcionando.",
            Values = [Cu(ExplorerAdvanced, "ShowTaskViewButton", 0, absentMeans: 1)],
        });
        list.Add(new RegistryTweak
        {
            Id = "explorer.seconds", Category = TweakCategory.Explorer, Note = TweakNote.Explorer,
            Name = "Show seconds in the taskbar clock",
            NameEs = "Mostrar segundos en el reloj",
            Description = "Handy for timing things in games.",
            DescriptionEs = "Útil para cronometrar cosas en los juegos.",
            Values = [Cu(ExplorerAdvanced, "ShowSecondsInSystemClock", 1, absentMeans: 0)],
        });
        list.Add(new RegistryTweak
        {
            Id = "explorer.shortcuttext", Category = TweakCategory.Explorer, Note = TweakNote.Explorer,
            Name = "No \"- Shortcut\" on new shortcuts",
            NameEs = "Sin \"- Acceso directo\" en los accesos nuevos",
            Description = "New shortcuts keep just the program's name.",
            DescriptionEs = "Los accesos directos nuevos se quedan solo con el nombre del programa.",
            Values = [new RegValue(Hive.HKCU, Explorer, "link", new byte[] { 0, 0, 0, 0 }, RegistryValueKind.Binary)],
        });
        list.Add(new RegistryTweak
        {
            Id = "explorer.autoplay", Category = TweakCategory.Explorer, Recommended = true,
            Name = "Turn off AutoPlay",
            NameEs = "Desactivar la reproducción automática",
            Description = "USB drives and discs no longer open or run anything by themselves. Safer.",
            DescriptionEs = "Los USB y discos ya no abren ni ejecutan nada por sí solos. Más seguro.",
            Values = [Cu($@"{Explorer}\AutoplayHandlers", "DisableAutoplay", 1, absentMeans: 0)],
        });
        list.Add(new RegistryTweak
        {
            Id = "explorer.leftalign", Category = TweakCategory.Explorer,
            Name = "Taskbar icons on the left",
            NameEs = "Iconos de la barra de tareas a la izquierda",
            Description = "Start and the taskbar icons go back to the left, like Windows 10.",
            DescriptionEs = "Inicio y los iconos de la barra de tareas vuelven a la izquierda, como en Windows 10.",
            Values = [Cu(ExplorerAdvanced, "TaskbarAl", 0, absentMeans: 1)],
            Available = () => IsWindows11,
        });

        // System
        list.Add(new RegistryTweak
        {
            Id = "system.remoteassist", Category = TweakCategory.System, Admin = true, Recommended = true,
            Name = "Turn off Remote Assistance",
            NameEs = "Desactivar Asistencia remota",
            Description = "Nobody can be invited to control your PC through Remote Assistance. Safer; many scams use it.",
            DescriptionEs = "Nadie puede ser invitado a controlar tu PC con Asistencia remota. Más seguro; lo usan muchas estafas.",
            Values = [Lm(@"SYSTEM\CurrentControlSet\Control\Remote Assistance", "fAllowToGetHelp", 0, absentMeans: 1, def: 1)],
        });
        list.Add(new RegistryTweak
        {
            Id = "system.ipv4", Category = TweakCategory.System, Admin = true, Caution = true, Note = TweakNote.Restart,
            Name = "Prefer IPv4 over IPv6",
            NameEs = "Preferir IPv4 sobre IPv6",
            Description = "Can help with routers that handle IPv6 badly. IPv6 still works when it's the only option.",
            DescriptionEs = "Puede ayudar con routers que manejan mal IPv6. IPv6 sigue funcionando cuando es la única opción.",
            Values = [Lm(@"SYSTEM\CurrentControlSet\Services\Tcpip6\Parameters", "DisabledComponents", 0x20)],
        });

        // Services
        list.Add(Service("services.semgr", "SEMgrSvc", 4, 3, recommended: true,
            "Payments and NFC service", "Servicio de pagos y NFC",
            "Tap-to-pay on PCs with NFC.", "Pagos sin contacto en PCs con NFC."));
        list.Add(Service("services.ajrouter", "AJRouter", 4, 3, recommended: true,
            "AllJoyn Router service", "Servicio de enrutador AllJoyn",
            "Old smart-home device protocol almost nothing uses.", "Protocolo antiguo de dispositivos domésticos que casi nada usa."));
        list.Add(Service("services.wisvc", "wisvc", 4, 3, recommended: false,
            "Windows Insider service", "Servicio de Windows Insider",
            "Only needed if you get Windows Insider preview builds.", "Solo hace falta si recibes versiones de prueba de Windows Insider.", caution: true));
        list.Add(Service("services.icssvc", "icssvc", 4, 3, recommended: false,
            "Mobile hotspot service", "Servicio de zona con cobertura móvil",
            "Only if you never share your PC's connection as a hotspot.", "Solo si nunca compartes la conexión del PC como zona Wi-Fi.", caution: true));
        list.Add(Service("services.lfsvc", "lfsvc", 4, 3, recommended: false,
            "Geolocation service", "Servicio de geolocalización",
            "Apps can't get your location at all.", "Las apps no pueden obtener tu ubicación.", caution: true));

        // Cleanup
        list.Add(new CleanupTweak
        {
            Id = "cleanup.wucache", Category = TweakCategory.Cleanup, Admin = true, MinAgeHours = 72,
            Name = "Windows Update downloads",
            NameEs = "Descargas de Windows Update",
            Description = "Update files older than three days. Windows downloads again whatever it still needs.",
            DescriptionEs = "Archivos de actualizaciones de hace más de tres días. Windows vuelve a bajar lo que necesite.",
            Folders = () => [Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "SoftwareDistribution", "Download")],
        });
        list.Add(new CleanupTweak
        {
            Id = "cleanup.docache", Category = TweakCategory.Cleanup, Admin = true,
            Name = "Delivery Optimization cache",
            NameEs = "Caché de Optimización de distribución",
            Description = "Update pieces Windows kept to share with other PCs.",
            DescriptionEs = "Trozos de actualizaciones que Windows guardaba para compartir con otros PCs.",
            Folders = () => [Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "ServiceProfiles", "NetworkService",
                "AppData", "Local", "Microsoft", "Windows", "DeliveryOptimization", "Cache")],
        });
        list.Add(new CleanupTweak
        {
            Id = "cleanup.inetcache", Category = TweakCategory.Cleanup,
            Name = "Internet cache",
            NameEs = "Caché de Internet",
            Description = "Temporary internet files used by Windows and older apps.",
            DescriptionEs = "Archivos temporales de Internet que usan Windows y apps antiguas.",
            Folders = () => [Path.Combine(LocalAppData, "Microsoft", "Windows", "INetCache")],
        });
        list.Add(new CleanupTweak
        {
            Id = "cleanup.thumbcache", Category = TweakCategory.Cleanup,
            Name = "Thumbnail and icon cache",
            NameEs = "Caché de miniaturas e iconos",
            Description = "Fixes wrong or blank thumbnails and icons. Windows rebuilds the cache as you browse.",
            DescriptionEs = "Arregla miniaturas e iconos incorrectos o en blanco. Windows rehace la caché al navegar.",
            Folders = () => [Path.Combine(LocalAppData, "Microsoft", "Windows", "Explorer")],
        });
    }

    private static void RunChecked(string file, string arguments)
    {
        var (code, output) = Sys.Run(file, arguments);
        if (code != 0) throw new Exception(string.IsNullOrWhiteSpace(output) ? $"{file} {arguments}: {code}" : output.Trim());
    }

    internal static string FormatSize(long bytes) =>
        bytes >= 1L << 30 ? (bytes / (double)(1L << 30)).ToString("0.0", CultureInfo.CurrentCulture) + " GB"
        : bytes >= 1L << 20 ? (bytes / (double)(1L << 20)).ToString("0", CultureInfo.CurrentCulture) + " MB"
        : (bytes / 1024.0).ToString("0", CultureInfo.CurrentCulture) + " KB";
}
