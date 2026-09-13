PROMPT 11 — PROFILE ENGINE

Proyecto:
C:\Users\B3RASCASA\Desktop\MRS-W11PRO-by_chuyo31

Contexto:
P03-P10 están terminados.
P10 está compilando correctamente.
Estado actual: 215/215 tests.
El flujo Catalog → ProtectionEngine → RemovalPlan → RemovalEngine está establecido.

Objetivo:
Implementar ProfileEngine para definir perfiles de eliminación mediante JSON.

IMPORTANTE:
- NO modificar RemovalEngine.
- NO modificar el ciclo Mount/Execute/Verify/Commit/Discard.
- NO debilitar ProtectionEngine.
- NO crear eliminaciones automáticas fuera de RemovalPlan.
- Los perfiles solo producen una selección de ComponentId.
- La protección final siempre corresponde a ProtectionEngine/RemovalPlanning.
- No inventar ComponentId que no existan en el catálogo.
- Mantener separación de responsabilidades.

1. PROFILE MODEL

Crear modelos en MRS.ProfileEngine:

ProfileDefinition:
- Id
- Name
- Description
- Version
- ComponentIds
- Optional metadata si resulta necesaria

ProfileLoadResult:
- perfiles cargados
- errores de validación

2. JSON

Los perfiles deben vivir fuera del código:

profiles/
├── minimal.json
├── light.json
├── recommended.json
├── clean.json
└── custom.json

Formato sencillo y versionado.

Ejemplo conceptual:

{
  "id": "recommended",
  "name": "Recomendado",
  "description": "...",
  "version": 1,
  "componentIds": [
    "..."
  ]
}

NO rellenar listas con IDs inventados.

Si el catálogo actual todavía no contiene suficientes ComponentId definitivos para construir los cinco perfiles, crear los perfiles estructuralmente válidos pero dejar claramente documentados los elementos pendientes.

3. PROFILE SERVICE

Crear servicio para:

- cargar perfiles;
- validar JSON;
- detectar IDs duplicados;
- detectar ComponentId inexistentes;
- devolver errores claros;
- obtener perfil por Id;
- devolver la selección de ComponentId.

No modificar el catálogo.

4. VALIDACIÓN

Un perfil nunca debe poder:

- eliminar componentes protegidos;
- eliminar frameworks;
- introducir ComponentId inexistentes silenciosamente;
- generar cascadas;
- saltarse RemovalPlan.

El ProfileEngine puede informar de conflictos, pero la decisión final debe seguir pasando por Catalog + ProtectionEngine + RemovalPlanning.

5. UI

Integrar los perfiles en la pantalla de selección:

PERFILES

[ Mínimo ]
[ Ligero ]
[ Recomendado ]
[ Limpio ]
[ Personalizado ]

Al seleccionar un perfil:

- cargar sus ComponentId;
- actualizar la selección de componentes;
- mostrar cuántos componentes selecciona;
- indicar si existen conflictos/bloqueos;
- NO ejecutar ninguna modificación todavía.

El usuario debe poder revisar la selección antes de crear la imagen.

6. PERSONALIZADO

El perfil "Personalizado" no debe imponer una lista fija.

Debe representar la selección manual realizada por el usuario.

Si la arquitectura actual no encaja exactamente con esto, implementar la solución más sencilla sin romper el flujo existente y documentarla.

7. TESTS

Añadir tests para:

- carga correcta de JSON;
- perfil inexistente;
- JSON inválido;
- IDs duplicados;
- ComponentId inexistente;
- selección correcta;
- perfiles independientes;
- protección respetada al pasar posteriormente por RemovalPlanning;
- no ejecución automática.

Ejecutar todos los tests existentes.

8. BUILD

Ejecutar:

dotnet test
dotnet build

Resultado obligatorio:

Crear:

prompts\11-resultado.md

Debe incluir:

- resumen;
- archivos creados/modificados;
- formato JSON;
- perfiles implementados;
- ComponentId realmente utilizados;
- decisiones arquitectónicas;
- tests;
- build;
- problemas pendientes;
- commit recomendado.

Si para definir los perfiles correctamente falta información del catálogo actual, NO inventarla. Documentar la limitación.

No hacer cambios fuera del alcance de P11.