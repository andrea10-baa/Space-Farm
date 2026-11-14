# Prompt de continuidad para el equipo de Space Farm

Repositorio oficial: <https://github.com/andrea10-baa/Space-Farm>

Copiá el texto siguiente en el Codex de cada integrante. Su primera tarea será actualizar la carpeta local que ya usa para Space Farm y leer el contexto. Todavía no le estamos pidiendo que desarrolle el juego.

---

Actuá como colaborador de desarrollo del equipo **Space Farm**. El repositorio oficial es <https://github.com/andrea10-baa/Space-Farm>. **Primero actualizá la carpeta local de Space Farm que tengo abierta con el contenido más reciente de ese repositorio.** Comprobá la ruta, la rama, el remoto y `git status` antes de sincronizar; conservá cualquier cambio local y no sobrescribás archivos. Si esa carpeta todavía no está vinculada al repositorio, integrá los archivos de forma segura o explicame qué dato necesitás para hacerlo. Después leé `README.md`, `AGENTS.md`, `SPACE_FARM_CONCEPTO.md`, el proyecto de Unity si existe y los últimos commits. El estado actual del repositorio es la fuente de verdad; este mensaje aporta contexto de las conversaciones anteriores.

## Contexto del desafío

Estamos creando un juego educativo para el desafío de construir un simulador de misión para jóvenes astronautas. El jugador debe gestionar un puesto avanzado en Marte y experimentar decisiones reales de ingeniería: soporte vital, energía, producción de alimentos, agua y protección contra radiación. Queremos que esas decisiones sean fáciles de entender y divertidas, aunque el ritmo y algunas amenazas sean ficticios.

## Visión del juego

El juego se llama **Space Farm**. Empieza con la llegada de una nave a Marte, una pequeña tripulación, suministros limitados y **un solo rover**. Se ve la base desde arriba, con construcción y expansión al estilo de un juego de estrategia como *Clash of Clans*. Esa referencia describe la vista y la progresión; no hemos decidido incluir combate entre jugadores.

La primera instalación esencial es una planta que produce oxígeno a partir del CO₂ marciano, **inspirada en MOXIE de la NASA**. MOXIE fue una demostración experimental pequeña; la planta del juego es una versión futura ficticia capaz de sostener una colonia. Requiere energía y mantenimiento. La base también debe conseguir agua, generar y almacenar energía, cultivar alimentos, proteger a la tripulación y gestionar el riesgo de radiación.

El bucle principal es: **explorar → descubrir un yacimiento → extraer → procesar recursos → construir o mejorar → sostener a más colonos → afrontar eventos → expandirse**. El mapa comienza parcialmente oculto. El rover explora y revela zonas del minimapa; puede recoger cantidades pequeñas de minerales. Tras localizar y estudiar un yacimiento de hierro, el jugador puede construir allí una mina fija, que produce más que el rover. Las minas son mejorables por niveles; se propusieron niveles 1 a 4, todavía sujetos al balance. Los yacimientos pueden agotarse y las minas requieren energía y personal. El mineral se refina antes de convertirse en material de construcción.

El rover inicial también se puede mejorar: mayor radio de exploración, capacidad de carga, autonomía y resistencia. Se propusieron tres niveles, pero sus valores concretos siguen abiertos. Más adelante se desbloquea un segundo rover mediante progreso y un garaje o taller; un tercero sería contenido futuro. Cada rover adicional consume recursos y energía. La pérdida o avería de un rover debe ser recuperable para evitar que la partida quede bloqueada.

La población solo puede crecer si hay camas, alimento, agua y **oxígeno sostenido con margen de seguridad**. Cada nuevo colono aporta trabajo, pero consume recursos y necesita protección. Por ejemplo, si una planta produce 4 unidades de O₂ por ciclo y cada persona consume 1, no conviene admitir cuatro colonos sin reserva: habría que construir otra planta o mejorar la existente. Esos números son ilustrativos y se ajustarán durante el prototipo.

El hierro obtenido en Marte sirve para construir barreras, edificios y mejoras. Algunos materiales o suministros especializados —semillas, nutrientes, filtros, repuestos o sustrato agrícola— pueden pedirse a la Tierra mediante una economía simplificada: pagar con recursos refinados o créditos logísticos y esperar la llegada de una nave. No afirmemos que todos los materiales terrestres existen o son accesibles en Marte. Hierro, hielo y silicio pueden tener base científica; cobre u oro, si aparecen, deben tratarse con cautela como decisiones de diseño y no como hechos confirmados para el lugar de juego.

## Dificultad y riesgo

La colonia puede perder recursos, producción, edificios o colonos si el jugador descuida la gestión. Hay tormentas de polvo que reducen la energía solar, averías de filtros o generadores, exposición a radiación, daños a cultivos y escasez de agua u oxígeno. También queremos **criaturas alienígenas** que ataquen el perímetro, roben suministros o dañen instalaciones. Son un elemento explícito de ciencia ficción: no existe evidencia confirmada de vida extraterrestre en Marte. El jugador puede construir barreras de hierro, sensores, compuertas y defensas sencillas. Los peligros deben anunciarse con tiempo suficiente para preparar una respuesta; las pérdidas deben ser importantes, pero recuperables.

## Primer prototipo jugable

Priorizar una demostración de unos **8 a 12 minutos** con un mapa limitado y parcialmente oculto, llegada de la nave, un rover inicial mejorable, descubrimiento de hierro, una mina con al menos una mejora, producción de oxígeno, energía, agua y alimento, incorporación de nuevos colonos, una cosecha, un pedido a la Tierra, una tormenta y un ataque de criaturas. El segundo rover puede desbloquearse dentro de esta demostración si el alcance lo permite. Debe sentirse como un ciclo completo con decisiones y consecuencias, no como una lista de sistemas sin terminar.

Tras cada evento o ciclo, la interfaz debe explicar de forma breve qué cambió y por qué: por ejemplo, cuánto O₂ se produjo y consumió, qué redujo la energía y cómo se afectó la seguridad de la colonia. Los valores del juego son simplificaciones educativas, no una simulación científica exacta.

## Forma de colaborar cuando empecemos a desarrollar

- Confirmá la tarea concreta que te dé más adelante y trabajá en una porción revisable del juego.
- Usá la versión de Unity definida en el proyecto. El plugin oficial de Unity para Codex está instalado en al menos una máquina, pero verificá el editor, el plugin y las herramientas disponibles en tu propio entorno.
- Seguí las instrucciones del repositorio y actualizá `SPACE_FARM_CONCEPTO.md` cuando una decisión de diseño cambie.
- No sobrescribás trabajo de compañeros. Al terminar, indicá archivos modificados, cómo probaste el cambio, decisiones tomadas y cualquier bloqueo real.
- Si una propuesta contradice el concepto o una decisión del equipo, explicá el conflicto y proponé una solución breve antes de implementarla.

**Tu tarea en esta sesión termina cuando la carpeta local esté actualizada y hayas leído el contexto.** Respondeme con la ruta de la carpeta, la rama y el último commit, más un resumen breve de lo que entendiste de Space Farm. Esperá mi siguiente instrucción antes de empezar a programar o cambiar el diseño.
