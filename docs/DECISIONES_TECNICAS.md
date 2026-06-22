# Decisiones técnicas

## Países como catálogo de aplicación

Contexto: los scripts no incluyen tablas codigueras de países.

Decisión: usar `catalogos-registro.json`, versionado y cargado al iniciar.

Consecuencias: no se modifican scripts originales y el catálogo se reutiliza en registro y administración.

Alternativas descartadas: crear tablas nuevas o hardcodear en Razor.

## Selector renderizado desde servidor

Contexto: el selector de tipo podía quedar vacío si JavaScript no cargaba.

Decisión: el GET usa `UY` como país inicial de demo y carga tipos permitidos desde servidor.

Consecuencias: el formulario funciona sin JavaScript.

Alternativas descartadas: depender exclusivamente de JavaScript.

## JavaScript como mejora progresiva

Contexto: se requiere UX dinámica, pero la validación real debe estar en servidor.

Decisión: JS filtra y muestra ayuda, pero no es fuente de verdad.

Consecuencias: el POST inválido reconstruye catálogos y valida todo en Application.

Alternativas descartadas: enviar regex internas como validación principal del cliente.

## Administración filtrada por país

Contexto: `Administrador` tiene `PaisSede`.

Decisión: consultar `PaisSede` desde DB en cada operación administrativa.

Consecuencias: manipular país o estadio en el request no permite salir de jurisdicción.

Alternativas descartadas: confiar en campos ocultos o selects.

## Creación de evento transaccional

Contexto: un evento requiere filas en cuatro tablas.

Decisión: usar una única conexión y `MySqlTransaction`.

Consecuencias: ante error no quedan eventos parciales.

Alternativas descartadas: insertar por pasos independientes desde controlador.

## Triggers como autoridad final

Contexto: la base ya modela reglas críticas.

Decisión: validar en C# para UX y dejar que triggers sean la última autoridad.

Consecuencias: se muestran mensajes funcionales de `SQLSTATE 45000`.

Alternativas descartadas: duplicar toda la lógica de triggers en Application.

## Sin eliminación física inicial

Contexto: estadios, sectores y equipos tienen relaciones por claves foráneas.

Decisión: no implementar baja física en esta iteración.

Consecuencias: se evita romper integridad referencial o inventar columnas `Activo`.

Alternativas descartadas: agregar soft delete no modelado.
