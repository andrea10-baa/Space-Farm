# Space Farm

Prototipo de un juego de estrategia y supervivencia educativa en Marte. El concepto completo está en [SPACE_FARM_CONCEPTO.md](SPACE_FARM_CONCEPTO.md).

![Marte generado con la semilla 1](docs/screenshots/mapa-semilla-1.png)

## Abrir el proyecto

1. Instalar **Unity 6.6.9f1** con soporte para Web si se quiere generar una versión Web.
2. En Unity Hub, añadir la carpeta `SpaceFarm` de este repositorio como proyecto.
3. Abrir `Assets/Scenes/SpaceFarm.unity` y pulsar **Play**.

## Un Marte distinto en cada partida

Cada partida genera una región de **80×50 sectores** a partir de una **semilla** entera. La misma semilla repite exactamente el mismo terreno, cráteres, yacimientos y puntos de interés; otra semilla los cambia. La semilla se ve en el título del minimapa y en el menú de pausa.

- **Relieve:** llanuras, campos de dunas, zonas rocosas, un cráter grande, varios medianos y pequeños, crestas y un cauce seco. Todo el relieve es visual: el rover puede cruzar cualquier sector, así que ninguna semilla deja un recurso inaccesible.
- **Base:** aterriza cerca del centro, en el terreno más despejado de esa zona. A 5 sectores a la redonda no hay cráteres, yacimientos ni puntos de interés, de modo que las 12 posiciones de barrera siempre quedan libres.
- **Yacimientos:** de 6 a 10 de hierro (`Fe-1`, `Fe-2`…) y de 4 a 6 de hielo (`Hielo-1`…), numerados del más cercano al más lejano, más 4 a 6 puntos de interés con una recompensa única. Siempre hay un hierro a 9 sectores o menos y un hielo a 10 o menos; el resto se reparte por todo el mapa. Nada aparece hasta que el rover revela su sector.
- **Varias instalaciones:** cada yacimiento estudiado admite su propia mina o extractor, con su nivel, avería y almacén. Las reglas que evitan que eso vuelva trivial la partida:
  - Cada mina necesita un operador: 2 colonos operan 1 mina, 3 colonos 2 y 4 colonos 3 (máximo).
  - Cada mina nueva cuesta 4 Fe más (8, 12, 16) y consume su propia energía cada sol.
  - Máximo 2 extractores; el segundo cuesta 10 Fe y el agua nunca supera 30.
  - La primera muestra de cada recurso trae 2 unidades; las siguientes, 1.
  - Incursión del sol 5: con menos de 4 barreras se averían todas las minas; con 4 a 7, solo la más lejana; desde 8 quedan protegidas.

## Controles

- **Cámara:** arrastrar con cualquier botón del mouse, WASD o flechas. Rueda: zoom. Un arrastre nunca se convierte en orden. **Volver a la base** e **Ir al rover** reubican la vista; **Seguir** acompaña al rover hasta que movés la cámara.
- **Explorar:** clic en el terreno envía el rover. Clic en una señal (`SEÑAL Fe-2`) lo manda a estudiarla.
- **Fichas:** clic en un yacimiento estudiado o una instalación abre su ficha. Desde ahí se construye la mina o el extractor, se mejora o repara y se ordena **Recoger** en esa mina concreta: el rover carga, vuelve al hábitat y descarga.
- **Barreras:** **Construir barrera** ilumina 12 posiciones alrededor del núcleo; cada clic válido cuesta 1 Fe. **Terminar** o Escape salen del modo.
- **Minimapa:** muestra terreno explorado, niebla, base, rover, yacimientos y puntos ya descubiertos y el recuadro de la cámara. Clic en zona explorada: centra la cámara sin mover al rover. `+`/`-` cambia el tamaño y `<`/`>` lo pasa de esquina; la preferencia se guarda.
- **Pausa:** Escape o el botón **Pausa**. Detiene rover, astronautas y simulación. Ofrece reanudar, reiniciar este mapa (misma semilla), empezar un mapa nuevo (otra semilla), ver controles y elegir el lado del minimapa.
- La tormenta ocurre en los soles 3 y 4 y la incursión en el sol 5. La primera victoria requiere llegar al sol 7 con al menos una mina, invernadero, tres colonos y 8 barreras.

La simulación es deliberadamente simplificada. La planta de oxígeno es una extrapolación ficticia inspirada en MOXIE; las criaturas son ciencia ficción.

## Estructura

- `SpaceFarmMap.cs`: generador determinista (semilla → terreno, cráteres, base, yacimientos, puntos) y sus reglas de validación. No usa `UnityEngine.Random`.
- `SpaceFarmTerrainPainter.cs`: pinta el mapa en una sola textura con relieve y da los colores del minimapa.
- `SpaceFarmState.cs`: reglas de recursos, yacimientos, edificios y eventos.
- `SpaceFarmGame.cs`: ciclo de la partida, cámara, entrada y viajes del rover.
- `SpaceFarmGameWorld.cs`: terreno, yacimientos e instalaciones, barreras, paneles y astronautas.
- `SpaceFarmGameFog.cs`: niebla de guerra, que solo recalcula la zona que cambia.
- `SpaceFarmGameHud.cs`: recursos, acciones, navegación y fichas contextuales.
- `SpaceFarmGameMinimap.cs` y `SpaceFarmMinimapInput.cs`: minimapa.
- `SpaceFarmGamePause.cs`: menú de pausa y reinicios.
- `Assets/Editor/SpaceFarmChecks.cs`: pruebas de reglas y de generación.
- `Assets/Editor/SpaceFarmPlayChecks.cs`: prueba en Play Mode del flujo real y capturas.

Todos los scripts de juego están en `SpaceFarm/Assets/Scripts/`.

## Pruebas

- **Space Farm → Comprobar reglas básicas:** reglas de la colonia, varias minas independientes y generación (semillas 1, 42 y 12345 más un barrido de 500 semillas).
- **Space Farm → Probar partida en Play Mode:** juega la escena real (descubrir, construir, producir, recoger, pausar en pleno viaje, minimapa, reinicios) y guarda capturas en `docs/screenshots/`.

Desde la línea de comandos:

```
Unity -batchmode -projectPath SpaceFarm -executeMethod SpaceFarm.Editor.SpaceFarmChecks.Run -quit
Unity -projectPath SpaceFarm -executeMethod SpaceFarm.Editor.SpaceFarmPlayChecks.Run -sfExit
```

El arte de los edificios es provisional y se puede sustituir sin cambiar las reglas. Si Unity importa las imágenes como texturas normales en otra máquina, usar **Space Farm → Preparar arte 2D** dentro del editor.

Los directorios `Library`, `Temp`, `Logs` y `UserSettings` del editor se excluyen de Git.
