$ruta = 'C:\Git\Proyectos\MisCatas\.vscode\tasks.json'
try {
    $texto = Get-Content $ruta -Raw
    # tasks.json admite comentarios // pero ConvertFrom-Json no: los quitamos.
    $limpio = ($texto -split "`n" | Where-Object { $_.Trim() -notlike '//*' }) -join "`n"
    $objeto = $limpio | ConvertFrom-Json
    Write-Host 'JSON VALIDO. Tareas:' -ForegroundColor Green
    foreach ($t in $objeto.tasks) { Write-Host ('  - ' + $t.label) }
}
catch {
    Write-Host ('JSON ROTO: ' + $_.Exception.Message) -ForegroundColor Red
}
