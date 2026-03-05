# Microsoft Agent Framework en acción

### Sesiones disponibles
#### Sesión 01 ([Link](https://www.linkedin.com/events/7431843634196570112/?viewAsMember=true))

Se introduce el Microsoft Agent Framework (versión RC, ya con API estable para producción) en una
serie de sesiones usando C# 14 / .NET 10. Se explica conceptualmente qué es un agente: un LLM combinado con
instrucciones de sistema (que definen su comportamiento) y herramientas (funciones/APIs que otorgan acceso a
información actualizada o privada ausente en el corpus de entrenamiento). Se compara el framework con alternativas
Python como LangChain/LangGraph, posicionándose como la opción natural para equipos .NET. En la parte práctica, se
crea un agente básico con AsAIAgent y GPT-5 Nano, ejecutándose un prompt simple. Se cierra anticipando que en la
sesión 2 se abordarán razonamiento, control del modelo y costos.

#### Sesión 02 ([Link](https://www.linkedin.com/posts/rdiazconcha_microsoft-agent-framework-en-acci%C3%B3n-sesi%C3%B3n-activity-7432628257209872384-4FYi?utm_source=share&utm_medium=member_desktop&rcm=ACoAACFJOm0Bu21UOrkGtbiQx9DjwJmSlpdqf74))

Se presentan los tipos esenciales del framework: IChatClient, la clase abstracta AIAgent y su implementación
principal ChatClientAgent. Se trabaja con structured output para obtener respuestas tipadas. Se demuestra cómo el
razonamiento de GPT-5 Nano consume ~90% de los tokens, y cómo desactivarlo vía ChatClientAgentRunOptions para
mejorar rendimiento y reducir costos. Se muestra también cómo inspeccionar el consumo de tokens en AgentResponse.

#### Sesión 03 ([Link](https://www.linkedin.com/events/7432982846694232064/?viewAsMember=true))

Se exploran los modos de ejecución: RunAsync (respuesta completa en un solo AgentResponse) vs. RunStreamingAsync
(tokens progresivos vía AgentResponseUpdate). Se detalla la clase base AIContent y sus tipos de contenido
(TextContent, UsageContent, etc.), heredados de Microsoft Extensions AI. Se demuestra la multimodalidad: enviar un
PDF junto con un prompt al modelo, construyendo mensajes con listas de AIContent que combinan texto y documentos
binarios.

#### Sesión 04 ([Link](https://www.linkedin.com/posts/rdiazconcha_microsoft-agent-framework-en-acci%C3%B3n-sesi%C3%B3n-activity-7434440316780228608-MHxv?utm_source=share&utm_medium=member_desktop&rcm=ACoAACFJOm0Bu21UOrkGtbiQx9DjwJmSlpdqf74))

Se aborda el concepto de herramientas (tools/function calling): cómo darle "agency" a los agentes permitiéndoles
invocar funciones externas. Se explica el flujo completo: el esquema JSON de la herramienta se envía al LLM, este
detecta cuándo debe invocarla y genera un FunctionCallContent con nombre y argumentos, y el framework ejecuta la
función real. Se demuestra creando herramientas de presupuesto de viaje con AIFunctionFactory.Create, y se comparte
el tip de usar nombres descriptivos (incluso estilo Python con snake_case) para mejorar la detección de intención
sin necesidad de descripciones explícitas.

#### Sesión 05 ([Link](https://www.linkedin.com/posts/rdiazconcha_microsoft-agent-framework-en-acci%C3%B3n-sesi%C3%B3n-activity-7434802623964143616-aSvG?utm_source=share&utm_medium=member_desktop&rcm=ACoAACFJOm0Bu21UOrkGtbiQx9DjwJmSlpdqf74))

Se cubre el uso de un agente como herramienta de otro agente mediante el método de extensión AsAIFunction. Se
introduce el estándar de Skills: archivos Markdown (skill.md) con un front matter YAML (name, description) seguido
de instrucciones, organizados en una estructura de carpetas. Se demuestra cómo cargar skills con
FileAgentSkillsProvider como AIContextProvider, y cómo el framework invoca automáticamente la función LoadSkill
cuando el prompt requiere conocimiento definido en un skill (ej. políticas de reembolso de gastos).

#### Sesión 06 ([Link](https://www.linkedin.com/posts/rdiazconcha_microsoft-agent-framework-en-acci%C3%B3n-sesi%C3%B3n-activity-7435164916489015296-REE6?utm_source=share&utm_medium=member_desktop&rcm=ACoAACFJOm0Bu21UOrkGtbiQx9DjwJmSlpdqf74))

Se anuncia el Release Candidate 3 del framework. Se introduce el concepto de sesiones (AgentSession), siendo
ChatClientAgentSession la implementación concreta para ChatClientAgent. Se explica el manejo del historial de chat:
por defecto usa InMemoryChatHistoryProvider, donde los mensajes se serializan como JSON en un state bag. Se
demuestra cómo persistir el historial usando Cosmos DB mediante CosmosChatHistoryProvider, pasando connection
string, database ID y container ID en las ChatClientAgentOptions.