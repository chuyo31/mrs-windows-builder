using MRS.ImageEngine.Models;

namespace MRS.ImageEngine.Inventory;

/// <summary>Resultado de una operación de inventario.</summary>
public sealed record InventoryResult(
    ImageInventory Inventory,
    string WorkspacePath,
    bool WorkspaceKept);
