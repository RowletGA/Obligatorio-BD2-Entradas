# Prueba punta a punta

Usar datos demo identificables. No incluir contraseñas ni documentos reales.

1. Registrar administrador demo en `/Account/Register`.
2. Convertirlo en administrador con el procedimiento de `docs/CONFIGURACION_DEMO.md`.
3. Cerrar sesión e iniciar sesión nuevamente.
4. Abrir `/Admin`.
5. Crear estadio en `/Admin/Estadios/Nuevo`.
6. Crear dos sectores en `/Admin/Sectores/Nuevo`.
7. Crear dos equipos en `/Admin/Equipos/Nuevo`.
8. Crear evento futuro en `/Admin/Eventos/Nuevo`.
9. Confirmar que el evento aparece en `/Eventos`.
10. Registrar comprador demo.
11. Iniciar sesión como comprador.
12. Abrir el detalle del evento y comprar dos entradas.
13. Verificar `/Compras` y `/Entradas/MisEntradas`.
14. Registrar receptor demo.
15. Desde comprador, transferir una entrada al correo del receptor.
16. Iniciar sesión como receptor.
17. Aceptar la transferencia en `/Transferencias`.
18. Confirmar que la entrada aparece en `/Entradas/MisEntradas` del receptor.
19. Registrar funcionario demo.
20. Convertirlo en funcionario con `docs/CONFIGURACION_DEMO.md`.
21. Como administrador, asignarlo en `/Admin/Funcionarios`.
22. Como funcionario, abrir `/Funcionario/Validar`.
23. Validar la entrada por ID usando token manual `DEMO`.
24. Intentar validarla por segunda vez y confirmar rechazo funcional.
25. Como administrador, abrir `/Admin/Reportes`.

## Casos de perfiles

Caso A, usuario general solamente:

1. Iniciar sesión.
2. Confirmar navegación con Inicio, Eventos, Mis compras, Mis entradas, Transferencias y Mi perfil.
3. Ver eventos, comprar, revisar compras y entradas.

Caso B, administrador solamente:

1. Iniciar sesión.
2. Confirmar navegación administrativa sin Inicio, Mis compras ni Mis entradas.
3. Crear o revisar evento y confirmar capacidad visible con precio por entrada claramente etiquetado.

Caso C, administrador y usuario general:

1. Iniciar sesión.
2. Elegir Administrador en `/Account/SeleccionarPerfil`.
3. Confirmar navegación administrativa.
4. Cambiar a Usuario General desde el menú.
5. Confirmar navegación comercial.
6. Volver a Administrador sin cerrar sesión.

Caso D, funcionario y usuario general:

1. Iniciar sesión.
2. Elegir Funcionario.
3. Confirmar asignaciones y validación.
4. Cambiar a Usuario General.
5. Confirmar compras, entradas y transferencias.

## Edición administrativa de eventos

Caso edición sin ventas:

1. Crear evento sin entradas.
2. Abrir detalle administrativo.
3. Entrar en `Editar evento`.
4. Cambiar fecha, hora, estadio, equipos, sectores y precios.
5. Guardar y confirmar que el detalle refleja los cambios.

Caso edición con ventas:

1. Abrir evento con entradas emitidas.
2. Confirmar advertencia en detalle.
3. Confirmar que no aparece edición estructural.
4. Cambiar estado desde la acción separada.
5. Intentar manipular POST estructural y confirmar rechazo del servidor.

Caso asignación:

1. Abrir `/Admin/Funcionarios`.
2. Confirmar que no aparecen eventos `FINALIZADO` ni `CANCELADO`.
3. Seleccionar evento `PROGRAMADO`.
4. Confirmar sectores habilitados sin duplicados.
5. Cambiar de evento y confirmar que cambia la lista completa de sectores.
6. Crear asignación válida.
7. Intentar duplicarla y confirmar mensaje claro.

Resultado esperado:

- Compra confirmada.
- Venta con total calculado por trigger.
- Transferencia aceptada cambia propietario.
- Validación marca entrada como `VALIDADA`.
- Segundo intento falla.
- Reportes muestran resultados.
