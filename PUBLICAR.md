# Publicar en GitHub

Esta carpeta contiene el proyecto público. No subas la carpeta de la versión
personal ni sus archivos de estado, historiales o capturas.

## Primera publicación

1. Creá un repositorio **público**, por ejemplo `dynamic-ip-monitor`.
2. Subí el contenido de esta carpeta, incluidas `.github` y `.gitignore`.
   Si usás Git, desde esta carpeta:

   ```powershell
   git init -b main
   git add .
   git commit -m "Initial public beta"
   git remote add origin https://github.com/TU_USUARIO/dynamic-ip-monitor.git
   git push -u origin main
   ```

3. Revisá que la ejecución de **Actions → Build and package** termine correctamente.
4. Desde el repositorio local:

   ```powershell
   git tag v0.3.0-beta
   git push origin v0.3.0-beta
   ```

5. El flujo creará una release en borrador con el ZIP y un archivo SHA-256.
   Abrí **Releases**, revisá el borrador y pulsá **Publish release**.
6. Compartí la dirección de esa release: `/releases/tag/v0.3.0-beta`.

También podés crear la release manualmente desde la web y adjuntar el ZIP
precompilado y su archivo SHA-256. No adjuntes el ZIP de código fuente como
si fuera la aplicación: quienes solo quieran usarla necesitan el ZIP para Windows.

La beta viene marcada como prerelease. El acceso `/releases/latest` puede omitir
prereleases; reservalo para cuando publiques una versión estable.

## Versiones siguientes

Actualizá la versión en `src/AssemblyInfo.cs`, `scripts/package.ps1`, este
documento y el nombre del ZIP en `README.md`. Compilá y probá antes de crear una
nueva etiqueta. El flujo rechaza etiquetas cuya versión no coincida con el paquete.

Documentación oficial de [GitHub Releases](https://docs.github.com/en/repositories/releasing-projects-on-github/managing-releases-in-a-repository).
