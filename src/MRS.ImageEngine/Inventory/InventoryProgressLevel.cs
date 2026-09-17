namespace MRS.ImageEngine.Inventory;

/// <summary>Nivel visual de un <see cref="InventoryProgressInfo"/>, para que la UI pueda distinguirlos sin acoplarse a la lógica de negocio.</summary>
public enum InventoryProgressLevel
{
    Info,
    Success,
    Warning,
    Error,
}
