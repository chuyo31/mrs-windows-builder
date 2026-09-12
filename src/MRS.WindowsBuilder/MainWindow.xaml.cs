using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
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
using MRS.RemovalEngine;
using MRS.RemovalEngine.Models;
using MRS.RemovalPlanning;
using MRS.RemovalPlanning.Models;
// "RemovalEngine" es a la vez el nombre de un namespace (MRS.RemovalEngine) y de
// la clase que contiene; dentro del árbol de namespaces "MRS.*" el namespace
// siempre gana en la búsqueda de nombres sin cualificar, así que se referencia
// la clase con un alias explícito.
using RemovalEngineClass = MRS.RemovalEngine.RemovalEngine;

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

    private readonly IIsoMounter _isoMounter;
    private readonly IWorkingImageFactory _workingImageFactory;
    private readonly RemovalEngineClass _removalEngine;
    private readonly RemovalVerifier _removalVerifier = new();

    private IsoInspectionResult? _iso;
    private ImageInfo? _imageInfo;
    private ImageInventory? _inventory;
    private ComponentCatalogResult? _catalog;
    private List<ComponentRow> _allComponentRows = new();
    private RemovalPlan? _pendingPlan;
    private RemovalPlan? _confirmedPlan;
    private List<ExecutionRow> _executionRows = new();
    private CancellationTokenSource? _executionCts;
    private bool _isApplyingChanges;
    private bool _executionUiUnlocked;
    private string? _selectedProfile;

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

        _logger.Info("MRS Windows Builder iniciado");
        _logger.Info("Esperando seleccionar una ISO");
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

    private void Profile_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb)
        {
            _selectedProfile = rb.Content?.ToString();
            _logger.Info($"Perfil seleccionado: {_selectedProfile}");
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

        try
        {
            var result = await _inventoryService.BuildInventoryFromIsoAsync(isoPath, edition.Index);
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
        }
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
        _catalog = catalog;

        ComponentsSummary.Text =
            $"{catalog.TotalCount} componentes · {catalog.ProtectedCount} protegidos · " +
            $"{catalog.RemovableCount} removibles · {catalog.OptionalCount} opcionales · " +
            $"{catalog.CriticalCount} críticos";

        _allComponentRows = catalog.Components
            .Select(c => new ComponentRow(c))
            .OrderBy(r => r.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

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

        InventoryOverlay.Visibility = Visibility.Collapsed;
        ComponentsOverlay.Visibility = Visibility.Visible;
        StatusText.Text = "Catálogo generado";
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

        ViewPlanButton.IsEnabled = (plan?.TotalSelected ?? 0) > 0;
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

        ComponentsOverlay.Visibility = Visibility.Collapsed;
        PlanOverlay.Visibility = Visibility.Visible;
        StatusText.Text = "Plan de modificación generado";
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
        _logger.Info($"Plan de modificación confirmado y guardado en memoria: {plan.TotalAllowed} acción(es) " +
                     $"pendiente(s), {plan.TotalBlocked} bloqueada(s). No se ha modificado el WIM.");
        ShowPlan(plan);

        if (plan.TotalAllowed == 0)
        {
            MessageBox.Show(this, "El plan no tiene ninguna acción permitida; no hay nada que aplicar.",
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

        await RunExecutionAsync(isoPath, edition.Index, plan);
    }

    // ---- Pantalla de ejecución (RemovalEngine) -----------------------------

    private async Task RunExecutionAsync(string isoPath, int index, RemovalPlan plan)
    {
        ShowExecutionScreen(plan);
        _executionCts = new CancellationTokenSource();
        _isApplyingChanges = true;
        _executionUiUnlocked = false;
        _logger.Entry += OnExecutionLogEntry;

        WorkingImage? workingImage = null;

        try
        {
            ExecutionStatusText.Text = "Preparando imagen...";
            _logger.Info("Preparando imagen...");

            string wimPath;
            await using (var isoMount = await _isoMounter.MountAsync(isoPath, _executionCts.Token))
            {
                wimPath = LocateInstallImage(isoMount.RootPath)
                    ?? throw new FileNotFoundException(@"La ISO no contiene sources\install.wim ni sources\install.esd.");

                ExecutionStatusText.Text = "Creando imagen de trabajo...";
                workingImage = await _workingImageFactory.CreateAsync(wimPath, index, workspaceRoot: null, _executionCts.Token);
            }

            ExecutionStatusText.Text = "Montando imagen...";
            var result = await _removalEngine.ExecuteAsync(workingImage, plan, _executionCts.Token);

            ReconcileExecutionRows(result);

            // La operación transaccional (Apply -> Commit -> Unmount -> comprobación
            // interna de montajes) ya ha terminado POR COMPLETO en este punto: la UI
            // se desbloquea aquí, antes de mostrar cualquier aviso o de lanzar el
            // reinventario opcional, para que "Cerrar" nunca dependa de un DISM extra.
            UnlockExecutionUi();

            if (result.Committed)
            {
                StatusText.Text = "Cambios aplicados";
                MessageBox.Show(this, $"Cambios aplicados correctamente ({result.ActionsExecuted.Count} acción(es)).",
                    "MRS Windows Builder", MessageBoxButton.OK, MessageBoxImage.Information);

                // Diagnóstico best-effort: no forma parte de la operación transaccional
                // y su duración (o un fallo) nunca debe volver a bloquear la UI.
                await VerifyExecutionAsync(workingImage, result);
            }
            else if (result.Phase == RemovalExecutionPhase.Cancelled)
            {
                StatusText.Text = "Operación cancelada";
                ExecutionStatusText.Text = "Operación cancelada. La imagen de trabajo no ha sido modificada.";
            }
            else
            {
                StatusText.Text = "Error al aplicar cambios";
                ExecutionStatusText.Text = "No se pudieron aplicar los cambios. Revisa el registro.";
                MessageBox.Show(this, "No se han podido aplicar los cambios. Los cambios se han descartado.",
                    "MRS Windows Builder", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (OperationCanceledException)
        {
            ExecutionStatusText.Text = "Operación cancelada. La imagen de trabajo no ha sido modificada.";
            StatusText.Text = "Operación cancelada";
        }
        catch (Exception ex)
        {
            _logger.Error($"Error inesperado al aplicar cambios: {ex.Message}");
            ExecutionStatusText.Text = "Error al preparar la imagen de trabajo.";
            StatusText.Text = "Error";
            MessageBox.Show(this, "No se han podido aplicar los cambios.",
                "MRS Windows Builder", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            // Red de seguridad: garantiza que la UI queda desbloqueada pase lo que
            // pase (fallo antes de montar, cancelación temprana, excepción
            // inesperada). Idempotente: si ya se desbloqueó arriba, no hace nada.
            UnlockExecutionUi();
            _executionCts?.Dispose();
            _executionCts = null;
        }
    }

    /// <summary>
    /// Marca terminada la operación de aplicación de cambios: desactiva Cancelar,
    /// habilita Cerrar y permite iniciar una nueva operación. Idempotente a
    /// propósito, para poder llamarse tanto nada más terminar la ejecución como,
    /// de forma defensiva, en el <c>finally</c>.
    /// </summary>
    private void UnlockExecutionUi()
    {
        if (_executionUiUnlocked)
            return;

        _executionUiUnlocked = true;
        _isApplyingChanges = false;
        _logger.Entry -= OnExecutionLogEntry;
        ExecutionCancelButton.IsEnabled = false;
        ExecutionCloseButton.IsEnabled = true;
    }

    private async Task VerifyExecutionAsync(WorkingImage workingImage, RemovalExecutionResult result)
    {
        try
        {
            ExecutionStatusText.Text = "Verificando cambios...";
            var reinventory = await _inventoryService.BuildInventoryAsync(workingImage.WorkingWimPath, workingImage.Index);
            var verification = _removalVerifier.Verify(result, _inventory ?? new ImageInventory(), reinventory.Inventory);

            _logger.Info($"Reinventario: {verification.Removed.Count} confirmado(s) eliminado(s), " +
                         $"{verification.StillPresent.Count} todavía presente(s), " +
                         $"{verification.UnexpectedChanges.Count} cambio(s) inesperado(s).");

            ExecutionStatusText.Text = $"Cambios aplicados y verificados: {verification.Removed.Count} confirmado(s).";
        }
        catch (Exception ex)
        {
            _logger.Warn($"No se pudo verificar el resultado sobre la imagen de trabajo: {ex.Message}");
        }
    }

    private void ShowExecutionScreen(RemovalPlan plan)
    {
        _executionRows = plan.Components
            .Where(c => c.Allowed)
            .Select(c => new ExecutionRow(c.ComponentId, c.DisplayName))
            .ToList();

        ExecutionStatusText.Text = "Preparando imagen...";
        ExecutionProgressText.Text = $"0 / {_executionRows.Count}";
        ExecutionCancelButton.IsEnabled = true;
        ExecutionCloseButton.IsEnabled = false;
        RefreshExecutionList();

        PlanOverlay.Visibility = Visibility.Collapsed;
        ExecutionOverlay.Visibility = Visibility.Visible;
        StatusText.Text = "Aplicando cambios...";
    }

    private void OnExecutionLogEntry(object? sender, LogEntry entry)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => OnExecutionLogEntry(sender, entry));
            return;
        }

        const string startPrefix = "[REMOVAL] Inicio: ";
        const string doneSuffix = " eliminado correctamente.";

        if (entry.Message.StartsWith(startPrefix, StringComparison.Ordinal))
        {
            var name = entry.Message[startPrefix.Length..];
            var row = _executionRows.FirstOrDefault(r => r.DisplayName == name);
            if (row is not null) row.Status = ExecutionRowStatus.InProgress;
            ExecutionStatusText.Text = "Aplicando cambios...";
            RefreshExecutionList();
        }
        else if (entry.Message.EndsWith(doneSuffix, StringComparison.Ordinal))
        {
            var name = entry.Message[..^doneSuffix.Length];
            var row = _executionRows.FirstOrDefault(r => r.DisplayName == name);
            if (row is not null) row.Status = ExecutionRowStatus.Done;
            UpdateExecutionProgress();
            RefreshExecutionList();
        }
    }

    private void ReconcileExecutionRows(RemovalExecutionResult result)
    {
        var executedIds = result.ActionsExecuted.Select(i => i.ComponentId).ToHashSet();
        var failedIds = result.ActionsFailed.Select(i => i.ComponentId).ToHashSet();

        foreach (var row in _executionRows)
        {
            row.Status = executedIds.Contains(row.ComponentId)
                ? ExecutionRowStatus.Done
                : failedIds.Contains(row.ComponentId)
                    ? ExecutionRowStatus.Error
                    : result.Phase == RemovalExecutionPhase.Cancelled
                        ? ExecutionRowStatus.Cancelled
                        : ExecutionRowStatus.Skipped;
        }

        UpdateExecutionProgress();
        RefreshExecutionList();
    }

    private void UpdateExecutionProgress()
        => ExecutionProgressText.Text = $"{_executionRows.Count(r => r.Status == ExecutionRowStatus.Done)} / {_executionRows.Count}";

    private void RefreshExecutionList()
        => ExecutionList.ItemsSource = _executionRows.ToList();

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

    private static string? LocateInstallImage(string mountRoot)
    {
        foreach (var name in new[] { "install.wim", "install.esd" })
        {
            var candidate = Path.Combine(mountRoot, "sources", name);
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
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
        _executionRows = new List<ExecutionRow>();
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

/// <summary>Fila de la pantalla de ejecución: un componente permitido del plan y su progreso real.</summary>
public sealed class ExecutionRow
{
    public ExecutionRow(string componentId, string displayName)
    {
        ComponentId = componentId;
        DisplayName = displayName;
    }

    public string ComponentId { get; }
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
