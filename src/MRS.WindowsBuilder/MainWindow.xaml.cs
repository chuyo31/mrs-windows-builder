using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Microsoft.Win32;
using MRS.ComponentCatalog;
using MRS.ComponentCatalog.Models;
using MRS.ComponentCatalog.Query;
using MRS.DismEngine.Dism;
using MRS.DismEngine.Logging;
using MRS.DismEngine.Processes;
using MRS.ImageEngine;
using MRS.ImageEngine.Inventory;
using MRS.ImageEngine.Iso;
using MRS.ImageEngine.Models;
using MRS.ImageEngine.Parsing;
using MRS.ISOEngine;
using MRS.ISOEngine.BootWim;
using MRS.ISOEngine.Models;
using MRS.ISOEngine.Oscdimg;
using MRS.ISOEngine.Pipeline;
using MRS.ISOEngine.Registry;
using MRS.ISOEngine.TreeCopy;
using MRS.PostInstall.Models;
using MRS.PostInstall.Packaging;
using MRS.ProfileEngine;
using MRS.ProfileEngine.Models;
using MRS.RemovalEngine;
using MRS.RemovalEngine.Models;
using MRS.RemovalPlanning;
using MRS.RemovalPlanning.Models;
// "RemovalEngine" es a la vez el nombre de un namespace (MRS.RemovalEngine) y de
// la clase que contiene; dentro del árbol de namespaces "MRS.*" el namespace
// siempre gana en la búsqueda de nombres sin cualificar, así que se referencia
// la clase con un alias explícito.
using RemovalEngineClass = MRS.RemovalEngine.RemovalEngine;
// MRS.ComponentCatalog.Models y MRS.ProfileEngine.Models (P13) tienen cada uno su
// propio "SecurityOptions" -- deliberadamente sin project reference entre ambos,
// igual que en P11 -- así que aquí, el único punto que conoce los dos mundos, se
// desambigua con un alias explícito para el del catálogo.
using CatalogSecurityOptions = MRS.ComponentCatalog.Models.SecurityOptions;
// "InstallationOptions" (P15) es a la vez un namespace (MRS.InstallationOptions) y el
// nombre del tipo que contiene (MRS.InstallationOptions.Models.InstallationOptions);
// mismo caso que RemovalEngine/ProfileEngine/SecurityOptions -- se referencia con un
// alias de nombre distinto (un alias con el MISMO nombre que el tipo no basta: el
// namespace sigue ganando en posiciones de tipo/atributo).
using InstallationOptionsModel = MRS.InstallationOptions.Models.InstallationOptions;

namespace MRS.WindowsBuilder;

/// <summary>
/// Interaction logic for MainWindow.xaml
///
/// La ventana no contiene lógica DISM, de catalogación ni de planificación:
/// delega en <see cref="ImageService"/>, <see cref="ImageInventoryService"/>,
/// <see cref="ComponentCatalogService"/> y <see cref="RemovalPlanBuilder"/>.
/// </summary>
public partial class MainWindow : Window
{
    private static readonly FilterOption[] FilterOptions =
    {
        new("Todos", CatalogFilterKind.All),
        new("Protegidos", CatalogFilterKind.Protected),
        new("Removibles", CatalogFilterKind.Removable),
        new("Opcionales", CatalogFilterKind.Optional),
        new("Críticos", CatalogFilterKind.Critical),
        new("Aplicaciones", CatalogFilterKind.Applications),
        new("Features", CatalogFilterKind.Features),
        new("Capabilities", CatalogFilterKind.Capabilities),
        new("Frameworks", CatalogFilterKind.Frameworks),
        new("Gaming", CatalogFilterKind.Gaming),
        new("Comunicación", CatalogFilterKind.Communication),
        new("IA", CatalogFilterKind.AI),
        new("Telemetría", CatalogFilterKind.Telemetry),
    };

    private readonly AppLogger _logger = new();
    private readonly ImageService _imageService;
    private readonly ImageInventoryService _inventoryService;
    private readonly ComponentCatalogService _catalogService = new();
    private readonly RemovalPlanBuilder _removalPlanBuilder = new();
    private readonly IProfileService _profileService = new ProfileService();

    private readonly IIsoMounter _isoMounter;
    private readonly IWorkingImageFactory _workingImageFactory;
    private readonly RemovalEngineClass _removalEngine;
    private readonly IIsoGenerationPipeline _isoGenerationPipeline;

    private IsoInspectionResult? _iso;
    private ImageInfo? _imageInfo;
    private ImageInventory? _inventory;
    private ComponentCatalogResult? _catalog;
    private List<ComponentRow> _allComponentRows = new();
    private RemovalPlan? _pendingPlan;
    private RemovalPlan? _confirmedPlan;
    private List<IsoPhaseRow> _isoPhaseRows = new();
    private CancellationTokenSource? _executionCts;
    private bool _isApplyingChanges;
    private bool _executionUiUnlocked;
    private ProfileLoadResult? _profileLoadResult;
    private string? _activeCatalogProfileId;
    private CatalogSecurityOptions _currentSecurityOptions = CatalogSecurityOptions.Safe;
    // P16, sección 9: BypassStorage empieza desactivado en la UI (a diferencia del
    // resto de InstallationOptions.Default) porque no hay mecanismo implementado
    // todavía; el checkbox correspondiente está deshabilitado en el XAML para que
    // nunca pueda marcarse desde la interfaz.
    private InstallationOptionsModel _installationOptions = InstallationOptionsModel.Default with { BypassStorage = false };

    public MainWindow()
    {
        InitializeComponent();

        _logger.Entry += OnLogEntry;

        var processRunner = new ProcessRunner();
        var dismRunner = new DismRunner(processRunner, _logger);
        _isoMounter = new IsoMounter(processRunner, _logger);

        _imageService = new ImageService(dismRunner, _isoMounter, new DismWimInfoParser(), _logger);
        _inventoryService = new ImageInventoryService(dismRunner, _isoMounter, _logger);
        _workingImageFactory = new WorkingImageFactory(dismRunner, _logger);
        _removalEngine = new RemovalEngineClass(dismRunner, _logger);

        // P24: une las piezas ya existentes (P16/P18/P19/P20/P23) en el único
        // pipeline real de generación de ISO -- MainWindow no reimplementa
        // ninguna de sus fases, solo lo construye e invoca.
        var registryEditor = new OfflineRegistryEditor(processRunner);
        var bootWimProvisioner = new BootWimProvisioner(_isoMounter, _logger);
        var bootWimModifier = new BootWimModifier(dismRunner, registryEditor, _logger);
        var installationImageService = new InstallationImageService(bootWimProvisioner, bootWimModifier, _logger);
        var treeCopier = new IsoTreeCopier(_isoMounter, _logger);
        var postInstallPackageBuilder = new PostInstallPackageBuilder(_logger);
        var oscdimgRunner = new OscdimgRunner(processRunner);
        _isoGenerationPipeline = new IsoGenerationPipeline(
            treeCopier, _isoMounter, installationImageService, _workingImageFactory,
            _removalEngine, postInstallPackageBuilder, oscdimgRunner, logger: _logger);

        LoadProfiles();

        _logger.Info("MRS Windows Builder iniciado");
        _logger.Info("Esperando seleccionar una ISO");
    }

    // ---- Perfiles (P11) -----------------------------------------------------

    /// <summary>
    /// Los perfiles viven fuera del código (<c>profiles/*.json</c>) para poder
    /// cambiarlos sin recompilar. Un archivo inválido se registra como error
    /// pero no impide arrancar la aplicación ni cargar el resto de perfiles.
    /// </summary>
    private void LoadProfiles()
    {
        var profilesDir = ResolveProfilesDirectory();
        _profileLoadResult = profilesDir is null
            ? new ProfileLoadResult(
                Array.Empty<ProfileDefinition>(),
                new[]
                {
                    new ProfileValidationError(
                        "profiles/", null, ProfileValidationErrorCode.DirectoryNotFound,
                        "No se encontró el directorio 'profiles' junto al ejecutable ni en los directorios superiores."),
                })
            : _profileService.LoadFromDirectory(profilesDir);

        foreach (var error in _profileLoadResult.Errors)
            _logger.Error($"[PROFILES] {error.FileName}: {error.Message}");

        _logger.Info($"[PROFILES] {_profileLoadResult.Profiles.Count} perfil(es) cargado(s)" +
                     (profilesDir is null ? "." : $" desde {profilesDir}."));
    }

    /// <summary>
    /// Busca una carpeta "profiles" a partir del directorio del ejecutable, subiendo
    /// hasta encontrarla (igual que <c>catalog/</c>, no se copia al output de
    /// MRS.WindowsBuilder; en desarrollo vive en la raíz del repositorio).
    /// </summary>
    private static string? ResolveProfilesDirectory()
    {
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 8 && !string.IsNullOrEmpty(dir); i++)
        {
            var candidate = Path.Combine(dir, "profiles");
            if (Directory.Exists(candidate) && Directory.EnumerateFiles(candidate, "*.json").Any())
                return candidate;

            dir = Path.GetDirectoryName(dir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        }

        return null;
    }

    // ---- Log ---------------------------------------------------------------

    private void OnLogEntry(object? sender, LogEntry entry)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => OnLogEntry(sender, entry));
            return;
        }

        LogBox.AppendText($"{entry}{Environment.NewLine}");
        LogBox.CaretIndex = LogBox.Text.Length;
        LogScroller.ScrollToEnd();
    }

    // ---- Seleccionar ISO -------------------------------------------------------

    private async void SelectIsoButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Seleccionar imagen ISO",
            Filter = "Imagen ISO (*.iso)|*.iso",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog() != true)
            return;

        var path = dialog.FileName;
        if (!File.Exists(path))
        {
            _logger.Error($"La ISO no existe: {path}");
            return;
        }

        IsoPathBox.Text = path;
        ResetImageInfo();
        _iso = null;
        SetBusy(true);
        StatusText.Text = "Comprobando ISO...";
        _logger.Info($"ISO seleccionada: {path}");

        try
        {
            _iso = await _imageService.InspectIsoAsync(path);

            if (!_iso.ImageFound)
            {
                _logger.Error(@"La ISO no contiene sources\install.wim ni sources\install.esd.");
                MessageBox.Show(this,
                    "La ISO no contiene una imagen de instalación válida.",
                    "MRS Windows Builder", MessageBoxButton.OK, MessageBoxImage.Warning);
                StatusText.Text = "ISO no válida";
                return;
            }

            _logger.Info($"Imagen detectada: sources\\{Path.GetFileName(_iso.ImagePath!)} ({_iso.Format}).");
            StatusText.Text = "ISO lista para analizar";
            FooterHint.Text = "Pulsa \"Analizar imagen\"";
        }
        catch (Exception ex)
        {
            _iso = null;
            _logger.Error($"No se pudo comprobar la ISO: {ex.Message}");
            MessageBox.Show(this, "No se ha podido leer la ISO.",
                "MRS Windows Builder", MessageBoxButton.OK, MessageBoxImage.Error);
            StatusText.Text = "Error";
        }
        finally
        {
            SetBusy(false);
        }
    }

    // ---- Analizar imagen ----------------------------------------------------

    private async void AnalyzeButton_Click(object sender, RoutedEventArgs e)
    {
        var isoPath = IsoPathBox.Text;
        if (string.IsNullOrWhiteSpace(isoPath))
            return;

        SetBusy(true);
        StatusText.Text = "Analizando imagen...";
        _logger.Info("Analizando imagen...");

        try
        {
            var info = await _imageService.AnalyzeIsoAsync(isoPath);
            _imageInfo = info;
            PopulateImageInfo(info);

            _logger.Info($"Análisis completado: {info.OperatingSystem} " +
                         $"{info.DisplayVersion ?? "(versión desconocida)"} · {info.Editions.Count} edición(es).");
            StatusText.Text = "Imagen analizada";
            FooterHint.Text = "Selecciona una edición para continuar";
        }
        catch (ImageAnalysisException ex)
        {
            _logger.Error("DISM no pudo analizar la imagen.");
            _logger.Error($"Código: {ex.ExitCode}");
            MessageBox.Show(this, "No se ha podido analizar la imagen.",
                "MRS Windows Builder", MessageBoxButton.OK, MessageBoxImage.Error);
            StatusText.Text = "Error de análisis";
        }
        catch (Exception ex)
        {
            _logger.Error($"Error inesperado al analizar la imagen: {ex.Message}");
            MessageBox.Show(this, "No se ha podido analizar la imagen.",
                "MRS Windows Builder", MessageBoxButton.OK, MessageBoxImage.Error);
            StatusText.Text = "Error de análisis";
        }
        finally
        {
            SetBusy(false);
        }
    }

    // ---- Edición / Perfiles ------------------------------------------------

    private void EditionCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (EditionCombo.SelectedItem is ImageEdition edition)
        {
            _logger.Info($"Edición seleccionada: {edition.Name} (índice {edition.Index}).");
            FooterHint.Text = "Pulsa \"Continuar\" para inventariar la imagen";
            ContinueButton.IsEnabled = true;
        }
        else
        {
            ContinueButton.IsEnabled = false;
        }
    }

    // ---- Continuar -> Inventario -----------------------------------------

    private async void ContinueButton_Click(object sender, RoutedEventArgs e)
    {
        var isoPath = IsoPathBox.Text;
        if (EditionCombo.SelectedItem is not ImageEdition edition || string.IsNullOrWhiteSpace(isoPath))
            return;

        SetBusy(true);
        ContinueButton.IsEnabled = false;
        StatusText.Text = "Inventariando imagen...";
        ShowInventoryProgress();

        // System.Progress<T> reenvía cada Report() al SynchronizationContext
        // capturado aquí (el de la UI): OnInventoryProgress se ejecuta siempre en
        // el hilo de la ventana sin bloquearlo. ImageInventoryService no conoce
        // WPF, solo un IProgress<InventoryProgressInfo> (mismo patrón que P10).
        IProgress<InventoryProgressInfo> progress = new Progress<InventoryProgressInfo>(OnInventoryProgress);

        try
        {
            var result = await _inventoryService.BuildInventoryFromIsoAsync(isoPath, edition.Index, default, progress);
            _inventory = result.Inventory;
            ShowInventory(edition, result);
            StatusText.Text = "Inventario completado";
        }
        catch (ImageAnalysisException ex)
        {
            _logger.Error($"Código DISM: {ex.ExitCode}");
            MessageBox.Show(this,
                "No se ha podido inventariar la imagen. Revisa el registro para ver qué fase falló.",
                "MRS Windows Builder", MessageBoxButton.OK, MessageBoxImage.Error);
            StatusText.Text = "Error de inventario";
        }
        catch (Exception ex)
        {
            _logger.Error($"Error inesperado durante el inventario: {ex.Message}");
            MessageBox.Show(this, "No se ha podido inventariar la imagen.",
                "MRS Windows Builder", MessageBoxButton.OK, MessageBoxImage.Error);
            StatusText.Text = "Error de inventario";
        }
        finally
        {
            SetBusy(false);
            HideInventoryProgress();
        }
    }

    private void ShowInventoryProgress()
    {
        InventoryProgressBar.Value = 0;
        InventoryPercentText.Text = "0 %";
        InventoryPhaseText.Text = "Preparando inventariado...";
        InventoryProgressPanel.Visibility = Visibility.Visible;
    }

    private void HideInventoryProgress() => InventoryProgressPanel.Visibility = Visibility.Collapsed;

    /// <summary>
    /// Único punto de entrada de la telemetría de progreso del inventariado hacia
    /// la UI (P22): actualiza la barra/porcentaje/fase visibles. No decide nada
    /// de negocio; solo presentación. El terminal/log (<see cref="_logger"/>)
    /// sigue funcionando exactamente igual, sin relación con esta barra.
    /// </summary>
    private void OnInventoryProgress(InventoryProgressInfo info)
    {
        InventoryProgressBar.Value = info.Percent;
        InventoryPercentText.Text = $"{info.Percent} %";
        InventoryPhaseText.Text = info.Message;
    }

    private void ShowInventory(ImageEdition edition, InventoryResult result)
    {
        var inv = result.Inventory;
        InventorySubtitle.Text =
            $"{edition.Name} · índice {edition.Index}" +
            (result.WorkspaceKept ? $"  ·  workspace: {result.WorkspacePath}" : string.Empty);

        CountPackages.Text = inv.PackageCount.ToString();
        CountApps.Text = inv.ProvisionedAppCount.ToString();
        CountFeatures.Text = inv.FeatureCount.ToString();
        CountCapabilities.Text = inv.CapabilityCount.ToString();
        CountDrivers.Text = inv.DriverCount.ToString();

        CategoryList.SelectedIndex = 0; // dispara CategoryList_SelectionChanged
        RenderCategory();

        InventoryOverlay.Visibility = Visibility.Visible;
    }

    private void CategoryList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        => RenderCategory();

    private void RenderCategory()
    {
        if (_inventory is null || InventoryGrid is null)
            return;

        var category = (CategoryList.SelectedItem as ListBoxItem)?.Content as string ?? "Paquetes";

        IEnumerable<InventoryRow> rows = category switch
        {
            "Apps" => _inventory.ProvisionedApps.Select(a => new InventoryRow(
                a.DisplayName.Length > 0 ? a.DisplayName : a.PackageName,
                a.Architecture ?? "",
                $"{a.Version} · {a.PackageName}")),

            "Features" => _inventory.Features.Select(f => new InventoryRow(f.Name, f.State ?? "", "")),

            "Capabilities" => _inventory.Capabilities.Select(c => new InventoryRow(c.Identity, c.State ?? "", "")),

            "Drivers" => _inventory.Drivers.Select(d => new InventoryRow(
                d.PublishedName,
                d.BootCritical == true ? "Boot critical" : "",
                $"{d.Provider} · {d.Class} · {d.Version} · {d.OriginalFileName}")),

            _ => _inventory.Packages.Select(p => new InventoryRow(
                p.PackageIdentity,
                p.State ?? "",
                string.Join(" · ", new[] { p.ReleaseType, p.InstallTime }.Where(s => !string.IsNullOrWhiteSpace(s))))),
        };

        InventoryGrid.ItemsSource = rows.ToList();
    }

    private void InventoryBackButton_Click(object sender, RoutedEventArgs e)
    {
        InventoryOverlay.Visibility = Visibility.Collapsed;
        StatusText.Text = "Imagen analizada";
    }

    private void InventoryNextButton_Click(object sender, RoutedEventArgs e)
    {
        if (_inventory is null)
            return;

        // El catálogo solo clasifica: no ejecuta DISM ni modifica la imagen.
        var catalog = _catalogService.BuildCatalog(_inventory);
        _catalog = catalog;

        _logger.Info($"Catálogo generado: {catalog.TotalCount} componentes " +
                     $"({catalog.ProtectedCount} protegidos, {catalog.RemovableCount} removibles, " +
                     $"{catalog.OptionalCount} opcionales, {catalog.CriticalCount} críticos).");

        ShowComponents(catalog);
    }

    // ---- Pantalla de componentes (catálogo) --------------------------------

    private void ShowComponents(ComponentCatalogResult catalog)
    {
        // Si se preseleccionó un perfil en la pantalla inicial, conservar su
        // SecurityOptions tal cual (P14): no se reinicia solo por cambiar de
        // pantalla. Debe leerse ANTES de reiniciar _currentSecurityOptions más abajo.
        var preSelectedProfileId = _activeCatalogProfileId;
        var preSelectedSecurityOptions = _currentSecurityOptions;

        _currentSecurityOptions = CatalogSecurityOptions.Safe;
        PopulateComponentRows(catalog);

        ComponentSearchBox.Text = string.Empty;

        if (ComponentFilterCombo.ItemsSource is null)
        {
            ComponentFilterCombo.ItemsSource = FilterOptions;
            ComponentFilterCombo.DisplayMemberPath = nameof(FilterOption.Label);
        }
        ComponentFilterCombo.SelectedIndex = 0;

        RenderComponents();
        ClearComponentDetail();
        UpdateSelectionSummary();

        // Aplicarlo ahora de verdad: ya existen ComponentId reales contra los que
        // filtrar/proteger. Reutiliza exactamente la misma lógica (ApplyProfile),
        // sin duplicarla.
        ResetProfileBar();
        if (preSelectedProfileId is not null)
        {
            _currentSecurityOptions = preSelectedSecurityOptions;
            ApplyProfile(preSelectedProfileId, preserveCurrentSecurityOptions: true);
        }

        InventoryOverlay.Visibility = Visibility.Collapsed;
        ComponentsOverlay.Visibility = Visibility.Visible;
        StatusText.Text = "Catálogo generado";
    }

    /// <summary>
    /// Reemplaza <see cref="_catalog"/> y <see cref="_allComponentRows"/> a partir de un
    /// <see cref="ComponentCatalogResult"/> ya construido, y refresca el resumen de la
    /// cabecera. Compartido por <see cref="ShowComponents"/> y
    /// <see cref="RebuildCatalogWithCurrentSecurityOptions"/> para no duplicar la
    /// construcción de filas.
    /// </summary>
    private void PopulateComponentRows(ComponentCatalogResult catalog)
    {
        _catalog = catalog;

        ComponentsSummary.Text =
            $"{catalog.TotalCount} componentes · {catalog.ProtectedCount} protegidos · " +
            $"{catalog.RemovableCount} removibles · {catalog.OptionalCount} opcionales · " +
            $"{catalog.CriticalCount} críticos";

        _allComponentRows = catalog.Components
            .Select(c => new ComponentRow(c))
            .OrderBy(r => r.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// P13: recalcula el catálogo (protección de Defender/Windows Update incluida)
    /// con <see cref="_currentSecurityOptions"/> vigente, preservando la selección
    /// manual actual por ComponentId. Pura recomputación en memoria sobre
    /// <see cref="_inventory"/> ya obtenido: no ejecuta DISM ni toca la imagen. No hace
    /// nada si todavía no hay imagen analizada (se llamó desde la preselección de la
    /// pantalla inicial).
    /// </summary>
    private void RebuildCatalogWithCurrentSecurityOptions()
    {
        if (_inventory is null || _catalog is null)
            return;

        var previouslySelected = _allComponentRows
            .Where(row => row.IsSelected)
            .Select(row => row.Component.Id)
            .ToHashSet(StringComparer.Ordinal);

        var catalog = _catalogService.BuildCatalog(_inventory, _currentSecurityOptions);
        PopulateComponentRows(catalog);

        foreach (var row in _allComponentRows)
            // row.Editable: si Defender/Windows Update volvieron a protegerse (por
            // ejemplo al cambiar a un perfil bloqueado), la selección previa se
            // descarta para ese componente igual que con cualquier otro protegido.
            row.IsSelected = row.Editable && previouslySelected.Contains(row.Component.Id);

        RenderComponents();
        UpdateSelectionSummary();
    }

    /// <summary>Un catálogo nuevo (nueva ISO/edición) invalida cualquier perfil activo anterior.</summary>
    private void ResetProfileBar()
    {
        _activeCatalogProfileId = null;
        _currentSecurityOptions = CatalogSecurityOptions.Safe;
        ProfileInfoText.Text = "Selecciona un perfil o marca componentes manualmente.";
        SecurityOptionsPanel.Visibility = Visibility.Collapsed;
        PreSecurityOptionsPanel.Visibility = Visibility.Collapsed;

        foreach (var btn in ProfileButtons)
            btn.Style = (Style)FindResource("OutlineButton");
    }

    private void RenderComponents()
    {
        if (_catalog is null || ComponentsGrid is null)
            return;

        var filterKind = (ComponentFilterCombo.SelectedItem as FilterOption)?.Kind ?? CatalogFilterKind.All;
        var filtered = CatalogQuery.Filter(_catalog.Components, filterKind);
        filtered = CatalogQuery.Search(filtered, ComponentSearchBox.Text);

        var filteredIds = filtered.Select(c => c.Id).ToHashSet();
        ComponentsGrid.ItemsSource = _allComponentRows
            .Where(row => filteredIds.Contains(row.Component.Id))
            .ToList();
    }

    private void ComponentSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        => RenderComponents();

    private void ComponentFilterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        => RenderComponents();

    private void ComponentsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ComponentsGrid.SelectedItem is ComponentRow row)
            ShowComponentDetail(row.Component);
        else
            ClearComponentDetail();
    }

    private void ShowComponentDetail(ComponentDefinition component)
    {
        DetailPlaceholder.Visibility = Visibility.Collapsed;
        DetailContent.Visibility = Visibility.Visible;

        DetailName.Text = component.DisplayName.Length > 0 ? component.DisplayName : component.Name;
        DetailCategory.Text = component.Category.ToString();
        DetailState.Text = component.Superseded ? "Superseded" : component.Installed ? "Installed" : "No instalado";
        DetailRisk.Text = component.Risk.ToString();
        DetailProtection.Text = component.Protection.ToString();
        DetailSource.Text = component.SourceType.ToString();
        DetailDescription.Text = Dash(component.Description);
        DetailProtectionReason.Text = Dash(component.ProtectionReason);

        DetailDependencies.Text = JoinNames(_catalog?.DependenciesOf(component.Id));
        DetailDependents.Text = JoinNames(_catalog?.DependentsOf(component.Id));
    }

    private void ClearComponentDetail()
    {
        DetailPlaceholder.Visibility = Visibility.Visible;
        DetailContent.Visibility = Visibility.Collapsed;
    }

    private void ComponentsBackButton_Click(object sender, RoutedEventArgs e)
    {
        ComponentsOverlay.Visibility = Visibility.Collapsed;
        InventoryOverlay.Visibility = Visibility.Visible;
        StatusText.Text = "Imagen analizada";
    }

    private void ComponentSelectionCheckBox_Changed(object sender, RoutedEventArgs e)
        => UpdateSelectionSummary();

    // ---- Perfiles (P11/P12) --------------------------------------------------

    /// <summary>
    /// Los 5 botones de perfil reales (minimal/light/recommended/clean/custom),
    /// tanto los de la pantalla inicial (preselección, antes de tener catálogo)
    /// como los de la pantalla COMPONENTES (aplicación real). Un único punto de
    /// verdad para resaltar el perfil activo en ambas zonas sin duplicar lógica.
    /// </summary>
    private IEnumerable<Button> ProfileButtons => new[]
    {
        ProfileMinimalButton, ProfileLightButton, ProfileRecommendedButton, ProfileCleanButton, ProfileCustomButton,
        ProfilePreMinimalButton, ProfilePreLightButton, ProfilePreRecommendedButton, ProfilePreCleanButton, ProfilePreCustomButton,
    };

    /// <summary>
    /// Único manejador para los 10 botones de perfil (5 en la pantalla inicial, 5 en
    /// COMPONENTES): ambos comparten el mismo <c>Tag</c> (el id real del perfil) y la
    /// misma lógica de aplicación, para no duplicarla entre pantallas.
    /// </summary>
    private void ProfileButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string profileId)
            return;

        ApplyProfile(profileId);
    }

    /// <summary>
    /// Aplica un perfil sobre la selección actual: Selección de perfil -&gt; ComponentId
    /// candidatos -&gt; filtrado por lo que exista realmente en <see cref="_allComponentRows"/>
    /// -&gt; checkboxes. Nunca ejecuta nada ni salta el flujo Catalog -&gt; ProtectionEngine -&gt;
    /// RemovalPlan: solo cambia qué casillas quedan marcadas, exactamente como si el
    /// usuario las hubiera marcado a mano. Si todavía no hay catálogo (se llamó desde la
    /// pantalla inicial, antes de analizar la ISO), solo queda como preselección: se
    /// vuelve a invocar automáticamente en cuanto <see cref="ShowComponents"/> genera el
    /// catálogo real.
    /// </summary>
    /// <param name="preserveCurrentSecurityOptions">
    /// P14: si es <c>true</c>, no recalcula <see cref="_currentSecurityOptions"/> a partir del
    /// perfil (salvo que esté bloqueado) — se usa al reaplicar la preselección de la
    /// pantalla inicial tras generar el catálogo, para conservar exactamente lo que el
    /// usuario ya había elegido allí.
    /// </param>
    private void ApplyProfile(string profileId, bool preserveCurrentSecurityOptions = false)
    {
        HighlightActiveProfileButton(profileId);

        if (_profileLoadResult is null)
            return;

        var profile = _profileService.GetProfile(_profileLoadResult, profileId);
        if (profile is null)
        {
            ProfileInfoText.Text = $"Perfil '{profileId}' no disponible (revisa profiles/*.json).";
            _logger.Error($"[PROFILES] Perfil '{profileId}' no encontrado al aplicarlo.");
            return;
        }

        var previousProfile = _activeCatalogProfileId is null
            ? null
            : _profileService.GetProfile(_profileLoadResult, _activeCatalogProfileId);

        _activeCatalogProfileId = profile.Id;

        // P13/P14: Mínimo/Ligero/Recomendado siempre fuerzan Defender/Windows Update
        // protegidos (EffectiveSecurityOptions) -- "los perfiles seguros siempre
        // parten de valores seguros", sin heredar nunca una configuración insegura.
        // Limpio/Personalizado parten de los valores por defecto del propio perfil
        // SOLO al entrar desde un perfil bloqueado (o sin perfil previo); entre
        // Limpio <-> Personalizado, o al reaplicar la preselección de la pantalla
        // inicial, se conserva la configuración ya elegida.
        if (profile.IsSecurityLocked)
            _currentSecurityOptions = ToCatalogSecurityOptions(profile.EffectiveSecurityOptions);
        else if (!preserveCurrentSecurityOptions && (previousProfile is null || previousProfile.IsSecurityLocked))
            _currentSecurityOptions = ToCatalogSecurityOptions(profile.EffectiveSecurityOptions);

        UpdateSecurityOptionsPanel(profile);
        RebuildCatalogWithCurrentSecurityOptions();

        if (profile.IsCustom)
        {
            // "Personalizado" no impone ninguna lista: es la selección manual tal cual está.
            ProfileInfoText.Text = "Personalizado: se mantiene la selección manual actual.";
            _logger.Info("[PROFILES] Perfil Personalizado activado: la selección no se modifica.");
            return;
        }

        var knownIds = _allComponentRows.Select(row => row.Component.Id).ToList();
        var protectedIds = _allComponentRows
            .Where(row => !row.Editable)
            .Select(row => row.Component.Id)
            .ToList();

        var selection = _profileService.GetSelection(profile, knownIds, protectedIds);
        var selectedIds = selection.SelectedComponentIds.ToHashSet();

        foreach (var row in _allComponentRows)
            // row.Editable: nunca se marca un componente protegido, ni siquiera si el
            // perfil lo pidiera. La decisión final de protección sigue siendo de
            // ProtectionEngine/RemovalPlanning, no de ProfileEngine.
            row.IsSelected = row.Editable && selectedIds.Contains(row.Component.Id);

        RenderComponents(); // refresca los checkboxes visibles (no-op si aún no hay catálogo)
        UpdateSelectionSummary();

        var pendingNote = profile.ComponentIds.Count == 0
            ? " (perfil sin ComponentId reales todavía; ver prompts/11-resultado.md)"
            : string.Empty;
        var unknownNote = selection.UnknownComponentIds.Count > 0
            ? $" · {selection.UnknownComponentIds.Count} ID(s) del perfil no existen en este catálogo"
            : string.Empty;
        var blockedNote = selection.BlockedComponentIds.Count > 0
            ? $" · ⚠ {selection.BlockedComponentIds.Count} bloqueado(s) por protección"
            : string.Empty;

        ProfileInfoText.Text =
            $"{profile.Name}: {selection.SelectedComponentIds.Count} componente(s) seleccionado(s)" +
            unknownNote + blockedNote + pendingNote;

        _logger.Info($"[PROFILES] Perfil '{profile.Id}' aplicado: {selection.SelectedComponentIds.Count} seleccionados, " +
                     $"{selection.UnknownComponentIds.Count} desconocidos, {selection.BlockedComponentIds.Count} bloqueados.");
    }

    private void HighlightActiveProfileButton(string profileId)
    {
        foreach (var btn in ProfileButtons)
            btn.Style = (Style)FindResource(
                string.Equals(btn.Tag as string, profileId, StringComparison.OrdinalIgnoreCase)
                    ? "AccentButton" : "OutlineButton");
    }

    private static CatalogSecurityOptions ToCatalogSecurityOptions(MRS.ProfileEngine.Models.SecurityOptions options)
        => new() { KeepDefender = options.KeepDefender, KeepWindowsUpdate = options.KeepWindowsUpdate };

    /// <summary>
    /// Muestra el panel de seguridad que corresponde a <paramref name="profile"/>
    /// (P13/P14) en ambas pantallas (inicial y COMPONENTES): solo información para
    /// Mínimo/Ligero/Recomendado (<see cref="ProfileDefinition.IsSecurityLocked"/>,
    /// solo en COMPONENTES), casillas modificables para Limpio/Personalizado en las
    /// dos. <see cref="_currentSecurityOptions"/> es la única fuente de verdad: este
    /// método solo la refleja visualmente, nunca la decide.
    /// </summary>
    private void UpdateSecurityOptionsPanel(ProfileDefinition profile)
    {
        // Pantalla COMPONENTES: badges de "protegido" para los perfiles bloqueados.
        SecurityOptionsPanel.Visibility = Visibility.Visible;
        SecurityLockedPanel.Visibility = profile.IsSecurityLocked ? Visibility.Visible : Visibility.Collapsed;
        SecurityConfigurablePanel.Visibility = profile.IsSecurityLocked ? Visibility.Collapsed : Visibility.Visible;

        // Pantalla inicial: sin badges (mockup de P14), solo aparece para Limpio/Personalizado.
        PreSecurityOptionsPanel.Visibility = profile.IsSecurityLocked ? Visibility.Collapsed : Visibility.Visible;

        if (profile.IsSecurityLocked)
            return;

        SetSecurityCheckboxesWithoutTriggeringChange();
    }

    /// <summary>
    /// Refleja <see cref="_currentSecurityOptions"/> en las 4 casillas (2 por pantalla)
    /// sin disparar <see cref="SecurityOption_Changed"/> — evitaría una reconstrucción
    /// redundante del catálogo justo después de haberla hecho ya.
    /// </summary>
    private void SetSecurityCheckboxesWithoutTriggeringChange()
    {
        foreach (var checkBox in DefenderCheckBoxes)
        {
            checkBox.Checked -= SecurityOption_Changed;
            checkBox.Unchecked -= SecurityOption_Changed;
            checkBox.IsChecked = _currentSecurityOptions.KeepDefender;
            checkBox.Checked += SecurityOption_Changed;
            checkBox.Unchecked += SecurityOption_Changed;
        }

        foreach (var checkBox in WindowsUpdateCheckBoxes)
        {
            checkBox.Checked -= SecurityOption_Changed;
            checkBox.Unchecked -= SecurityOption_Changed;
            checkBox.IsChecked = _currentSecurityOptions.KeepWindowsUpdate;
            checkBox.Checked += SecurityOption_Changed;
            checkBox.Unchecked += SecurityOption_Changed;
        }

        foreach (var warning in DefenderWarningTexts)
            warning.Visibility = _currentSecurityOptions.KeepDefender ? Visibility.Collapsed : Visibility.Visible;

        foreach (var warning in WindowsUpdateWarningTexts)
            warning.Visibility = _currentSecurityOptions.KeepWindowsUpdate ? Visibility.Collapsed : Visibility.Visible;
    }

    private IEnumerable<CheckBox> DefenderCheckBoxes => new[] { KeepDefenderCheckBox, PreKeepDefenderCheckBox };
    private IEnumerable<CheckBox> WindowsUpdateCheckBoxes => new[] { KeepWindowsUpdateCheckBox, PreKeepWindowsUpdateCheckBox };
    private IEnumerable<TextBlock> DefenderWarningTexts => new[] { DefenderWarningText, PreDefenderWarningText };
    private IEnumerable<TextBlock> WindowsUpdateWarningTexts => new[] { WindowsUpdateWarningText, PreWindowsUpdateWarningText };

    /// <summary>
    /// P13/P14: el usuario cambia si Limpio/Personalizado mantienen Defender/Windows
    /// Update, desde cualquiera de las dos pantallas (inicial o COMPONENTES) que
    /// comparten <see cref="_currentSecurityOptions"/> como única fuente de verdad.
    /// Nunca ejecuta ninguna acción sobre Windows ni sobre la imagen: solo cambia el
    /// estado en memoria y reconstruye el catálogo (protección) para que el plan lo
    /// refleje. Solo registra en el log y reconstruye si el valor realmente cambió
    /// (nunca por sincronizar la otra pantalla ni por un render).
    /// </summary>
    private void SecurityOption_Changed(object sender, RoutedEventArgs e)
    {
        if (_activeCatalogProfileId is null || _profileLoadResult is null || sender is not CheckBox checkBox)
            return;

        var profile = _profileService.GetProfile(_profileLoadResult, _activeCatalogProfileId);
        if (profile is null || profile.IsSecurityLocked)
            return; // defensa en profundidad: estas casillas no deberían ni mostrarse aquí.

        var isChecked = checkBox.IsChecked == true;
        var previous = _currentSecurityOptions;
        CatalogSecurityOptions updated;
        bool isDefender;

        if (checkBox == KeepDefenderCheckBox || checkBox == PreKeepDefenderCheckBox)
        {
            updated = previous with { KeepDefender = isChecked };
            isDefender = true;
        }
        else if (checkBox == KeepWindowsUpdateCheckBox || checkBox == PreKeepWindowsUpdateCheckBox)
        {
            updated = previous with { KeepWindowsUpdate = isChecked };
            isDefender = false;
        }
        else
        {
            return;
        }

        if (updated == previous)
            return; // sin cambio real (p. ej. sincronización desde SetSecurityCheckboxesWithoutTriggeringChange).

        _currentSecurityOptions = updated;
        SetSecurityCheckboxesWithoutTriggeringChange(); // mantiene sincronizadas ambas pantallas

        _logger.Info(isDefender
            ? $"[SECURITY] Mantener Microsoft Defender: {(updated.KeepDefender ? "true" : "false")}"
            : $"[SECURITY] Mantener Windows Update: {(updated.KeepWindowsUpdate ? "true" : "false")}");

        RebuildCatalogWithCurrentSecurityOptions();
    }

    /// <summary>
    /// Selección -&gt; Catálogo -&gt; Protección -&gt; Dependencias -&gt; RemovalPlan, recalculado en
    /// vivo con cada checkbox. Nunca ejecuta DISM ni toca el WIM.
    /// </summary>
    private RemovalPlan? BuildPlanFromCurrentSelection()
    {
        if (_catalog is null)
            return null;

        var selections = _allComponentRows
            .Select(row => new ComponentSelection(row.Component.Id, row.IsSelected))
            .ToList();

        var imageId = _imageInfo is null
            ? "Imagen sin identificar"
            : $"{_imageInfo.OperatingSystem} {_imageInfo.DisplayVersion} ({(EditionCombo.SelectedItem as ImageEdition)?.Name})";

        return _removalPlanBuilder.Build(_inventory ?? new ImageInventory(), _catalog, selections, imageId);
    }

    private void UpdateSelectionSummary()
    {
        var plan = BuildPlanFromCurrentSelection();
        _pendingPlan = plan;

        SelectionSummaryText.Text = plan is null
            ? "Seleccionados: 0    Permitidos: 0    Bloqueados: 0    ⚠ Advertencias: 0"
            : $"Seleccionados: {plan.TotalSelected}    Permitidos: {plan.TotalAllowed}    " +
              $"Bloqueados: {plan.TotalBlocked}    ⚠ Advertencias: {plan.Warnings.Count}";

        // P23: 0 seleccionados es un estado válido ("no se eliminará nada"), no
        // un motivo para bloquear "Ver plan" -- pero el botón tampoco se activa
        // solo porque exista un catálogo si el plan no se pudo construir (p. ej.
        // sin inventario todavía).
        ViewPlanButton.IsEnabled = plan is { IsValid: true };
    }

    private void ViewPlanButton_Click(object sender, RoutedEventArgs e)
    {
        var plan = BuildPlanFromCurrentSelection();
        if (plan is null)
            return;

        _pendingPlan = plan;
        ShowPlan(plan);
    }

    // ---- Pantalla de plan de modificación ----------------------------------

    private void ShowPlan(RemovalPlan plan)
    {
        PlanItemsList.ItemsSource = plan.Components.Select(item => new PlanItemRow(item)).ToList();
        PlanSummaryText.Text =
            $"{plan.TotalSelected} seleccionados    {plan.TotalAllowed} acciones permitidas    " +
            $"{plan.TotalBlocked} bloqueados    {plan.Warnings.Count} advertencias";

        // P23: 0 seleccionados es un resultado válido ("no se eliminará nada"),
        // distinto de "hay seleccionados pero todos están bloqueados" (ese caso
        // sigue mostrando la lista normal, con cada componente en rojo/bloqueado).
        NoRemovalsBanner.Visibility = plan.TotalSelected == 0 ? Visibility.Visible : Visibility.Collapsed;

        // P24: el texto deja claro que "0 eliminaciones" es un resultado válido
        // que igualmente genera una ISO -- nunca "no hay nada que hacer".
        PlanConfirmButton.Content = plan.TotalSelected == 0 ? "GENERAR ISO" : "CONFIRMAR Y GENERAR ISO";

        // Las opciones de instalación se muestran siempre, con o sin eliminaciones.
        RefreshInstallationOptionsCheckboxes();

        ComponentsOverlay.Visibility = Visibility.Collapsed;
        PlanOverlay.Visibility = Visibility.Visible;
        StatusText.Text = "Plan de modificación generado";
    }

    // ---- Opciones de instalación / OOBE / compatibilidad (P15) ---------------
    //
    // Independiente del catálogo de componentes y de RemovalPlan (P15, sección 1):
    // no decide qué se elimina ni afecta a ninguna protección. Solo configura el
    // comportamiento del instalador/OOBE de la ISO final. MRS.ISOEngine todavía no
    // genera ninguna ISO: esto solo captura la configuración para cuando exista esa
    // fase (ver prompts/15-resultado.md).

    private void RefreshInstallationOptionsCheckboxes()
    {
        var checkBoxes = new[]
        {
            AllowLocalAccountCheckBox, AllowOfflineOobeCheckBox,
            BypassTpmCheckBox, BypassSecureBootCheckBox, BypassCpuCheckBox, BypassRamCheckBox, BypassStorageCheckBox,
        };

        foreach (var checkBox in checkBoxes)
        {
            checkBox.Checked -= InstallationOption_Changed;
            checkBox.Unchecked -= InstallationOption_Changed;
        }

        AllowLocalAccountCheckBox.IsChecked = _installationOptions.AllowLocalAccount;
        AllowOfflineOobeCheckBox.IsChecked = _installationOptions.AllowOfflineOobe;
        BypassTpmCheckBox.IsChecked = _installationOptions.BypassTpm;
        BypassSecureBootCheckBox.IsChecked = _installationOptions.BypassSecureBoot;
        BypassCpuCheckBox.IsChecked = _installationOptions.BypassCpu;
        BypassRamCheckBox.IsChecked = _installationOptions.BypassRam;
        BypassStorageCheckBox.IsChecked = _installationOptions.BypassStorage;

        foreach (var checkBox in checkBoxes)
        {
            checkBox.Checked += InstallationOption_Changed;
            checkBox.Unchecked += InstallationOption_Changed;
        }
    }

    /// <summary>
    /// Solo actualiza <see cref="_installationOptions"/> en memoria; nunca ejecuta
    /// nada sobre Windows, sobre la imagen ni sobre ningún WIM. Cada casilla es
    /// independiente: desactivar una nunca cambia ninguna otra.
    /// </summary>
    private void InstallationOption_Changed(object sender, RoutedEventArgs e)
    {
        // Varias casillas fijan IsChecked="True" directamente en el XAML, lo que
        // dispara este evento durante InitializeComponent(), antes de que los
        // demás CheckBox con x:Name de este panel estén conectados a sus campos
        // (siguen siendo null en ese momento) -> NullReferenceException. La
        // ventana (this) no queda IsInitialized hasta que EndInit() se completa
        // al final de InitializeComponent(), así que basta con salir aquí; una
        // vez inicializada la ventana, el comportamiento es el de siempre.
        if (!IsInitialized)
            return;

        _installationOptions = _installationOptions with
        {
            AllowLocalAccount = AllowLocalAccountCheckBox.IsChecked == true,
            AllowOfflineOobe = AllowOfflineOobeCheckBox.IsChecked == true,
            BypassTpm = BypassTpmCheckBox.IsChecked == true,
            BypassSecureBoot = BypassSecureBootCheckBox.IsChecked == true,
            BypassCpu = BypassCpuCheckBox.IsChecked == true,
            BypassRam = BypassRamCheckBox.IsChecked == true,
            BypassStorage = BypassStorageCheckBox.IsChecked == true,
        };
    }

    private void PlanBackButton_Click(object sender, RoutedEventArgs e)
    {
        PlanOverlay.Visibility = Visibility.Collapsed;
        ComponentsOverlay.Visibility = Visibility.Visible;
        StatusText.Text = "Catálogo generado";
    }

    private void PlanCancelSelectionButton_Click(object sender, RoutedEventArgs e)
    {
        foreach (var row in _allComponentRows)
            row.IsSelected = false;

        RenderComponents(); // refresca los checkboxes visibles
        UpdateSelectionSummary();
        _logger.Info("Selección de componentes cancelada.");

        PlanOverlay.Visibility = Visibility.Collapsed;
        ComponentsOverlay.Visibility = Visibility.Visible;
    }

    private async void PlanConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isApplyingChanges)
            return; // ya hay una aplicación de cambios en curso: no se permite solapar otra.

        var plan = BuildPlanFromCurrentSelection();
        if (plan is null)
            return;

        _confirmedPlan = plan;
        _logger.Info($"Plan de modificación confirmado y guardado en memoria: {plan.TotalSelected} seleccionado(s), " +
                     $"{plan.TotalAllowed} acción(es) pendiente(s), {plan.TotalBlocked} bloqueada(s). No se ha modificado el WIM.");
        ShowPlan(plan);

        // P23: distinguir explícitamente los tres casos posibles.
        //   A) 0 seleccionados            -> "no se eliminará nada", continuar.
        //   B) seleccionados pero todos bloqueados -> mantener el bloqueo, no generar.
        //   C) hay acciones permitidas    -> flujo de eliminación de siempre.
        // 0 seleccionados NUNCA es un error: es distinto de "0 permitidos porque
        // todo está bloqueado", que sí sigue impidiendo continuar.
        if (plan.TotalSelected > 0 && plan.TotalAllowed == 0)
        {
            MessageBox.Show(this,
                "No hay ninguna acción permitida: todos los componentes seleccionados están bloqueados. " +
                "No se aplicará ningún cambio.",
                "MRS Windows Builder", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // Nunca se comienza sin confirmación explícita (Parte 25 del prompt).
        var confirmation = MessageBox.Show(this,
            "Se creará una copia de trabajo de la imagen original.\n" +
            "El original no será modificado.\n" +
            "Si una operación falla, los cambios se descartarán.",
            "Aplicar cambios", MessageBoxButton.OKCancel, MessageBoxImage.Warning, MessageBoxResult.Cancel);

        if (confirmation != MessageBoxResult.OK)
            return;

        var isoPath = IsoPathBox.Text;
        var edition = EditionCombo.SelectedItem as ImageEdition;
        if (string.IsNullOrWhiteSpace(isoPath) || edition is null)
            return;

        await RunIsoGenerationAsync(isoPath, edition.Index, plan);
    }

    // ---- Pantalla de generación de ISO (P24: MRS.ISOEngine.IsoGenerationPipeline) ----
    //
    // MainWindow no reimplementa ninguna fase (workspace, árbol de la ISO,
    // boot.wim, install.wim, PostInstall, validación, oscdimg): todas viven en
    // IsoGenerationPipeline (P19/P20/P23), que ya conoce el caso "0 eliminaciones"
    // (RemovalEngine se salta el montaje/commit por sí solo, ver P23). Esta
    // pantalla solo construye la solicitud, invoca el pipeline y presenta su
    // progreso -- ningún dato se decide aquí que no viniera ya de la UI.

    private static readonly (string Stage, string Label)[] PipelineStageLabels =
    {
        ("Comprobación de entorno", "Validando entorno"),
        ("Validación", "Validando entorno"),
        ("Generation workspace", "Preparando workspace"),
        ("Preparando árbol de la ISO", "Copiando árbol ISO"),
        ("Modificando boot.wim", "Preparando boot.wim"),
        ("Integración PostInstall", "Preparando PostInstall"),
        ("Validación final", "Validación final"),
        ("Generando ISO (oscdimg)", "Creando ISO con oscdimg"),
        ("Finalizando", "Finalizando"),
    };

    private async Task RunIsoGenerationAsync(string isoPath, int index, RemovalPlan plan)
    {
        // P24, sección POSTINSTALL: no existe todavía ningún mecanismo en la UI
        // para seleccionar los instaladores reales (.NET Desktop Runtime/PCPI);
        // activarlo sin eso produciría una solicitud que la validación (P18)
        // rechazaría siempre por archivos inexistentes. Se documenta como
        // limitación conocida en vez de inventar rutas (ver prompts/24-resultado.md).
        const bool postInstallEnabled = false;
        var outputIsoPath = BuildOutputIsoPath(isoPath);

        ShowIsoGenerationScreen(plan, postInstallEnabled);
        _executionCts = new CancellationTokenSource();
        _isApplyingChanges = true;
        _executionUiUnlocked = false;

        // System.Progress<T> reenvía cada Report() al SynchronizationContext
        // capturado aquí (el de la UI), así que OnIsoGenerationProgress se
        // ejecuta siempre en el hilo de la ventana sin bloquearlo.
        // IsoGenerationPipeline no conoce WPF: solo ve un
        // IProgress<InstallationProgressInfo> (mismo patrón que P10/P22).
        IProgress<InstallationProgressInfo> progress = new Progress<InstallationProgressInfo>(OnIsoGenerationProgress);

        if (plan.TotalSelected == 0)
            _logger.Info("[REMOVAL] Sin eliminaciones de componentes.");

        var request = new IsoGenerationRequest
        {
            SourceIsoPath = isoPath,
            EditionIndex = index,
            Architecture = "amd64",
            // InstallationOptions se transmite tal cual: esta pantalla no decide
            // ni cambia ningún valor (cuenta local, OOBE offline, bypasses).
            InstallationOptions = _installationOptions,
            AccountConfiguration = new AutounattendConfiguration(),
            RemovalPlan = plan,
            PostInstallConfiguration = new PostInstallConfiguration { Enabled = postInstallEnabled },
            PostInstallSourceFiles = new PostInstallSourceFiles(),
            OutputIsoPath = outputIsoPath,
        };

        try
        {
            var result = await _isoGenerationPipeline.GenerateAsync(request, _executionCts.Token, progress);

            UnlockExecutionUi();

            if (result.Success)
            {
                MarkAllPhaseRowsDone();
                StatusText.Text = "ISO generada";
                ExecutionStatusText.Text = $"ISO generada correctamente. Ruta: {result.OutputIsoPath}";
                MessageBox.Show(this, $"ISO generada correctamente\nRuta: {result.OutputIsoPath}",
                    "MRS Windows Builder", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MarkCurrentPhaseRow(ExecutionRowStatus.Error);
                StatusText.Text = "Error al generar la ISO";
                ExecutionStatusText.Text = result.Errors.Count > 0
                    ? string.Join(" ", result.Errors)
                    : "No se pudo generar la ISO. Revisa el registro.";
                MessageBox.Show(this, "No se ha podido generar la ISO. Revisa el registro para ver qué fase falló.",
                    "MRS Windows Builder", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (OperationCanceledException)
        {
            MarkCurrentPhaseRow(ExecutionRowStatus.Cancelled);
            ExecutionStatusText.Text = "Generación cancelada. No se ha creado ninguna ISO final.";
            StatusText.Text = "Operación cancelada";
        }
        catch (Exception ex)
        {
            _logger.Error($"Error inesperado al generar la ISO: {ex.Message}");
            MarkCurrentPhaseRow(ExecutionRowStatus.Error);
            ExecutionStatusText.Text = "Error inesperado al generar la ISO.";
            StatusText.Text = "Error";
            MessageBox.Show(this, "No se ha podido generar la ISO.",
                "MRS Windows Builder", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            // Red de seguridad: garantiza que la UI queda desbloqueada pase lo que
            // pase (fallo antes de empezar, cancelación temprana, excepción
            // inesperada). Idempotente: si ya se desbloqueó arriba, no hace nada.
            UnlockExecutionUi();
            _executionCts?.Dispose();
            _executionCts = null;
        }
    }

    /// <summary>Ruta determinista junto a la ISO original: nunca sobrescribe la ISO de origen.</summary>
    private static string BuildOutputIsoPath(string sourceIsoPath)
    {
        var directory = Path.GetDirectoryName(sourceIsoPath);
        var name = Path.GetFileNameWithoutExtension(sourceIsoPath);
        return Path.Combine(string.IsNullOrEmpty(directory) ? "." : directory, $"{name}-MRS.iso");
    }

    /// <summary>
    /// Marca terminada la generación de ISO: desactiva Cancelar, habilita Cerrar
    /// y permite iniciar una nueva operación. Idempotente a propósito, para
    /// poder llamarse tanto nada más terminar como, de forma defensiva, en el
    /// <c>finally</c>.
    /// </summary>
    private void UnlockExecutionUi()
    {
        if (_executionUiUnlocked)
            return;

        _executionUiUnlocked = true;
        _isApplyingChanges = false;
        ExecutionCancelButton.IsEnabled = false;
        ExecutionCloseButton.IsEnabled = true;
    }

    private void ShowIsoGenerationScreen(RemovalPlan plan, bool postInstallEnabled)
    {
        var labels = new List<string>
        {
            "Preparando generación",
            "Validando entorno",
            "Preparando workspace",
            "Copiando árbol ISO",
            "Preparando boot.wim",
            "Preparando install.wim",
        };

        // Fases condicionales: si no aplican, no se muestran como si se
        // hubieran ejecutado (P24, "FASES VISIBLES").
        if (plan.TotalSelected > 0)
            labels.Add("Aplicando eliminaciones");
        if (postInstallEnabled)
            labels.Add("Preparando PostInstall");

        labels.Add("Validación final");
        labels.Add("Creando ISO con oscdimg");
        labels.Add("Finalizando");
        labels.Add("100 % completado");

        _isoPhaseRows = labels.Select(l => new IsoPhaseRow(l)).ToList();

        ExecutionStatusText.Text = "Preparando generación...";
        ExecutionProgressText.Text = $"Fase 1 / {_isoPhaseRows.Count}";
        ExecutionStageText.Text = "Etapa: --";
        ExecutionPercentText.Text = "0%";
        ExecutionProgressBar.Value = 0;
        ExecutionTerminal.Document.Blocks.Clear();
        ExecutionCancelButton.IsEnabled = true;
        ExecutionCloseButton.IsEnabled = false;
        RefreshExecutionList();

        PlanOverlay.Visibility = Visibility.Collapsed;
        ExecutionOverlay.Visibility = Visibility.Visible;
        StatusText.Text = "Generando ISO...";
    }

    /// <summary>
    /// Único punto de entrada de la telemetría de progreso hacia la UI: actualiza
    /// la barra/porcentaje/etapa/mensaje, la lista de fases y el terminal. No
    /// contiene ninguna lógica de negocio; solo presentación. El porcentaje y las
    /// fases son exactamente los que reporta <see cref="IIsoGenerationPipeline"/>
    /// -- nunca se inventa aquí ningún progreso interno de DISM.
    /// </summary>
    private void OnIsoGenerationProgress(InstallationProgressInfo info)
    {
        ExecutionProgressBar.Value = info.Percent;
        ExecutionPercentText.Text = $"{info.Percent}%";
        ExecutionStageText.Text = $"Etapa: {info.Stage}";
        ExecutionStatusText.Text = info.Message;
        AppendTerminalLine(info);

        var label = MapStageToPhaseLabel(info.Stage, info.Percent);
        if (label is not null)
            AdvancePhaseRows(label, info.Level);

        UpdatePhaseProgressText();
        RefreshExecutionList();
    }

    /// <summary>
    /// Traduce el nombre de fase real del pipeline (P19/P20) a la etiqueta visible
    /// de la pantalla (P24). "Modificando install.wim" cubre tanto la exportación
    /// de la edición Pro como, si hay eliminaciones, la ejecución del RemovalPlan
    /// sobre esa misma copia (P19 nunca separa ambas en fases DISM distintas):
    /// el umbral del 60% es el propio punto en el que WorkingImageFactory termina
    /// (P19, "Exportación de la imagen de trabajo") y RemovalEngine tomaría el
    /// relevo si hubiera acciones que ejecutar.
    /// </summary>
    private string? MapStageToPhaseLabel(string stage, int percent)
    {
        if (stage == "Modificando install.wim")
        {
            var hasRemovalsRow = _isoPhaseRows.Any(r => r.DisplayName == "Aplicando eliminaciones");
            return hasRemovalsRow && percent >= 60 ? "Aplicando eliminaciones" : "Preparando install.wim";
        }

        foreach (var (pipelineStage, label) in PipelineStageLabels)
            if (pipelineStage == stage)
                return label;

        return null;
    }

    private void AdvancePhaseRows(string label, InstallationProgressLevel level)
    {
        var index = _isoPhaseRows.FindIndex(r => r.DisplayName == label);
        if (index < 0)
            return;

        for (var i = 0; i < index; i++)
            if (_isoPhaseRows[i].Status is ExecutionRowStatus.Pending or ExecutionRowStatus.InProgress)
                _isoPhaseRows[i].Status = ExecutionRowStatus.Done;

        _isoPhaseRows[index].Status = level == InstallationProgressLevel.Error
            ? ExecutionRowStatus.Error
            : ExecutionRowStatus.InProgress;

        // "Preparando generación" no está ligada a ningún stage del pipeline
        // (es la preparación que hace esta pantalla antes de invocarlo): se
        // marca completada en cuanto llega el primer progreso real.
        if (_isoPhaseRows.Count > 0 && _isoPhaseRows[0].Status == ExecutionRowStatus.Pending)
            _isoPhaseRows[0].Status = ExecutionRowStatus.Done;
    }

    private void MarkAllPhaseRowsDone()
    {
        foreach (var row in _isoPhaseRows)
            if (row.Status != ExecutionRowStatus.Error)
                row.Status = ExecutionRowStatus.Done;

        RefreshExecutionList();
    }

    private void MarkCurrentPhaseRow(ExecutionRowStatus status)
    {
        var current = _isoPhaseRows.FirstOrDefault(r => r.Status == ExecutionRowStatus.InProgress);
        if (current is not null)
            current.Status = status;

        RefreshExecutionList();
    }

    private void UpdatePhaseProgressText()
    {
        if (_isoPhaseRows.Count == 0)
            return;

        var done = _isoPhaseRows.Count(r => r.Status == ExecutionRowStatus.Done);
        ExecutionProgressText.Text = $"Fase {Math.Min(done + 1, _isoPhaseRows.Count)} / {_isoPhaseRows.Count}";
    }

    private void AppendTerminalLine(InstallationProgressInfo info)
    {
        var color = info.Level switch
        {
            InstallationProgressLevel.Success => Brushes.LightGreen,
            InstallationProgressLevel.Warning => Brushes.Khaki,
            InstallationProgressLevel.Error => Brushes.IndianRed,
            _ => new SolidColorBrush(Color.FromRgb(0xB6, 0xF5, 0xC8)),
        };

        var source = info.Level switch
        {
            InstallationProgressLevel.Success => "OK    ",
            InstallationProgressLevel.Warning => "WARN  ",
            InstallationProgressLevel.Error => "ERROR ",
            _ when info.Message.StartsWith("DISM:", StringComparison.OrdinalIgnoreCase) => "DISM  ",
            _ => "MRS   ",
        };

        var line = $"[{info.Timestamp:HH:mm:ss}] {source} {info.Message}";
        var paragraph = new Paragraph(new Run(line) { Foreground = color }) { Margin = new Thickness(0, 0, 0, 2) };

        ExecutionTerminal.Document.Blocks.Add(paragraph);
        ExecutionTerminal.ScrollToEnd();
    }

    private void RefreshExecutionList()
        => ExecutionList.ItemsSource = _isoPhaseRows.ToList();

    private void ExecutionCancelButton_Click(object sender, RoutedEventArgs e)
    {
        _executionCts?.Cancel();
        ExecutionCancelButton.IsEnabled = false;
        _logger.Info("Cancelación solicitada por el usuario.");
    }

    private void ExecutionCloseButton_Click(object sender, RoutedEventArgs e)
    {
        ExecutionOverlay.Visibility = Visibility.Collapsed;
        ComponentsOverlay.Visibility = Visibility.Visible;
        StatusText.Text = "Catálogo generado";
    }

    private static string JoinNames(IEnumerable<ComponentDefinition>? components)
    {
        var names = components?.Select(c => c.DisplayName.Length > 0 ? c.DisplayName : c.Name).ToList();
        return names is { Count: > 0 } ? string.Join(", ", names) : "--";
    }

    // ---- Estado de la interfaz ------------------------------------------------

    private void SetBusy(bool busy)
    {
        SelectIsoButton.IsEnabled = !busy;
        AnalyzeButton.IsEnabled = !busy && (_iso?.ImageFound ?? false);
        EditionCombo.IsEnabled = !busy && EditionCombo.Items.Count > 0;
        ContinueButton.IsEnabled = !busy && EditionCombo.SelectedItem is ImageEdition;
    }

    private void ResetImageInfo()
    {
        _imageInfo = null;
        _inventory = null;
        _catalog = null;
        _allComponentRows = new List<ComponentRow>();
        _pendingPlan = null;
        _confirmedPlan = null;
        _isoPhaseRows = new List<IsoPhaseRow>();
        InventoryOverlay.Visibility = Visibility.Collapsed;
        ComponentsOverlay.Visibility = Visibility.Collapsed;
        PlanOverlay.Visibility = Visibility.Collapsed;
        ExecutionOverlay.Visibility = Visibility.Collapsed;

        InfoOs.Text = "--";
        InfoVersion.Text = "--";
        InfoBuild.Text = "--";
        InfoArch.Text = "--";
        InfoLang.Text = "--";
        InfoType.Text = "--";

        EditionCombo.ItemsSource = null;
        EditionCombo.Items.Clear();
        EditionCombo.IsEnabled = false;
        ContinueButton.IsEnabled = false;
    }

    private void PopulateImageInfo(ImageInfo info)
    {
        InfoOs.Text = Dash(info.OperatingSystem);
        InfoVersion.Text = Dash(info.DisplayVersion);
        InfoBuild.Text = Dash(info.Build);
        InfoArch.Text = info.Architecture == ImageArchitecture.Unknown
            ? "--"
            : info.Architecture.ToString().ToLowerInvariant();
        InfoLang.Text = Dash(info.Language);
        InfoType.Text = info.Format == ImageFormat.Unknown
            ? "--"
            : info.Format.ToString().ToUpperInvariant();

        EditionCombo.ItemsSource = info.Editions;
        EditionCombo.DisplayMemberPath = nameof(ImageEdition.Name);
        EditionCombo.SelectedIndex = -1;
        EditionCombo.IsEnabled = info.Editions.Count > 0;
    }

    private static string Dash(string? value)
        => string.IsNullOrWhiteSpace(value) ? "--" : value;
}

/// <summary>Fila mostrada en la tabla de inventario (solo presentación).</summary>
public sealed record InventoryRow(string Name, string State, string Details);

/// <summary>Opción del ComboBox de filtro de la pantalla de componentes.</summary>
public sealed record FilterOption(string Label, CatalogFilterKind Kind);

/// <summary>
/// Fila de la pantalla COMPONENTES: envuelve un <see cref="ComponentDefinition"/>
/// con el estado de selección de la casilla. Puramente de presentación; el
/// catálogo en sí no sabe nada de la UI.
/// </summary>
public sealed class ComponentRow
{
    public ComponentRow(ComponentDefinition component)
    {
        Component = component;
        Editable = component.Protection != ComponentProtection.Protected;
    }

    public ComponentDefinition Component { get; }

    public string DisplayName => Component.DisplayName.Length > 0 ? Component.DisplayName : Component.Name;

    public string CategoryLabel => Component.Category.ToString();

    /// <summary>Falso para componentes protegidos: la casilla queda bloqueada.</summary>
    public bool Editable { get; }

    public Visibility LockVisibility => Editable ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>Selección de preparación; no dispara ninguna modificación de la imagen.</summary>
    public bool IsSelected { get; set; }

    public string StatusLabel => Component.Protection switch
    {
        ComponentProtection.Protected => "🔒 Protegido",
        ComponentProtection.Removable => "🟢 Removible",
        ComponentProtection.Optional => "🟡 Opcional",
        ComponentProtection.Recommended => "🔵 Recomendado",
        _ => "⚪ Desconocido",
    };
}

/// <summary>
/// Fila de la pantalla PLAN DE MODIFICACIÓN: presenta un
/// <see cref="RemovalPlanItem"/> con el estilo "✓ ELIMINAR" / "🔒 BLOQUEADO"
/// del mockup. Puramente de presentación.
/// </summary>
public sealed class PlanItemRow
{
    private static readonly Brush AllowedBrush = FrozenBrush(0x3F, 0xD0, 0x7A);
    private static readonly Brush BlockedBrush = FrozenBrush(0xE5, 0x48, 0x4D);

    public PlanItemRow(RemovalPlanItem item) => Item = item;

    public RemovalPlanItem Item { get; }

    private bool Blocked => !Item.Allowed;

    public string StatusText => Blocked ? "🔒 BLOQUEADO" : "✓ ELIMINAR";

    public Brush StatusBrush => Blocked ? BlockedBrush : AllowedBrush;

    public string Name => Item.DisplayName;

    public string Subtitle => Blocked
        ? Item.Category.ToString()
        : $"{Item.Category} · {Item.Action}";

    public string? Detail => Blocked ? Item.BlockReason : $"Riesgo: {RiskLabel(Item.Risk)}";

    public Visibility DetailVisibility => string.IsNullOrWhiteSpace(Detail) ? Visibility.Collapsed : Visibility.Visible;

    private static string RiskLabel(ComponentRisk risk) => risk switch
    {
        ComponentRisk.Critical => "Crítico",
        ComponentRisk.High => "Alto",
        ComponentRisk.Medium => "Medio",
        ComponentRisk.Low => "Bajo",
        _ => "Desconocido",
    };

    private static Brush FrozenBrush(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}

/// <summary>Estado visual de una fila en la pantalla APLICANDO CAMBIOS.</summary>
public enum ExecutionRowStatus
{
    Pending,
    InProgress,
    Done,
    Error,
    Skipped,
    Cancelled,
}

/// <summary>Fila de la pantalla GENERANDO ISO (P24): una fase del pipeline y su estado visual.</summary>
public sealed class IsoPhaseRow
{
    public IsoPhaseRow(string displayName) => DisplayName = displayName;

    public string DisplayName { get; }
    public ExecutionRowStatus Status { get; set; } = ExecutionRowStatus.Pending;

    private string Icon => Status switch
    {
        ExecutionRowStatus.Pending => "○",
        ExecutionRowStatus.InProgress => "⏳",
        ExecutionRowStatus.Done => "✓",
        ExecutionRowStatus.Error => "✗",
        ExecutionRowStatus.Skipped => "⊘",
        ExecutionRowStatus.Cancelled => "⊘",
        _ => "○",
    };

    public string Label => $"{Icon} {DisplayName}";
}
