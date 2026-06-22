ALTER TABLE Usuario
ADD COLUMN HashContrasena VARCHAR(255) NULL
AFTER CorreoElectronico;
