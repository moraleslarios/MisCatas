$escritorio = [Environment]::GetFolderPath('Desktop')
$shell = New-Object -ComObject WScript.Shell

foreach ($nombre in @('MisCatas - ARRANCAR', 'MisCatas - ARRANCAR (RED)', 'MisCatas - PARAR')) {
    $ruta = Join-Path $escritorio ($nombre + '.lnk')
    if (Test-Path $ruta) {
        $l = $shell.CreateShortcut($ruta)
        Write-Host ''
        Write-Host $nombre -ForegroundColor Cyan
        Write-Host ('  Apunta a : ' + $l.TargetPath)
        Write-Host ('  Carpeta  : ' + $l.WorkingDirectory)
        Write-Host ('  Existe   : ' + (Test-Path $l.TargetPath))
    }
    else {
        Write-Host ('NO EXISTE: ' + $ruta) -ForegroundColor Red
    }
}
Write-Host ''
