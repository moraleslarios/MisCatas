# MisCatas

Aplicación web para organizar catas de vino entre amigos: un administrador
monta la cata (vinos, participantes) y los asistentes puntúan los vinos.

> **Estado del proyecto: fases 1, 2 y 3 completadas.**
> La fase 1 cubrió los cimientos: modelo de datos, acceso a datos, login y los
> CRUD de administración. La fase 2 aplicó el **diseño visual definitivo** a las
> tres experiencias (móvil para los catadores, panel denso para el administrador
> y pantalla de televisión para el escaparate). La fase 3 ha añadido el **motor
> de votación en tiempo real**: reloj autoritativo en el servidor, difusión por
> SignalR, mesa de control en vivo y revelación final.

---

## Requisitos

| Requisito | Versión usada |
|---|---|
| [.NET SDK](https://dotnet.microsoft.com/download) | **10.0.402** (fijada en `global.json`) |
| SQL Server (Express/Developer sirven) | instancia `LOCAL` |

No se necesita **Node ni npm**: Tailwind CSS se compila con su CLI standalone.

---

## Puesta en marcha

### 1. Descargar el CLI de Tailwind y las fuentes

El binario de Tailwind pesa 38 MB y los `.woff2` de las tipografías tampoco
están versionados. Se descargan una sola vez:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\descargar-tailwind.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File tools\descargar-fuentes.ps1
```

Tailwind queda en `tools\tailwindcss.exe` y las fuentes en
`src\MisCatas.Web\wwwroot\fuentes\`. El build del proyecto web invoca a Tailwind
automáticamente, así que no hay que volver a pensar en él.

### 2. Ajustar la cadena de conexión

En `src/MisCatas.Web/appsettings.json`:

```json
"ConnectionStrings": {
  "Default": "Server=7940HS\\LOCAL;Database=Catas;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=True;MultipleActiveResultSets=False"
}
```

Cambia `7940HS\LOCAL` por tu servidor. La base de datos **no hay que crearla a
mano**: al arrancar se aplican las migraciones de EF Core y se siembran los
datos mínimos.

### 3. Arrancar

La forma cómoda, sin tocar la línea de comandos:

| Qué quieres | Qué ejecutas | Dónde abre |
|---|---|---|
| Arrancar normal | `Arrancar.cmd` (doble clic) o `.\Arrancar.ps1` | `https://localhost:5443` |
| Arrancar para móviles y tele | `Arrancar-Red.cmd` o `.\Arrancar.ps1 -Lan` | `https://192.168.x.x:5443` |
| Arrancar sin HTTPS | `.\Arrancar.ps1 -Http` | `http://localhost:5080` (redirige al seguro) |
| Arrancar sin abrir navegador | `.\Arrancar.ps1 -SinNavegador` | — |
| Recompilar antes de arrancar | `.\Arrancar.ps1 -Compilar` | `https://localhost:5443` |
| Parar | `Parar.cmd` o `.\Parar.ps1` | libera 5443 y 5080 |

`tools\crear-accesos-directos.ps1` deja esos tres iconos en el Escritorio
(ARRANCAR, ARRANCAR (RED) y PARAR).

A mano también vale:

```powershell
dotnet run --project src\MisCatas.Web
```

La aplicación escucha por **los dos puertos a la vez**:

- **`https://localhost:5443`** → el seguro, el que atiende de verdad.
- `http://localhost:5080` → solo redirige al seguro, para que los enlaces y los
  QR antiguos sigan llevando a algún sitio.

Escucha en `0.0.0.0`, así que también se abre desde el móvil con la IP de la
máquina (`https://192.168.x.x:5443`) tras abrir los puertos con
`tools\abrir-puerto-firewall.ps1`. El certificado es propio del equipo, así que
la primera vez el navegador avisa: «Avanzado» → «Continuar». Es normal.

Los puertos están en un solo sitio en el código:
`src\MisCatas.Web\Seguridad\PuertosApp.cs`.

---

## Usuarios iniciales

La siembra (`SemillaDatos`) es **idempotente**: solo crea lo que falta, así que
se puede ejecutar en cada arranque sin duplicar nada.

| Usuario | Contraseña | Rol | Notas |
|---|---|---|---|
| `admin` | `123456` | Administrador | **Obliga a cambiar la contraseña en el primer acceso.** |
| `escaparate` | `escaparate` | Escaparate | Cuenta de solo lectura para la pantalla de la tele. |

Para los usuarios que crea el administrador, **la contraseña inicial es su
número de teléfono** y también se les obliga a cambiarla al entrar.

Hay tres roles y cada uno entra en un sitio distinto
(`RutasApp.InicioSegunRol`):

| Rol | Aterriza en | Puede |
|---|---|---|
| `Usuario` | `/` | ver sus catas, votar y consultar resultados |
| `Admin` | `/admin` | todo lo anterior más la administración completa |
| `Escaparate` | `/escaparate` | únicamente la pantalla de la tele |

---

## Trabajar con los estilos

El CSS vive en dos sitios y conviene no confundirlos:

- **`src/MisCatas.Web/Styles/estilos.css`** → el **fuente**. Es el que se edita.
- **`src/MisCatas.Web/wwwroot/css/estilos.css`** → el **compilado**. Lo genera
  Tailwind, está en `.gitignore` y **nunca se edita a mano**.

Se recompila solo en cada `dotnet build`. Mientras se tocan estilos es más
cómodo dejar el modo vigilancia en una terminal aparte:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\vigilar-css.ps1
```

Además de Tailwind se usa **CSS aislado de Blazor** (los ficheros
`*.razor.css`), que se encarga de los estilos propios de cada componente.
Ojo: **el CSS aislado no pasa por Tailwind**, así que ahí no funciona `@apply`;
hay que escribir valores literales o usar las variables CSS de `:root`. Y el
CSS aislado **solo se regenera al compilar**, no con el modo vigilancia.

### ⚠️ Tailwind hay que lanzarlo desde la raíz del repositorio

Los patrones de `content` de `tailwind.config.js` son **relativos a la raíz**
(`./src/MisCatas.Web/**`). Si el CLI se ejecuta desde otro directorio no
encuentra ningún fichero y **purga en silencio todas las clases**, sin dar
ningún error: la aplicación se queda sin utilidades (`flex`, `w-4`, `grid`…) y
el diseño se descompone. El síntoma más llamativo son los **iconos SVG
gigantes**, porque sin `w-*`/`h-*` se estiran hasta el ancho del contenedor.

Por eso el target `CompilarTailwind` del `.csproj` fija `WorkingDirectory` a la
raíz y `tools\vigilar-css.ps1` hace `Push-Location`. Para detectar una
regresión basta mirar el tamaño del compilado: lo normal son **~18 KB**; si
baja a ~8 KB, está purgando.

```powershell
Get-Item src\MisCatas.Web\wwwroot\css\estilos.css | Select-Object Length
```

---

## Diseño visual (fase 2)

La interfaz se apoya en tres piezas: los *design tokens* de
`tailwind.config.js`, un puñado de clases de componente `mc-*` definidas en
`Styles/estilos.css`, y el CSS aislado de cada página.

### Paleta

| Token | Uso |
|---|---|
| `burdeos` (`#6B1F2E`) | color principal: cabeceras, botones primarios, cifras |
| `granate` (`#8C2F39`) | apoyo del principal, degradados |
| `dorado` (`#C9A227`) | acentos, medallas y filetes |
| `dorado-texto` (`#6E5508`) | el dorado sobre crema no llega a contraste AA, para texto se usa este |
| `crema` (`#F7F3EE`) | fondos |
| `carbon` (`#1C1A1B`) | textos |

Hay además cuatro familias semánticas (`exito`, `aviso`, `peligro`, `info`),
cada una con las variantes `DEFAULT`, `suave` (fondo) y `texto` (contraste AA).

### Tipografía

Dos familias, **autoalojadas** en `wwwroot/fuentes/` (sin Google Fonts, sin
peticiones a terceros): **Playfair Display** para los titulares y **Inter**
para el resto. Se descargan con `tools\descargar-fuentes.ps1`.

### Clases de componente

En lugar de repetir cadenas largas de utilidades por todo el markup, los
patrones que se repiten están encapsulados:

- Contenedores: `mc-tarjeta`, `mc-tarjeta-oscura`, `mc-filete`, `mc-contenedor`
- Botones: `mc-boton-primario`, `mc-boton-secundario`, `mc-boton-dorado`,
  `mc-boton-peligro`, `mc-boton-fantasma`, `mc-boton-grande`, `mc-boton-icono`
- Formularios: `mc-campo`, `mc-etiqueta`, `mc-ayuda`, `mc-control`
- Estados: `mc-pastilla-borrador`, `mc-pastilla-abierta`, `mc-pastilla-encurso`,
  `mc-pastilla-finalizada`
- Avisos: `mc-aviso-ok`, `mc-aviso-error`
- Texto: `mc-titulo-marca`, `mc-antetitulo`, `mc-cifra`

> Las variantes ya incluyen su clase base, así que se escribe
> `class="mc-pastilla-encurso"`, **no** `class="mc-pastilla mc-pastilla-encurso"`.

### Los tres layouts

| Layout | Para quién | Pensado para |
|---|---|---|
| `MainLayout` | catadores | móvil en vertical, objetivos táctiles grandes |
| `AdminLayout` | administrador | escritorio, navegación de 5 secciones, tablas densas |
| `EscaparateLayout` | pantalla de la tele | 1920×1080 sin scroll, legible de lejos |

### Iconos

No hay librería de iconos: `Components/Shared/Iconos` tiene un único componente
`IconoVino` con 32 trazados SVG propios (`enum Icono`) y un `enum TamanoIcono`
que se traduce a utilidades `w-*`/`h-*` de Tailwind. Si se le pasa `Etiqueta`
es accesible; si no, se marca `aria-hidden` como decorativo.

### Clases generadas en tiempo de ejecución

Blazor inyecta por su cuenta `.invalid`, `.valid.modified`,
`.validation-message` y `.blazor-error-boundary`. Como esas cadenas no aparecen
escritas en ningún `.razor`, **Tailwind las purgaría**: por eso están definidas
**fuera de cualquier `@layer`** en `Styles/estilos.css`. No moverlas de ahí.

---

## Estructura de la solución

```
MisCatas.slnx
├── src/MisCatas.Domain          Entidades y enums. Sin dependencias.
├── src/MisCatas.Application     Interfaces de servicio, DTOs, patrón Resultado.
├── src/MisCatas.Infrastructure  EF Core, migraciones y la implementación de los servicios.
├── src/MisCatas.Web             Blazor Web App (Interactive Server).
│   └── TiempoReal/              Hub de SignalR, bus en memoria y cliente por circuito.
└── tests/MisCatas.Tests         Pruebas xUnit (78 pruebas).
```

Decisiones que conviene conocer antes de tocar código:

- **Toda la lógica de negocio está en los servicios**, nunca en los `.razor`.
- Los servicios reciben **`IDbContextFactory<AppDbContext>`**, no un
  `AppDbContext` con ámbito. Con el render interactivo de servidor el ciclo de
  vida del componente no encaja con el de un contexto *scoped*.
- Los servicios devuelven **`Resultado` / `Resultado<T>` /
  `ResultadoPaginado<T>`** en lugar de lanzar excepciones para los errores
  esperables (validaciones, no encontrado…).
- Cada componente se reparte en **tres ficheros**: `.razor`, `.razor.cs` y
  `.razor.css`.
- La autenticación es **por cookie**. Las páginas de cuenta (entrar, salir,
  cambiar contraseña) usan **render estático** con
  `@attribute [ExcludeFromInteractiveRouting]`, porque escribir la cookie
  requiere `HttpContext`.
- Las rutas no se escriben a mano en el markup: salen de **`Seguridad/RutasApp`**
  y `RutasCuenta`.
- La autorización va por **políticas** (`Seguridad/ClaimsMisCatas`):
  `SoloAdmin`, `Votantes` (admin + usuario) y `Pantalla` (admin + escaparate).
- El tiempo real pasa siempre por **`TiempoReal/ClienteCataHub`**, que escucha
  el bus en memoria `BusCata`. **Ningún componente crea un `Timer` propio**: el
  reloj es autoritativo en el servidor.
- La cultura es **`es-ES`**: decimales con coma y fechas `dd/MM/yyyy`.

---

## La regla de oro: la cata es a ciegas

Mientras una cata **no esté finalizada**, un catador solo puede saber el
**número de orden** del vino que está puntuando: ni nombre, ni añada, ni bodega,
ni quién lo trajo.

Eso **no se resuelve ocultando cosas con CSS** (sería trivial saltárselo con las
herramientas del navegador). Se resuelve en el servidor, proyectando **DTOs
distintos** según el momento, en `MisCatas.Application/Participacion`:

- **`VinoCiegoDto`** → solo `NumeroOrden`, estado de la votación y mi propio
  voto. Es lo único que viaja al cliente durante la cata.
- **`VinoReveladoDto`** → los datos completos, la nota media y la medalla. Solo
  se construye cuando la cata ya está finalizada.

`ObtenerResultadoAsync` devuelve `null` si la cata no está finalizada, y
`ObtenerCataCiegaAsync` devuelve `null` si quien pregunta no es participante.

---

## Ciclo de vida de una cata

```
Borrador ──abrir──▶ Abierta ──lanzar 1.er voto──▶ EnCurso ──finalizar──▶ Finalizada
   ◀────────────────────┘                             ◀──────reabrir──────┘
```

- En **Borrador** y **Abierta** la cata es editable: se añaden y reordenan
  vinos y participantes desde `/admin/catas/{id}/montar`.
- Para pasar a **Abierta** o **EnCurso** hace falta al menos **un vino y un
  participante**.
- **Solo puede haber una cata activa a la vez** (`Abierta` o `EnCurso`). La
  regla vive en `Application/Catas/CataActiva` y se valida en el servidor en
  todos los caminos que ocupan ese hueco.
- En **EnCurso** se gobierna desde la mesa de control
  `/admin/catas/{id}/control`.
- La cata **nunca se finaliza sola**: siempre la cierra el anfitrión. Y una cata
  finalizada **se puede reabrir**; al hacerlo los datos de los vinos vuelven a
  ocultarse.
- Al **Finalizar** se revelan los vinos y se calcula la clasificación.

Las transiciones legales están centralizadas en
`Application/Votacion/TransicionesCata` (y cubiertas por pruebas), de modo que
la interfaz nunca decide por su cuenta qué botones valen.

---

## Motor de votación en tiempo real (fase 3)

### El reloj lo manda el servidor

**No hay ningún temporizador por navegador.** Si cada móvil contase sus propios
segundos, diez catadores verían diez relojes distintos. En su lugar:

1. El servidor guarda `FinPrevistoUtc` y emite un **tick cada 500 ms** con el
   estado completo (`EstadoVivoDto`).
2. Cada pantalla **interpola** entre ticks a partir de la hora de recepción, así
   que el número baja con suavidad aunque la red hipe.
3. Recargar la página recupera el vino actual, el voto ya emitido y el tiempo
   real que queda. No se pierde nada.

`EstadoVivoDto` concentra todo lo que una pantalla necesita saber y lo deriva por
sí mismo: `SegundosRestantesEnteros`, `UmbralAlertaSegundos`, `EnAlertaRoja`,
`AceptaVotos` y `PermiteRectificar`.

### Secuencia de una votación

```
admin pulsa «lanzar»
      │
      ▼
Preparacion ── cuenta atrás 5 → 0 a pantalla completa, sincronizada
      │
      ▼
Abierta ───── 90 s para votar (TiempoVotoSegundos)
      │            └─ al quedar el 25 % (PorcentajeAlertaRoja) el reloj se pone rojo
      ▼
Cerrada ───── se consolidan los votos y se permite rectificar
```

`SegundosEnVotar` se mide **desde que la votación se abre de verdad**, es decir
después del cero de la cuenta atrás, para que la preparación no infle el tiempo
de nadie.

### Difusión: `CataHub` y `BusCata`

El hub `TiempoReal/CataHub` se publica en su ruta con WebSockets habilitados y
organiza las conexiones en el grupo `cata-{id}` más un subgrupo por rol. Los
eventos tienen nombre propio en `EventosCata` (`VotacionAbierta`,
`TickTiempoRestante`, `AlertaRoja`, `ResumenActualizado`, `CataFinalizada`,
`RevelacionIniciada`…).

> **Decisión de diseño:** las pantallas de la aplicación **no usan el paquete
> `Microsoft.AspNetCore.SignalR.Client`**. Con `InteractiveServer` los
> componentes ya se ejecutan en el servidor; montar un cliente SignalR ahí
> duplicaría las conexiones WebSocket (serían ~28 con doce móviles y dos teles),
> no tendría `HttpContext` del que copiar la cookie de autenticación y añadiría
> un salto de red inútil.
>
> En su lugar hay un **bus en memoria**, `TiempoReal/BusCata`, al que los
> componentes se suscriben a través de `ClienteCataHub` (registrado como
> *scoped*: uno por circuito, es decir uno por pestaña). El notificador
> `NotificadorCataSignalR` publica cada evento **en los dos sitios**: en el bus
> interno y en el hub, que sigue disponible para clientes externos.

Los resúmenes se envían **ya calculados** (nunca una orden de «recalcula tú») y
con un **throttle de ~500 ms**, para que una ráfaga de diez votos no provoque
diez recálculos. No hay backplane: es una sola máquina en una red local.

### Ausencias y no-votos

Al cerrar un vino, a quien no haya votado se le inserta un voto con
`EsAutomatico = true`. Qué valor lleva depende de la configuración:

| `PoliticaNoVoto` | Efecto |
|---|---|
| `NoCuenta` *(por defecto)* | nota nula, **excluido de la media** |
| `NotaFija` | se le asigna `NotaPorDefectoNoVoto` |

En las pantallas de resultados estos votos se muestran como `—` y nunca
contaminan la mejor ni la peor nota.

### Cálculo del resultado

`Application/Votacion/CalculadoraResultados` es una clase **estática y pura**
(no toca la base de datos), lo que la hace trivial de probar:

- La puntuación de un vino es la **media aritmética** de las notas que cuentan.
- **Desempate**: gana el vino con **menor desviación típica**, es decir el que
  puso de acuerdo a la mesa. Si sigue el empate, el que tenga más votos; y en
  último término el número de orden.
- Por cada vino se publican media, número de votos, mejor nota, peor nota,
  desviación típica y tiempo medio de votación.

### Varios administradores, un solo mando

Pueden entrar varios administradores en la mesa de control, pero solo uno manda:
el que figura en `AdminControlaId`. Al resto se les muestra la pantalla en modo
lectura con un aviso explícito y los botones deshabilitados.

### Rectificación

Un catador puede **rectificar su nota entre votaciones** (nunca con la votación
abierta). El voto se guarda con *upsert* sobre el índice único
`(CataId, VinoId, UsuarioId)` y queda marcado con `FueRectificado = true`.

### Revelación final

Cuando la cata se finaliza, el anfitrión activa la revelación y las televisiones
muestran el ranking con el **dueño del vino ganador como gran titular** de la
noche: tipografía serif, acento dorado y laurel. Es el momento que da sentido a
toda la ceremonia a ciegas.

---

## Pruebas

```powershell
dotnet test tests\MisCatas.Tests\MisCatas.Tests.csproj
```

**78 pruebas, todas en verde.** La cobertura se concentra deliberadamente en la
**lógica pura**, que es donde viven las reglas de negocio delicadas y donde un
fallo silencioso arruinaría una cata entera:

| Fichero | Qué protege |
|---|---|
| `CalculadoraResultadosTests` | medias, redondeo a dos decimales, desviación típica y toda la cadena de desempates |
| `TransicionesCataTests` | la máquina de estados: qué transiciones son legales y cuáles no |
| `EstadoVivoDtoTests` | el reloj: redondeo hacia arriba, umbral de alerta roja, cuándo se aceptan votos y cuándo se rectifica |
| `CataActivaTests` | la regla de «una sola cata activa» y sus mensajes |
| `ParticipacionDtosTests` | los DTO de cara al usuario: ocultación a ciegas, porcentajes, medallas, no-votos |

Notas para quien añada pruebas:

- El `.csproj` ya incluye `<Using Include="Xunit" />`, así que **no hay que
  escribir `using Xunit;`** en los ficheros de test.
- Ya está referenciado `Microsoft.EntityFrameworkCore.Sqlite`, pensado para
  futuras pruebas de integración con base de datos en memoria.
- Cuidado al comparar cadenas con decimales: `MetricasVino.MediaTexto` usa
  `ToString("0.0")` **sin cultura explícita**, de modo que depende del hilo de
  prueba. Comparar contra `"8,5"` y `"8.5"`. En cambio
  `VotoReveladoDto.NotaTexto` sí fija `es-ES` y se puede comparar directo.

---

## Migraciones

Los comandos se lanzan **desde la raíz del repositorio**, con el proyecto de
infraestructura como destino y el web como arranque:

```powershell
# Crear una migración nueva
dotnet ef migrations add NombreDeLaMigracion `
  --project src\MisCatas.Infrastructure `
  --startup-project src\MisCatas.Web `
  --output-dir Persistencia\Migraciones

# Aplicarla
dotnet ef database update `
  --project src\MisCatas.Infrastructure `
  --startup-project src\MisCatas.Web
```

No suele hacer falta aplicarlas a mano: **la aplicación migra y siembra al
arrancar**. `Program.cs` llama a `MigrarYSembrarAsync()`, que ejecuta
`Database.MigrateAsync` y después `SemillaDatos.EjecutarAsync` (idempotente).

> Es normal ver el aviso *«The Entity Framework tools version is older than
> that of the runtime»*. Es inocuo.

---

## Scripts de `tools/`

| Script | Para qué |
|---|---|
| `descargar-tailwind.ps1` | baja el CLI standalone de Tailwind (no está en el repositorio) |
| `descargar-fuentes.ps1` | baja las fuentes a `wwwroot` para que no dependan de internet |
| `vigilar-css.ps1` | recompila el CSS al guardar, en modo vigilancia |
| `abrir-puerto-firewall.ps1` | abre el 5443 (HTTPS) y el 5080 (HTTP) en el firewall para que entren los móviles |
| `crear-accesos-directos.ps1` | deja en el Escritorio los iconos ARRANCAR, ARRANCAR (RED) y PARAR |
| `validar-accesos-directos.ps1` | comprueba que esos tres iconos apuntan a donde deben |
| `arrancar-silencioso.ps1` | arranca dejando el log en un fichero (para no inundar el terminal) |
| `arrancar-produccion.ps1` | publica en Release y arranca como en la cata real (HTTPS, accesible en la red) |
| `crear-usuario-pruebas.ps1` | crea un usuario de pruebas |
| `montar-escenario-pruebas.ps1` | deja una cata montada con vinos y participantes |
| `finalizar-cata-pruebas.ps1` | finaliza la cata de pruebas para ver la revelación |
| `probar-hub.ps1` | comprueba a mano la difusión en tiempo real |
| `auditar-desbordes.ps1` | revisa los `*.razor.css` buscando textos que puedan desbordar |
| `oscurecer-textos.ps1` | ayuda a ajustar contrastes de la paleta |

---

## Avisos conocidos

- **Visual Studio bloquea los DLL.** Si la aplicación está corriendo desde el
  IDE, `dotnet build` falla por fichero en uso. Hay que detener el proceso
  `MisCatas.Web` antes de compilar.
- **Reiniciar la aplicación invalida las cookies.** La protección de datos vive
  en memoria, así que después de cada compilación hay que volver a iniciar
  sesión. Es esperado en desarrollo.
- `SixLabors.ImageSharp` emite cuatro líneas de aviso sobre su licencia en cada
  compilación. Conviene revisarla antes de publicar nada.

---

## Qué llega después

Las tres fases previstas están cerradas. Ideas para más adelante, por orden de
utilidad:

1. **Pruebas de integración** de `ServicioVotacion` y `ServicioResultados` con
   SQLite en memoria. Haría falta fabricar un `IDbContextFactory`, un
   `EstadoCatasActivas` y un doble de `INotificadorCata`; se dejó fuera porque
   la lógica delicada ya está cubierta por las pruebas unitarias.
2. **Backplane de SignalR** solo si alguna vez esto deja de ser una red local
   con un único servidor. Hoy sería complejidad gratuita.
3. **Revisar `ImageSharp`** o sustituirlo por una alternativa con licencia
   cómoda antes de publicar la aplicación en Release.
4. Exportar el resultado de una cata a PDF para mandárselo al grupo al día
   siguiente.
