# Auditoria da fase 2

Data: 2026-07-10.

## Escopo auditado

- `Assets/Runtime/Assembly-CSharp/Unturned/LinuxPerformance/LinuxPerformanceRuntime.cs`
- `Assets/Resources/Shaders/LinuxPerformance/FSR1.shader`
- `Native/FidelityFXLinux/`
- `Assets/Tests/LinuxPerformance/LinuxPerformanceTests.cs`
- `tools/`
- `docs/linux-performance/`
- `GLRenderer.cs`, `MainCamera.cs`, `GraphicsSettings.cs`, `GraphicsSettingsData.cs`, `MenuConfigurationGraphicsUI.cs`, `.gitignore`

## Estado geral

| Item | Responsabilidade | Implementação real | Chamado em runtime | Testes | Fallback | Risco/limitação |
| --- | --- | --- | --- | --- | --- | --- |
| `HardwareCapabilities` | Detectar SO, CPU e memória de sistema | Sim, via `SystemInfo`/`Application.platform` | Sim, na inicialização do `UpscalerManager` | Criado, mas Test Runner não gerou XML | N/A | Memória livre real não é medida, apenas memória instalada |
| `LinuxGraphicsCapabilities` | Detectar API, GPU, formatos e plugin | Parcial real | Sim | Sem resultado verificável | Fallback sem plugin | `SupportsMotionVectors` ainda é capacidade presumida do Built-in Pipeline, não valida conteúdo da textura |
| `NativeRenderBackend` | ABI e capacidades do `.so` | Sim para ABI/capability plugin | Sim | Criado, sem XML | Fallback Unity | Não expõe dispositivo Vulkan, imagens, command buffers nem pipelines |
| `Fsr1Backend` | Executar FSR 1 espacial | Sim, EASU + RCAS em shader | Sim, via `GLRenderer.OnRenderImage` | Sem XML | `Graphics.Blit` nativo | Validação visual automática ainda não concluiu captura |
| `Fsr2Backend` | Backend temporal FSR 2 | Não funcional, bloqueado | Seleção/fallback apenas | Sem teste real | FSR 1/nativo | Plugin não tem backend Vulkan temporal nem acesso aos recursos Vulkan da Unity |
| `Fsr31UpscalerBackend` | Backend temporal FSR 3.1 Upscaling | Não funcional, bloqueado | Seleção/fallback apenas | Sem teste real | FSR 2/FSR 1/nativo | Mesmo bloqueio do FSR 2; Frame Generation não implementado |
| `UpscalerManager` | Selecionar backend e renderizar | Sim para FSR 1/fallback | Sim | Sem XML | Sim | Reavalia backend por frame; aceitável, mas pode ser refinado |
| `DynamicResolutionController` | Aplicar escala interna | Parcial real via `ScalableBufferManager` | Sim | Sem XML | Escala 1.0 | Não mede gargalo GPU/CPU; não deve ser tratado como controlador adaptativo completo |
| `MotionAdaptiveResolutionController` | Reduzir escala por movimento de câmera | Parcial real | Sim | Sem XML | Retorna a 1.0 em menus | Não validado em gameplay, veículos, espectador ou mira |
| `LowLatencyController` | Ajustar FPS/buffering controlável | Parcial | Sim | Sem XML | Sem efeito quando desativado | Não mede latência CPU-submit-present |
| `MemoryBudgetManager` | Aplicar perfis de memória | Parcial real | Sim na inicialização | Sem XML | Perfil automático | Atua em texture streaming; não mede RSS nem altera todos os subsistemas pedidos |
| `VisibilityBudgetManager` | Culling básico | Parcial real | Sim na inicialização | Sem XML | Perfil original | Ajusta `layerCullDistances`; não implementa CullingGroup, setores ou gameplay culling |
| `RenderTargetPool` | Reutilizar RT por descritor | Sim | Sim no FSR 1 | Teste criado, sem XML | `Clear()` em release | Sem TTL por frames; resize depende de descritor diferente |
| `PerformanceTelemetry` | Overlay e snapshots | Parcial real | Sim | Sem XML | Overlay desativado por padrão | Sem GPU frame time real e sem CSV contínuo |
| `LinuxPerformanceBootstrap` | Inicializar pacote na câmera principal | Sim | Sim quando `MainCamera` acorda | Sem XML | Release em disable/destroy | Player gráfico não alcançou log do bootstrap antes do timeout testado |

## Hook de renderização

- `GLRenderer.OnRenderImage` é o ponto de aplicação.
- `MainCamera.Awake` adiciona `LinuxPerformanceBootstrap` e `GLRenderer`.
- O hook agora ignora câmeras que não sejam `MainCamera.instance`, reduzindo risco de aplicar FSR em preview, minimapa, reflexão ou UI.
- A blit final original é preservada se `UpscalerManager.Render` retornar `false`.
- Marcadores adicionados:
  - `LinuxPerformance.RenderHook`
  - `LinuxPerformance.FSR1.EASU`
  - `LinuxPerformance.FSR1.RCAS`
  - `LinuxPerformance.DynamicResolution`
  - `LinuxPerformance.Telemetry`
  - `LinuxPerformance.Culling`
- Não foi comprovado visualmente que a UI é composta sempre depois do upscaling.
- Não foi comprovado em Player que o source já chega na resolução interna esperada em todos os caminhos.

## FSR 1

O shader anterior foi substituído por um port manual do algoritmo AMD FidelityFX FSR 1, baseado em `ffx_fsr1.h v1.20210629`, licença MIT.

Características presentes:

- constantes EASU calculadas a partir de resolução de entrada e saída;
- kernel EASU com 12 taps relevantes;
- cálculo de luma, direção, comprimento, stretch, lobe e clamp;
- deringing por min/max dos 4 pixels centrais;
- passe RCAS separado;
- alpha preservado;
- sem plugin nativo obrigatório;
- compilado para Vulkan na build Linux.

Limitações:

- ainda não há comparação automatizada lado a lado Native/Bilinear/FSR1;
- PSNR/SSIM não foram gerados;
- tempo real de EASU/RCAS não foi medido em GPU.

## Plugin nativo

Classificação: `Capability plugin`.

O plugin compila como `.so`, exporta ABI e capacidades, e é copiado para o Player. Ele não é um backend FidelityFX funcional.

Ausente no plugin:

- `IUnityGraphicsVulkan`;
- criação de contexto FidelityFX;
- acesso a `VkDevice`, `VkImage` e command buffers da Unity;
- descriptors/pipelines/SPIR-V;
- dispatch compute;
- barreiras/transições de layout;
- resize temporal;
- device loss;
- FSR 2;
- FSR 3.1.

## Recursos bloqueados

FSR 2 e FSR 3.1 Upscaling permanecem bloqueados por falta de backend Vulkan temporal real. O bloqueio técnico não é "falta de shader"; é falta de integração nativa com recursos Vulkan da Unity e SDK temporal oficial carregado.

Frame Generation e FSR 4.1 permanecem indisponíveis na RX 580. Nenhum caminho simula esses recursos.

## Código morto ou não comprovado

- FSR 2/3.1 são interfaces de fallback, não funcionalidade.
- CACAO, SSSR e HZB não estão implementados como efeitos reais.
- Benchmark de gameplay ainda não executa cena/rota automatizada.
- Test Runner ainda não gera XML apesar de pacote carregado.
