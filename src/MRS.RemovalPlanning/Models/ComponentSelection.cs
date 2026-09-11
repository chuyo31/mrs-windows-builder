namespace MRS.RemovalPlanning.Models;

/// <summary>
/// Selección del usuario sobre un componente concreto. Usa el
/// <see cref="ComponentId"/> estable del catálogo, nunca un índice de fila del
/// DataGrid (una fila puede reordenarse por búsqueda/filtro).
/// </summary>
public sealed record ComponentSelection(string ComponentId, bool Selected);
