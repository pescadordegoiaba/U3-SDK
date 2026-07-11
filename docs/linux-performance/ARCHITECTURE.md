# Arquitetura Linux Performance

O pacote fica em `SDG.Unturned.LinuxPerformance` e é ativado por `LinuxPerformanceBootstrap` na `MainCamera`.

- `HardwareCapabilities` e `LinuxGraphicsCapabilities`: detectam OS, CPU, GPU, API gráfica, memória, formatos e plugin.
- `PerformanceSettings`: espelha as opções persistidas em `GraphicsSettingsData` e valida limites.
- `UpscalerManager`: escolhe backend por ordem segura: FSR 3.1 real, FSR 2 real, FSR 1, resolução dinâmica, nativo.
- `Fsr1Backend`: executa EASU e RCAS em shader Unity.
- `Fsr2Backend` e `Fsr31UpscalerBackend`: interfaces temporais reais com estado `Unsupported` até backend Vulkan validado.
- `DynamicResolutionController` e `MotionAdaptiveResolutionController`: controlam escala interna.
- `LowLatencyController`, `MemoryBudgetManager`, `RenderTargetPool`, `VisibilityBudgetManager` e `PerformanceTelemetry`: ajustes e diagnóstico.

Todos os recursos têm fallback para o blit original.

## Culling visual regional

`VisibilityBudgetManager` recebe cada `LevelObject` depois que sua lista de renderers foi preenchida e mantém índices por região. Não há busca global de objetos. O `Tick` considera somente a região atual, as oito vizinhas, regiões entrando/saindo do raio e filas persistentes de mudanças.

Objetos interativos, rubble/destrutíveis, NPCs, objetos de missão ou rede, triggers e assets com `isCollisionImportant` são classificados como `NeverCull`. Colliders, triggers, nav e o estado ativo do `GameObject` não são modificados pelo manager. As únicas mudanças adicionais são `shadowCastingMode`, `Light`, `ParticleSystem` e `Animator` cosmético; a visibilidade principal continua sob autoridade de `LevelObject.SetIsVisibleByGameplayBudget`.

O budget inicial é de 32 transições de visibilidade por `LevelObject`, 16 mudanças de sombra e 8 mudanças para cada grupo de luz, partícula e animator. O limite de 32 não representa renderers individuais: um `LevelObject` pode controlar vários renderers de forma atômica. Itens enfileirados usam generation para invalidar mudanças obsoletas quando o estado desejado se inverte.

Selecionar `Original`, entrar em modo cinematográfico, trocar mapa ou destruir o controlador restaura os estados capturados. A restauração é idempotente e respeita a composição de visibilidade já existente em `LevelObject`, incluindo região, condições e `CullingVolume`.

## Renderização explícita em baixa resolução

`LowResolutionWorldRenderer` existe somente na `MainCamera`. Quando a escala é menor que 1,0 ele configura buffers persistentes separados para `SceneColorLowRes` e `SceneDepthLowRes`, renderiza o mundo nessas dimensões e fornece um `UpscaledColor` no tamanho real do backbuffer. `GLRenderer` executa o backend/fallback nesse target nativo, desenha seus eventos GL depois do upscale e finalmente apresenta no backbuffer. UI `ScreenSpaceOverlay`, mira, textos e `OnGUI` continuam sendo compostos posteriormente pela Unity em resolução nativa.

Minimapa, reflexões e câmeras secundárias não recebem o componente nem passam pelo fluxo. Os targets só são recriados quando resolução, escala, HDR/formato ou descriptor mudam. Escala 1,0 mantém o caminho nativo; falha de criação ou divergência nas dimensões reais do source usa blit direto ao backbuffer.
