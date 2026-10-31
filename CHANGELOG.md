# Historial de cambios

## 0.5.2-beta

- La unidad de minutos aparece junto al número en una sola línea (45m), también en el ícono real de Windows.
- Durante el último minuto se muestra <1m en lugar de segundos.
- Ayuda, capturas y documentación actualizadas para reflejar el cambio.

## 0.5.1-beta

- La casilla Iniciar con Windows queda visible en la ventana principal, fuera de los ajustes avanzados.
- Se conserva el inicio en bandeja y la preferencia de cada usuario.
- Capturas y documentación actualizadas.

## 0.5.0-beta

- Acciones de uso diario debajo del contador, con mayor prioridad para seguir en bandeja.
- Botón Actualizar ahora para consultar la conexión sin esperar la lectura periódica.
- Reinicio del router separado y acompañado por el aviso de corte de Internet.
- Referencia manual, ciclo, inicio con Windows y archivos agrupados en ajustes avanzados plegables.
- Mejoras de contraste, orden del teclado, configuración y desplazamiento en ventanas pequeñas.
- Interfaz revisada en español e inglés; el estado de reconexión usa un color neutral.

## 0.4.1-beta

- Si el router sigue arrancando, se reintenta la conexión cada 30 segundos en lugar de bloquear el ingreso por 10 minutos.
- La espera de 10 minutos queda reservada para un rechazo de autenticación.
- Durante la recuperación se actualiza la IP visible y se permite corregir la configuración.
- Prueba de reinicio simulado con varios intentos durante el arranque y recuperación del contador e ícono.

## 0.4.0-beta

- Botón para reiniciar un ZTE F6600 compatible, con confirmación en cada uso.
- Solicitud con token de sesión e integridad del panel; nunca se repite al perder la respuesta.
- Estado de reconexión hasta recuperar una lectura del router, sin adelantar el contador.
- Interfaz y mensajes nuevos en español e inglés.
- Pruebas simuladas de éxito, rechazo y respuesta perdida; preparación verificada sin reinicio real.

## 0.3.2-beta

- Se normalizan las fechas vacías de versiones anteriores sin depender de la zona horaria.
- Un campo de fecha inválido también permite recuperar el archivo de respaldo.

## 0.3.1-beta

- Se separaron las pruebas del ejecutable y se quitaron los modos internos de captura y diagnóstico.
- Un corte de Internet ya no provoca nuevos ingresos innecesarios al panel del router.
- Los cambios rápidos de IP actualizan correctamente el inicio de sesión.
- Cambiar de idioma ya no repite avisos cuando el ciclo usa horas decimales.
- Estado y configuración se guardan de forma atómica y recuperan su respaldo si el archivo principal está dañado.
- Se validan referencias y valores de ciclo dañados antes de construir la ventana.
- La ventana permite cerrar Windows y mantiene la casilla de inicio automático consistente si falla el registro.
- Se unificó la validación de IPv4 y se retiraron campos y traducciones sin uso.
- La versión del paquete se obtiene del ejecutable para evitar desajustes.

## 0.3.0-beta

- Interfaz, menús de bandeja, notificaciones y fechas en español e inglés.
- Selector de idioma con cambio inmediato y preferencia guardada.
- Detección del idioma de Windows; inglés como alternativa para otros idiomas.
- Catálogo central de traducciones e instrucciones en inglés.
- Pruebas de traducciones, detección, persistencia y vistas en ambos idiomas.

## 0.2.2-beta

- Ícono simplificado a un único número grande de horas completas.
- Con menos de una hora muestra minutos y, durante el último minuto, segundos.
- Sin ceros a la izquierda; conserva los colores y el tiempo completo al pasar el mouse.

## 0.2.1-beta

- Contador en el ícono de la bandeja: horas arriba y minutos abajo.
- Segundos durante el último minuto y tiempo completo al pasar el mouse.
- Verde con 3 h o más, amarillo entre 1 y 3 h y rojo con menos de 1 h.
- Indicadores separados para referencia desconocida y estimación vencida.
- El contador de la ventana usa los mismos colores.
- Actualización de íconos con liberación de recursos nativos.

## 0.2.0-beta

- Primera beta pública para Windows.
- Dirección del router y credenciales opcionales configurables.
- Acceso al router habilitado expresamente por cada usuario.
- Lectura del inicio de sesión PPPoE desde el contador WAN del ZTE F6600.
- Respaldo mediante cambios de IP pública.
- Avisos de reconexión, bandeja e inicio opcional con Windows.
- Datos locales separados de los archivos de la aplicación.
- Contraseña opcional protegida con Windows DPAPI.
- Compilación, pruebas aisladas y empaquetado para GitHub Releases.
