# Space Farm — concepto de juego

**Versión:** 0.4 · **Estado:** propuesta para discutir con el equipo · **Género:** exploración, construcción, supervivencia y defensa de una base marciana · **Plataforma inicial propuesta:** PC y WebGL con Unity.

## La idea en una frase

El jugador dirige una pequeña misión humana ficticia en Marte: construye una base vista desde arriba, extrae y procesa recursos, cultiva alimentos, recibe nuevos colonos y defiende una colonia cada vez más difícil de sostener.

## Promesa al jugador

«Llegaste con lo indispensable. Cada edificio y cada nuevo colono te ayudará a crecer, pero aumentará el consumo y el territorio que debes proteger. Convierte un campamento frágil en una granja marciana capaz de resistir crisis».

El atractivo visual y de progresión se parece al de un constructor de bases como *Clash of Clans*: mapa de arriba, edificios que se colocan y mejoran, zonas que se desbloquean y una base que crece a la vista. La partida combina exploración, ingeniería, agricultura y defensa. Las amenazas incluyen condiciones reales de Marte, fallos de sistemas y criaturas alienígenas ficticias.

## Público y objetivo educativo

La primera versión está pensada para jóvenes de aproximadamente 12 a 17 años; la edad se puede ajustar con el equipo. Al terminar una partida, el estudiante debería poder explicar al menos dos relaciones entre sistemas, por ejemplo: producir oxígeno consume energía; cultivar comida requiere agua, nutrientes y luz; extraer agua del hielo exige equipo y energía; una tormenta de polvo reduce la generación solar.

La experiencia debe enseñar mediante consecuencias visibles de las decisiones, con explicaciones breves y opción de volver a intentar.

## Inicio de la historia

Una nave de descenso deja en Marte a una pequeña tripulación, **un único rover** y contenedores de carga. La nave trae reservas para pocos *soles* (días marcianos), baterías y un módulo experimental de producción de oxígeno. El campamento es temporal y no puede vivir indefinidamente de suministros terrestres.

La primera misión jugable es instalar y encender un **generador de oxígeno inspirado en MOXIE**. En el universo del juego es una versión futura, ampliada y capaz de alimentar la base; no es el pequeño aparato que viajó en el rover Perseverance. Funciona tomando CO₂ de la atmósfera y gastando electricidad. El tutorial enseña enseguida su primera disyuntiva: si la energía baja demasiado, la producción de oxígeno también cae.

### Primeros minutos

1. Aterrizaje: aparece la nave, se despliega el hábitat y se muestran reservas limitadas.
2. El jugador coloca el generador de oxígeno y lo conecta a la batería inicial.
3. Construye paneles solares para sostenerlo sin agotar la batería.
4. Envía el rover hacia una zona oculta del minimapa. Descubre un yacimiento de hierro y trae una pequeña muestra.
5. Construye la primera mina sobre ese yacimiento para obtener más mineral por sol.
6. Decide qué construir después: extractor de hielo, protección del hábitat o invernadero. Una alerta explica el costo de posponer cualquiera de los tres.

## Ciclo principal de juego

**Explorar → extraer → procesar → construir → asignar recursos y defensas → avanzar un sol → resolver consecuencias → reparar y mejorar la base.**

Cada sol tiene una fase de planificación. El jugador coloca o mejora edificios, define prioridades de consumo y prepara defensas. Al avanzar el tiempo, el juego calcula producción, reservas, población, daño a edificios, salud de la tripulación y crecimiento de los cultivos. Después presenta un informe corto: «El invernadero avanzó 15 %, pero el oxígeno bajó porque las luces usaron la energía de reserva». Esos porcentajes son parámetros del juego, no mediciones de NASA.

El tiempo puede avanzar rápido y pausarse. Los pedidos terrestres y las construcciones tardan algunos turnos para crear decisiones, sin obligar a esperar horas reales.

## Sistemas conectados

| Sistema | Produce o aporta | Necesita | Decisión interesante |
|---|---|---|---|
| Generador tipo MOXIE | Oxígeno | CO₂ atmosférico, electricidad, mantenimiento | Priorizar oxígeno frente a otras máquinas durante una caída de energía. |
| Paneles y baterías | Electricidad y reserva nocturna | Espacio, materiales, mantenimiento | Ampliar generación o gastar metales en el invernadero. |
| Extractor de hielo y purificador | Agua utilizable | Yacimiento, energía, filtros | Usar agua en consumo humano o en cultivos. |
| Reciclador | Recupera parte del agua usada | Energía, piezas | Invertir temprano para depender menos de envíos. |
| Refinería | Materiales de construcción a partir de roca | Regolito, energía, tiempo | Refinar hoy o guardar energía para soporte vital. |
| Rover explorador | Revela el mapa, escanea y recoge muestras pequeñas | Batería, tiempo, reparaciones; otro rover requiere taller y materiales | Mejorar uno aumenta su rendimiento; fabricar otro permite dos misiones simultáneas. |
| Mina sobre yacimiento | Mineral de hierro en mayor cantidad | Yacimiento confirmado, energía y acceso desde la base | Mejorar la mina aumenta la producción, pero gasta recursos y agota antes el depósito. |
| Invernadero hidropónico | Alimentos tras varios soles | Agua, nutrientes, luz, energía, espacio | Sembrar pronto cuesta recursos antes de entregar cosecha. |
| Refugio protegido | Menor exposición de la tripulación | Materiales y tiempo de construcción | Proteger la tripulación o acelerar la expansión productiva. |
| Hábitat ampliado | Camas y capacidad de población | Metal, oxígeno, agua y comida suficientes | Aceptar nuevos colonos acelera trabajos, pero eleva todos los consumos. |
| Barreras y sensores | Tiempo de respuesta y protección frente a incursiones | Hierro refinado, reparación; sensores consumen energía | Gastar hierro en una cerca o en otro generador de oxígeno. |

Las cifras iniciales deben ser sencillas y calibrarse jugando. La regla importante es que ningún edificio sea una solución gratuita: todos tienen entradas, salidas y mantenimiento comprensibles.

## Recursos y economía

Para que el juego se entienda desde el primer minuto, habría **seis indicadores principales**: energía, oxígeno, agua, comida, población y salud de la tripulación. Los materiales de construcción —regolito, metal refinado y silicio— se muestran al abrir el menú de construcción. La exposición a radiación aparece como riesgo del hábitat y en las salidas al exterior. Cada edificio tiene un estado de integridad y puede quedar dañado o fuera de servicio.

El mapa comienza parcialmente oculto. El hierro y el silicio son buenos recursos iniciales porque se han detectado en material marciano. El hielo es estratégico y está distribuido de forma desigual. Cobre, oro u otros minerales pueden aparecer como hallazgos poco comunes **del universo ficticio del juego**, sin afirmar que exista un depósito explotable junto a la base ni que Marte tenga todos los recursos de la Tierra.

El flujo del hierro tiene dos pasos claros: **yacimiento → mineral de hierro extraído → refinería → hierro utilizable para construcción o intercambio**. Así encontrar un recurso no equivale a recibir materiales listos al instante.

## Exploración, minimapa y minas

El rover es los ojos de la colonia. El jugador marca un destino en el minimapa y el rover avanza mientras su batería lo permite. Su radio de exploración revela terreno antes oculto: zonas libres para construir, rutas más seguras, hielo y puntos con minerales. Cuando detecta un posible depósito, debe escanearlo para saber qué contiene y cuánta reserva aproximada tiene. El minimapa conserva los lugares descubiertos, marca los nodos con texto e icono y deja visible qué zonas siguen sin explorar.

El rover puede recoger **una muestra o una pequeña carga** y traerla a la base. Eso permite investigar el depósito o resolver una emergencia, pero es lento. La nave inicial trae un pequeño equipo de refinado y metal suficiente para construir la primera mina; así el jugador no queda atrapado antes de tener producción local. Una excavadora móvil podría ser una mejora posterior para recolección intermedia. La **mina fija** solo se construye sobre un yacimiento de hierro ya confirmado y conectado al área operativa de la colonia. Produce cada sol, consume energía y deja de producir si se daña, se corta su conexión o se agota el depósito.

### Flota y mejoras de los rovers

La misión comienza con **un rover**. En esa etapa debe elegir entre explorar terreno nuevo, escanear un depósito, recoger una pequeña carga o regresar a recargar. Dejarlo lejos de la base durante una tormenta o con poca batería retrasa la exploración. Si queda averiado, puede recuperarse mediante una operación de rescate que consume tiempo y materiales; el jugador nunca queda bloqueado permanentemente por perder su único rover.

El rover tiene mejoras de **escáner**, **carga**, **batería** y **resistencia**. Para que el jugador perciba el avance sin manejar demasiados módulos, la propuesta inicial agrupa esas mejoras en niveles. El número final de niveles se decidirá al probar el juego; tres son una referencia, no una regla cerrada:

| Nivel de un rover | Radio que revela en el minimapa | Carga de mineral por viaje | Alcance total de batería | Costo y utilidad |
|---|---:|---:|---:|---|
| Nivel 1, inicial | 2 casillas | 2 unidades | 6 casillas | Explora cerca de la base y trae muestras. |
| Nivel 2 | 3 casillas | 4 unidades | 8 casillas | Requiere metal y piezas; descubre más terreno y tarda menos viajes. |
| Nivel 3 | 4 casillas | 6 unidades | 11 casillas | Requiere piezas avanzadas; alcanza yacimientos lejanos. |

Estas cifras son **parámetros provisionales del juego**. El radio de escaneo determina cuánto terreno se descubre alrededor del rover; el alcance de batería determina si puede llegar y volver. La carga determina cuánto mineral puede traer sin una mina. Las mejoras también pueden darle resistencia frente a tormentas o ataques, pero no deben volver innecesarias las minas fijas.

Un **segundo rover** se desbloquea al mejorar la base y construir un pequeño taller o garaje con punto de recarga; un tercero quedaría para una colonia más avanzada. Cada rover adicional cuesta hierro refinado, piezas y energía de carga. Su ventaja principal es la **simultaneidad**: uno puede explorar mientras otro trae mineral, repara una instalación remota o patrulla. La mejora del rover existente y la fabricación de otro compiten por los mismos recursos, así que ninguna elección es automáticamente mejor.

**Ejemplo:** aparece una alerta de hielo al norte y una mina de hierro queda expuesta al sur. Con un solo rover, el jugador elige qué atender primero. Con dos puede explorar y proteger la mina a la vez, pero debe mantener dos baterías y disponer de repuestos para ambos.

### Niveles de la mina de hierro

Las siguientes cifras son **unidades provisionales de juego**, no tasas científicas:

| Nivel | Extracción aproximada | Energía por sol | Requisito adicional | Elección que crea |
|---|---:|---:|---|---|
| Rover inicial, sin mina | Hasta 2 por viaje corto | Recarga de batería | Tiempo de exploración | Obtener algo ahora o seguir buscando. |
| Mina 1 | 3 | 1 | Yacimiento escaneado | Producción constante con inversión inicial. |
| Mina 2 | 5 | 2 | Hierro refinado para mejorarla | Más hierro o reservarlo para defensas. |
| Mina 3 | 8 | 3 | Un operador disponible | Más producción a cambio de trabajo y consumo de la colonia. |
| Mina 4 | 12 | 5 | Dos operadores y piezas avanzadas | Alto rendimiento y mayor dependencia de energía y defensa. |

Las mejoras aumentan la **velocidad** de extracción, no crean mineral nuevo. Cada yacimiento tiene reservas limitadas y visibles tras el escaneo. Una mina de nivel alto vacía antes el depósito, así que el jugador debe explorar otros puntos, trasladar maquinaria o decidir si conviene mejorar. Al agotarse un nodo, se puede desmontar la mina y recuperar parte de sus materiales; la pérdida obliga a cambiar de plan sin volver inútil toda la inversión.

Una mina alejada de la base abre otra tensión: necesita conexión eléctrica y queda fuera del perímetro protegido. El jugador puede tender un cable, construir una pequeña defensa o aceptar el riesgo de que una incursión corte la producción. El rover también puede quedar averiado y requerir reparación, por lo que explorar sin mirar su batería tiene consecuencias.

Los materiales cumplen dos funciones:

- **Construcción local:** el metal refinado sirve para módulos, tuberías y mejoras.
- **Intercambio de misión:** completar cuotas de exploración o reservar materiales valiosos genera *créditos logísticos* para pedir suministros a la Tierra. Es una regla económica ficticia: el juego no necesita representar que se envían toneladas de mineral físicamente de Marte a la Tierra.

El jugador abre un **terminal de suministros terrestres** y solicita semillas, nutrientes, filtros, repuestos o sustrato agrícola. También puede pedir «tierra» como nombre familiar en la interfaz, explicando que se trata de un sustrato preparado para el cultivo. Cada pedido cuesta créditos, ocupa capacidad limitada de carga y tarda turnos en llegar. Para una partida corta, esos plazos estarán acelerados y el juego lo indicará como simplificación.

Esto crea una decisión: gastar metal en una nueva máquina, o comprometerlo en un contrato para obtener los nutrientes que permitirán la siguiente cosecha.

## La granja como centro del juego

El nombre *Space Farm* debe sentirse en la jugabilidad. El objetivo no es solamente construir edificios; es conseguir la **primera cosecha** y luego mantener ciclos agrícolas estables.

La agricultura empieza con un pequeño módulo hidropónico, semillas y nutrientes llevados desde la Tierra. El jugador decide qué sembrar según tres atributos fáciles de entender: tiempo hasta cosecha, consumo de agua y rendimiento de comida. Más adelante puede reciclar nutrientes, ampliar bandejas de cultivo y experimentar con sustrato tratado de origen local. El regolito sin tratar no se presenta como tierra fértil.

Ejemplo de decisión: una lechuga de crecimiento rápido ayuda pronto, pero aporta menos alimento total; un cultivo de mayor rendimiento tarda más y consume reservas durante varios soles. Los cultivos concretos y sus números se elegirán al balancear el prototipo.

## Crecimiento de la población

La población crece cuando el jugador solicita una nueva tripulación a la Tierra y demuestra que la base puede sostenerla. Para autorizar su llegada debe haber **camas libres**, producción sostenida de **oxígeno con margen de seguridad**, agua y comida para varios soles, y un área de aterrizaje. La regla de juego propuesta exige mantener esas condiciones durante dos soles antes de aceptar más personas; las cifras se calibrarán en el prototipo.

Cada colono consume oxígeno, agua y alimento por sol y necesita un espacio protegido. A cambio, aumenta la capacidad de trabajar: permite operar otra máquina, reparar más rápido o cuidar más cultivos. La población, por tanto, es a la vez progreso y riesgo. Si una máquina falla, las reservas de emergencia dan al jugador tiempo para reparar o reducir actividad; la base no pierde de forma instantánea por una pequeña variación.

**Ejemplo de balance, con unidades ficticias del juego:** un generador entrega 4 unidades de oxígeno por sol y cada persona consume 1. Con dos personas hay margen; con tres todavía se cumple una reserva del 25 %; una cuarta persona necesita un segundo generador para conservar ese margen. El jugador ve el cálculo antes de confirmar la llegada.

## Defensa y pérdidas

Los **acechadores del subsuelo** son criaturas ficticias del universo *Space Farm*. Sus primeras señales son huellas cerca del hielo y daños menores en cables. Más adelante llegan en pequeñas incursiones, atraídos —según las reglas narrativas del juego— por la actividad de la base. Intentan alcanzar depósitos de agua, cultivos y generadores. No representan un descubrimiento científico de vida en Marte.

El jugador coloca segmentos de barrera de hierro, una puerta reforzada y un sensor. La barrera retrasa a los acechadores; el sensor avisa con un sol de anticipación y consume electricidad; un rover puede patrullar o reparar durante la incursión. Una base grande necesita más perímetro, así que expandir la granja también aumenta el costo defensivo. En la primera versión, los enemigos siguen rutas visibles y anunciadas para que las defensas sean comprensibles.

Un ataque puede **romper un panel**, **abrir una fuga de oxígeno**, **robar comida almacenada**, **destruir una cosecha** o **dejar temporalmente fuera de servicio un edificio**. Reparar cuesta hierro, repuestos y tiempo. Si una zona queda desprotegida, los colonos pueden lesionarse o evacuar, reduciendo la fuerza de trabajo. Las pérdidas deben cambiar el plan del jugador sin borrar de inmediato toda la partida.

La decisión central sería: «Tenemos 12 unidades de hierro. Un nuevo generador permitirá recibir dos colonos; una barrera protegerá el invernadero; mejorar la mina acelerará la extracción; reparar las baterías mejorará la supervivencia durante la tormenta. ¿Qué construimos primero?». Así, población, extracción, defensa y supervivencia compiten por los mismos recursos.

### Reglas de dificultad justa

- El jugador recibe señales antes de un ataque importante: huellas, lectura del sensor o aviso de tormenta.
- La amenaza sube después de que la base tenga una granja funcional; el tutorial no comienza con una derrota inevitable.
- Las reservas y reparaciones permiten recuperarse de errores. Un ataque puede doler sin terminar la partida.
- La dificultad crece por decisiones del jugador: más población y más superficie producen más recursos, pero requieren más energía, suministros y perímetro defensivo.
- El informe de fin de sol explica la causa de cada pérdida y qué protección habría reducido el daño.

## Progresión propuesta

### Capítulo 1: «Respirar»

Encender el generador de oxígeno, estabilizar la energía y sobrevivir los primeros soles. Se aprende que la base es una red de sistemas dependientes.

### Capítulo 2: «Encontrar agua»

Explorar con el rover, descubrir el primer yacimiento de hierro, construir una mina básica y luego localizar hielo para extraer, purificar y reciclar agua. Se introduce la elección entre consumo humano, agricultura y expansión industrial.

### Capítulo 3: «Primera cosecha»

Construir el invernadero, solicitar insumos a la Tierra, mantener las condiciones de cultivo y completar la primera cosecha.

### Capítulo 4: «Crecer»

Ampliar el hábitat, recibir nuevos colonos, levantar barreras, mejorar el rover y construir un taller que permita un segundo vehículo. El primer ataque aparece después de construir el invernadero; una flota mayor e incursiones más complejas pueden quedar para después del hackatón.

## Eventos que generan decisiones

1. **Tormenta de polvo:** cae la producción solar. El jugador pausa la refinería o reduce la iluminación del invernadero para sostener el oxígeno.
2. **Filtro agotado:** la purificación de agua rinde menos. Puede usar un repuesto, fabricarlo o pedirlo a la Tierra.
3. **Alerta de radiación:** se pospone una salida de colonos al exterior o se usa un refugio mejor protegido.
4. **Fallo del generador de oxígeno:** se consume la reserva mientras la reparación exige metal y tiempo.
5. **Cultivo enfermo:** se pierde parte de una cosecha si no se actúa; el juego explica qué condición estaba fuera de rango.
6. **Incursión alienígena ficticia:** los acechadores prueban el perímetro. Una barrera mal ubicada deja expuesto un depósito o un cable de energía.
7. **Fuga después de un ataque:** si el hábitat o una tubería recibe daño, el oxígeno almacenado disminuye hasta que se repare.

Cada evento ofrece varias respuestas, muestra su costo **antes de confirmar** y explica el resultado **después**. La dificultad surge de combinar eventos con las decisiones previas del jugador.

## Victoria, fracaso y aprendizaje

**Victoria de la primera versión:** mantener operativa a la tripulación durante el periodo de misión, recibir al menos un nuevo colono, lograr una cosecha y superar una incursión. Se puede mostrar una evaluación de tres logros: seguridad de la tripulación, producción agrícola y menor dependencia de la Tierra.

**Fracaso recuperable:** si se agota un recurso crítico, la población debe evacuar o la base queda inoperable, se presenta la causa principal y se permite reiniciar desde el inicio del capítulo o intentar otra estrategia. El mensaje final debe decir algo concreto: «Aceptaste nuevos colonos sin suficiente producción de oxígeno; cuando la incursión dañó el generador, la reserva duró solo un sol».

## Experiencia visual e interacción

La cámara muestra la base desde arriba con ligera inclinación o estilo isométrico sencillo. El terreno rojizo se divide en zonas explorables cubiertas inicialmente por una niebla de exploración; cada construcción cambia visualmente la colonia. El minimapa muestra los rovers, las zonas descubiertas, los yacimientos escaneados y el radio de exploración del vehículo seleccionado. Al tocar un rover se ven su tarea, batería y carga; al tocar un edificio o mina aparece una tarjeta con lo que produce, lo que consume, su estado y la reserva restante del depósito. El panel superior mantiene visibles los recursos vitales y sus tendencias: «agua: 4 soles restantes» comunica más que una barra aislada.

El juego debe funcionar con ratón y controles táctiles básicos, mostrar texto junto con colores e iconos, tener botones grandes y permitir pausar. Una breve cápsula «Ciencia detrás de esta decisión» puede aparecer al terminar cada capítulo, sin interrumpir cada acción.

## Alcance recomendado para el hackatón

Una **partida corta completa** es la primera meta. Incluye un mapa fijo con niebla de exploración, **un rover inicial mejorable y el desbloqueo de un segundo**, un yacimiento de hierro escaneable, extracción pequeña con rover, mina de hierro de niveles 1 y 2, edificios esenciales y segmentos de barrera, un colono nuevo, un pedido a la Tierra, una tormenta y una incursión preparada, con primera cosecha al final. Los números se ajustan para que dure unos 8–12 minutos de demostración. Los niveles altos de minas y rovers quedan definidos para la progresión posterior y pueden entrar al prototipo si el ciclo principal ya funciona. Una sola clase de enemigo y una sola oleada bastan para probar el sistema defensivo.

El prototipo puede ser 2D isométrico o 2.5D sencillo en Unity; la sensación de expansión depende más de colocar edificios y ver sus consecuencias que de modelar un planeta entero. Después se pueden añadir generación de mapas, más cultivos, tecnologías, varios biomas y campañas largas.

## Qué es ciencia y qué es ficción

| Elemento | Base científica | Adaptación del juego |
|---|---|---|
| Producción de oxígeno | MOXIE demostró en Perseverance que puede extraerse oxígeno del CO₂ marciano. | El generador de la base es una versión futura ampliada. |
| Minerales | Hay hierro y silicio en muestras estudiadas; encontrar y explotar una concentración útil depende del lugar y de tecnología aún por desarrollar. | El mapa coloca yacimientos minables cerca de la base y permite mejorar minas de forma simplificada. |
| Agua | Se han identificado regiones con hielo bajo la superficie. | El extractor y el purificador operan en pocos turnos. |
| Cultivos | El regolito marciano no es suelo fértil listo para sembrar y puede contener percloratos. | Se empieza con hidroponía y sustrato importado; tratar suelo local es una mejora posterior. |
| Pedidos terrestres | Los viajes a Marte son largos y dependen de oportunidades orbitales. | La entrega llega en pocos turnos por ritmo de juego; se marca como tiempo acelerado. |
| Alienígenas | NASA no ha confirmado vida extraterrestre en Marte. | Los acechadores y sus ataques son ciencia ficción declarada en la historia y en la presentación. |

La presentación del proyecto debe decir que **Space Farm es una simulación educativa inspirada en investigaciones de NASA**, con reglas ficticias y números simplificados. No debe presentarse como un simulador exacto de una misión humana real.

## Fuentes iniciales para el equipo

- NASA/JPL, [resultado final de MOXIE](https://www.jpl.nasa.gov/news/nasas-oxygen-generating-experiment-moxie-completes-mars-mission/).
- NASA, [elementos observados en el suelo marciano](https://science.nasa.gov/photojournal/a-rainbow-of-martian-elements/).
- NASA, [mapa de hielo subsuperficial en Marte](https://www.nasa.gov/solar-system/planets/mars/nasa-is-locating-ice-on-mars-with-this-new-map/).
- NASA, [dificultades de cultivar alimentos en Marte](https://www.nasa.gov/science-research/nasa-plant-researchers-explore-question-of-deep-space-food-crops/).
- NASA, [efecto de las tormentas de polvo en la energía](https://www.nasa.gov/wp-content/uploads/2024/01/mars-surface-power-generation-challenges-and-considerations.pdf).
- NASA, [tiempos de comunicación con Marte](https://www.nasa.gov/missions/tech-demonstration/space-communications-7-things-you-need-to-know/).
- NASA, [estado de la búsqueda de vida en Marte](https://www.nasa.gov/general/is-there-life-on-mars-we-asked-a-nasa-scientist-episode-31/).

## Decisiones que el equipo puede cerrar después

1. Estilo visual: 2D isométrico ilustrado o 2.5D de bajo detalle.
2. ¿La tripulación se ve caminando o se representa solo mediante el hábitat y retratos?
3. ¿Cuánto dura una partida completa y cuántos soles representa?
4. ¿Qué cultivos serán los primeros dos y qué diferencias jugables tendrán?
5. ¿El juego se enfoca primero en PC o en una versión WebGL compartible con los jueces?
6. ¿Los acechadores serán criaturas del subsuelo, o prefieren que el atacante ficticio sea un dron de otra misión? La primera opción hace más memorable el mundo; ambas pueden compartir la misma lógica de incursión.
7. ¿El rover hará toda la exploración automáticamente al marcar un destino, o el jugador lo conducirá directamente en algunas misiones especiales? Para el primer prototipo se recomienda marcar destinos.
8. ¿Cuántos niveles y cuántos rovers máximos tendrá la versión completa? Tres de cada uno es solo una propuesta inicial de balance.
