using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using MRS.DismEngine.Dism;
using MRS.DismEngine.Logging;
using MRS.DismEngine.Processes;
using MRS.ImageEngine;
using MRS.ImageEngine.Inventory;
using MRS.ImageEngine.Iso;
using MRS.ImageEngine.Models;
using MRS.ImageEngine.Parsing;

namespace MRS.WindowsBuilder;

/// <summary>
/// Interaction logic for MainWindow.xaml
///
/// La ventana no contiene lógica DISM: delega en <see cref="ImageService"/> y
/// <see cref="ImageInventoryService"/>.
/// </summary>
public partial class MainWindow : Window
{
    private readonly AppLogger _logger = new();
    private readonly ImageService _imageService;
    private readonly ImageInventoryService _inventoryService;

    private IsoInspectionResult? _iso;
    private ImageInfo? _imageInfo;
    private ImageInventory? _inventory;
    private string? _selectedProfile;

    public MainWindow()
    {
        InitializeComponent();

        _logger.Entry += OnLogEntry;

        var processRunner = new ProcessRunner();
        var dismRunner = new DismRunner(processRunner, _logger);
        var isoMounter = new IsoMounter(processRunner, _logger);

        _imageService = new ImageService(dismRunner, isoMounter, new DismWimInfoParser(), _logger);
        _inventoryService = new ImageInventoryService(dismRunner, isoMounter, _logger);

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
        => _logger.Info("Inventario revisado. La siguiente fase (selección/limpieza) aún no está disponible.");

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
        InventoryOverlay.Visibility = Visibility.Collapsed;

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
