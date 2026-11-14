# Space Farm

Prototipo inicial de un juego de estrategia y supervivencia educativa en Marte. El concepto completo está en [SPACE_FARM_CONCEPTO.md](SPACE_FARM_CONCEPTO.md).

## Abrir el proyecto

1. Instalar **Unity 6.6.9f1** con soporte para Web si se quiere generar una versión Web.
2. En Unity Hub, añadir la carpeta `SpaceFarm` de este repositorio como proyecto.
3. Abrir `Assets/Scenes/SpaceFarm.unity` y pulsar **Play**.

## Primera versión jugable

- Hacé clic en el mapa para enviar el único rover. Revela sectores cercanos y estudia automáticamente los yacimientos cuando llega a ellos.
- El hierro está al noreste de la nave y el hielo al noroeste. Al estudiarlos se desbloquean la mina y el extractor de agua.
- Usá los botones de la derecha para construir, mejorar, recibir un colono y avanzar un sol. El panel inferior explica los resultados.
- La tormenta ocurre en los soles 3 y 4; la primera incursión, en el sol 5. Para completar esta versión, mantené la colonia hasta el sol 7 con mina, invernadero, tres colonos y barreras.

La simulación es deliberadamente simplificada. La planta de oxígeno es una extrapolación ficticia inspirada en MOXIE; las criaturas son ciencia ficción.

## Estructura

- `SpaceFarm/Assets/Scripts/SpaceFarmState.cs`: reglas de recursos, edificios y eventos.
- `SpaceFarm/Assets/Scripts/SpaceFarmGame.cs`: mapa, rover, interacción e interfaz.
- `SpaceFarm/Assets/Scripts/SpaceFarmPalette.cs`: colores del prototipo.
- `SpaceFarm/Assets/Scenes/SpaceFarm.unity`: escena principal.

Los directorios `Library`, `Temp`, `Logs` y `UserSettings` del editor se excluyen de Git.
