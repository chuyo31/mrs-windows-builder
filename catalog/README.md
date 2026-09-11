# catalog/

Reglas de clasificación y protección del catálogo de componentes, separadas del
motor (`MRS.ComponentCatalog`) para poder actualizarlas sin recompilar.

```
catalog/
  win11/
    components.json         # ClassificationRule[] - identificación (categoría)
    protection-rules.json   # ProtectionRule[]      - protección específica y revisable
  win10/                    # reservado para reglas específicas de Windows 10
  shared/                   # reservado para reglas comunes a varias versiones
```

El contenido de `win11/` es equivalente al conjunto embebido en
`MRS.ComponentCatalog.Rules.DefaultCatalogRules`, que es el que usa la
aplicación por defecto (fiable y sin depender de rutas de archivo). Estos JSON
son la representación externa preparada para el futuro: `JsonCatalogRuleLoader`
ya sabe leerlos si más adelante se decide cargarlos en tiempo de ejecución.

Deliberadamente es una base pequeña, no una lista de cientos de reglas: el
catálogo real siempre sale de lo que exista en el inventario de la imagen
(`ImageInventory`), estas reglas solo lo clasifican.
