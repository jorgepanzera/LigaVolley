Quiero que implementes el siguiente slice integral en LigaVolley:

# Slice: Match-specific Libero Declaration & Informational Roster Roles v1

## 1. Objetivo

Separar definitivamente dos conceptos que actualmente están acoplados:

1. La **función habitual o principal** de una jugadora dentro del CompetitionRoster.
2. La **declaración reglamentaria de líbero** para un partido concreto.

En una liga amateur, una misma jugadora puede actuar como armadora, punta, central, opuesta o incluso líbero en partidos diferentes. Por eso, `CompetitionRosterPlayer.PrimaryPlayerRole` no debe determinar ni restringir cómo juega en un Match.

Principio rector:

> `CompetitionRosterPlayer.PrimaryPlayerRole` es información opcional y orientativa. `MATCH_LIBERO` es la única autoridad que determina quién está reglamentariamente declarado como líbero en un partido.

Este slice debe permitir que:

* una jugadora cuyo rol habitual sea `LIBERO` juegue como regular en un partido;
* una jugadora cuyo rol habitual sea `SETTER`, `OUTSIDE_HITTER`, `MIDDLE_BLOCKER` u `OPPOSITE` sea declarada líbero en un partido;
* el rol habitual `LIBERO` solamente ayude a preseleccionar candidatos al abrir el acta;
* el operador pueda cambiar libremente esa preselección antes de confirmar la apertura;
* el máximo de líberos se valide sobre `MATCH_LIBERO`, no sobre CompetitionRoster;
* CompetitionRoster pueda tener más de dos jugadoras activas con función habitual `LIBERO`, respetando el máximo general de jugadores activos.

## 2. Diagnóstico obligatorio previo

Antes de modificar código, inspecciona completamente la implementación actual y documenta en el informe final:

1. Cómo se representa `PLAYER_ROLE`.
2. Dónde se utiliza `CompetitionRosterPlayer.PrimaryPlayerRoleId`.
3. Qué validaciones, triggers, servicios y UI limitan actualmente a dos jugadores `LIBERO` en un CompetitionRoster.
4. Si el máximo está duplicado entre:

   * Domain;
   * Application;
   * Infrastructure/EF;
   * SQL Server;
   * API;
   * Admin;
   * tests;
   * seeders.
5. Cómo `GET /api/scorer/matches/{matchId}/open-context` proyecta actualmente los jugadores y sus roles.
6. Cómo la UI de apertura:

   * selecciona convocados;
   * identifica candidatos a líbero;
   * construye `liberoCompetitionRosterPlayerIds`;
   * permite o no modificar esas selecciones.
7. Cómo `POST /open` crea:

   * `MATCH_PLAYER`;
   * `MATCH_LIBERO`;
   * `rulesSnapshot`.
8. Dónde se valida actualmente el máximo de líberos.
9. Si una jugadora con rol distinto de `LIBERO` puede declararse hoy como líbero.
10. Si una jugadora con rol habitual `LIBERO` puede convocarse hoy como regular sin quedar declarada en `MATCH_LIBERO`.
11. Cómo se validan las alineaciones contra `MATCH_LIBERO`.
12. Cómo afectan estas decisiones a:

    * reentrada;
    * IndexedDB;
    * sync;
    * takeover;
    * Admin MatchSheet Oversight;
    * Public Live;
    * partido demo.
13. Qué discrepancias existen entre código, contratos, pruebas y documentación.

Después del diagnóstico, implementa el slice completo. No te detengas en el análisis salvo que encuentres un riesgo real de pérdida de datos o una contradicción que requiera una decisión adicional.

## 3. Semántica definitiva de `PLAYER_ROLE`

Mantener los códigos existentes:

* `SETTER`
* `OUTSIDE_HITTER`
* `MIDDLE_BLOCKER`
* `OPPOSITE`
* `LIBERO`

No es necesario renombrar códigos ni claves persistidas.

Cambiar y documentar su semántica:

> `PLAYER_ROLE` describe una función habitual, principal o preferida dentro de un CompetitionRoster. No representa una habilitación reglamentaria ni una declaración efectiva para un Match.

`CompetitionRosterPlayer.PrimaryPlayerRoleId`:

* continúa siendo nullable;
* puede conservar su nombre técnico si cambiarlo produce una migración innecesaria;
* debe mostrarse en la UI como **“Función habitual”** o **“Rol habitual”**;
* no limita alineaciones;
* no determina sustituciones;
* no determina reemplazos de líbero;
* no genera warnings deportivos;
* no bloquea que la jugadora actúe en otra función;
* no se copia como autoridad a `MATCH_PLAYER`;
* puede utilizarse para ordenar, filtrar, mostrar o sugerir candidatos.

No agregues roles tácticos generales a `MATCH_PLAYER`.

## 4. CompetitionRoster

### 4.1 Eliminar el máximo de dos roles habituales `LIBERO`

Eliminar toda regla que limite a dos los jugadores activos cuyo `PrimaryPlayerRole` sea `LIBERO` dentro de un CompetitionRoster.

Esto incluye cualquier implementación en:

* agregados o servicios de dominio;
* Application;
* endpoints;
* validadores;
* EF Core;
* índices, constraints o triggers SQL;
* Admin Web;
* tests;
* seeders;
* documentación.

No debe existir error ni warning por tener tres o más jugadores con función habitual `LIBERO`.

### 4.2 Mantener las demás reglas

Conservar:

* máximo general de 15 jugadores activos por roster;
* unicidad de jugador dentro del roster;
* estados vigentes;
* Health Card como warning;
* edición de roster según lifecycle;
* preservación histórica de miembros inactivos;
* ausencia de dorsal y capitanía en CompetitionRoster;
* demás invariantes existentes no relacionadas con este cambio.

Tener más de dos funciones habituales `LIBERO` no amplía el máximo general de 15.

### 4.3 Admin Web

En la gestión del CompetitionRoster:

* cambiar la etiqueta visible de `Rol` por `Función habitual` o `Rol habitual`;
* explicar discretamente que es informativo;
* permitir seleccionar cualquiera de los códigos existentes;
* permitir valor vacío si el contrato actual lo admite;
* no mostrar contadores ni errores de “máximo dos líberos”;
* mantener los contadores generales de jugadores y técnicos;
* no presentar `LIBERO` como habilitación para el partido.

## 5. Declaración reglamentaria por partido

`MATCH_LIBERO` debe ser la única fuente de verdad para saber qué jugadores están declarados como líberos en el Match.

Una declaración válida debe cumplir:

* el jugador pertenece al mismo `MATCH_TEAM`;
* forma parte de los jugadores seleccionados para la convocatoria;
* existe como `MATCH_PLAYER`;
* no está duplicado en `MATCH_LIBERO`;
* la cantidad declarada no supera el máximo efectivo del partido;
* si `rulesSnapshot.liberoEnabled=false`, no se admiten declaraciones;
* deben quedar al menos seis jugadores convocados no declarados como líbero para construir una alineación regular P1–P6.

No imponer ninguna correspondencia entre `MATCH_LIBERO` y `PrimaryPlayerRole`.

Casos válidos:

| Función habitual     | Declaración del Match       | Comportamiento       |
| -------------------- | --------------------------- | -------------------- |
| `LIBERO`             | No declarada como líbero    | Jugadora regular     |
| `LIBERO`             | Declarada en `MATCH_LIBERO` | Líbero reglamentario |
| `SETTER`             | Declarada en `MATCH_LIBERO` | Líbero reglamentario |
| `OUTSIDE_HITTER`     | Declarada en `MATCH_LIBERO` | Líbero reglamentario |
| `MIDDLE_BLOCKER`     | Declarada en `MATCH_LIBERO` | Líbero reglamentario |
| `OPPOSITE`           | Declarada en `MATCH_LIBERO` | Líbero reglamentario |
| Cualquier rol o null | No declarada                | Jugadora regular     |

No generar WARNING por declarar como líbero a una jugadora cuyo rol habitual sea distinto de `LIBERO`.

No generar WARNING por utilizar como regular a una jugadora cuyo rol habitual sea `LIBERO`.

## 6. Máximo efectivo de líberos

El máximo reglamentario debe validarse durante la apertura contra el valor efectivo que quedará congelado en el MatchSheet:

`effectiveMaxLiberos = rulesSnapshot.maxLiberos`

La validación debe aplicarse al número de filas o declaraciones de `MATCH_LIBERO`, no a los roles habituales del CompetitionRoster.

Comportamiento esperado:

* cero declarados: válido si las demás condiciones se cumplen;
* uno declarado: válido;
* dos declarados: válido cuando el máximo efectivo es dos;
* más del máximo: rechazo determinista;
* declaraciones cuando `liberoEnabled=false`: rechazo determinista.

Reutiliza los códigos existentes cuando sean semánticamente correctos. Si no existe un código claro para exceder el máximo durante la apertura, agrega uno explícito, estable y documentado. No uses mensajes de texto como contrato.

La apertura debe calcular y validar las reglas efectivas antes de materializar el agregado, y congelar exactamente los mismos valores utilizados para validar. Evita divergencias entre prevalidación y persistencia.

## 7. Preselección editable en OpenMatchSheet

### 7.1 Concepto

El rol habitual `LIBERO` solo debe generar una **preselección de conveniencia en la UI**.

La preselección:

* no es una decisión del backend;
* no crea `MATCH_LIBERO`;
* no se persiste hasta confirmar `POST /open`;
* no convierte automáticamente a nadie en líbero;
* es completamente editable;
* debe aplicarse únicamente a jugadores incluidos en la convocatoria actual.

### 7.2 Comportamiento según cantidad de candidatos habituales

Si entre los convocados seleccionados existen:

* cero candidatos con rol habitual `LIBERO`: no preseleccionar a nadie;
* uno: preseleccionarlo;
* dos, con máximo efectivo dos: preseleccionar ambos;
* más candidatos que el máximo efectivo: no elegir arbitrariamente los primeros.

Cuando existan más candidatos habituales que el máximo:

* destacarlos como candidatos habituales;
* mostrar claramente el máximo permitido;
* pedir al operador que seleccione hasta ese máximo;
* no depender del orden de base de datos, nombre, dorsal o ID para truncar la selección.

Si el máximo efectivo es uno y hay dos o más candidatos habituales, aplicar la misma regla: no realizar una elección arbitraria.

### 7.3 Edición por el operador

Antes de abrir el acta, el operador debe poder:

* quitar cualquier preselección;
* dejar el equipo sin líberos declarados;
* seleccionar uno o dos, según las reglas efectivas;
* declarar como líbero a cualquier otro jugador convocado;
* volver a convertir en regular a un candidato habitual;
* cambiar la convocatoria y obtener un estado coherente.

Si un jugador deja de estar convocado:

* debe eliminarse automáticamente de la selección de líberos de la UI;
* no debe quedar un ID oculto o inválido en el payload.

Si se incorpora a la convocatoria un jugador con rol habitual `LIBERO` después de que el usuario ya editó manualmente la selección:

* no sobrescribas silenciosamente la decisión manual;
* define una lógica de estado predecible;
* las sugerencias automáticas iniciales no deben reaplicarse destruyendo elecciones del operador.

La UX debe distinguir claramente:

* jugador convocado;
* función habitual;
* declarado como líbero para este partido;
* capitán del partido;
* dorsal del partido.

No uses solamente color para marcar la declaración.

## 8. Convocatoria y mínimo de seis regulares

`POST /open` debe exigir que, después de descontar los jugadores declarados en `MATCH_LIBERO`, queden al menos seis jugadores convocados disponibles como regulares por lado.

Ejemplos:

* 6 convocados, 0 líberos declarados: válido.
* 7 convocados, 1 líbero declarado: válido.
* 8 convocados, 2 líberos declarados: válido.
* 6 convocados, 1 líbero declarado: inválido.
* 7 convocados, 2 líberos declarados: inválido.

Esta es una invariante estructural necesaria para representar una alineación regular completa, no una preferencia táctica.

Usa un código de error determinista y documentado. Reutiliza uno existente solo si su significado ya cubre exactamente esta situación; de lo contrario, agrega uno específico.

La misma regla debe reflejarse coherentemente en:

* open-context/readiness, como blocker cuando pueda evaluarse;
* validación de `POST /open`;
* UI de apertura;
* tests;
* documentación.

No confundas el mínimo general del roster activo con el mínimo efectivo de regulares seleccionados para el partido.

## 9. Congelamiento e independencia histórica

Al confirmar `POST /open`:

* materializar los `MATCH_PLAYER` seleccionados;
* persistir dorsal y capitanía del partido según el diseño vigente;
* crear `MATCH_LIBERO` exclusivamente con la selección confirmada;
* congelar las reglas efectivas;
* conservar la apertura transaccional e idempotente.

Después de abrir el MatchSheet:

* cambiar `PrimaryPlayerRole` en CompetitionRoster no modifica `MATCH_PLAYER`;
* cambiarlo a `LIBERO` no agrega un `MATCH_LIBERO`;
* quitar `LIBERO` no elimina una declaración existente;
* cambios posteriores del roster no reinterpretan la convocatoria;
* reentrada, offline, sync y takeover usan exclusivamente la declaración congelada.

No agregues sincronización retroactiva desde CompetitionRoster hacia MatchSheet.

## 10. Integración con Observed Libero Replacements

Este slice debe respetar completamente **Observed Libero Replacements & Effective Court v1**.

Solo los jugadores declarados en `MATCH_LIBERO` pueden participar en acciones:

* `LIBERO_ENTER`;
* `LIBERO_EXIT`;
* intercambio entre líberos.

La cancha efectiva continúa cambiando únicamente mediante hechos observados y confirmados.

El rol habitual `LIBERO`:

* no habilita un reemplazo;
* no convierte automáticamente una jugadora en candidata operativa durante el set si no fue declarada;
* no modifica la cancha;
* no crea sugerencias operativas para jugadores no declarados;
* no aparece como autoridad en Rules Assistant.

Un jugador con función habitual distinta de `LIBERO`, pero declarado en `MATCH_LIBERO`, debe funcionar exactamente igual que cualquier otro líbero declarado.

Un jugador con función habitual `LIBERO`, pero no declarado en `MATCH_LIBERO`, debe funcionar exactamente igual que cualquier regular:

* puede integrar P1–P6;
* puede participar en sustituciones regulares;
* no puede usar acciones de reemplazo de líbero.

## 11. Rules Assistant e invariantes

No agregues warnings basados en diferencias entre función habitual y declaración del partido.

El Rules Assistant debe consultar:

* `MATCH_LIBERO`;
* `rulesSnapshot`;
* estado del set;
* cancha efectiva;
* historial de reemplazos observados;

y nunca `CompetitionRosterPlayer.PrimaryPlayerRole` para decidir si una acción de líbero es válida.

Conservar las clasificaciones existentes:

* referencias inválidas;
* jugador no declarado;
* duplicación efectiva;
* lifecycle inválido;
* máximo imposible de representar;
* tracking deshabilitado:

  deben seguir siendo HARD cuando corresponda.

Las irregularidades deportivas representables durante el partido continúan como WARNING confirmable según Scorer Rules Assistant.

No debilites:

* `libero_not_declared`;
* `lineup_libero_not_allowed`;
* `duplicate_effective_player`;
* `libero_already_on_court`;
* `invalid_libero_replacement`;
* `libero_tracking_disabled`;
* demás garantías técnicas existentes.

## 12. API y contratos

Revisa primero si los contratos actuales ya permiten el comportamiento mediante:

* `open-context`;
* selección de convocados;
* `liberoCompetitionRosterPlayerIds`;
* `POST /open`;
* `GET /sheet`.

Prefiere una evolución aditiva y compatible.

`open-context` debe proporcionar al frontend lo necesario para distinguir:

* identidad del jugador del roster;
* nombre;
* estado;
* función habitual nullable;
* si es candidato habitual a líbero;
* límites efectivos necesarios para validar la selección;
* demás datos actuales de convocatoria.

No agregues un booleano persistido como “es líbero” al CompetitionRoster. Si expones una ayuda derivada como `isHabitualLiberoCandidate`, debe ser inequívocamente informativa y derivable del código de rol.

El payload de `POST /open` debe seguir expresando explícitamente la selección efectiva de líberos. El backend nunca debe completar silenciosamente esa lista leyendo `PrimaryPlayerRole`.

La ausencia o lista vacía debe significar: ningún líbero declarado para ese equipo.

Documenta la semántica de null, ausencia y lista vacía conforme a las convenciones actuales y evita interpretaciones diferentes entre frontend y backend.

## 13. Admin Match Operations

Actualizar readiness y supervisión cuando corresponda:

### Antes de abrir el acta

Readiness puede mostrar:

* cantidad de jugadores activos del roster;
* candidatos habituales a líbero, solo como información;
* blockers estructurales existentes.

No debe bloquear porque el CompetitionRoster tenga más de dos funciones habituales `LIBERO`.

Si readiness no conoce todavía la convocatoria concreta, no debe inventar cuántos líberos serán declarados. Puede advertir que la selección se realizará al abrir el acta.

### Después de abrir el acta

MatchSheet Oversight debe mostrar:

* convocatoria congelada;
* líberos efectivamente declarados en `MATCH_LIBERO`;
* cancha efectiva;
* reemplazos observados.

No debe recalcular declaraciones usando roles actuales del CompetitionRoster.

## 14. Public

No es necesario publicar la función habitual del CompetitionRoster.

Public Live debe continuar mostrando únicamente:

* estado canónico;
* cancha efectiva confirmada;
* líbero actuante cuando corresponda.

No agregues planteles completos ni roles habituales a Public.

Una jugadora declarada como líbero en el Match debe aparecer como tal según el estado central aunque su función habitual sea otra. Una jugadora con función habitual `LIBERO` que juega como regular no debe aparecer como líbero por esa metadata.

## 15. Offline, sync y takeover

La declaración `MATCH_LIBERO` se congela en la apertura central del acta y debe estar incluida en el snapshot necesario para operar offline.

Verifica que:

* reentrada recupere exactamente los líberos declarados;
* IndexedDB no los vuelva a derivar desde el roster;
* replay utilice `MATCH_LIBERO`;
* sync valide acciones contra la declaración congelada;
* takeover conserve la declaración;
* reconciliación no cambie candidatos por modificaciones posteriores del roster;
* un cliente offline no dependa de consultar el CompetitionRoster para saber quién puede actuar como líbero.

No agregues stores Dexie nuevos salvo que exista una necesidad arquitectónica real y justificada. Conserva los cinco stores actuales.

## 16. Persistencia y migración

Inspecciona el esquema efectivo antes de diseñar la migración.

La migración debe:

* eliminar el trigger, constraint o mecanismo que limite a dos roles habituales `LIBERO` en CompetitionRoster;
* preservar todas las filas existentes;
* no cambiar IDs de `PLAYER_ROLE`;
* no reescribir `PrimaryPlayerRoleId`;
* no modificar `MATCH_LIBERO` históricos;
* no alterar MatchSheets existentes;
* no reinterpretar eventos;
* conservar el máximo general de 15 jugadores activos.

Si la restricción también existe en código, eliminarla allí; una migración aislada no alcanza.

El `Down` debe ser explícito sobre el riesgo de reinstalar el límite:

* no eliminar ni modificar silenciosamente jugadores;
* si existen rosters incompatibles con el límite anterior, rechazar el downgrade con un mensaje claro;
* solo reinstalar la restricción cuando los datos actuales sean compatibles.

## 17. Compatibilidad

Mantener compatibilidad con:

* CompetitionRosters actuales;
* MatchSheets abiertos o cerrados;
* `MATCH_LIBERO` existentes;
* eventos de reemplazo observados;
* eventos legacy automáticos;
* protocolo y snapshot vigentes;
* Swagger/Postman existentes.

No hace falta incrementar la versión del protocolo deportivo si los eventos y snapshots no cambian. Si concluyes que sí es necesario, justifícalo y soporta explícitamente las versiones anteriores.

Las actas históricas deben producir exactamente la misma cancha y declaraciones que antes de la migración.

## 18. Seeder y partido demo

Actualizar el seeder demo solamente en lo necesario para verificar esta semántica.

Debe continuar creando:

* partido programado;
* rosters activos;
* jugadores suficientes;
* oficiales;
* uno o más candidatos habituales a líbero;
* ningún MatchSheet abierto al finalizar el seed.

El flujo de apertura demo debe permitir:

* aceptar la preselección habitual;
* quitarla y abrir sin líbero;
* declarar como líbero a un jugador cuyo rol habitual sea distinto;
* dejar como regular al candidato habitual.

No alteres el seed LIVOSUR original ni inventes datos masivos innecesarios.

El comando de reinicio debe continuar dejando el demo `SCHEDULED`, sin acta, y conservar su idempotencia.

## 19. Pruebas mínimas obligatorias

Agregar o actualizar pruebas Domain, Application, Integration, Admin, Scorer y E2E según corresponda.

### CompetitionRoster

1. Un roster acepta cero roles habituales `LIBERO`.
2. Acepta uno.
3. Acepta dos.
4. Acepta tres o más mientras no supere 15 jugadores activos.
5. El máximo general de 15 continúa vigente.
6. `PrimaryPlayerRoleId` sigue siendo nullable.
7. Cambiar la función habitual no modifica MatchSheets existentes.
8. La migración elimina la restricción anterior sin perder datos.
9. El downgrade rechaza datos incompatibles en lugar de borrarlos.

### Apertura

10. Candidato habitual único queda preseleccionado en la UI.
11. Dos candidatos quedan preseleccionados si el máximo es dos.
12. Más candidatos que el máximo no produce truncamiento arbitrario.
13. El operador puede quitar todas las preselecciones.
14. El operador puede declarar otro convocado.
15. Un jugador no convocado no puede quedar seleccionado como líbero.
16. Cambiar la convocatoria limpia selecciones inválidas.
17. Una edición manual no es sobrescrita por la lógica de preselección.
18. Lista vacía crea cero `MATCH_LIBERO`.
19. Jugadora habitual `LIBERO` no declarada queda como regular.
20. Jugadora de otro rol declarada crea un `MATCH_LIBERO`.
21. Más del máximo efectivo se rechaza determinísticamente.
22. `liberoEnabled=false` rechaza declaraciones.
23. Seis convocados y cero líberos es válido.
24. Seis convocados y un líbero es inválido.
25. Siete convocados y un líbero es válido.
26. Siete convocados y dos líberos es inválido.
27. Ocho convocados y dos líberos es válido.
28. Apertura idempotente no duplica `MATCH_PLAYER` ni `MATCH_LIBERO`.

### Operación deportiva

29. Habitual `LIBERO` no declarado puede integrar P1–P6.
30. Jugadora de otro rol declarada no puede integrar P1–P6.
31. Solo declarados aparecen como candidatos de `LIBERO_ENTER`.
32. Un habitual `LIBERO` no declarado puede intervenir en sustitución regular.
33. Reemplazos observados funcionan con un declarado cuyo rol habitual no sea `LIBERO`.
34. Dos líberos declarados funcionan con el intercambio observado.
35. Rules Assistant no consulta la función habitual para validar la acción.

### Estado distribuido

36. `GET /sheet` devuelve la declaración congelada.
37. Reentrada offline conserva los declarados.
38. Replay usa la declaración del Match.
39. Sync rechaza un `LIBERO_ENTER` de un no declarado.
40. Takeover conserva la declaración.
41. Admin muestra los declarados del Match.
42. Public no confunde rol habitual con líbero reglamentario.
43. Una modificación posterior del roster no cambia Admin, Scorer ni Public para el Match existente.

## 20. E2E real esperado

Extiende o agrega un E2E opt-in sobre el partido demo y SQL Server real configurado en secrets:

1. Reiniciar el demo.
2. Obtener `open-context`.
3. Verificar candidato habitual.
4. Seleccionar convocatoria, dorsales y capitán.
5. Quitar la preselección habitual.
6. Declarar como líbero a otro jugador convocado cuyo rol habitual sea distinto.
7. Abrir el MatchSheet.
8. Verificar `MATCH_PLAYER` y `MATCH_LIBERO` mediante API y SQL.
9. Preparar el set:

   * el habitual `LIBERO` no declarado puede integrar la alineación regular;
   * el declarado alternativo no puede integrarla.
10. Iniciar el set.
11. Registrar un reemplazo observado con el líbero declarado.
12. Verificar cancha efectiva local.
13. Probar operación offline, recarga y sync.
14. Verificar estado central, Admin y Public Live.
15. Ejecutar takeover y confirmar que se conserva la declaración.
16. Verificar que un jugador no declarado produce el rechazo correspondiente.
17. Dejar el demo nuevamente `SCHEDULED`, sin acta.
18. Documentar la limpieza necesaria de IndexedDB para repetir el flujo.

Las pruebas automatizadas SQL deben usar bases aisladas y no tratar `LigaVolleyDev` como base descartable.

## 21. Documentación y contratos

Actualizar:

* documentación de arquitectura;
* modelo de dominio;
* People y CompetitionRosters;
* Match/Scorer;
* Observed Libero Replacements;
* decisiones y pendientes;
* Swagger/OpenAPI;
* colección Postman unificada;
* `AGENTS.md` cuando corresponda.

Documentar expresamente:

* `PrimaryPlayerRole` como función habitual;
* ausencia de autoridad deportiva del rol del roster;
* eliminación del máximo de dos roles habituales `LIBERO`;
* `MATCH_LIBERO` como autoridad exclusiva del partido;
* preselección editable;
* comportamiento cuando hay más candidatos que el máximo;
* mínimo de seis regulares disponibles;
* congelamiento del MatchSheet;
* integración con reemplazos observados;
* compatibilidad histórica.

Actualizar ejemplos de requests y responses de apertura para incluir:

* sin líberos;
* con líbero habitual;
* con líbero de otro rol;
* exceso del máximo;
* insuficiencia de regulares.

## 22. Fuera de alcance

No incluir en este slice:

* agregar roles tácticos a `MATCH_PLAYER`;
* registrar armadora, punta, central u opuesta por partido;
* registrar funciones tácticas por set;
* inferir funciones desde P1–P6;
* estadísticas por posición o función;
* cambios al motor de rotación;
* rediseño de reemplazos observados;
* edición de una convocatoria después de abrir el MatchSheet;
* autenticación;
* sanciones;
* cambios en fixture, standings o CompetitionFormat no requeridos;
* publicación de planteles o roles habituales en Public.

No resuelvas incidentalmente esos temas.

## 23. Criterios de aceptación

El slice se considera completo solamente si:

* CompetitionRoster admite más de dos funciones habituales `LIBERO`;
* se mantiene el máximo general de 15 jugadores activos;
* la UI denomina claramente el dato como habitual/informativo;
* `POST /open` recibe una selección explícita de líberos;
* el backend no deriva declaraciones desde el rol habitual;
* las preselecciones pueden quitarse;
* cualquier convocado puede declararse;
* no hay selección arbitraria cuando los candidatos superan el máximo;
* el máximo se valida sobre `MATCH_LIBERO`;
* quedan al menos seis regulares por lado;
* `MATCH_LIBERO` queda congelado;
* alineaciones y reemplazos consultan exclusivamente `MATCH_LIBERO`;
* roles habituales diferentes no generan warnings;
* offline, sync y takeover preservan la declaración;
* Admin y Public no reinterpretan el roster;
* la migración preserva datos e impide downgrade destructivo;
* las actas históricas no cambian;
* contratos y documentación quedan actualizados;
* pasan builds y suites relevantes;
* se ejecuta el E2E real;
* se informa cualquier fallo preexistente de forma separada y verificable.

## 24. Forma de trabajo y entrega

* Lee primero `AGENTS.md` y la documentación vigente.
* Inspecciona el repositorio antes de asumir nombres o estructuras.
* Conserva cambios locales existentes y evita modificaciones ajenas.
* Reutiliza contratos y entidades existentes cuando expresen correctamente la semántica.
* No dupliques validaciones entre frontend y backend sin una fuente compartida o comportamiento equivalente comprobado.
* Aplica la migración SQL Server necesaria.
* Ejecuta las pruebas unitarias, de integración, frontend y E2E relevantes.
* No alteres pruebas para ocultar fallos.
* Al finalizar, entrega:

  * diagnóstico del comportamiento anterior;
  * decisiones técnicas;
  * archivos y contratos modificados;
  * migración y comportamiento de `Down`;
  * UX final;
  * compatibilidad;
  * resultados exactos de cada suite;
  * fallos preexistentes separados;
  * estado final del partido demo.

Implementa y verifica el slice integral. Solo detente antes de realizar una operación destructiva o si descubres una contradicción real que impida preservar datos, historia o autoridad.

