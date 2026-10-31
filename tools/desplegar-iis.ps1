#Requires -RunAsAdministrator

<#
.SYNOPSIS
    Instala MisCatas en el IIS de este equipo y la publica en https://miscatas.es

.DESCRIPTION
    Hace de una vez todo lo que normalmente se haría a mano. Se puede volver a
    ejecutar tantas veces como se quiera: cada paso comprueba antes si ya está
    hecho, así que repetirlo no rompe nada (sirve también para actualizar la web
    después de hacer cambios).

    Pasos:
      1. Comprueba que IIS está instalado y, si no, lo instala.
      2. Comprueba el complemento de IIS para .NET (el "Hosting Bundle") y lo
         instala si falta. Sin él IIS no sabe arrancar la aplicación.
      3. Apunta el nombre miscatas.es a este mismo equipo.
      4. Fabrica (una sola vez) el certificado de seguridad de miscatas.es y lo
         marca como de confianza, para que el navegador no avise.
      5. Publica la aplicación en C:\inetpub\MisCatas.
      6. Crea el sitio web y su grupo de aplicaciones en IIS.
      7. Da permisos a la web sobre su carpeta y sobre la base de datos.
      8. Arranca el sitio y comprueba que responde.

.NOTES
    Hay que ejecutarlo como ADMINISTRADOR. La forma fácil es botón derecho
    sobre Desplegar-IIS.cmd -> "Ejecutar como administrador".

.EXAMPLE
    .\desplegar-iis.ps1
    Despliegue completo.

.EXAMPLE
    .\desplegar-iis.ps1 -SinPublicar
    Solo revisa y rehace la configuración de IIS, sin volver a compilar.
#>

[CmdletBinding()]
param(
    # Nombre con el que se verá la web. Si se cambia, hay que cambiarlo también
    # en src\MisCatas.Web\web.config (variable Sitio__UrlPublica).
    [string]$Dominio = 'miscatas.es',

    # Carpeta donde queda la web publicada.
    [string]$Carpeta = 'C:\inetpub\MisCatas',

    # Nombre del sitio y del grupo de aplicaciones dentro de IIS.
    [string]$Sitio = 'MisCatas',

    # No vuelve a compilar ni publicar: reutiliza lo que ya hay en $Carpeta.
    [switch]$SinPublicar,

    # No abre el navegador al terminar.
    [switch]$SinNavegador
)

$ErrorActionPreference = 'Stop'

$raiz = Split-Path -Parent $PSScriptRoot
$proyecto = Join-Path $raiz 'src\MisCatas.Web'
$rutaAjustes = Join-Path $proyecto 'appsettings.json'
$cssMinimoBytes = 14000
$totalPasos = 8

function Escribir-Paso {
    param([int]$Numero, [string]$Texto)
    Write-Host ''
    Write-Host "[$Numero/$totalPasos] $Texto" -ForegroundColor Cyan
}

function Escribir-Ok {
    param([string]$Texto)
    Write-Host "      OK  $Texto" -ForegroundColor Green
}

function Escribir-Info {
    param([string]$Texto)
    Write-Host "      --  $Texto" -ForegroundColor DarkGray
}

function Escribir-Aviso {
    param([string]$Texto)
    Write-Host "      !!  $Texto" -ForegroundColor Yellow
}

Write-Host ''
Write-Host '=============================================================' -ForegroundColor Magenta
Write-Host '  MisCatas - instalacion en el IIS de este equipo' -ForegroundColor Magenta
Write-Host "  Direccion final: https://$Dominio" -ForegroundColor Magenta
Write-Host '=============================================================' -ForegroundColor Magenta


# --- 1. IIS -----------------------------------------------------------------
# Se piden también los WebSockets: la cata en vivo (los votos que aparecen al
# instante en el escaparate) viaja por ahí. Sin esa pieza la pantalla se queda
# congelada aunque la web cargue bien.
Escribir-Paso 1 'Revisando IIS...'

$piezasIis = @(
    'IIS-WebServerRole',
    'IIS-WebServer',
    'IIS-CommonHttpFeatures',
    'IIS-StaticContent',
    'IIS-DefaultDocument',
    'IIS-HttpErrors',
    'IIS-HttpRedirect',
    'IIS-HttpCompressionStatic',
    'IIS-HttpCompressionDynamic',
    'IIS-RequestFiltering',
    'IIS-Security',
    'IIS-WebSockets',
    'IIS-ApplicationDevelopment',
    'IIS-NetFxExtensibility45',
    'IIS-ISAPIExtensions',
    'IIS-ISAPIFilter',
    'IIS-WebServerManagementTools',
    'IIS-ManagementConsole'
)

$faltan = @()
foreach ($pieza in $piezasIis) {
    $estado = Get-WindowsOptionalFeature -Online -FeatureName $pieza -ErrorAction SilentlyContinue
    if ($null -eq $estado) {
        Escribir-Info "La pieza '$pieza' no existe en esta version de Windows: se omite."
        continue
    }
    if ($estado.State -ne 'Enabled') {
        $faltan += $pieza
    }
}

if ($faltan.Count -eq 0) {
    Escribir-Ok 'IIS ya estaba instalado con todo lo necesario.'
}
else {
    Escribir-Info "Instalando $($faltan.Count) pieza(s) de IIS. Esto tarda un par de minutos..."
    $resultado = Enable-WindowsOptionalFeature -Online -FeatureName $faltan -All -NoRestart
    Escribir-Ok 'IIS instalado.'
    if ($resultado.RestartNeeded) {
        Escribir-Aviso 'Windows pide reiniciar. Si algo falla mas abajo, reinicia y vuelve a ejecutar este script.'
    }
}

if (-not (Get-Service -Name 'W3SVC' -ErrorAction SilentlyContinue)) {
    throw 'IIS sigue sin aparecer despues de instalarlo. Reinicia el equipo y vuelve a ejecutar este script.'
}

Import-Module WebAdministration -ErrorAction Stop
Escribir-Ok 'Herramientas de IIS disponibles.'


# --- 2. Complemento de IIS para .NET (Hosting Bundle) -----------------------
# IIS por si solo no sabe ejecutar una aplicacion .NET: necesita este modulo.
# Se instala DESPUES de IIS a proposito; si se hace antes, no se registra.
Escribir-Paso 2 'Revisando el complemento de IIS para .NET...'

$rutaModulo = Join-Path $env:ProgramFiles 'IIS\Asp.Net Core Module\V2\aspnetcorev2.dll'

if (Test-Path $rutaModulo) {
    Escribir-Ok 'El complemento ya estaba instalado.'
}
else {
    $descarga = Join-Path $env:TEMP 'dotnet-hosting-win.exe'
    $url = 'https://aka.ms/dotnet/10.0/dotnet-hosting-win.exe'

    Escribir-Info 'Descargando el complemento (unos 100 MB)...'
    try {
        # TLS 1.2 explicito: PowerShell 5.1 negocia TLS 1.0 por defecto y la
        # descarga falla con un error de conexion que no dice nada.
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        Invoke-WebRequest -Uri $url -OutFile $descarga -UseBasicParsing
    }
    catch {
        throw @"
No se ha podido descargar el complemento de IIS para .NET.

Descargalo a mano desde:
  https://dotnet.microsoft.com/download/dotnet/10.0
  (seccion Windows -> "Hosting Bundle")

Instalalo y vuelve a ejecutar este script.

Detalle del error: $($_.Exception.Message)
"@
    }

    if (-not (Test-Path $descarga) -or (Get-Item $descarga).Length -lt 1MB) {
        throw "La descarga de '$descarga' esta incompleta. Instala el Hosting Bundle a mano desde https://dotnet.microsoft.com/download/dotnet/10.0"
    }

    Escribir-Info 'Instalando el complemento (silencioso, puede tardar)...'
    $instalacion = Start-Process -FilePath $descarga -ArgumentList '/quiet', '/norestart' -Wait -PassThru
    Remove-Item $descarga -Force -ErrorAction SilentlyContinue

    # 0 = correcto, 3010 = correcto pero pide reiniciar, 1638 = ya habia otra version.
    if ($instalacion.ExitCode -notin @(0, 3010, 1638)) {
        throw "El instalador del complemento ha terminado con el codigo $($instalacion.ExitCode). Instalalo a mano desde https://dotnet.microsoft.com/download/dotnet/10.0"
    }

    Escribir-Info 'Reiniciando IIS para que reconozca el complemento...'
    & iisreset.exe /restart | Out-Null

    if (-not (Test-Path $rutaModulo)) {
        throw 'El complemento se ha instalado pero no aparece. Reinicia el equipo y vuelve a ejecutar este script.'
    }
    Escribir-Ok 'Complemento instalado.'
}


# --- 3. El nombre miscatas.es apunta a este equipo --------------------------
# No existe ningun servidor de nombres que sepa que miscatas.es es este PC, asi
# que se apunta a mano en el fichero hosts de Windows. Solo vale en este equipo.
Escribir-Paso 3 "Apuntando $Dominio a este equipo..."

$rutaHosts = Join-Path $env:SystemRoot 'System32\drivers\etc\hosts'
$lineas = @(Get-Content -Path $rutaHosts -ErrorAction SilentlyContinue)
$nombres = @($Dominio, "www.$Dominio")
$nuevas = @()

foreach ($nombre in $nombres) {
    # Se busca el nombre como palabra suelta para no confundir miscatas.es con
    # www.miscatas.es ni con un comentario que lo mencione.
    $yaEsta = $lineas | Where-Object {
        $_ -notmatch '^\s*#' -and $_ -match "(^|\s)$([regex]::Escape($nombre))(\s|$)"
    }
    if ($yaEsta) {
        Escribir-Info "$nombre ya estaba apuntado."
    }
    else {
        $nuevas += "127.0.0.1`t$nombre"
    }
}

if ($nuevas.Count -gt 0) {
    $bloque = @('', '# MisCatas - web local (lo pone tools\desplegar-iis.ps1)') + $nuevas
    Add-Content -Path $rutaHosts -Value $bloque -Encoding ASCII
    Escribir-Ok "Añadido(s) $($nuevas.Count) nombre(s) al fichero hosts."
}
else {
    Escribir-Ok 'El fichero hosts ya estaba correcto.'
}

# La cache de nombres de Windows guarda los fallos anteriores: se limpia.
& ipconfig.exe /flushdns | Out-Null


# --- 4. Certificado de seguridad de miscatas.es -----------------------------
# Hace falta uno propio porque ningun organismo oficial puede certificar un
# nombre que solo existe en esta casa. Se marca como de confianza en este equipo
# para que el navegador no muestre la pantalla roja de aviso.
Escribir-Paso 4 "Preparando el certificado de $Dominio..."

$nombreAmistoso = "MisCatas ($Dominio)"
$certificado = Get-ChildItem -Path Cert:\LocalMachine\My |
    Where-Object { $_.FriendlyName -eq $nombreAmistoso -and $_.NotAfter -gt (Get-Date).AddDays(30) } |
    Sort-Object NotAfter -Descending |
    Select-Object -First 1

if ($certificado) {
    Escribir-Info "Reutilizando el certificado existente (valido hasta $($certificado.NotAfter.ToString('dd/MM/yyyy')))."
}
else {
    $certificado = New-SelfSignedCertificate `
        -DnsName $nombres `
        -FriendlyName $nombreAmistoso `
        -CertStoreLocation 'Cert:\LocalMachine\My' `
        -KeyExportPolicy Exportable `
        -KeyLength 2048 `
        -NotAfter (Get-Date).AddYears(5)

    Escribir-Ok "Certificado creado, valido hasta $($certificado.NotAfter.ToString('dd/MM/yyyy'))."
}

# Copia en el almacen de "entidades de confianza": sin esto el navegador
# desconfia de su propio certificado.
$yaConfiado = Get-ChildItem -Path Cert:\LocalMachine\Root |
    Where-Object { $_.Thumbprint -eq $certificado.Thumbprint }

if ($yaConfiado) {
    Escribir-Info 'El certificado ya estaba marcado como de confianza.'
}
else {
    $almacen = New-Object System.Security.Cryptography.X509Certificates.X509Store('Root', 'LocalMachine')
    $almacen.Open('ReadWrite')
    $almacen.Add($certificado)
    $almacen.Close()
    Escribir-Ok 'Certificado marcado como de confianza en este equipo.'
}


# --- 5. Publicar la aplicacion ---------------------------------------------
# "Publicar" deja en una carpeta la web lista para servir: ya compilada, con el
# CSS minificado y con todos los ficheros estaticos de verdad en wwwroot.
Escribir-Paso 5 'Publicando la aplicacion...'

if ($SinPublicar) {
    Escribir-Info 'Publicacion omitida (-SinPublicar).'
    if (-not (Test-Path (Join-Path $Carpeta 'MisCatas.Web.dll'))) {
        throw "No hay nada publicado en '$Carpeta'. Ejecuta el script sin -SinPublicar al menos una vez."
    }
}
else {
    # Si el sitio ya existe, se para: IIS mantiene los ficheros abiertos y la
    # copia fallaria a medias, dejando la web rota.
    if (Test-Path "IIS:\Sites\$Sitio") {
        Escribir-Info 'Parando el sitio para poder reemplazar los ficheros...'
        Stop-Website -Name $Sitio -ErrorAction SilentlyContinue
        Stop-WebAppPool -Name $Sitio -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 3
    }

    # La aplicacion arrancada por Arrancar.cmd bloquea los DLL de compilacion.
    # Solo se mata MisCatas.Web: parar todos los "dotnet" se llevaria por
    # delante Visual Studio y las extensiones del editor.
    $previos = @(Get-Process -Name 'MisCatas.Web' -ErrorAction SilentlyContinue)
    if ($previos.Count -gt 0) {
        Escribir-Info "Parando $($previos.Count) instancia(s) de pruebas..."
        $previos | Stop-Process -Force
        Start-Sleep -Seconds 2
    }

    if (-not (Test-Path $Carpeta)) {
        New-Item -Path $Carpeta -ItemType Directory -Force | Out-Null
    }

    Escribir-Info 'Compilando y copiando (incluye el CSS de Tailwind)...'
    & dotnet publish $proyecto -c Release -o $Carpeta --nologo -v minimal
    if ($LASTEXITCODE -ne 0) {
        throw "La publicacion ha fallado (codigo $LASTEXITCODE). Revisa los errores de arriba."
    }

    # Comprobaciones: estos son los fallos que dejan la web en blanco o sin
    # estilos, y es mucho mejor enterarse aqui que delante de los invitados.
    $cssPublicado = Join-Path $Carpeta 'wwwroot\css\estilos.css'
    if (-not (Test-Path $cssPublicado)) {
        throw "Falta '$cssPublicado'. Ejecuta tools\descargar-tailwind.ps1 y vuelve a publicar."
    }

    $bytesCss = (Get-Item $cssPublicado).Length
    if ($bytesCss -lt $cssMinimoBytes) {
        throw "El CSS publicado solo tiene $bytesCss bytes (se esperaban mas de $cssMinimoBytes). Tailwind ha purgado de mas y la web se veria sin estilos."
    }

    foreach ($critico in @('MisCatas.Web.dll', 'web.config', 'wwwroot\_framework\blazor.web.js', 'wwwroot\MisCatas.Web.styles.css')) {
        if (-not (Test-Path (Join-Path $Carpeta $critico))) {
            throw "Falta el fichero publicado '$critico'. La publicacion esta incompleta."
        }
    }

    Escribir-Ok "Publicado en $Carpeta (CSS de $bytesCss bytes)."
}


# --- 6. Sitio web y grupo de aplicaciones en IIS ---------------------------
Escribir-Paso 6 'Configurando el sitio en IIS...'

# Grupo de aplicaciones. "No Managed Code" porque la web es .NET moderno: no
# usa el .NET antiguo que IIS cargaria por defecto.
if (Test-Path "IIS:\AppPools\$Sitio") {
    Escribir-Info "El grupo de aplicaciones '$Sitio' ya existia."
}
else {
    New-WebAppPool -Name $Sitio | Out-Null
    Escribir-Ok "Grupo de aplicaciones '$Sitio' creado."
}

Set-ItemProperty "IIS:\AppPools\$Sitio" -Name managedRuntimeVersion -Value ''
Set-ItemProperty "IIS:\AppPools\$Sitio" -Name startMode -Value 'AlwaysRunning'
# Sin perfil de usuario, el guardado de las claves de cifrado falla.
Set-ItemProperty "IIS:\AppPools\$Sitio" -Name processModel.loadUserProfile -Value $true
# Sin apagado por inactividad ni reciclados: una cata en vivo puede estar horas
# abierta en el televisor sin que nadie toque nada.
Set-ItemProperty "IIS:\AppPools\$Sitio" -Name processModel.idleTimeout -Value ([TimeSpan]::Zero)
Set-ItemProperty "IIS:\AppPools\$Sitio" -Name recycling.periodicRestart.time -Value ([TimeSpan]::Zero)
Escribir-Ok 'Grupo de aplicaciones ajustado (siempre activo, sin reciclados).'

# El sitio por defecto de IIS ocupa los puertos 80 y 443 sin nombre, lo que
# impide que miscatas.es responda. Se para (no se borra, por si hiciera falta).
$porDefecto = Get-Website -Name 'Default Web Site' -ErrorAction SilentlyContinue
if ($porDefecto -and $porDefecto.State -eq 'Started') {
    Stop-Website -Name 'Default Web Site'
    Set-ItemProperty "IIS:\Sites\Default Web Site" -Name serverAutoStart -Value $false
    Escribir-Info 'Parado el sitio de ejemplo de IIS ("Default Web Site"): ocupaba los puertos.'
}

# Sitio web.
if (Test-Path "IIS:\Sites\$Sitio") {
    Escribir-Info "El sitio '$Sitio' ya existia: se actualiza su configuracion."
    Set-ItemProperty "IIS:\Sites\$Sitio" -Name physicalPath -Value $Carpeta
    Set-ItemProperty "IIS:\Sites\$Sitio" -Name applicationPool -Value $Sitio
}
else {
    New-Website -Name $Sitio -PhysicalPath $Carpeta -ApplicationPool $Sitio `
        -HostHeader $Dominio -Port 80 -Force | Out-Null
    Escribir-Ok "Sitio '$Sitio' creado."
}

# Enlaces (por que direccion y puerto responde el sitio).
# Se usa SNI (SslFlags 1) para que varios sitios puedan compartir el puerto 443
# distinguiendose por el nombre.
$enlaces = @(
    @{ Protocolo = 'http';  Puerto = 80;  Nombre = $Dominio },
    @{ Protocolo = 'http';  Puerto = 80;  Nombre = "www.$Dominio" },
    @{ Protocolo = 'https'; Puerto = 443; Nombre = $Dominio },
    @{ Protocolo = 'https'; Puerto = 443; Nombre = "www.$Dominio" }
)

foreach ($enlace in $enlaces) {
    $existe = Get-WebBinding -Name $Sitio -Protocol $enlace.Protocolo -ErrorAction SilentlyContinue |
        Where-Object { $_.bindingInformation -eq "*:$($enlace.Puerto):$($enlace.Nombre)" }

    if ($existe) {
        Escribir-Info "El enlace $($enlace.Protocolo)://$($enlace.Nombre) ya existia."
        continue
    }

    if ($enlace.Protocolo -eq 'https') {
        New-WebBinding -Name $Sitio -Protocol 'https' -Port $enlace.Puerto `
            -HostHeader $enlace.Nombre -SslFlags 1 | Out-Null
    }
    else {
        New-WebBinding -Name $Sitio -Protocol 'http' -Port $enlace.Puerto `
            -HostHeader $enlace.Nombre | Out-Null
    }
    Escribir-Ok "Enlace $($enlace.Protocolo)://$($enlace.Nombre) creado."
}

# Asignar el certificado a los dos enlaces seguros.
foreach ($nombre in $nombres) {
    $enlaceHttps = Get-WebBinding -Name $Sitio -Protocol 'https' |
        Where-Object { $_.bindingInformation -eq "*:443:$nombre" }

    if ($enlaceHttps) {
        $enlaceHttps.AddSslCertificate($certificado.Thumbprint, 'My')
    }
}
Escribir-Ok 'Certificado asignado a los enlaces seguros.'


# --- 7. Permisos -----------------------------------------------------------
Escribir-Paso 7 'Dando permisos a la web...'

$identidad = "IIS AppPool\$Sitio"

# a) Carpeta. La web necesita leer todo y escribir en 'claves', donde guarda
#    las claves de cifrado de las cookies. Si no puede escribir, cada reinicio
#    invalida las sesiones y los formularios dan error de token.
& icacls.exe $Carpeta /grant "$($identidad):(OI)(CI)(RX)" /T /C /Q | Out-Null

$carpetaClaves = Join-Path $Carpeta 'claves'
if (-not (Test-Path $carpetaClaves)) {
    New-Item -Path $carpetaClaves -ItemType Directory -Force | Out-Null
}
& icacls.exe $carpetaClaves /grant "$($identidad):(OI)(CI)(M)" /T /C /Q | Out-Null
Escribir-Ok 'Permisos de carpeta concedidos.'

# b) Base de datos. La conexion usa "autenticacion de Windows", asi que quien
#    entra en SQL Server es la propia identidad del sitio de IIS, no el usuario
#    que ejecuta este script. Hay que darle de alta expresamente.
$servidor = $null
$baseDatos = $null
try {
    $ajustes = Get-Content -Raw -Path $rutaAjustes | ConvertFrom-Json
    $cadena = $ajustes.ConnectionStrings.Default
    if ($cadena -match 'Server\s*=\s*([^;]+)') { $servidor = $Matches[1].Trim() }
    if ($cadena -match 'Database\s*=\s*([^;]+)') { $baseDatos = $Matches[1].Trim() }
}
catch {
    Escribir-Aviso "No se ha podido leer '$rutaAjustes': $($_.Exception.Message)"
}

if (-not $servidor -or -not $baseDatos) {
    Escribir-Aviso 'No se ha podido averiguar el servidor de base de datos. Da de alta el acceso a mano (ver el aviso del final).'
}
else {
    Escribir-Info "Dando acceso a '$identidad' en $servidor / $baseDatos..."

    # El acceso se concede con SQL directo para no depender de modulos extra
    # (sqlcmd o SqlServer) que pueden no estar instalados.
    $sql = @"
IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'$identidad')
    CREATE LOGIN [$identidad] FROM WINDOWS;

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'$identidad')
    CREATE USER [$identidad] FOR LOGIN [$identidad];

ALTER ROLE [db_owner] ADD MEMBER [$identidad];
"@

    # db_owner porque la aplicacion aplica ella misma las migraciones al
    # arrancar: necesita poder crear y modificar tablas.
    try {
        $conexion = New-Object System.Data.SqlClient.SqlConnection
        $conexion.ConnectionString = "Server=$servidor;Database=$baseDatos;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=True;Connect Timeout=15"
        $conexion.Open()

        $comando = $conexion.CreateCommand()
        $comando.CommandText = $sql
        $comando.ExecuteNonQuery() | Out-Null

        $conexion.Close()
        Escribir-Ok "Acceso a la base de datos concedido a '$identidad'."
    }
    catch {
        Escribir-Aviso "No se ha podido dar el acceso a la base de datos: $($_.Exception.Message)"
        Escribir-Aviso 'Hazlo a mano con SQL Server Management Studio (ver el aviso del final).'
    }
    finally {
        if ($conexion -and $conexion.State -eq 'Open') { $conexion.Close() }
    }
}


# --- 8. Arrancar y comprobar ----------------------------------------------
Escribir-Paso 8 'Arrancando el sitio...'

Start-WebAppPool -Name $Sitio -ErrorAction SilentlyContinue
Start-Website -Name $Sitio

# El primer arranque es lento: la aplicacion aplica las migraciones de base de
# datos y siembra los datos iniciales antes de responder.
Escribir-Info 'Esperando a que responda (el primer arranque tarda)...'

$codigo = $null
for ($intento = 1; $intento -le 12; $intento++) {
    Start-Sleep -Seconds 5
    # -k porque el certificado es propio; curl no conoce el almacen de Windows.
    $codigo = (& curl.exe -k -s -o NUL -w '%{http_code}' "https://$Dominio/" 2>$null)
    if ($codigo -and $codigo -ne '000') { break }
    Escribir-Info "Intento $intento de 12..."
}

Write-Host ''
Write-Host '=============================================================' -ForegroundColor Magenta

# 200 = la pagina se ha servido. 302 = redirige a la pantalla de entrada
# (normal, porque hay que identificarse). Las dos cosas significan que va bien.
if ($codigo -in @('200', '302')) {
    Write-Host '  LISTO. La web ya esta instalada en IIS.' -ForegroundColor Green
    Write-Host ''
    Write-Host "  Este equipo : https://$Dominio" -ForegroundColor White
    Write-Host "  Tambien     : https://www.$Dominio" -ForegroundColor White
    Write-Host "  Carpeta     : $Carpeta" -ForegroundColor DarkGray
    Write-Host ''
    Write-Host '  Se arranca sola al encender el equipo: no hay que hacer nada.' -ForegroundColor DarkGray
    Write-Host '  Para quitarla: tools\retirar-iis.ps1 (como administrador).' -ForegroundColor DarkGray
    Write-Host ''
    Write-Host '  OJO: los moviles y el televisor NO conocen el nombre' -ForegroundColor Yellow
    Write-Host '  miscatas.es, porque solo existe en este equipo. Para ellos' -ForegroundColor Yellow
    Write-Host '  sigue valiendo la direccion con la IP (Arrancar-Red.cmd).' -ForegroundColor Yellow
}
else {
    Write-Host '  La web no responde todavia.' -ForegroundColor Red
    Write-Host ''
    Write-Host "  Respuesta recibida: '$codigo' (000 = nadie contesta)" -ForegroundColor Yellow
    Write-Host ''
    Write-Host '  Cosas que mirar, por orden:' -ForegroundColor White
    Write-Host '   1. Que el grupo de aplicaciones siga arrancado:' -ForegroundColor DarkGray
    Write-Host "      Get-WebAppPoolState -Name $Sitio" -ForegroundColor DarkGray
    Write-Host '   2. El registro de errores de Windows (Visor de eventos ->' -ForegroundColor DarkGray
    Write-Host '      Registros de Windows -> Aplicacion), buscando "IIS AspNetCore".' -ForegroundColor DarkGray
    Write-Host '   3. Que la web tenga acceso a la base de datos: el usuario' -ForegroundColor DarkGray
    Write-Host "      '$identidad' debe existir en SQL Server con permisos sobre '$baseDatos'." -ForegroundColor DarkGray
    Write-Host '   4. Que nadie mas ocupe el puerto 443:' -ForegroundColor DarkGray
    Write-Host '      Get-NetTCPConnection -LocalPort 443 -State Listen' -ForegroundColor DarkGray
}

Write-Host '=============================================================' -ForegroundColor Magenta
Write-Host ''

if (-not $SinNavegador -and $codigo -in @('200', '302')) {
    Start-Process "https://$Dominio/"
}
