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

Resultado esperado:

- Compra confirmada.
- Venta con total calculado por trigger.
- Transferencia aceptada cambia propietario.
- Validación marca entrada como `VALIDADA`.
- Segundo intento falla.
- Reportes muestran resultados.
