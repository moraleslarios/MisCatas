# Arranca MisCatas dejando el log en un fichero, para que el terminal del agente
# no se inunde con las consultas SQL de EF Core (nivel Information).
#
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools\arrancar-silencioso.ps1
#
# El log queda en tools\salida-app.log y la app en https://localhost:5443

$ErrorActionPreference = 'Stop'

$raiz = Split-Path -Parent $PSScriptRoot
$log = Join-Path $PSScriptRoot 'salida-app.log'

Get-Process -Name MisCatas.Web -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 800

if (Test-Path $log) { Remove-Item $log -Force }

$exe = Join-Path $raiz 'src\MisCatas.Web\bin\Debug\net10.0\MisCatas.Web.exe'
if (-not (Test-Path $exe)) {
    Write-Host "No existe $exe. Compila primero con: dotnet build src\MisCatas.Web\MisCatas.Web.csproj"
    exit 1
}

# IMPORTANTE: sin esto la app arranca en Production y NO sirve los CSS aislados
# de los componentes (MisCatas.Web.styles.css da error 500 y la app sale sin formato).
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:DOTNET_ENVIRONMENT = 'Development'

$proceso = Start-Process -FilePath $exe `
    -WorkingDirectory (Join-Path $raiz 'src\MisCatas.Web') `
    -RedirectStandardOutput $log `
    -RedirectStandardError (Join-Path $PSScriptRoot 'salida-app.err.log') `
    -WindowStyle Hidden `
    -PassThru

Write-Host "MisCatas arrancada. PID $($proceso.Id). Log: $log"

# Esperamos a ver la linea de Kestrel para confirmar que esta escuchando.
$limite = (Get-Date).AddSeconds(30)
while ((Get-Date) -lt $limite) {
    if ((Test-Path $log) -and (Select-String -Path $log -Pattern 'Application started' -Quiet)) {
        Write-Host 'LISTA: escuchando en https://localhost:5443'
        exit 0
    }
    Start-Sleep -Milliseconds 500
}

Write-Host 'AVISO: no se vio "Application started" en 30 s. Revisa el log.'
