# Finaliza la cata 3 con votos en todos los vinos para probar
# la pantalla de resultado y la revelacion del escaparate.
$sql = @"
SET NOCOUNT ON;
DECLARE @cata INT = 3;

UPDATE CataVinos SET EstadoVotacion = 2 WHERE CataId = @cata;

DELETE FROM Votos WHERE CataId = @cata;

INSERT INTO Votos (CataId, VinoId, UsuarioId, Nota, Comentario, SegundosEnVotar, FechaVotoUtc, FueRectificado, EsAutomatico)
SELECT @cata, cv.VinoId, cu.UsuarioId,
       CAST(4.0 + (cv.Orden * 1.5) + ((cu.UsuarioId % 3) * 0.5) AS DECIMAL(4,1)),
       CASE WHEN cv.Orden = 1 THEN 'Muy redondo' ELSE NULL END,
       30 + (cv.Orden * 7), SYSUTCDATETIME(), 0, 0
FROM CataVinos cv
CROSS JOIN CataUsuarios cu
WHERE cv.CataId = @cata AND cu.CataId = @cata;

UPDATE Catas
SET Estado = 3, VinoActualId = NULL, FechaFinalizacion = SYSUTCDATETIME(), Fecha = CAST(GETDATE() AS DATE)
WHERE Id = @cata;

SELECT COUNT(*) AS Votos FROM Votos WHERE CataId = @cata;
SELECT Estado, Fecha FROM Catas WHERE Id = @cata;
"@

sqlcmd -S "7940HS\LOCAL" -E -d Catas -Q $sql -W -s "|"
