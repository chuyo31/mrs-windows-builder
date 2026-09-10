using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace MRS.WindowsBuilder;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private string? _selectedProfile;

    public MainWindow()
    {
        InitializeComponent();

        Log("[INFO] MRS Windows Builder iniciado");
        Log("[INFO] Esperando seleccionar una ISO");
    }

    private void SelectIsoButton_Click(object sender, RoutedEventArgs e)
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

        IsoPathBox.Text = dialog.FileName;
        AnalyzeButton.IsEnabled = true;
        StatusText.Text = "ISO seleccionada";
        FooterHint.Text = "Pulsa \"Analizar imagen\" para continuar";

        Log($"[INFO] ISO seleccionada: {dialog.FileName}");
    }

    private void AnalyzeButton_Click(object sender, RoutedEventArgs e)
    {
        Log("[INFO] Analizando imagen...");
        Log("[INFO] Función pendiente de conectar con MRS.ImageEngine");
        StatusText.Text = "Análisis pendiente";
    }

    private void Profile_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton rb)
            return;

        _selectedProfile = rb.Content?.ToString();
        Log($"[INFO] Perfil seleccionado: {_selectedProfile}");

        UpdateContinueState();
    }

    private void ContinueButton_Click(object sender, RoutedEventArgs e)
    {
        Log($"[INFO] Continuar con perfil {_selectedProfile} (paso siguiente pendiente)");
    }

    private void UpdateContinueState()
    {
        bool ready = !string.IsNullOrEmpty(IsoPathBox.Text) && !string.IsNullOrEmpty(_selectedProfile);
        ContinueButton.IsEnabled = ready;

        if (ready)
            FooterHint.Text = "Listo para continuar";
    }

    private void Log(string message)
    {
        LogBox.AppendText($"{message}{Environment.NewLine}");
        LogBox.CaretIndex = LogBox.Text.Length;
        LogScroller.ScrollToEnd();
    }
}
