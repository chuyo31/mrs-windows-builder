PROMPT 12 — COMPLETAR PERFILES EN LA UI

Proyecto:
C:\Users\B3RASCASA\Desktop\MRS-W11PRO-by_chuyo31

P11 ya está implementado y tiene 5 perfiles JSON:

profiles/minimal.json
profiles/light.json
profiles/recommended.json
profiles/clean.json
profiles/custom.json

Problema:
La UI actual solo muestra 3 botones:
NORMAL
LIGHT
MEDIUM

Esto es incorrecto y parece corresponder a una nomenclatura anterior.

Objetivo:
Integrar correctamente los 5 perfiles existentes de P11 en la pantalla COMPONENTES.

Debe mostrar exactamente:

MÍNIMO
LIGERO
RECOMENDADO
LIMPIO
PERSONALIZADO

Requisitos:

1. No crear perfiles nuevos.
2. No cambiar los JSON existentes salvo que sea estrictamente necesario.
3. Usar ProfileService/ProfileDefinition existente.
4. No duplicar la lógica de perfiles en MainWindow.
5. Los botones deben corresponder a los perfiles reales:
   minimal → MÍNIMO
   light → LIGERO
   recommended → RECOMENDADO
   clean → LIMPIO
   custom → PERSONALIZADO

6. Al seleccionar un perfil:
   - aplicar únicamente su selección actual mediante ProfileService;
   - respetar ProtectionEngine;
   - un componente protegido nunca debe seleccionarse;
   - Personalizado no debe imponer ninguna selección;
   - mantener el comportamiento implementado en P11.

7. La UI debe mostrar los 5 botones correctamente en la misma zona de perfiles.
   Adaptar el layout para que no queden cortados ni provoquen scroll innecesario.

8. Revisar y eliminar cualquier referencia UI a:
   NORMAL
   MEDIUM
   o cualquier perfil anterior que ya no forme parte de P11.

9. No modificar:
   RemovalEngine
   RemovalPlanning
   ComponentCatalog
   ProtectionEngine
   ciclo Mount/Execute/Verify/Commit/Discard
   telemetría P10.

10. Mantener compatibilidad con los perfiles JSON actuales.

TESTS:
- Ejecutar todos los tests existentes.
- Añadir tests solo si son necesarios para validar el mapeo de los 5 perfiles.
- dotnet test
- dotnet build

RESULTADO:
Crear:

prompts/12-resultado.md

Indicar:
- archivos modificados;
- perfiles mostrados;
- mapeo UI → ProfileDefinition;
- tests;
- resultado del build;
- cualquier problema encontrado.

No hagas cambios fuera de este alcance.