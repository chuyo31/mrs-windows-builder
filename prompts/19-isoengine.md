PROMPT 19 — ISOENGINE: PIPELINE COMPLETO DE GENERACIÓN

Objetivo:
Implementar el pipeline real de generación de una ISO Windows 11 Pro a partir de la ISO original y de un workspace de trabajo.

Esta fase debe unir las piezas ya implementadas:

- análisis ISO/WIM;
- inventario;
- perfiles;
- RemovalPlan;
- RemovalEngine;
- InstallationOptions;
- boot.wim / LabConfig;
- autounattend;
- PostInstall.

IMPORTANTE:
La prioridad de P19 es conseguir un pipeline completo, seguro y reproducible.

NO optimizar todavía agresivamente el tamaño.
NO añadir nuevas reglas de eliminación.
NO implementar bypass de almacenamiento.
NO modificar la lógica existente de RemovalEngine salvo integración estrictamente necesaria.

==================================================
1. PIPELINE OBJETIVO
==================================================

El flujo debe quedar conceptualmente:

ISO ORIGINAL
    ↓
VALIDACIÓN
    ↓
GENERATION WORKSPACE
    ↓
PREPARACIÓN
    ├── boot.wim
    ├── install.wim
    ├── autounattend.xml
    └── PostInstall
    ↓
MODIFICACIÓN BOOT.WIM
    ├── LabConfig
    └── configuración Setup/OOBE
    ↓
MODIFICACIÓN INSTALL.WIM
    ├── selección Pro
    └── RemovalPlan
    ↓
INTEGRACIÓN POSTINSTALL
    ├── .NET
    └── PCPI
    ↓
VALIDACIÓN
    ↓
OSCDIMG
    ↓
ISO FINAL