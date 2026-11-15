# Space Farm

Prototipo de un juego de estrategia y supervivencia educativa en Marte. El concepto completo está en [SPACE_FARM_CONCEPTO.md](SPACE_FARM_CONCEPTO.md).

![Marte generado con la semilla 1](docs/screenshots/mapa-semilla-1.png)

## Abrir el proyecto

1. Instalar **Unity 6.6.9f1** con soporte para Web si se quiere generar una versión Web.
2. En Unity Hub, añadir la carpeta `SpaceFarm` de este repositorio como proyecto.
3. Abrir `Assets/Scenes/SpaceFarm.unity` y pulsar **Play**.

Al pulsar **Iniciar expedición**, la nave baja hasta la zona de la base durante unos cinco segundos. Los controles quedan bloqueados hasta que aparecen las instalaciones iniciales. La secuencia vuelve a reproducirse al reiniciar.

## Un Marte distinto en cada partida

Cada partida genera una región de **128×80 sectores** a partir de una **semilla** entera. La misma semilla repite exactamente el mismo terreno, cráteres, yacimientos y puntos de interés; otra semilla los cambia. La semilla se ve en el título del minimapa y en el menú de pausa.

- **Relieve:** llanuras, campos de dunas, zonas rocosas, un cráter grande, varios medianos y pequeños, crestas y un cauce seco. Todo el relieve es visual: el rover puede cruzar cualquier sector, así que ninguna semilla deja un recurso inaccesible.
- **Base:** aterriza cerca del centro, en el terreno más despejado de esa zona. A 5 sectores a la redonda no hay cráteres, yacimientos ni puntos de interés.
- **Yacimientos:** de 7 a 10 de hierro (`Fe-1`, `Fe-2`…) y de 5 a 7 de hielo (`Hielo-1`…), numerados del más cercano al más lejano, más 4 a 6 puntos de interés. Hay al menos un depósito de cada recurso en cada una de las cinco zonas de progreso. La misma semilla reproduce sus posiciones.
- **Niveles y exploración:** el nivel de la colonia sube con XP al estudiar depósitos, descubrir puntos, construir, mejorar y sobrevivir soles. Los niveles 1–4 permiten al rover llegar hasta 11, 24, 39 y 56 sectores desde la base; el nivel 5 abre todo el mapa. La niebla también respeta ese límite. Cada nivel suma dos astronautas y dos plazas al hábitat.
- **Varias instalaciones:** cada yacimiento estudiado admite su propia mina o extractor, con su nivel y almacén. Las reglas que evitan que eso vuelva trivial la partida:
  - La capacidad de minas crece con la tripulación, hasta tres; cada mina produce 5 Fe por sol en nivel 1 u 8 en nivel 2 y guarda hasta 18/36 Fe. El rover carga 12 Fe por viaje, o 24 mejorado.
  - Cada mina nueva cuesta 4 Fe más (8, 12, 16) y consume su propia energía cada sol.
  - Máximo 5 extractores; cada uno nuevo cuesta 4 Fe más. Agua, O2 y comida se almacenan hasta 60.
  - El almacén inicial guarda 50 Fe. Cada almacén adicional cuesta 12 Fe y suma 50 espacios, hasta cinco almacenes. El hierro no entra en la reserva si no hay espacio; las barras muestran el inventario y su capacidad.
  - La primera muestra de cada recurso trae 2 unidades; las siguientes, 1.
  - Incursión del sol 5: los muros reducen la pérdida de comida. Las minas nunca se averían.

## Controles

- **Cámara:** arrastrar el mapa con el mouse, WASD o flechas. Rueda: zoom. Un arrastre nunca se convierte en orden. **Volver a la base** e **Ir al rover** reubican la vista; **Seguir** acompaña al rover hasta que movés la cámara.
- **Explorar:** clic en el terreno envía el rover por un camino libre dentro del alcance del nivel actual. No cruza muros ni instalaciones. Clic en una señal (`SEÑAL Fe-2`) lo manda a estudiarla.
- **Construcción:** el botón **Construir** abre un catálogo con miniaturas, función, costo y requisitos. Elegí un plano y movelo por el mapa: cian indica posición válida y rojo explica el bloqueo. Un clic sobre un sector válido confirma; Escape, clic derecho o **Cancelar** abandonan el plano sin gastar hierro. También podés arrastrar una tarjeta del catálogo y soltarla sobre el mapa. Hasta cinco invernaderos y cinco plantas de O2 pueden ocupar celdas reveladas y libres; mina y extractor deben ir exactamente sobre el yacimiento estudiado que elijas.
- **Fichas:** clic en un yacimiento estudiado o una instalación abre su ficha. Desde ahí podés iniciar la colocación de una mina o extractor, mejorar y ordenar **Recoger** en una mina concreta: el rover carga el hierro, busca un espacio libre junto a un almacén y lo descarga allí. Si el almacén está lleno, podés construir otro y ordenar al rover que entregue la carga pendiente.
- **Muros:** elegí **Construir muros** en el catálogo y colocalos libremente en celdas reveladas. No hay un máximo fijo. Cada uno cuesta 1 Fe y se une automáticamente con los muros de los cuatro sectores vecinos para formar tramos y esquinas. No se pueden poner sobre instalaciones, el rover u otros muros. Los astronautas no bloquean la construcción. Los muros próximos al hábitat reducen la pérdida de comida durante la incursión.
- **Tripulación:** el botón **Astronautas** abre un panel propio. Elegí una persona y después una instalación construida: mina, extractor, invernadero, planta de O2 o paneles solares. Un trabajador despierto aporta **+3 unidades por sol** a las primeras cuatro instalaciones o **+5 energía por sol** a los paneles. El panel muestra su tarea, energía y descanso; **Dejar tarea** lo libera. Cada instalación admite un astronauta. Quienes se cansan vuelven al dormitorio y descansan automáticamente; durante el descanso no dan bonificación. El movimiento de los astronautas evita edificios, muros, rover y compañeros. Los ciclos de trabajo y descanso duran más para que el cambio de actividad sea visible. Más astronautas consumen más O2, agua y comida por sol.
- **Economía inicial:** la nave trae 36 Fe, 20 energía y 20 de cada recurso vital. Los paneles producen 14 energía por sol; cada planta de O2 y cada invernadero producen 7, y cada extractor 8 de agua. Las tormentas aún reducen la generación solar y el crecimiento de la población exige construir más instalaciones.
- **Mejoras:** el primer nivel tarda 30 segundos reales. Cada nivel posterior duplica el tiempo y suma 2 Fe al costo anterior; la pausa detiene el contador y las mejoras en curso no se cobran dos veces. Al completarse, el cambio de nivel y su arte aparecen en ese mismo instante. Los paneles mejorados generan más energía y amplían la batería.
- **Minimapa:** muestra terreno explorado, niebla, base, rover, yacimientos y puntos ya descubiertos y el recuadro de la cámara. Clic en zona explorada: centra la cámara sin mover al rover. `+`/`-` cambia el tamaño y `<`/`>` lo pasa de esquina; la preferencia se guarda.
- **Pausa:** Escape o el botón **Pausa**. Detiene rover, astronautas y simulación. Ofrece reanudar, reiniciar este mapa (misma semilla), empezar un mapa nuevo (otra semilla), ver controles y elegir el lado del minimapa.
- **Pedido a la Tierra:** cuesta 4 Fe y llega tras dos soles con +5 agua y +5 comida. Solo puede haber un pedido pendiente; su llegada se muestra con una nave que aterriza, entrega los suministros y despega. Pausar detiene la animación, y reiniciar elimina la nave de la partida anterior.
- Las tormentas se determinan al avanzar el sol: pueden ocurrir desde el sol 3, su probabilidad aumenta con el nivel de colonia y el paso del tiempo, y como mínimo hay un sol de respiro entre tormentas. Reducen la generación solar; cada tormenta también tiene una probabilidad de averiar una instalación elegida al azar (paneles, dormitorio, invernadero, rover o una mina). Solo puede haber una avería activa a la vez. Repararla cuesta hierro y energía, y devuelve la producción o el movimiento afectados. Las minas ya no se averían solas con el paso del tiempo. La incursión alienígena sigue ocurriendo en el sol 5. La primera victoria requiere llegar al sol 7 con al menos una mina, un invernadero y la colonia en nivel 3.

La simulación es deliberadamente simplificada. La planta de oxígeno es una extrapolación ficticia inspirada en MOXIE; las criaturas son ciencia ficción.

## Estructura

- `SpaceFarmMap.cs`: generador determinista (semilla → terreno, cráteres, base, yacimientos, puntos) y sus reglas de validación. No usa `UnityEngine.Random`.
- `SpaceFarmTerrainPainter.cs`: pinta el mapa en una sola textura con relieve y da los colores del minimapa.
- `SpaceFarmState.cs`: reglas de recursos, yacimientos, posiciones elegidas para edificios, validación de ubicación y eventos.
- `SpaceFarmGame.cs`: ciclo de la partida, cámara, entrada y viajes del rover.
- `SpaceFarmGameConstruction.cs` y `SpaceFarmBuildCardDrag.cs`: plano visual, modos de interacción, confirmación y arrastre desde el catálogo.
- `SpaceFarmGameWorld.cs`: terreno, yacimientos e instalaciones, barreras, paneles y astronautas.
- `SpaceFarmGameFog.cs`: niebla de guerra, que solo recalcula la zona que cambia.
- `SpaceFarmGameHud.cs`: recursos, acciones, navegación y fichas contextuales.
- `SpaceFarmGameCrew.cs`: panel de tripulación y asignación individual a instalaciones.
- `SpaceFarmGameMinimap.cs` y `SpaceFarmMinimapInput.cs`: minimapa.
- `SpaceFarmGamePause.cs`: menú de pausa y reinicios.
- `Assets/Editor/SpaceFarmChecks.cs`: pruebas de reglas y de generación.
- `Assets/Editor/SpaceFarmPlayChecks.cs`: prueba en Play Mode del flujo real y capturas.

Todos los scripts de juego están en `SpaceFarm/Assets/Scripts/`.

## Pruebas

- **Space Farm → Comprobar reglas básicas:** recursos garantizados en cinco zonas para 100 semillas, niveles, alcance, descanso, producción y mejoras de 30 segundos.
- **Space Farm → Probar partida en Play Mode:** juega la escena real (descubrir, construir, producir, recoger, pausar en pleno viaje, minimapa, reinicios) y guarda capturas en `docs/screenshots/`.

Desde la línea de comandos:

```
Unity -batchmode -projectPath SpaceFarm -executeMethod SpaceFarm.Editor.SpaceFarmChecks.Run -quit
Unity -projectPath SpaceFarm -executeMethod SpaceFarm.Editor.SpaceFarmPlayChecks.Run -sfExit
```

El arte de los edificios es provisional y se puede sustituir sin cambiar las reglas. Si Unity importa las imágenes como texturas normales en otra máquina, usar **Space Farm → Preparar arte 2D** dentro del editor.

Los directorios `Library`, `Temp`, `Logs` y `UserSettings` del editor se excluyen de Git.
