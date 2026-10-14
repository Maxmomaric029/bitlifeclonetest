# BitLifeClone 🧬

Clon tipo BitLife (juego de vida) en **C# / WPF (.NET 8)** con todo el contenido del juego en **JSON** en `src/BitLifeClone/Data/`.

## Cómo se juega

Nace un personaje (nombre, género y país), y con el botón **+ Edad** pasa el año a año: escuela, universidad, trabajo, familia, crimen, salud, guerra, fama, propiedades y licencias. Todo lo que pasa sale en el feed con los cambios de stats y dinero.

## Estructura

```
src/BitLifeClone/
├── Data/            # TODO el contenido del juego (JSON) — fuente única de verdad: Data/Schema.md
│   ├── Events/      # pools de eventos (vida civil por cityStatus + guerra por especialidad)
│   ├── Jobs/        # 25+ categorías de trabajo con sueldos por fórmula
│   ├── Relationships/ # acciones por tipo de persona + subeventos + plantillas de personalidad
│   ├── School/      # sistema de notas, acciones, extracurriculares, trabajitos, cadenas
│   ├── War/         # reglas de guerra y rivalidades entre países
│   ├── Crime/       # crímenes, juicios y vida en prisión
│   ├── Health/      # clínica, adicciones y diagnósticos
│   ├── Fame/        # fama, viralidad y escándalos
│   ├── Licenses/    # licencias con examen
│   ├── Properties/  # bienes raíces y vehículos
│   └── World/       # países, ciudades y nombres
├── Models/ Services/ ViewModels/ Views/ Converters/ Themes/   # cliente WPF (MVVM a mano)
tools/              # validadores de datos en Python
saves/              # partidas guardadas (no se suben)
```

## Ejecutar y desarrollar

```bash
# correr (se necesita .NET SDK 8 con la carga de trabajo Windows Desktop)
dotnet build BitLifeClone.sln
dotnet run --project src/BitLifeClone

# validar contenido del juego (Python 3)
python tools/validate.py          # VALIDACION OK = 0 errores
python tools/test_validator.py    # 6/6 errores plantados detectados
```

## CI

GitHub Actions (`.github/workflows/dotnet.yml`) compila el proyecto en cada push y muestra los errores del compilador; además corre el validador de datos para que ningún JSON roto entre al repo.
