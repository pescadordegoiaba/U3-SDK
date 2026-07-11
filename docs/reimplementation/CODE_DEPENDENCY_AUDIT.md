# Auditoria de código e dependências para reimplementação/wrapper

Data da auditoria: 2026-07-10  
Projeto: U3-SDK / Unturned  
Foco: reimplementação em outra linguagem, engine ou wrapper de compatibilidade  
Estratégia recomendada: wrapper/compatibilidade primeiro

## 1. Resumo executivo

O projeto é fortemente acoplado à Unity. A maior parte do runtime fica em `Assets/Runtime/Assembly-CSharp`, com o jogo concentrado em `Assets/Runtime/Assembly-CSharp/Unturned`. Há também assemblies menores mais portáveis, como `SystemEx`, `SDG.NetTransport`, `SDG.NetPak` e `UnturnedDat`.

Para reimplementar o jogo fora da Unity, o caminho mais realista não é traduzir tudo diretamente para outra engine de uma vez. O caminho recomendado é criar uma camada de compatibilidade que simule as partes essenciais de:

- `UnityEngine`;
- ciclo de vida `MonoBehaviour`;
- cenas;
- assets;
- física;
- renderização;
- UI;
- áudio;
- input;
- Steamworks;
- transporte de rede.

Depois disso, os sistemas do jogo podem ser migrados gradualmente para uma engine própria, Godot, Unreal, Bevy, C++/Rust custom, ou outro runtime.

Classificação geral:

| Área | Dependência Unity | Dependência Steam | Portabilidade | Comentário |
| --- | --- | --- | --- | --- |
| `Unturned` | Muito alta | Alta em Provider/ItemStore | Baixa a média | Gameplay, assets, UI, player, level, server |
| `Framework` | Média | Baixa | Média | Mistura utilitários portáveis com módulos Unity |
| `Glazier_*` | Muito alta | Não direta | Baixa | UI depende de uGUI, UI Toolkit, IMGUI |
| `NetTransport_*` | Baixa a média | Alta nos transports Steam | Média | Melhor candidato para abstração |
| `NetMessaging` | Média | Indireta | Média | Mensagens do jogo, depende de Provider e netcode |
| `Provider` | Média | Alta | Média | Serviços, estatísticas, economia, autenticação |
| `SteamworksProvider` | Baixa Unity, alta Steam | Muito alta | Baixa sem shim Steam |
| `CustomPostProcess` | Muito alta | Não | Baixa | Renderização Unity/PostProcessing |
| `ItemStore` | Média | Alta | Média | UI + Steam Inventory |
| `SystemEx` | Nenhuma Unity | Não | Alta | Portável |
| `UnturnedDat` | Baixa | Não | Alta | Parser/dados |
| `SDG.NetPak` | Baixa | Não | Alta | Serialização de pacotes |
| `LinuxPerformance` | Muito alta | Não | Baixa | Render/Unity/Vulkan settings |

## 2. Metodologia

A auditoria foi baseada em inspeção estática com comandos não destrutivos:

```bash
find Assets -name '*.asmdef' -o -name '*.asmref'
rg -n "^namespace |\\b(class|struct|interface|enum)\\b" Assets -g '*.cs'
rg -n "using Unity|MonoBehaviour|ScriptableObject|GameObject|Transform|Camera|RenderTexture|Physics|Resources|AssetBundle|Steamworks" Assets -g '*.cs'
find Assets/Runtime/Assembly-CSharp -mindepth 1 -maxdepth 1 -type d
```

Critérios usados:

| Categoria | Significado |
| --- | --- |
| Portável | Pode ser migrado com pouca ou nenhuma dependência Unity |
| Requer wrapper Unity | Usa tipos Unity, mas pode rodar com uma camada simulada |
| Requer reescrita parcial | Mistura lógica de jogo com Unity/Steam/render/física |
| Requer reescrita total | Depende diretamente de engine, render, UI ou editor |
| Somente Editor | Ferramentas de import/build/editor |
| Somente teste/build | Testes e scripts |

## 3. Mapa de assemblies e pacotes

### Assembly-CSharp

Assembly principal do jogo. Inclui:

- gameplay;
- UI;
- rede;
- Provider;
- Steamworks;
- renderização;
- carregamento de assets;
- ferramentas runtime;
- menus;
- player;
- level;
- zombies;
- veículos;
- inventário.

É o maior bloco e o mais acoplado à Unity.

### Assembly-CSharp-Editor

Código de editor/importação/build. Não deve ser portado para runtime. Em uma reimplementação, deve virar ferramentas separadas de conversão de assets e geração de dados.

### SDG.NetPak

Assembly pequeno e relativamente portável. Contém serialização binária/bit-level de rede. Candidato forte para manter em C# ou portar para Rust/C++.

### SDG.NetTransport

Interfaces de transporte de rede sem dependência de engine (`noEngineReferences: true`). Boa fronteira para reimplementação.

### SystemEx

Extensões de sistema sem Unity (`noEngineReferences: true`). Deve ser tratado como código portável.

### UnityEx

Extensões e utilitários para Unity. Requer wrapper ou reescrita.

### UnturnedDat

Parser/dados de formato `.dat`. Boa peça para port independente.

### LiveConfig

Configuração dinâmica. Tem dependências do projeto e Unity. Requer adaptação.

### Steamworks.NET

Binding externo para Steam. Em outro runtime precisará de:

- binding Steam equivalente;
- shim de Steam;
- backend alternativo;
- ou remoção de recursos dependentes de Steam.

## 4. Mapa por pasta do runtime

Contagem aproximada de arquivos C# em `Assets/Runtime/Assembly-CSharp`:

| Pasta | Arquivos C# | Papel | Portabilidade |
| --- | ---: | --- | --- |
| `Unturned` | 1118 | Código principal do jogo | Baixa a média |
| `Framework` | 200 | Utilitários, módulos, IO, devkit | Média |
| `NetGen` | 107 | Código gerado/apoio de rede | Média |
| `Provider` | 52 | Serviços online/plataforma | Média |
| `NetMessaging` | 29 | Mensagens client/server | Média |
| `SteamworksProvider` | 26 | Integração Steam | Baixa sem Steam shim |
| `Glazier_uGUI` | 25 | UI Unity uGUI | Baixa |
| `Glazier_UIToolkit` | 23 | UI Toolkit | Baixa |
| `Glazier_IMGUI` | 22 | IMGUI | Baixa |
| `NetInvokable` | 16 | RPC/invocação de rede | Média |
| `ItemStore` | 11 | Loja/Steam Inventory/UI | Média-baixa |
| `Glazier` | 6 | Abstração de UI | Média |
| `NetTransport_SystemSockets` | 5 | Transporte socket | Alta |
| `NetTransport_SteamNetworking` | 4 | Transporte Steam antigo | Baixa sem Steam |
| `NetTransport_SteamNetworkingSockets` | 4 | Transporte Steam sockets | Baixa sem Steam |
| `NetTransport_UNetLLAPI` | 3 | Transporte Unity legado | Baixa |
| `General` | 3 | Utilitários gerais | Média |
| `CustomPostProcess` | 2 | Pós-processamento Unity | Baixa |
| `NetPak` | 1 | Ponte para NetPak | Alta |
| `NetTransport_Loopback` | 1 | Transporte local | Alta |

## 5. Mapa interno de `Unturned`

Contagem aproximada de arquivos C# por pasta:

| Pasta | Arquivos | Tipo de código | Portabilidade |
| --- | ---: | --- | --- |
| `Bundles` | 235 | Assets, bundles, definições de conteúdo | Requer wrapper asset/Unity |
| `Level` | 106 | Mundo, terreno, lighting, regiões | Requer reescrita parcial/total |
| `Command` | 95 | Comandos server/admin | Média |
| `UI` | 86 | Menus/player/editor UI | Baixa |
| `Sleek` | 62 | Toolkit UI próprio sobre Unity | Requer wrapper UI |
| `Interactable` | 55 | Objetos interativos/veículos | Requer wrapper Unity/física |
| `Managers` | 52 | Managers globais | Requer wrapper lifecycle |
| `ModHooks` | 46 | Hooks/modding | Média |
| `Player` | 44 | Player, movimento, input, câmera | Baixa sem engine |
| `Provider` | 44 | Plataforma/servidor/cliente | Média |
| `Tools` | 35 | Utilitários | Média |
| `Useable` | 31 | Itens utilizáveis | Requer wrapper gameplay/input |
| `Settings` | 22 | Configurações | Média-alta |
| `Inventory` | 18 | Inventário | Média |
| `Game` | 17 | Bootstrap/câmera/gameflow | Baixa |
| `Menu` | 17 | Menus | Baixa |
| `Files` | 17 | Arquivos/config | Média-alta |
| `Throwables` | 11 | Itens arremessáveis | Requer física |
| `Damage` | 9 | Dano | Média |
| `Regions` | 8 | Regiões/culling/interesse | Média |
| `ServerListCuration` | 6 | Lista/serviços | Média |
| `Zombies` | 5 | IA zombies | Requer engine/gameplay |
| `Characters` | 3 | Personagem/visual | Baixa |
| `Decals` | 3 | Decals/render | Baixa |
| `Loading` | 3 | Loading | Requer assets/lifecycle |
| `Attachments` | 2 | Attachments | Média |
| `Network` | 2 | Rede runtime | Média |
| `Animals` | 1 | Animais | Requer engine/gameplay |
| `Constants` | 1 | Constantes | Alta |
| `LinuxPerformance` | 1 | Render/performance Linux | Baixa |
| `Utils` | 42 | Utilitários | Média-alta |

## 6. O que é Unity mesmo

Esta seção lista dependências que pertencem à Unity ou ao ecossistema Unity e que não existem automaticamente em outra engine.

### Ciclo de vida

APIs e padrões:

- `MonoBehaviour`;
- `Awake`;
- `Start`;
- `Update`;
- `LateUpdate`;
- `FixedUpdate`;
- `OnEnable`;
- `OnDisable`;
- `OnDestroy`;
- `OnGUI`;
- coroutines;
- `StartCoroutine`;
- `IEnumerator` em rotinas Unity.

Impacto:

- Todo sistema de lifecycle precisa ser simulado.
- A ordem de inicialização da Unity é parte do comportamento.
- Muitos managers globais dependem implicitamente de cenas e GameObjects.

Wrapper necessário:

```text
IBehaviour
IUpdateSystem
IFixedUpdateSystem
ISceneLifecycle
ICoroutineRunner
```

### Objetos e cena

APIs:

- `GameObject`;
- `Transform`;
- `Component`;
- `GetComponent`;
- `AddComponent`;
- `Destroy`;
- `DontDestroyOnLoad`;
- `SceneManager`;
- prefabs;
- cenas `.unity`.

Impacto:

- É a dependência mais difícil de substituir.
- O jogo espera hierarquia de cena e componentes.
- Wrapper precisa mapear entidade/componente de outra engine para comportamento Unity-like.

### Matemática e tipos básicos Unity

APIs:

- `Vector2`;
- `Vector3`;
- `Vector4`;
- `Quaternion`;
- `Color`;
- `Color32`;
- `Bounds`;
- `Plane`;
- `Ray`;
- `Rect`;
- `Mathf`;

Impacto:

- São relativamente fáceis de portar.
- Podem virar tipos próprios ou aliases para tipos da engine destino.

Prioridade alta para wrapper.

### Renderização

APIs:

- `Camera`;
- `Renderer`;
- `MeshRenderer`;
- `SkinnedMeshRenderer`;
- `Material`;
- `Shader`;
- `RenderTexture`;
- `Graphics.Blit`;
- `GL`;
- `QualitySettings`;
- `ScalableBufferManager`;
- `SystemInfo.graphics*`;
- PostProcessing.

Impacto:

- Impossível portar sem planejar render pipeline novo.
- Shaders Unity/HLSL precisam de tradução ou substituição.
- Recursos como FSR1 atual dependem de `RenderTexture`, `Shader`, `Material` e `Graphics.Blit`.

### Física

APIs:

- `Rigidbody`;
- `Collider`;
- `Physics`;
- `RaycastHit`;
- layers/masks;
- triggers;
- `FixedUpdate`.

Impacto:

- Movimento, dano, veículos, throwables, zombies e interações dependem da semântica de física.
- Um wrapper precisa reproduzir colisões, queries e eventos.

### UI

APIs:

- uGUI: `Canvas`, `RectTransform`, `Image`, `RawImage`, `Button`, `Text`, layout groups;
- UI Toolkit: `VisualElement`, styles, events;
- IMGUI: `OnGUI`, `GUI`, `GUILayout`;
- TextMesh Pro.

Impacto:

- `Glazier`, `Sleek` e menus precisam de camada UI própria.
- A abstração `Sleek` ajuda, mas implementações atuais são Unity.

### Assets

APIs:

- `Resources.Load`;
- `AssetBundle`;
- `Material`;
- `Texture2D`;
- `AudioClip`;
- `AnimationClip`;
- prefabs;
- scenes;
- meta GUIDs;
- import settings.

Impacto:

- Reimplementação precisa converter assets Unity para formato externo ou carregar AssetBundles via runtime compatível.
- Sem essa camada, gameplay pode rodar headless, mas cliente gráfico não.

### Input, tempo e sistema

APIs:

- `Input`;
- `Time.deltaTime`;
- `Time.realtimeSinceStartup`;
- `Application`;
- `Screen`;
- `Cursor`;
- `SystemInfo`.

Impacto:

- Wrapper simples é viável.
- Precisa preservar diferenças entre realtime, scaled time e fixed time.

## 7. O que é código do jogo

### Gameplay e domínio

Inclui:

- inventário;
- dano;
- itens;
- uso de armas/ferramentas;
- comandos;
- regiões;
- interações;
- veículos;
- zombies;
- animais;
- player.

Parte dessa lógica é portável conceitualmente, mas a implementação mistura Unity em movimento, física, render, áudio, animação, input e networking.

Estratégia:

1. Separar dados e regras puras.
2. Criar interfaces para engine.
3. Manter semântica original.
4. Reimplementar apenas os adaptadores de Unity.

### Assets e dados

`Bundles`, `Files`, `UnturnedDat` e partes de `Level` formam o núcleo de dados.

Itens mais portáveis:

- parser `.dat`;
- IDs;
- definições de assets;
- configs;
- tabelas;
- comandos.

Itens dependentes de Unity:

- prefabs;
- materiais;
- shaders;
- terrain;
- iluminação;
- colliders;
- animações;
- cenas.

### Rede

Camadas:

- `SDG.NetTransport`: interface de transporte;
- `NetTransport_SystemSockets`: socket puro, mais portável;
- `NetTransport_Loopback`: portável;
- `NetTransport_SteamNetworking*`: depende Steam;
- `NetTransport_UNetLLAPI`: depende Unity;
- `NetMessaging`: mensagens do jogo;
- `NetPak`: serialização.

Estratégia:

- Portar primeiro `SDG.NetTransport` + `NetPak`.
- Substituir Steam transport por adapter equivalente.
- Manter payloads e protocolos antes de mexer em gameplay.

### UI do jogo

Camadas:

- `Sleek`: abstração histórica de UI do jogo;
- `Glazier`: backend abstrato mais novo;
- `Glazier_uGUI`: implementação Unity uGUI;
- `Glazier_UIToolkit`: implementação Unity UI Toolkit;
- `Glazier_IMGUI`: implementação IMGUI;
- `MenuConfigurationGraphicsUI` e outros menus.

Estratégia:

- Preservar interfaces `Sleek`/`Glazier` como fronteira.
- Reimplementar backend em outra UI.
- Não tentar portar `RectTransform` diretamente se a engine destino tiver layout próprio.

## 8. Dependências externas

| Dependência | Uso | Caminho de reimplementação |
| --- | --- | --- |
| UnityEngine | Engine, render, física, lifecycle, assets | Wrapper amplo ou troca de engine |
| UnityEditor | Ferramentas/import/build | Não portar para runtime |
| Steamworks.NET | Steam API, auth, inventory, networking | Steam shim ou backend alternativo |
| Newtonsoft.Json | JSON | Portável ou substituir por lib equivalente |
| TextMesh Pro | Texto UI Unity | Substituir por renderer de texto da engine |
| PostProcessing | Pós-processamento | Reimplementar no render pipeline alvo |
| Native/FidelityFXLinux | Plugin Linux de capacidades | Expandir só se houver backend Vulkan real |

## 9. Inventário consolidado de classes por módulo

Esta tabela resume os módulos e a classe de portabilidade predominante. Para uma listagem literal de todas as declarações, use o comando no fim desta seção.

| Módulo | Exemplos de classes/sistemas | Dependência predominante | Recomendação |
| --- | --- | --- | --- |
| `CustomPostProcess` | `SkyFog`, `SrScope` | Unity PostProcessing/render | Reescrita total no novo renderer |
| `Framework/IO` | serializers, streams, deserializers | Baixa Unity | Portar cedo |
| `Framework/Modules` | `Module`, `ModuleHook`, config de módulos | Mista | Separar loader portável de hooks Unity |
| `Glazier` | `GlazierBase`, `GlazierFactory` | Unity lifecycle | Manter interfaces, trocar backend |
| `Glazier_uGUI` | botões, labels, fields, scroll views | Unity UI | Reescrita de backend |
| `Glazier_UIToolkit` | elementos UI Toolkit | Unity UI Toolkit | Reescrita de backend |
| `Glazier_IMGUI` | UI imediata | Unity IMGUI | Reescrita ou remover |
| `ItemStore` | `ItemStore`, `SteamItemStore`, menus | Steam + Unity UI | Separar store service de UI |
| `NetMessaging` | handlers client/server | Jogo + Provider | Portar após NetPak/transport |
| `NetPak` | ponte para serialização | Baixa | Portar cedo |
| `NetTransport_Loopback` | loopback | Baixa | Portar cedo |
| `NetTransport_SystemSockets` | socket client/server | Baixa | Portar cedo |
| `NetTransport_SteamNetworking*` | Steam networking | Steam | Criar Steam adapter |
| `NetTransport_UNetLLAPI` | Unity LLAPI | Unity legado | Não priorizar |
| `Provider` | serviços online/plataforma | Mista | Criar interfaces por serviço |
| `SteamworksProvider` | Steam auth/services | Steam | Shim Steam obrigatório |
| `Unturned/Bundles` | assets e conteúdo | Unity assets | Converter dados e criar asset registry |
| `Unturned/Level` | mapa, lighting, terrain | Unity world/render/física | Reescrita parcial/total |
| `Unturned/Player` | movimento/input/câmera | Unity física/input | Reescrita com adapter de engine |
| `Unturned/Inventory` | inventário | Mista | Separar lógica pura |
| `Unturned/Interactable` | veículos/objetos | Unity física/world | Reescrita parcial |
| `Unturned/UI` | menus e HUD | Unity/Sleek/Glazier | Reimplementar backend |
| `Unturned/Settings` | configurações | Baixa a média | Portar cedo |
| `Unturned/Command` | comandos | Baixa a média | Portar cedo para servidor |
| `Unturned/LinuxPerformance` | FSR/render/settings Linux | Unity render | Não portar diretamente; recriar por renderer |

Comando para gerar inventário literal de declarações:

```bash
rg -n "^namespace |\\b(class|struct|interface|enum)\\b" Assets -g '*.cs' > class-inventory.txt
```

Observação: no momento da auditoria, esse levantamento retornava milhares de declarações. Para manter este arquivo útil como documento de engenharia, a análise aprofunda módulos e classes centrais em vez de colar uma listagem bruta enorme.

## 10. Classes e sistemas centrais aprofundados

### `Provider`

Papel:

- centro de plataforma;
- sessão client/server;
- serviços;
- Steam;
- autenticação;
- conexão;
- server lifecycle.

Dependências:

- Unity lifecycle;
- Steamworks;
- transporte de rede;
- filesystem;
- configurações;
- assets.

Portabilidade:

- Requer reescrita parcial.
- Deve virar interface de plataforma.

Wrapper proposto:

```text
IPlatformServices
IAuthenticationService
IServerBrowserService
IWorkshopService
IEconomyService
ITransportFactory
```

### `Player`

Papel:

- entidade local/remota;
- movimento;
- input;
- câmera;
- inventário;
- vida;
- interação;
- animação.

Dependências:

- `MonoBehaviour`;
- `Transform`;
- `Rigidbody`/physics;
- câmera;
- input;
- rede;
- UI.

Portabilidade:

- Requer wrapper Unity e depois reescrita gradual.
- O modelo de dados do player deve ser separado do controller Unity.

### `Level`

Papel:

- mundo;
- mapas;
- terreno;
- objetos;
- iluminação;
- navegação;
- regiões;
- spawns.

Dependências:

- scenes;
- terrain;
- renderers;
- colliders;
- asset bundles;
- lighting;
- Unity object hierarchy.

Portabilidade:

- Uma das áreas mais caras.
- Em wrapper, precisa simular scene graph e asset loading.
- Em engine nova, precisa conversor de mapa.

### `Bundles`

Papel:

- definição/carregamento de conteúdo;
- assets do jogo;
- skins;
- itens;
- objetos;
- recursos;
- veículos.

Dependências:

- `AssetBundle`;
- `Resources`;
- `UnityEngine.Object`;
- GUIDs/meta;
- materiais/texturas/prefabs.

Portabilidade:

- Dados `.dat` são portáveis.
- Referências a assets Unity exigem conversão.

### `Sleek` e `Glazier`

Papel:

- camada de UI do jogo;
- widgets;
- menus;
- backend para Unity uGUI/UI Toolkit/IMGUI.

Dependências:

- Unity UI;
- `GameObject`;
- `RectTransform`;
- TextMesh Pro;
- input/event system.

Portabilidade:

- Interfaces podem ser mantidas.
- Backends Unity devem ser reescritos.

### `NetPak`

Papel:

- serialização binária eficiente;
- leitura/escrita de bits;
- payloads de rede.

Dependências:

- poucas ou nenhuma dependência Unity relevante.

Portabilidade:

- Alta.
- Deve ser um dos primeiros subsistemas a portar/testar.

### `NetTransport`

Papel:

- abstração de transporte client/server;
- sockets;
- loopback;
- Steam networking.

Dependências:

- interface base portável;
- implementações Steam dependem Steamworks;
- UNet depende Unity legado.

Portabilidade:

- Alta para interfaces e sockets.
- Média/baixa para Steam.

### `LinuxPerformance`

Papel:

- otimizações Linux adicionadas neste fork;
- FSR1;
- detecção GPU;
- resolução dinâmica;
- plugin Linux de capacidades.

Dependências:

- Unity render;
- `RenderTexture`;
- `Shader`;
- `Graphics.Blit`;
- `SystemInfo`;
- `QualitySettings`;
- plugin `.so`.

Portabilidade:

- Baixa.
- Deve ser refeito no renderer destino, não traduzido diretamente.

## 11. Plano de wrapper/compatibilidade

### Camada 1: tipos básicos Unity

Implementar primeiro:

```text
Vector2
Vector3
Vector4
Quaternion
Color
Bounds
Ray
Rect
Mathf
Time
Debug/Log
```

Objetivo:

- permitir compilar código lógico sem engine real.

### Camada 2: lifecycle e entidade/componente

Implementar:

```text
GameObject
Component
Transform
MonoBehaviour
Scene
Object.Destroy
Object.Instantiate
Coroutine runner
```

Objetivo:

- rodar managers e sistemas simples.

### Camada 3: assets

Implementar:

```text
Resources.Load
AssetBundle facade
Texture/Material/Shader placeholders
AudioClip placeholders
Prefab registry
```

Objetivo:

- carregar dados e referências sem renderizar tudo.

### Camada 4: rede

Portar/validar:

```text
SDG.NetPak
SDG.NetTransport
NetTransport_SystemSockets
NetTransport_Loopback
NetMessaging
```

Objetivo:

- levantar servidor/headless primeiro.

### Camada 5: física e mundo

Substituir:

```text
Physics.Raycast
Colliders
Rigidbodies
Triggers
Layer masks
FixedUpdate
```

Objetivo:

- simular gameplay em mapa simples.

### Camada 6: UI e render

Substituir:

```text
Sleek backend
Glazier backend
Camera
Renderer
Material
Shader
PostProcessing
```

Objetivo:

- cliente visual fora da Unity.

## 12. Ordem recomendada de reimplementação

1. Congelar commit base e gerar inventário automático de classes.
2. Portar tipos básicos (`SystemEx`, `UnturnedDat`, `SDG.NetPak`).
3. Portar interfaces de transporte (`SDG.NetTransport`).
4. Criar shim mínimo de Unity para compilar lógica pura.
5. Isolar `Provider` em interfaces.
6. Rodar servidor/headless sem render.
7. Portar configs, comandos e dados.
8. Criar conversor de assets/mapas.
9. Reimplementar física.
10. Reimplementar UI.
11. Reimplementar render.
12. Substituir Steamworks por shim ou backend real.

## 13. Riscos principais

| Risco | Gravidade | Motivo |
| --- | --- | --- |
| Dependência de cenas Unity | Alta | Muitas referências estão em cenas/prefabs |
| AssetBundles | Alta | Formato e import pipeline Unity |
| Física | Alta | Gameplay depende de semântica Unity |
| UI | Alta | Menus/HUD usam Sleek/Glazier sobre Unity |
| Steamworks | Alta | Auth, inventory, networking, workshop |
| Render/shaders | Alta | Shaders e materiais são Unity |
| Lifecycle implícito | Alta | Ordem de `Awake/Start/Update` afeta estado |
| Netcode | Média-alta | Precisa preservar protocolo |
| Editor tools | Média | Necessárias para conversão/build |

## 14. Decisão técnica recomendada

Não começar por portar render ou player completo.

Começar por:

1. dados;
2. serialização;
3. rede;
4. servidor/headless;
5. wrapper mínimo de Unity;
6. gameplay em simulação;
7. cliente visual.

Essa ordem reduz risco porque separa código de domínio de dependências visuais e permite validar comportamento com testes automatizados antes de enfrentar renderização, assets e UI.

## 15. Conclusão

O projeto tem muita lógica reaproveitável, mas ela está misturada com Unity em pontos críticos. A reimplementação mais segura é criar uma camada de compatibilidade que permita compilar e executar partes do código original, começando pelos sistemas mais portáveis.

Partes mais portáveis:

- `SystemEx`;
- `UnturnedDat`;
- `SDG.NetPak`;
- `SDG.NetTransport`;
- comandos;
- configurações;
- parte de inventário;
- parte de dados de assets.

Partes menos portáveis:

- render;
- UI;
- física;
- scenes/prefabs;
- player controller;
- level/terrain;
- Steamworks;
- AssetBundles.

O objetivo inicial deve ser um runtime headless compatível, não um cliente gráfico completo.
